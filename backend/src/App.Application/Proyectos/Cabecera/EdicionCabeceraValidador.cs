using App.Application.Comun;
using App.Application.Erp;
using App.Application.Proyectos.Crear;
using App.Application.Proyectos.Personal;
using App.Domain.Proyectos;
using App.Domain.Proyectos.Cabecera;
using App.Domain.Proyectos.Cronograma;
using App.Domain.Proyectos.Estados;
using static App.Application.Proyectos.Crear.ReglasPersonal;

namespace App.Application.Proyectos.Cabecera;

/// <summary>Plan válido de la edición de cabecera (lo usan la vista previa y la escritura).</summary>
public sealed record PlanCabecera(
    DateOnly FechaInicio,
    DateOnly FechaFin,
    bool CambiaInicio,
    bool CambiaFin,
    HorarioErp? HorarioNuevo,
    TimeOnly? SalidaAlmuerzo,
    TimeOnly? RegresoAlmuerzo,
    bool CambiaAlmuerzo,
    PlanRecorte? Recorte,
    IReadOnlyList<DescansoAgregado> DescansosAgregados,
    IReadOnlyList<ActividadCorte> ActividadesFinales,
    IReadOnlyList<int> ActividadesEliminadas,
    ActividadNueva? ActividadNueva,
    IReadOnlyList<string> Advertencias)
{
    public bool CambiaActividad => ActividadNueva is not null;
    public bool SinCambios => !CambiaInicio && !CambiaFin && HorarioNuevo is null && !CambiaAlmuerzo && !CambiaActividad;

    /// <summary>C7: solo cambia la actividad → CAMBIO_ACTIVIDAD; cualquier otro cambio → EDICION_CABECERA.</summary>
    public string TipoEtapa => CambiaActividad && !CambiaInicio && !CambiaFin && HorarioNuevo is null && !CambiaAlmuerzo
        ? EdicionCabeceraValidador.TipoCambioActividad
        : EdicionCabeceraValidador.TipoEdicionCabecera;
}

public sealed record ResultadoValidacionCabecera(PlanCabecera? Plan, IReadOnlyDictionary<string, string[]> Errores);

/// <summary>
/// Validación y plan de "Editar cabecera" (TAREA-18: C1–C6, P1–P3). Orden de aplicación: inicio (P1) → fecha fin
/// (C3 ampliar / C4 acortar con RecorteProyecto) → actividad (C6) sobre el resultado. Horario y almuerzo (C5) con
/// ReglasHorarioAlmuerzo (mismos mensajes que la creación). Ver docs/fases/FASE_5_Edicion_Cabecera.md.
/// </summary>
public sealed class EdicionCabeceraValidador(ICatalogoErp erp)
{
    public const string TipoEdicionCabecera = "EDICION_CABECERA";
    public const string TipoCambioActividad = "CAMBIO_ACTIVIDAD";
    public const string MensajePersonalAntesDelInicio = "Hay personal que empieza antes de la nueva fecha de inicio; ajusta primero el personal.";
    public const string MensajeActividadesAntesDelInicio = "Hay actividades que terminan antes de la nueva fecha de inicio.";
    public const string AdvertenciaActividadPasada = "La nueva actividad aplica desde una fecha ya transcurrida.";
    public const string AdvertenciaSinPrincipalInicial = "El proyecto quedará sin principal inicial.";

