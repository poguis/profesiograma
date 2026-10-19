using App.Application.Proyectos;

namespace App.Application.Cronograma;

// ------------------------------------------------------------------ solicitudes (query string como texto: R3)

/// <summary>GET /api/cronograma. Fechas "yyyy-MM-dd"; sin fechas, el mes actual en Ecuador (D6).</summary>
public sealed record CronogramaSolicitud(string? Desde, string? Hasta, string? ProyectoId, string? DepartamentoId, string? CodigoEkon);

/// <summary>GET /api/proyectos/{id}/cronograma. Sin fechas: desde el inicio del proyecto, máximo CRONOGRAMA_MAX_DIAS (D12).</summary>
public sealed record ProyectoCronogramaSolicitud(string? Desde, string? Hasta);

// ------------------------------------------------------------------ respuesta (dispersa: los días LIBRE no se envían, D7)

public sealed record HorarioCronogramaDto(int? Codigo, string? Descripcion, TimeOnly? Entrada, TimeOnly? Salida, bool CruzaMedianoche);

public sealed record ProyectoCronogramaDto(
    int Id, string Codigo, string Nombre, string Estado, string Grupo, int CompaniaId, string? ProyectoErpId,
    string? DimensionUegpId, HorarioCronogramaDto Horario, AlmuerzoDto Almuerzo);

public sealed record AsignacionCeldaDto(int ProyectoId, string Rol, string? Actividad);

public sealed record NovedadCeldaDto(
    string Tipo, string Sigla, string Color, string Aplicacion, string Origen, string EstadoReporte, string ObservacionReporte);

/// <param name="Estado">NOVEDAD | CHOQUE | PROYECTO | OTRO_PROYECTO | DESCANSO.</param>
/// <param name="OtrosProyectos">D2: días de trabajo (rol ≠ DESCANSO) en proyectos que no vienen en la respuesta; sin datos de esos proyectos.</param>
/// <param name="NovedadesExtra">D9: otras novedades PERSONA del mismo día (gana la de Id mayor).</param>
public sealed record CeldaCronogramaDto(
    DateOnly Fecha, string Estado, IReadOnlyList<AsignacionCeldaDto> Asignaciones, int OtrosProyectos, NovedadCeldaDto? Novedad,
    int NovedadesExtra);

/// <param name="Cargo">Cargo asignado en el proyecto (el del último día del rango).</param>
public sealed record AsignadoEnDto(int ProyectoId, string? Cargo);

public sealed record PersonaCronogramaDto(string CodigoEkon, string Nombre, IReadOnlyList<AsignadoEnDto> AsignadoEn,
    IReadOnlyList<CeldaCronogramaDto> Dias);

public sealed record LeyendaNovedadDto(string Tipo, string Sigla, string Color);

/// <param name="Hasta">Fin efectivo (recortado si el rango pedido superaba MaxDias).</param>
/// <param name="Recortado">D5 revisada (TAREA-21): el rango superaba MaxDias y se muestran los primeros MaxDias días.</param>
/// <param name="HastaSolicitado">Fin pedido (o el fin del proyecto con D12) cuando hubo recorte; null si no.</param>
public sealed record CronogramaDto(
    DateOnly Desde, DateOnly Hasta, int MaxDias, IReadOnlyList<ProyectoCronogramaDto> Proyectos,
    IReadOnlyList<PersonaCronogramaDto> Personas, IReadOnlyList<LeyendaNovedadDto> LeyendaNovedades,
    bool Recortado = false, DateOnly? HastaSolicitado = null);

// ------------------------------------------------------------------ lectura (Infrastructure)

/// <param name="PropietarioUsuarioId">R1: null = Admin (todos); si no, solo los proyectos de ese usuario.</param>
/// <param name="SoloProyectoId">Endpoint por proyecto: solo los días de ese proyecto.</param>
/// <param name="ProyectoId">Personas con días en ese proyecto (con todos sus proyectos visibles).</param>
/// <param name="DepartamentoId">Personas con días en proyectos de ese departamento (con todos sus proyectos visibles).</param>
public sealed record FiltroCronograma(
    DateOnly Desde, DateOnly Hasta, int? PropietarioUsuarioId, int? SoloProyectoId = null, int? ProyectoId = null,
    int? DepartamentoId = null, string? CodigoEkon = null);

