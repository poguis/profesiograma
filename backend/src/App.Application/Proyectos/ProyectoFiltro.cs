namespace App.Application.Proyectos;

/// <summary>
/// Parámetros crudos del query string del listado. Se reciben como texto para que
/// ProyectoConsultaServicio devuelva los errores de formato en español.
/// </summary>
public sealed record ProyectoListadoSolicitud(
    string? Estado, string? Grupo, string? Texto,
    string? Desde, string? Hasta,
    string? Pagina, string? Tamano);

/// <summary>Filtro ya validado que recibe Infrastructure. Null = sin filtro.</summary>
/// <param name="PropietarioUsuarioId">Visibilidad (R1): null = todos los proyectos (Admin).</param>
public sealed record ProyectoFiltro(
    string? EstadoCodigo, string? GrupoCodigo, string? Texto,
    DateOnly? Desde, DateOnly? Hasta,
    int Pagina, int Tamano,
    int? PropietarioUsuarioId);
