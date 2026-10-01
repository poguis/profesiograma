namespace App.Application.Proyectos.Crear;

/// <summary>
/// Cuerpo de POST /api/proyectos y /api/proyectos/previsualizar (FASE_5 §7.1).
/// Solo Ids y valores elegidos: nombres, RUC, horario y actividad los obtiene el servidor del ERP.
/// Horas del almuerzo como texto "HH:mm" (validación en español).
/// </summary>
public sealed record CrearProyectoSolicitud(
    int? CompaniaId,
    string? Grupo,
    string? ProyectoErpId,
    string? ActividadId,
    string? DimensionUegpId,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    int? HorarioCodigo,
    string? SalidaAlmuerzo,
    string? RegresoAlmuerzo,
    int? DepartamentoId,
    IReadOnlyList<PrincipalSolicitud>? Principales,
    IReadOnlyList<BackSolicitud>? Backs);

/// <summary>Principal. El servidor le asigna Numero = posición en la lista (1..n).</summary>
public sealed record PrincipalSolicitud(int? EmpleadoId, string? Jornada, DateOnly? FechaInicio, DateOnly? FechaFin, string? Cargo);

/// <summary>Back. Numero = posición (1..n). PrincipalRelacionado = número (posición) del principal que cubre.</summary>
public sealed record BackSolicitud(
    int? EmpleadoId,
    string? TipoRegistro,
    int? DiasDescanso,
    DateOnly? FechaInicio,
    DateOnly? FechaFin,
    int? PrincipalRelacionado,
    string? Observacion,
    string? Cargo);
