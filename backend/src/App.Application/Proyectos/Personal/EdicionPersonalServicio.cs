using App.Application.Comun;
using App.Application.Proyectos.Crear;
using App.Application.Proyectos.Estados;
using App.Application.Seguridad;
using App.Domain.Proyectos;
using App.Domain.Proyectos.Cronograma;

namespace App.Application.Proyectos.Personal;

/// <summary>
/// Caso de uso "Actualizar personal" (TAREA-17, ACTUALIZACION_PERSONAL sobre proyectos ACTIVO).
/// Corte = hoy en Ecuador → validar → MotorCronograma.Regenerar → cruces internos, históricos y externos →
/// previsualizar (no guarda) o registrar en una transacción con applock (§7.1), recalculando todo dentro del bloqueo.
/// Ver docs/fases/FASE_5_Edicion_Cronograma.md §8.
/// </summary>
public sealed class EdicionPersonalServicio(
    IEdicionPersonalRepositorio repositorio,
    IDatosReferenciaProyecto datos,
    IConsultaCrucesExternos crucesExternos,
    EdicionPersonalValidador validador,
    ITransaccionAsignaciones transaccion,
    IUsuarioActual usuario,
    TimeProvider reloj)
{
    public const string TipoMovimiento = "ACTUALIZACION_PERSONAL";

    // ------------------------------------------------------------------ GET edicion

    public async Task<EdicionPersonalDto?> ObtenerEdicionAsync(int proyectoId, CancellationToken ct)
    {
        if (!TryObtenerVisibilidad(out var propietario))
        {
            return null;
        }

        var corte = FechaNegocio.Hoy(reloj);
        var proyecto = await repositorio.ObtenerAsync(proyectoId, propietario, corte, ct);
        if (proyecto is null)
        {
            return null;
        }

        var limites = await datos.ObtenerLimitesAsync(ct);
        var motivo = EdicionPersonalValidador.MotivoNoEditable(proyecto, corte);

        return new EdicionPersonalDto(
            proyecto.Id, proyecto.Codigo, proyecto.EstadoCodigo, proyecto.FechaInicio, proyecto.FechaFin, corte,
            motivo is null, motivo,
            proyecto.Personal.Select(p =>
            {
                var historica = p.EsHistorica(corte);
                return new PersonaEdicionDto(
                    p.Id, CalculadorCruces.NombreRol(p.Rol), p.Numero, Empleado(p), historica ? "HISTORICO" : "VIGENTE",
                    p.JornadaCodigo, p.FechaInicio, p.FechaFin, p.TipoRegistro, p.DiasDescanso, p.PrincipalRelacionadoId,
                    p.Cargo, p.Observacion,
                    historica
                        ? new PermisosEdicionDto(false, null, false, false)
                        : new PermisosEdicionDto(p.FechaInicio >= corte, corte.AddDays(-1), p.Rol == RolCronograma.Principal,
                            p.FechaInicio >= corte));
            }).ToList(),
            new LimitesEdicionDto(limites.MaxPrincipales, limites.MaxBacks, limites.MaxDiasDescansoBack));
    }

    // ------------------------------------------------------------------ previsualizar / registrar

    public async Task<ResultadoEdicionPersonal> PrevisualizarAsync(int proyectoId, ActualizarPersonalSolicitud s, CancellationToken ct)
    {
        var r = await CalcularAsync(proyectoId, s, ct);
        return r.Error ?? ResultadoEdicionPersonal.Previsualizado(r.Calculo!.Previsualizacion);
    }

    public async Task<ResultadoEdicionPersonal> RegistrarAsync(int proyectoId, ActualizarPersonalSolicitud s, CancellationToken ct)
    {
        // Verificación previa fuera de la transacción: no se abre el bloqueo si la solicitud no es válida o tiene cruces.
        var previo = await CalcularAsync(proyectoId, s, ct);
        if (previo.Error is { } error)
        {
            return error;
        }

        if (previo.Calculo!.Previsualizacion.Cruces.Count > 0)
        {
            return ResultadoEdicionPersonal.ConCruces(previo.Calculo.Previsualizacion);
        }

        return await transaccion.EjecutarAsync(async ctTx =>
        {
            // E7: dentro del applock se vuelve a leer y a recalcular todo (estado, personal, base y cruces externos).
            var r = await CalcularAsync(proyectoId, s, ctTx);
            if (r.Error is { } errorTx)
            {
                // Lo que era válido fuera y ya no lo es dentro: los datos cambiaron entre la lectura y el bloqueo.
                return errorTx.Estado == EstadoEdicion.Invalido ? ResultadoEdicionPersonal.Cambiado() : errorTx;
            }

            var c = r.Calculo!;
            if (c.Previsualizacion.Cruces.Count > 0)
            {
                return ResultadoEdicionPersonal.ConCruces(c.Previsualizacion);
            }

            try
            {
                var version = await repositorio.ObtenerUltimaVersionEtapaAsync(proyectoId, ctTx) + 1;
                await repositorio.AplicarAsync(CrearCambio(c, version), ctTx);
                return ResultadoEdicionPersonal.Hecho(new PersonalActualizadoDto(proyectoId, version));
            }
            catch (ConflictoConcurrenciaException)
            {
                return ResultadoEdicionPersonal.Cambiado(); // se revierte (confirmar = false)
            }
        }, x => x.Estado == EstadoEdicion.Realizado, ct);
    }

    // ------------------------------------------------------------------ cálculo común

    private sealed record Calculo(
        DatosEdicion Datos, DateOnly Corte, PlanEdicion Plan, ResultadoRegeneracion Regeneracion,
        IReadOnlyDictionary<string, int> ClavesGuardadas, PrevisualizacionPersonalDto Previsualizacion);

    private sealed record Resultado(ResultadoEdicionPersonal? Error, Calculo? Calculo);

    private async Task<Resultado> CalcularAsync(int proyectoId, ActualizarPersonalSolicitud s, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (!TryObtenerVisibilidad(out var propietario))
        {
            return new Resultado(ResultadoEdicionPersonal.NoEncontrado(), null);
        }

        var corte = FechaNegocio.Hoy(reloj); // E1
        var proyecto = await repositorio.ObtenerAsync(proyectoId, propietario, corte, ct);
        if (proyecto is null)
        {
            return new Resultado(ResultadoEdicionPersonal.NoEncontrado(), null);
        }

        // Datos de referencia: jornadas, límites y empleados activos de las personas NUEVAS (D4).
        var idsNuevos = (s.Principales ?? []).Where(p => p.Id is null).Select(p => p.EmpleadoId)
            .Concat((s.Backs ?? []).Where(b => b.Id is null).Select(b => b.EmpleadoId))
            .OfType<int>().Distinct().ToList();
        var activos = idsNuevos.Count == 0
            ? new Dictionary<int, EmpleadoRef>()
            : await datos.ObtenerEmpleadosActivosAsync(idsNuevos, ct);
        var jornadas = await datos.ObtenerJornadasAsync(ct);
        var limites = await datos.ObtenerLimitesAsync(ct);

        var validacion = validador.Validar(s, proyecto, corte, jornadas, limites, activos);
        if (validacion.Cambiado)
        {
            return new Resultado(ResultadoEdicionPersonal.Cambiado(), null);
        }

        if (validacion.Plan is not { } plan)
        {
            return new Resultado(ResultadoEdicionPersonal.Invalido(validacion.Errores), null);
        }

        // Motor (TAREA-16): vigentes y nuevas por su clave; la base trae la clave de su persona (históricas: "h{Id}").
        var clavesGuardadas = plan.Principales.Concat(plan.Backs).Where(p => p.Id is not null)
            .ToDictionary(p => p.Clave, p => p.Id!.Value, StringComparer.Ordinal);
        var clavePorId = clavesGuardadas.ToDictionary(kv => kv.Value, kv => kv.Key);

        var regeneracion = MotorCronograma.Regenerar(new SolicitudRegeneracion(
            proyecto.FechaInicio, proyecto.FechaFin, corte,
            plan.Principales.Select(p => new PrincipalEdicion(p.Clave, p.Numero, p.Empleado.Id, p.Inicio, p.Fin,
                p.Jornada!.DiasTrabajo, p.Jornada.DiasDescanso, p.EsNueva)).ToList(),
            plan.Backs.Select(b => new BackEdicion(b.Clave, b.Numero, b.Empleado.Id, b.Inicio, b.Fin, b.TipoRegistro,
                b.DiasDescanso, b.EsNueva)).ToList(),
            proyecto.DiasBase.Select(d => new DiaExistente(clavePorId.GetValueOrDefault(d.PersonalId, $"h{d.PersonalId}"), d.Dia)).ToList()));

        // Empleados de todo el proyecto (guardados + nuevos) para nombres y cruces.
        var empleados = proyecto.Personal
            .Select(p => new EmpleadoRef(p.EmpleadoId, p.CodigoEkon, p.NombreCompleto, p.Cargo))
            .Concat(plan.Principales.Concat(plan.Backs).Select(p => p.Empleado))
            .DistinctBy(x => x.Id)
            .ToDictionary(x => x.Id);

        // E6: internos + históricos propios (motor) + externos (otros proyectos vigentes, excluyendo este).
        var cruces = CalculadorCruces.Internos(regeneracion.CrucesInternos, empleados)
            .Concat(CalculadorCruces.Historicos(regeneracion.CrucesHistoricos, empleados))
            .Concat(await BuscarExternosAsync(proyecto.Id, regeneracion.DiasTrabajoRegenerados, empleados, ct))
            .ToList();

        var advertencias = new List<string>();
        if (regeneracion.HayDiasAnterioresAlCorte)
        {
            advertencias.Add($"Se generarán días anteriores al corte ({ReglasPersonal.Formato(corte)}) para el personal nuevo."); // E5 (M1)
        }

        var previsualizacion = new PrevisualizacionPersonalDto(
            corte,
            Personas(plan),
            regeneracion.Tramos.Select(t => new TramoDto(
                CalculadorCruces.NombreRol(t.Rol), t.Tipo == TipoAsignacionCronograma.Auto ? "AUTO" : "MANUAL", t.Bloque,
                new PersonaDto(CalculadorCruces.NombreRol(t.Persona.Rol), t.Persona.Numero),
                t.EmpleadoId, empleados[t.EmpleadoId].CodigoEkon, empleados[t.EmpleadoId].NombreCompleto, t.Inicio, t.Fin, t.Dias)).ToList(),
            cruces,
            CalculadorCruces.Resumen(cruces),
            advertencias);

        return new Resultado(null, new Calculo(proyecto, corte, plan, regeneracion, clavesGuardadas, previsualizacion));
    }

    private async Task<IReadOnlyList<CruceDto>> BuscarExternosAsync(int proyectoId, IReadOnlyList<DiaAsignado> trabajo,
        IReadOnlyDictionary<int, EmpleadoRef> empleados, CancellationToken ct)
    {
        if (trabajo.Count == 0)
        {
            return [];
        }

        var existentes = await crucesExternos.BuscarAsync(
            trabajo.Select(d => d.EmpleadoId).Distinct().ToList(), trabajo.Min(d => d.Fecha), trabajo.Max(d => d.Fecha),
            excluirProyectoId: proyectoId, ct);
        return CalculadorCruces.Externos(trabajo, existentes, empleados);
    }

    private static List<PersonaCambioDto> Personas(PlanEdicion plan) =>
    [
        .. plan.Historicas.Select(h => new PersonaCambioDto($"h{h.Id}", h.Id, CalculadorCruces.NombreRol(h.Rol), h.Numero, Empleado(h),
            "HISTORICO", EdicionPersonalValidador.SinCambio)),
        .. plan.Principales.Concat(plan.Backs).Select(p => new PersonaCambioDto(p.Clave, p.Id, CalculadorCruces.NombreRol(p.Rol), p.Numero,
            new EmpleadoEdicionDto(p.Empleado.Id, p.Empleado.CodigoEkon, p.Empleado.NombreCompleto),
            p.EsNueva ? "NUEVO" : "VIGENTE", p.Accion)),
        .. plan.Eliminadas.Select(x => new PersonaCambioDto($"e{x.Id}", x.Id, CalculadorCruces.NombreRol(x.Rol), x.Numero, Empleado(x),
            "VIGENTE", EdicionPersonalValidador.Eliminado)),
    ];

    // ------------------------------------------------------------------ escritura

    private static CambioPersonal CrearCambio(Calculo c, int version)
    {
        var plan = c.Plan;

        RelacionPrincipal Relacion(PersonaPlan p) => new(p.Relacion.PrincipalId, p.Relacion.PrincipalClaveNueva);
        static string Tipo(PersonaPlan p) =>
            p.TipoRegistro == TipoRegistroBack.Descanso ? ProyectoPersonal.TipoRegistroDescanso : ProyectoPersonal.TipoRegistroJornada;

        var vigentes = plan.Principales.Concat(plan.Backs).Where(p => !p.EsNueva).Select(p => new PersonaVigenteActualizada(
            p.Id!.Value, p.Jornada?.Id, p.Jornada?.DiasTrabajo, p.DiasDescanso, p.Inicio, p.Fin, Tipo(p), p.Cargo, p.Observacion,
            Relacion(p))).ToList();

        var nuevas = plan.Principales.Concat(plan.Backs).Where(p => p.EsNueva).Select(p => new PersonaNueva(
            p.Clave, p.Rol, p.Numero, p.Empleado.Id, p.Jornada?.Id, p.Jornada?.DiasTrabajo, p.DiasDescanso, p.Inicio, p.Fin,
            Tipo(p), p.Cargo, p.Observacion, Relacion(p))).ToList();

        var dias = c.Regeneracion.DiasAInsertar.Select(d => c.ClavesGuardadas.TryGetValue(d.Clave, out var id)
            ? new DiaParaInsertar(id, null, d.Dia)
            : new DiaParaInsertar(null, d.Clave, d.Dia)).ToList();

        // Actividad vigente en el corte (la de mayor versión si se solapan).
        var actividad = c.Datos.Actividades
            .Where(a => a.FechaInicio <= c.Corte && a.FechaFin >= c.Corte)
            .OrderByDescending(a => a.Version)
            .FirstOrDefault()?.Codigo;

        // D8: snapshot del personal RESULTANTE (históricas + vigentes + nuevas), formato común.
        var snapshot = SnapshotPersonal.Serializar(
            plan.Historicas.Select(h => new ElementoSnapshot(h.Numero, CalculadorCruces.NombreRol(h.Rol), h.CodigoEkon, h.NombreCompleto,
                    h.FechaInicio, h.FechaFin, h.JornadaCodigo, h.DiasTrabajo, h.DiasDescanso, h.TipoRegistro))
                .Concat(plan.Principales.Concat(plan.Backs).Select(p => new ElementoSnapshot(p.Numero, CalculadorCruces.NombreRol(p.Rol),
                    p.Empleado.CodigoEkon, p.Empleado.NombreCompleto, p.Inicio, p.Fin, p.Jornada?.Codigo, p.Jornada?.DiasTrabajo,
                    p.DiasDescanso, Tipo(p))))
                .OrderBy(x => x.Rol == "PRINCIPAL" ? 0 : 1).ThenBy(x => x.Numero));

        return new CambioPersonal(c.Datos.Id, c.Corte, version, TipoMovimiento, c.Datos.FechaInicio, c.Datos.FechaFin, actividad,
            snapshot, vigentes, nuevas, plan.Eliminadas.Select(x => x.Id).ToList(), dias);
    }

    private static EmpleadoEdicionDto Empleado(PersonaGuardada p) => new(p.EmpleadoId, p.CodigoEkon, p.NombreCompleto);

    /// <summary>R1: Admin ve todos (null); cualquier otro rol solo sus proyectos.</summary>
    private bool TryObtenerVisibilidad(out int? propietarioUsuarioId)
    {
        if (usuario.TieneRol(RolesApp.Admin))
        {
            propietarioUsuarioId = null;
            return true;
        }

        propietarioUsuarioId = usuario.UsuarioId;
        return propietarioUsuarioId is not null;
    }
}