    public async Task<ResultadoValidacionCabecera> ValidarAsync(EditarCabeceraSolicitud s, DatosCabecera datos, DateOnly hoy, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(datos);
        var c = datos.Cabecera;
        var e = new Dictionary<string, List<string>>();

        // C1: solo proyectos ACTIVO.
        if (MotivoNoEditable(c) is { } motivo)
        {
            Agregar(e, "proyecto", motivo);
            return Invalido(e);
        }

        // --- Fechas (C2, C3, C4, P1, P2)
        var inicio = s.FechaInicio ?? c.FechaInicio;
        var fin = s.FechaFin ?? c.FechaFin;
        var cambiaInicio = inicio != c.FechaInicio;
        var cambiaFin = fin != c.FechaFin;

        if (cambiaInicio)
        {
            if (MotivoInicioNoEditable(c, hoy) is { } motivoInicio)
            {
                Agregar(e, "fechaInicio", motivoInicio);
            }
            else if (inicio < hoy)
            {
                Agregar(e, "fechaInicio", $"La nueva fecha de inicio no puede ser anterior a hoy ({Formato(hoy)}).");
            }
            else if (datos.Personal.Any(p => p.Corte.FechaInicio < inicio))
            {
                Agregar(e, "fechaInicio", MensajePersonalAntesDelInicio);
            }
        }

        if (cambiaFin && fin < hoy)
        {
            Agregar(e, "fechaFin", $"La fecha fin no puede ser anterior a hoy ({Formato(hoy)}).");
        }
        else if (fin < inicio)
        {
            Agregar(e, "fechaFin", "La fecha fin debe ser mayor o igual a la fecha de inicio.");
        }

        // P1: las actividades que empezaban en el inicio anterior pasan al nuevo.
        var actividades = datos.Actividades.Select(a => a.Corte).ToList();
        if (cambiaInicio && !e.ContainsKey("fechaInicio"))
        {
            var movimiento = MovimientoInicioProyecto.Calcular(actividades, c.FechaInicio, inicio);
            if (movimiento.TerminanAntes)
            {
                Agregar(e, "fechaInicio", MensajeActividadesAntesDelInicio);
            }

            actividades = [.. movimiento.Actividades];
        }

        // --- Horario y almuerzo (C5)
        HorarioErp? horarioNuevo = null;
        if (s.HorarioCodigo is int codigoHorario && codigoHorario != c.HorarioCodigo)
        {
            horarioNuevo = await ReglasHorarioAlmuerzo.ValidarHorarioAsync(codigoHorario, erp, e, ct);
        }

        var salida = s.SalidaAlmuerzo is null
            ? c.SalidaAlmuerzo
            : ReglasHorarioAlmuerzo.LeerHora(s.SalidaAlmuerzo, "salidaAlmuerzo", ReglasHorarioAlmuerzo.MensajeSalidaObligatoria, e);
        var regreso = s.RegresoAlmuerzo is null
            ? c.RegresoAlmuerzo
            : ReglasHorarioAlmuerzo.LeerHora(s.RegresoAlmuerzo, "regresoAlmuerzo", ReglasHorarioAlmuerzo.MensajeRegresoObligatorio, e);
        if (s.SalidaAlmuerzo is not null || s.RegresoAlmuerzo is not null)
        {
            ReglasHorarioAlmuerzo.ValidarAlmuerzo(salida, regreso, s.SalidaAlmuerzo is not null, s.RegresoAlmuerzo is not null, e);
        }

        var cambiaAlmuerzo = salida != c.SalidaAlmuerzo || regreso != c.RegresoAlmuerzo;

        // --- Actividad (C6): datos básicos y catálogo ERP.
        ActividadErp? actividadErp = null;
        if (s.Actividad is { } solicitud)
        {
            actividadErp = await ValidarActividadAsync(solicitud, c, inicio, fin, e, ct);
        }

        if (e.Count > 0)
        {
            return Invalido(e);
        }

        // --- Fecha fin: ampliar (C3) o acortar con RecorteProyecto (C4).
        var advertencias = new List<string>();
        PlanRecorte? recorte = null;
        IReadOnlyList<DescansoAgregado> descansos = [];
        if (fin > c.FechaFin)
        {
            actividades = [.. AmpliacionProyecto.Calcular(actividades, c.FechaFin, fin)];
        }
        else if (fin < c.FechaFin)
        {
            recorte = RecorteProyecto.Calcular(inicio, c.FechaFin, fin, datos.Personal.Select(p => p.Corte).ToList(),
                datos.DiasPosteriores, actividades);
            var eliminadas = recorte.ActividadesEliminadas.ToHashSet();
            var recortadas = recorte.ActividadesRecortadas.ToDictionary(r => r.Id, r => r.FinNuevo);
            actividades = actividades.Where(a => !eliminadas.Contains(a.Id))
                .Select(a => recortadas.TryGetValue(a.Id, out var nuevoFin) ? a with { FechaFin = nuevoFin } : a)
                .ToList();

            // H15 (TAREA-18b): el descanso posterior de los backs que quedan (recortados o no) se vuelve a insertar, como lo
            // generaría el motor; el recorte lo borró por estar después de F.
            var eliminados = recorte.PersonalEliminado.ToHashSet();
            var nuevosFines = recorte.PersonalRecortado.ToDictionary(r => r.Id, r => r.FinNuevo);
            descansos = DescansoBacksTrasRecorte.Calcular(fin, datos.Personal
                .Where(p => p.Corte.Rol == RolCronograma.Back && !eliminados.Contains(p.Corte.Id))
                .Select(p => new BackTrasRecorte(p.Corte.Id, p.Corte.Numero, p.Corte.EmpleadoId, p.Corte.FechaInicio,
                    nuevosFines.GetValueOrDefault(p.Corte.Id, p.Corte.FechaFin),
                    p.TipoRegistro == ProyectoPersonal.TipoRegistroDescanso ? TipoRegistroBack.Descanso : TipoRegistroBack.Jornada,
                    p.DiasDescanso))
                .ToList());

            // C4: el recorte elimina a todos los principales iniciales (no bloquea).
            if (datos.PrincipalesIniciales.Count > 0 && datos.PrincipalesIniciales.All(recorte.PersonalEliminado.Contains))
            {
                advertencias.Add(AdvertenciaSinPrincipalInicial);
            }
        }

        // --- Actividad (C6) sobre el resultado de la fecha fin.
        ActividadNueva? actividadNueva = null;
        if (actividadErp is not null)
        {
            var versionMaxima = datos.Actividades.Select(a => a.Version).DefaultIfEmpty(0).Max();
            var cambio = CambioActividad.Calcular(actividades, actividadErp.Id, s.Actividad!.Desde!.Value, fin, versionMaxima);
            if (cambio.MismaActividad)
            {
                Agregar(e, "actividad.actividadId",
                    $"La actividad {actividadErp.Id} ya está vigente el {Formato(s.Actividad.Desde.Value)}.");
                return Invalido(e);
            }

            actividades = [.. cambio.Actividades];
            var nueva = cambio.Nueva!;
            actividadNueva = new ActividadNueva(nueva.Version, nueva.Codigo, Truncar(actividadErp.Descripcion, 300),
                Truncar(actividadErp.Tipo, 100), nueva.FechaInicio, nueva.FechaFin);
            if (nueva.FechaInicio < hoy)
            {
                advertencias.Add(AdvertenciaActividadPasada);
            }
        }

        var finales = actividades.Select(a => a.Id).ToHashSet();
        var actividadesEliminadas = datos.Actividades.Where(a => !finales.Contains(a.Id)).Select(a => a.Id).ToList();

        var plan = new PlanCabecera(inicio, fin, cambiaInicio, cambiaFin, horarioNuevo, salida, regreso, cambiaAlmuerzo, recorte, descansos,
            actividades, actividadesEliminadas, actividadNueva, advertencias);
        if (plan.SinCambios)
        {
            advertencias.Add(ResultadoEdicionPersonal.AdvertenciaSinCambios); // C9
        }

        return new ResultadoValidacionCabecera(plan, Congelar(e));
    }

