using App.Application.Comun;
using App.Application.Proyectos.Crear;
using App.Application.Proyectos.Estados;
using App.Application.Proyectos.Personal;
using App.Application.Seguridad;
using App.Domain.Proyectos.Cronograma;
using App.Domain.Proyectos.Estados;

namespace App.Application.Proyectos.Reactivacion;

/// <summary>
/// Caso de uso "Reactivar" (TAREA-17b, SUSPENDIDO → ACTIVO). Reutiliza la TAREA-17: núcleo del validador,
/// CalculoPersonal (motor con corte = R, cruces internos, históricos y externos), escritura D7 y snapshot.
/// Todo el personal guardado queda histórico (R5); el primer principal nuevo es el inicial (R6); el proyecto pasa a
/// ACTIVO con la nueva fecha fin (R4); se crea la actividad REACTIVACION (R8) y la etapa (R9).
/// Ver docs/fases/FASE_5_Estados_Proyecto.md §8.
/// </summary>
public sealed class ReactivacionServicio(
    IEdicionPersonalRepositorio repositorio,
    IReactivacionRepositorio repositorioReactivacion,
    IDatosReferenciaProyecto datos,
    IConsultaCrucesExternos crucesExternos,
    ReactivacionValidador validador,
    ITransaccionAsignaciones transaccion,
    IUsuarioActual usuario,
    TimeProvider reloj)
{
    public const string TipoMovimiento = "REACTIVACION";
    public const string AdvertenciaSinInicial = "El proyecto no tiene principal inicial; se propone el último principal.";
    public const string AdvertenciaInactivo = "El empleado del principal propuesto no está activo; elige otro principal.";
    public const string AdvertenciaSinPrincipales = "El proyecto no tiene principales; agrega uno.";
    public const string AdvertenciaDiasTranscurridos = "Se generarán días ya transcurridos.";

    // ------------------------------------------------------------------ GET reactivacion

    public async Task<ReactivacionDto?> ObtenerAsync(int proyectoId, CancellationToken ct)
    {
        if (!TryObtenerVisibilidad(out var propietario))
        {
            return null;
        }

        // Sin días: el GET no los necesita (corte mínimo = base vacía).
        var proyecto = await repositorio.ObtenerAsync(proyectoId, propietario, DateOnly.MinValue, ct);
        if (proyecto is null)
        {
            return null;
        }

        var limites = await datos.ObtenerLimitesAsync(ct);
        var motivo = ReactivacionValidador.MotivoNoReactivable(proyecto);
        var advertencias = new List<string>();
        var propuesto = motivo is null ? await ProponerPrincipalAsync(proyecto, advertencias, ct) : null;

        return new ReactivacionDto(
            proyecto.Id, proyecto.Codigo, proyecto.EstadoCodigo, proyecto.FechaInicio, proyecto.FechaFin, proyecto.FechaFin.AddDays(1),
            motivo is null, motivo, propuesto,
            proyecto.Personal.Select(p => new PersonaReactivacionDto(
                p.Id, CalculadorCruces.NombreRol(p.Rol), p.Numero, CalculoPersonal.Empleado(p), p.JornadaCodigo, p.FechaInicio, p.FechaFin,
                p.TipoRegistro, p.DiasDescanso, p.EsPrincipalInicial)).ToList(),
            new LimitesEdicionDto(limites.MaxPrincipales, limites.MaxBacks, limites.MaxDiasDescansoBack),
            advertencias);
    }

    /// <summary>
    /// R7: el principal inicial con mayor FechaFin (como vwProyectoResumen). Si no hay inicial, el principal con mayor
    /// FechaFin (con advertencia). Sin principales: null (con advertencia). Empleado inactivo: se propone con advertencia.
    /// </summary>
    private async Task<PrincipalPropuestoDto?> ProponerPrincipalAsync(DatosEdicion proyecto, List<string> advertencias, CancellationToken ct)
    {
        var principales = proyecto.Personal.Where(p => p.Rol == RolCronograma.Principal)
            .OrderByDescending(p => p.FechaFin).ThenByDescending(p => p.Numero).ToList();
        var elegido = principales.FirstOrDefault(p => p.EsPrincipalInicial);
        if (elegido is null && principales.Count > 0)
        {
            elegido = principales[0];
            advertencias.Add(AdvertenciaSinInicial);
        }

        if (elegido is null)
        {
            advertencias.Add(AdvertenciaSinPrincipales);
            return null;
        }

        var activos = await datos.ObtenerEmpleadosActivosAsync([elegido.EmpleadoId], ct);
        var activo = activos.ContainsKey(elegido.EmpleadoId);
        if (!activo)
        {
            advertencias.Add(AdvertenciaInactivo);
        }

        return new PrincipalPropuestoDto(
            new EmpleadoPropuestoDto(elegido.EmpleadoId, elegido.CodigoEkon, elegido.NombreCompleto, activo),
            elegido.JornadaCodigo, elegido.Cargo);
    }

    // ------------------------------------------------------------------ previsualizar / registrar

    public async Task<ResultadoReactivacion> PrevisualizarAsync(int proyectoId, ReactivarProyectoSolicitud s, CancellationToken ct)
    {
        var r = await CalcularAsync(proyectoId, s, ct);
        return r.Error ?? ResultadoReactivacion.Previsualizado(r.Calculo!.Previsualizacion);
    }

    public async Task<ResultadoReactivacion> RegistrarAsync(int proyectoId, ReactivarProyectoSolicitud s, CancellationToken ct)
    {
        // Verificación previa fuera de la transacción: no se abre el bloqueo si la solicitud no es válida o tiene cruces.
        var previo = await CalcularAsync(proyectoId, s, ct);
        if (previo.Error is { } error)
        {
            return error;
        }

        if (previo.Calculo!.Previsualizacion.Cruces.Count > 0)
        {
            return ResultadoReactivacion.ConCruces(previo.Calculo.Previsualizacion);
        }

        return await transaccion.EjecutarAsync(async ctTx =>
        {
            // Dentro del applock se vuelve a leer y a recalcular todo. Lo que era válido fuera y ya no lo es dentro
            // (p. ej. el proyecto ya no está SUSPENDIDO), o una FechaFin distinta, significa que el proyecto cambió.
            var r = await CalcularAsync(proyectoId, s, ctTx);
            if (r.Error is { } errorTx)
            {
                return errorTx.Estado == EstadoEdicion.Invalido ? ResultadoReactivacion.Cambiado() : errorTx;
            }

            var c = r.Calculo!;
            if (c.Datos.FechaFin != previo.Calculo.Datos.FechaFin)
            {
                return ResultadoReactivacion.Cambiado();
            }

            if (c.Previsualizacion.Cruces.Count > 0)
            {
                return ResultadoReactivacion.ConCruces(c.Previsualizacion);
            }

            try
            {
                var version = await repositorio.ObtenerUltimaVersionEtapaAsync(proyectoId, ctTx) + 1;
                await repositorio.AplicarAsync(CrearCambio(c, version), ctTx);
                return ResultadoReactivacion.Hecho(new ProyectoReactivadoDto(proyectoId, CodigosEstadoProyecto.Activo, version));
            }
            catch (ConflictoConcurrenciaException)
            {
                return ResultadoReactivacion.Cambiado(); // se revierte (confirmar = false)
            }
        }, x => x.Estado == EstadoEdicion.Realizado, ct);
    }

    // ------------------------------------------------------------------ cálculo

    private sealed record Calculo(
        DatosEdicion Datos, DateOnly Reactivacion, DateOnly FechaFin, PlanEdicion Plan, CalculoRegeneracion Regeneracion,
        ActividadReactivacionDto? Actividad, PrevisualizacionReactivacionDto Previsualizacion);

    private sealed record Resultado(ResultadoReactivacion? Error, Calculo? Calculo);

    private async Task<Resultado> CalcularAsync(int proyectoId, ReactivarProyectoSolicitud s, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (!TryObtenerVisibilidad(out var propietario))
        {
            return new Resultado(ResultadoReactivacion.NoEncontrado(), null);
        }

        // Corte = R (R5). Sin fecha la validación responde 400 antes de usar los días.
        var proyecto = await repositorio.ObtenerAsync(proyectoId, propietario, s.Fecha ?? DateOnly.MinValue, ct);
        if (proyecto is null)
        {
            return new Resultado(ResultadoReactivacion.NoEncontrado(), null);
        }

        // Datos de referencia: jornadas, límites y empleados activos (todas las personas son nuevas).
        var idsEmpleados = (s.Principales ?? []).Select(p => p.EmpleadoId)
            .Concat((s.Backs ?? []).Select(b => b.EmpleadoId))
            .OfType<int>().Distinct().ToList();
        var activos = idsEmpleados.Count == 0
            ? new Dictionary<int, EmpleadoRef>()
            : await datos.ObtenerEmpleadosActivosAsync(idsEmpleados, ct);
        var jornadas = await datos.ObtenerJornadasAsync(ct);
        var limites = await datos.ObtenerLimitesAsync(ct);

        var validacion = validador.Validar(s, proyecto, jornadas, limites, activos);
        if (validacion.Plan is not { } plan)
        {
            return new Resultado(ResultadoReactivacion.Invalido(validacion.Errores), null);
        }

        var reactivacion = s.Fecha!.Value;
        var fechaFin = s.FechaFin!.Value;
        var calculo = await CalculoPersonal.RegenerarAsync(proyecto, plan, reactivacion, fechaFin, crucesExternos, ct);

        // R8 / H11: la actividad vigente en la fecha de suspensión (FechaFin actual) continúa desde R.
        var actividad = await repositorioReactivacion.ObtenerActividadVigenteAsync(proyecto.Id, proyecto.FechaFin, ct) is { } a
            ? new ActividadReactivacionDto(a.Codigo, a.Descripcion, a.Tipo, reactivacion, fechaFin)
            : null;

        var advertencias = new List<string>();
        if (reactivacion < FechaNegocio.Hoy(reloj))
        {
            advertencias.Add(AdvertenciaDiasTranscurridos); // R3
        }

        var previsualizacion = new PrevisualizacionReactivacionDto(
            reactivacion, proyecto.FechaFin, fechaFin, actividad,
            CalculoPersonal.Personas(plan), calculo.Tramos, calculo.Cruces, CalculadorCruces.Resumen(calculo.Cruces), advertencias);

        return new Resultado(null, new Calculo(proyecto, reactivacion, fechaFin, plan, calculo, actividad, previsualizacion));
    }

    // ------------------------------------------------------------------ escritura

    private static CambioPersonal CrearCambio(Calculo c, int version)
    {
        var (vigentes, nuevas) = CalculoPersonal.PersonalParaEscribir(c.Plan);

        // R8: versión = máx + 1 sobre las actividades leídas dentro del applock (UQ_ProyectoActividad_Version como red).
        var actividad = c.Actividad is { } a
            ? new ActividadNueva(c.Datos.Actividades.Select(x => x.Version).DefaultIfEmpty(0).Max() + 1, a.Codigo, a.Descripcion, a.Tipo,
                a.FechaInicio, a.FechaFin)
            : null;

        // R9: etapa REACTIVACION, de R a la nueva fecha fin, corte = R y snapshot del personal resultante (históricas + nuevas).
        // Actividad: regla común de etapas (O3) con corte R sobre las actividades resultantes (la nueva cubre R).
        var actividades = c.Datos.Actividades.ToList();
        if (actividad is not null)
        {
            actividades.Add(new ActividadCorte(0, actividad.Version, actividad.Codigo, actividad.FechaInicio, actividad.FechaFin));
        }

        var actividadEtapa = ActividadVigente.Elegir(actividades, c.Datos.FechaInicio, c.FechaFin, c.Reactivacion)?.Codigo;
        return new CambioPersonal(c.Datos.Id, c.Reactivacion, version, TipoMovimiento, c.Reactivacion, c.FechaFin, actividadEtapa,
            CalculoPersonal.Snapshot(c.Plan), vigentes, nuevas, [], CalculoPersonal.Dias(c.Regeneracion),
            new ReactivacionAplicar(c.Plan.Principales[0].Clave, c.FechaFin, actividad));
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
