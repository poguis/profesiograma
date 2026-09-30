namespace App.Application.Comun;

/// <summary>Página de resultados de un listado. Total = registros que cumplen el filtro (sin paginar).</summary>
public sealed record PaginaResultado<T>(IReadOnlyList<T> Items, int Pagina, int Tamano, int Total);
