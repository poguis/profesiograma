namespace App.Application.Catalogos;

public interface ICatalogoConsultas
{
    /// <summary>Catálogos activos ordenados por Orden.</summary>
    Task<CatalogosDto> ObtenerTodosAsync(CancellationToken ct);

    Task<IReadOnlyList<ParametroDto>> ObtenerParametrosAsync(CancellationToken ct);
}
