using App.Application.Proyectos.Estados;
using App.Application.Proyectos.Personal;
using App.Domain.Proyectos.Estados;

namespace App.Application.Proyectos.Cabecera;

// ------------------------------------------------------------------ solicitud (TAREA-18)

/// <summary>Cambio de actividad (C6): actividad del catálogo ERP del proyecto ERP y fecha desde la que aplica.</summary>
public sealed record ActividadCabeceraSolicitud(string? ActividadId, DateOnly? Desde);

/// <summary>Cuerpo de POST /api/proyectos/{id}/cabecera[/previsualizar]. Un campo ausente (null) no cambia.</summary>
public sealed record EditarCabeceraSolicitud(
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    int? HorarioCodigo,
    string? SalidaAlmuerzo,
    string? RegresoAlmuerzo,
    ActividadCabeceraSolicitud? Actividad);

// ------------------------------------------------------------------ lectura

/// <summary>Cabecera guardada del proyecto (sin datos sensibles). RowVer para detectar cambios entre lecturas.</summary>
public sealed record CabeceraGuardada(
    int Id, string Codigo, string EstadoCodigo, DateOnly FechaInicio, DateOnly FechaFin,
    string GrupoCodigo, bool RequiereProyectoErp, int CompaniaId, string? ProyectoErpId,
    int? HorarioCodigo, string? HorarioDescripcion, TimeOnly? HoraEntrada, TimeOnly? HoraSalida,
    TimeOnly? SalidaAlmuerzo, TimeOnly? RegresoAlmuerzo, byte[] RowVer);

/// <summary>Actividad guardada (ProyectoActividad) con descripción, tipo y tipo de movimiento.</summary>
public sealed record ActividadGuardada(
    int Id, int Version, string Codigo, string? Descripcion, string? Tipo, string TipoMovimiento, DateOnly FechaInicio, DateOnly FechaFin)
{
    public ActividadCorte Corte => new(Id, Version, Codigo, FechaInicio, FechaFin);
}

/// <summary>
/// Proyecto leído para editar la cabecera. DiasPosteriores = días con Fecha &gt; fecha pedida (solo hacen falta al acortar).
/// PrincipalesIniciales = Id del personal con EsPrincipalInicial (advertencia de C4).
/// </summary>
public sealed record DatosCabecera(
    CabeceraGuardada Cabecera,
    IReadOnlyList<PersonalCambio> Personal,
    IReadOnlySet<int> PrincipalesIniciales,
    IReadOnlyList<DiaCorte> DiasPosteriores,
    IReadOnlyList<ActividadGuardada> Actividades);

// ------------------------------------------------------------------ escritura

/// <summary>Horario del ERP con los mismos campos que guarda la creación.</summary>
public sealed record HorarioNuevo(
    int Codigo, string? Descripcion, TimeOnly? HoraEntrada, TimeOnly? HoraSalida, short? MinutosJornada, short? MinutosTrabajados, string? Tipo);

/// <summary>Actividad existente con fechas nuevas (P1: inicio movido; C3: fin extendido; C4/C6: fin recortado).</summary>
public sealed record ActividadModificada(int Id, DateOnly FechaInicio, DateOnly FechaFin);

/// <summary>Todo lo que se escribe en la transacción (recalculado dentro del applock).</summary>
/// <param name="Recorte">Plan de RecorteProyecto si se acorta la fecha fin (C4); null si no.</param>
/// <param name="Horario">null = el horario no cambia.</param>
public sealed record CambioCabeceraAplicar(
    int ProyectoId,
    int Version,
    string TipoMovimiento,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    DateOnly FechaCorte,
    string? ActividadCodigo,
    string SnapshotPersonal,
    HorarioNuevo? Horario,
    TimeOnly? SalidaAlmuerzo,
    TimeOnly? RegresoAlmuerzo,
    PlanRecorte? Recorte,
    IReadOnlyList<int> ActividadesEliminadas,
    IReadOnlyList<ActividadModificada> ActividadesModificadas,
    ActividadNueva? ActividadNueva);

public interface ICabeceraRepositorio
{
    /// <summary>
    /// Proyecto no eliminado y visible (propietarioUsuarioId null = Admin). null si no existe o no es visible.
    /// Carga los días con Fecha &gt; fechaDias (DateOnly.MaxValue = ninguno).
    /// </summary>
    Task<DatosCabecera?> ObtenerAsync(int proyectoId, int? propietarioUsuarioId, DateOnly fechaDias, CancellationToken ct);

    /// <summary>Última versión de etapa (0 si no hay). Dentro de la transacción.</summary>
    Task<int> ObtenerUltimaVersionEtapaAsync(int proyectoId, CancellationToken ct);

    /// <summary>Escribe el cambio (dentro de ITransaccionAsignaciones). Orden de la TAREA-14 (EscrituraRecorte).</summary>
    /// <exception cref="ConflictoConcurrenciaException">RowVer o duplicado de versión.</exception>
    Task AplicarAsync(CambioCabeceraAplicar cambio, CancellationToken ct);
}
