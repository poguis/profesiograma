using App.Application.Proyectos.Personal;

namespace App.Application.Proyectos.Reactivacion;

// ------------------------------------------------------------------ solicitud (TAREA-17b)

/// <summary>
/// Cuerpo de POST /api/proyectos/{id}/reactivacion[/previsualizar].
/// Fecha = R (FechaFin actual &lt; R ≤ FechaFin); FechaFin = nueva fecha fin del proyecto (H5).
/// Personas con la forma de la TAREA-17, pero todas nuevas: sin id (R1). El primer principal empieza en R (R6).
/// </summary>
public sealed record ReactivarProyectoSolicitud(
    DateOnly? Fecha,
    DateOnly? FechaFin,
    IReadOnlyList<PrincipalEdicionSolicitud>? Principales,
    IReadOnlyList<BackEdicionSolicitud>? Backs);

// ------------------------------------------------------------------ lectura

/// <summary>Actividad vigente en la fecha de suspensión, con los datos que se copian a la fila REACTIVACION (R8).</summary>
public sealed record ActividadParaReactivar(string Codigo, string? Descripcion, string? Tipo);

/// <summary>Lecturas propias de la reactivación. El resto (proyecto, personal, días, escritura) es IEdicionPersonalRepositorio.</summary>
public interface IReactivacionRepositorio
{
    /// <summary>Actividad vigente en la fecha (la de mayor versión si se solapan); null si no hay.</summary>
    Task<ActividadParaReactivar?> ObtenerActividadVigenteAsync(int proyectoId, DateOnly fecha, CancellationToken ct);
}
