namespace App.Application.Erp;

/// <summary>Resultado de una consulta al ERP que depende de una compañía o proyecto existente.</summary>
/// <param name="NoEncontrado">Motivo del 404 (compañía o proyecto ERP inexistente); null si hay valor.</param>
public sealed record ResultadoErp<T>(T? Valor, string? NoEncontrado)
{
    public static ResultadoErp<T> Ok(T valor) => new(valor, null);
    public static ResultadoErp<T> Falta(string motivo) => new(default, motivo);
}

/// <summary>
/// Consultas de catálogos del ERP para la API. Verifica que la compañía (y el proyecto ERP, para actividades)
/// exista antes de listar: así se distingue "no existe" (404) de "existe pero no tiene datos" (lista vacía).
/// </summary>
public sealed class CatalogoErpConsultaServicio(ICatalogoErp erp)
{
    public const string CompaniaNoEncontrada = "Compañía no encontrada.";
    public const string ProyectoNoEncontrado = "Proyecto ERP no encontrado o no está activo.";

    public Task<IReadOnlyList<CompaniaErp>> ListarCompaniasAsync(CancellationToken ct) => erp.ListarCompaniasAsync(ct);

    public Task<IReadOnlyList<HorarioErp>> ListarHorariosAsync(CancellationToken ct) => erp.ListarHorariosAsync(ct);

    public async Task<ResultadoErp<IReadOnlyList<ProyectoErp>>> ListarProyectosAsync(int companiaId, CancellationToken ct) =>
        await erp.ObtenerCompaniaAsync(companiaId, ct) is null
            ? ResultadoErp<IReadOnlyList<ProyectoErp>>.Falta(CompaniaNoEncontrada)
            : ResultadoErp<IReadOnlyList<ProyectoErp>>.Ok(await erp.ListarProyectosAsync(companiaId, ct));

    public async Task<ResultadoErp<IReadOnlyList<DimensionErp>>> ListarDimensionesAsync(int companiaId, CancellationToken ct) =>
        await erp.ObtenerCompaniaAsync(companiaId, ct) is null
            ? ResultadoErp<IReadOnlyList<DimensionErp>>.Falta(CompaniaNoEncontrada)
            : ResultadoErp<IReadOnlyList<DimensionErp>>.Ok(await erp.ListarDimensionesAsync(companiaId, ct));

    public async Task<ResultadoErp<IReadOnlyList<ActividadErp>>> ListarActividadesAsync(
        int companiaId, string proyectoErpId, CancellationToken ct)
    {
        if (await erp.ObtenerCompaniaAsync(companiaId, ct) is null)
        {
            return ResultadoErp<IReadOnlyList<ActividadErp>>.Falta(CompaniaNoEncontrada);
        }

        if (await erp.ObtenerProyectoAsync(companiaId, proyectoErpId, ct) is null)
        {
            return ResultadoErp<IReadOnlyList<ActividadErp>>.Falta(ProyectoNoEncontrado);
        }

        return ResultadoErp<IReadOnlyList<ActividadErp>>.Ok(await erp.ListarActividadesAsync(companiaId, proyectoErpId, ct));
    }
}
