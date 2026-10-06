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
                    p.Id, CalculadorCruces.NombreRol(p.Rol), p.Numero, CalculoPersonal.Empleado(p), historica ? "HISTORICO" : "VIGENTE",
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

        if (previo.Calculo!.SinCambios)
        {
            return ResultadoEdicionPersonal.Invalido(ResultadoEdicionPersonal.ErroresSinCambios()); // C9
        }

        if (previo.Calculo.Previsualizacion.Cruces.Count > 0)
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
        DatosEdicion Datos, DateOnly Corte, PlanEdicion Plan, CalculoRegeneracion Regeneracion, PrevisualizacionPersonalDto Previsualizacion,
        bool SinCambios);

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

        // Motor (TAREA-16) y cruces: cálculo común con la reactivación (CalculoPersonal).
        var calculo = await CalculoPersonal.RegenerarAsync(proyecto, plan, corte, proyecto.FechaFin, crucesExternos, ct);

        var advertencias = new List<string>();
        if (calculo.Regeneracion.HayDiasAnterioresAlCorte)
        {
            advertencias.Add($"Se generarán días anteriores al corte ({ReglasPersonal.Formato(corte)}) para el personal nuevo."); // E5 (M1)
        }

        // C9: sin cambios = todas las personas enviadas SIN_CAMBIO, ninguna nueva y ninguna eliminada.
        var sinCambios = plan.Eliminadas.Count == 0
                         && plan.Principales.Concat(plan.Backs).All(p => !p.EsNueva && p.Accion == EdicionPersonalValidador.SinCambio);
        if (sinCambios)
        {
            advertencias.Add(ResultadoEdicionPersonal.AdvertenciaSinCambios);
        }

        var previsualizacion = new PrevisualizacionPersonalDto(
            corte,
            CalculoPersonal.Personas(plan),
            calculo.Tramos,
            calculo.Cruces,
            CalculadorCruces.Resumen(calculo.Cruces),
            advertencias);

        return new Resultado(null, new Calculo(proyecto, corte, plan, calculo, previsualizacion, sinCambios));
    }

    // ------------------------------------------------------------------ escritura

    private static CambioPersonal CrearCambio(Calculo c, int version)
    {
        var (vigentes, nuevas) = CalculoPersonal.PersonalParaEscribir(c.Plan);
        return new CambioPersonal(c.Datos.Id, c.Corte, version, TipoMovimiento, c.Datos.FechaInicio, c.Datos.FechaFin,
            ActividadVigente.Elegir(c.Datos.Actividades, c.Datos.FechaInicio, c.Datos.FechaFin, c.Corte)?.Codigo, // O3: regla de etapas
            CalculoPersonal.Snapshot(c.Plan),
            vigentes, nuevas, c.Plan.Eliminadas.Select(x => x.Id).ToList(), CalculoPersonal.Dias(c.Regeneracion));
    }

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