/// <summary>D2: qué proyectos NO vienen en la respuesta (se cuentan sin exponer sus datos).</summary>
/// <param name="VisiblesDePropietario">Global: los de otro propietario (no visibles para el usuario).</param>
/// <param name="SalvoProyectoId">Por proyecto: todos los demás proyectos.</param>
public sealed record FiltroOtrosProyectos(
    IReadOnlyCollection<int> EmpleadoIds, DateOnly Desde, DateOnly Hasta, int? VisiblesDePropietario, int? SalvoProyectoId);

public sealed record DiaCronograma(int ProyectoId, int EmpleadoId, string CodigoEkon, string NombreCompleto, DateOnly Fecha,
    byte RolAsignacionId, string Rol, string? Cargo);

public sealed record DiaOtrosProyectos(int EmpleadoId, DateOnly Fecha, int Cantidad);

public sealed record NovedadDiaCronograma(
    int NovedadId, DateOnly Fecha, byte TipoAplicacionId, string Aplicacion, int? EmpleadoId, int? ProyectoId, string Tipo,
    string Sigla, string Color, string EstadoReporte, string ObservacionReporte, string Origen);

public sealed record ActividadCronograma(int ProyectoId, int Id, int Version, string Codigo, DateOnly FechaInicio, DateOnly FechaFin);

public sealed record RangoProyecto(int Id, DateOnly FechaInicio, DateOnly FechaFin);

/// <summary>Consultas del cronograma (TAREA-20). Solo lectura, sin N+1: cada una es una sola consulta.</summary>
public interface ICronogramaConsultas
{
    /// <summary>Parametro CRONOGRAMA_MAX_DIAS; null si falta o no es un entero.</summary>
    Task<int?> ObtenerMaxDiasAsync(CancellationToken ct);

    /// <summary>(1) Días asignados en el rango de proyectos no eliminados y visibles, con los filtros.</summary>
    Task<IReadOnlyList<DiaCronograma>> ObtenerDiasAsync(FiltroCronograma filtro, CancellationToken ct);

    /// <summary>(1b) D2: cantidad de días de trabajo por persona y fecha en proyectos que no vienen en la respuesta.</summary>
    Task<IReadOnlyList<DiaOtrosProyectos>> ObtenerOtrosProyectosAsync(FiltroOtrosProyectos filtro, CancellationToken ct);

    /// <summary>(2) Novedades no anuladas del rango: GENERAL, PERSONA de esas personas y PROYECTO de esos proyectos.</summary>
    Task<IReadOnlyList<NovedadDiaCronograma>> ObtenerNovedadesAsync(
        IReadOnlyCollection<int> empleadoIds, IReadOnlyCollection<int> proyectoIds, DateOnly desde, DateOnly hasta, CancellationToken ct);

    /// <summary>(3) Actividades de esos proyectos que se solapan con el rango.</summary>
    Task<IReadOnlyList<ActividadCronograma>> ObtenerActividadesAsync(
        IReadOnlyCollection<int> proyectoIds, DateOnly desde, DateOnly hasta, CancellationToken ct);

    /// <summary>(4) Cabeceras de esos proyectos (horario, almuerzo, compañía, ERP y grupo).</summary>
    Task<IReadOnlyList<ProyectoCronogramaDto>> ObtenerProyectosAsync(IReadOnlyCollection<int> proyectoIds, CancellationToken ct);

    /// <summary>(5) Proyecto no eliminado y visible (R1); null si no existe o no es visible (404).</summary>
    Task<RangoProyecto?> ObtenerRangoProyectoAsync(int proyectoId, int? propietarioUsuarioId, CancellationToken ct);
}
