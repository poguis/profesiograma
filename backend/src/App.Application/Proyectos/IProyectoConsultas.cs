using App.Application.Comun;

namespace App.Application.Proyectos;

/// <summary>Consultas de solo lectura de proyectos. La visibilidad llega resuelta en los parámetros (no conoce roles).</summary>
public interface IProyectoConsultas
{
    Task<PaginaResultado<ProyectoResumenDto>> ListarAsync(ProyectoFiltro filtro, CancellationToken ct);

    /// <summary>
    /// Null si el proyecto no existe, está eliminado o, cuando propietarioUsuarioId no es null, no le pertenece.
    /// La actividad vigente se calcula respecto de <paramref name="hoy"/> (fecha de negocio, Ecuador).
    /// </summary>
    Task<ProyectoDetalleDto?> ObtenerDetalleAsync(int id, int? propietarioUsuarioId, DateOnly hoy, CancellationToken ct);
}