    /// <summary>C1: motivo por el que no se puede editar la cabecera (null = se puede).</summary>
    public static string? MotivoNoEditable(CabeceraGuardada c) =>
        string.Equals(c.EstadoCodigo, CodigosEstadoProyecto.Activo, StringComparison.OrdinalIgnoreCase)
            ? null
            : $"Solo se puede editar la cabecera de un proyecto ACTIVO (estado actual: {c.EstadoCodigo}).";

    /// <summary>C2: el inicio solo se puede cambiar si el proyecto aún no empieza (inicio actual &gt; hoy).</summary>
    public static string? MotivoInicioNoEditable(CabeceraGuardada c, DateOnly hoy) =>
        c.FechaInicio > hoy ? null : $"La fecha de inicio ya no se puede cambiar: el proyecto empezó el {Formato(c.FechaInicio)}.";

    private async Task<ActividadErp?> ValidarActividadAsync(ActividadCabeceraSolicitud s, CabeceraGuardada c, DateOnly inicio, DateOnly fin,
        Dictionary<string, List<string>> e, CancellationToken ct)
    {
        if (!c.RequiereProyectoErp || c.ProyectoErpId is null)
        {
            Agregar(e, "actividad.actividadId", $"El grupo {c.GrupoCodigo} no usa actividad.");
            return null;
        }

        var actividadId = LectorParametros.Normalizar(s.ActividadId);
        if (actividadId is null)
        {
            Agregar(e, "actividad.actividadId", "La actividad es obligatoria.");
        }

        if (s.Desde is not DateOnly desde)
        {
            Agregar(e, "actividad.desde", "La fecha desde la que aplica la actividad es obligatoria.");
        }
        else if (desde < inicio || desde > fin)
        {
            Agregar(e, "actividad.desde", $"La fecha desde debe estar dentro del rango del proyecto ({Formato(inicio)} – {Formato(fin)}).");
        }

        if (actividadId is null)
        {
            return null;
        }

        var actividad = await erp.ObtenerActividadAsync(c.CompaniaId, c.ProyectoErpId, actividadId, ct);
        if (actividad is null)
        {
            Agregar(e, "actividad.actividadId", "La actividad no existe en el proyecto ERP.");
        }

        return actividad;
    }

    private static string? Truncar(string? valor, int maximo) =>
        valor is null || valor.Length <= maximo ? valor : valor[..maximo];

    private static ResultadoValidacionCabecera Invalido(Dictionary<string, List<string>> e) => new(null, Congelar(e));
}
