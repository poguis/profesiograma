using App.Application.Erp;

namespace App.Infrastructure.Erp;

/// <summary>
/// Base común de los modos Http y Simulado: los Obtener* buscan en el Listar* correspondiente
/// (una sola definición de "obtener por Id"; en Http los listados están en caché).
/// </summary>
internal abstract class CatalogoErpBase : ICatalogoErp
{
    public abstract Task<IReadOnlyList<CompaniaErp>> ListarCompaniasAsync(CancellationToken ct);
    public abstract Task<IReadOnlyList<ProyectoErp>> ListarProyectosAsync(int companiaId, CancellationToken ct);
    public abstract Task<IReadOnlyList<DimensionErp>> ListarDimensionesAsync(int companiaId, CancellationToken ct);
    public abstract Task<IReadOnlyList<ActividadErp>> ListarActividadesAsync(int companiaId, string proyectoErpId, CancellationToken ct);
    public abstract Task<IReadOnlyList<HorarioErp>> ListarHorariosAsync(CancellationToken ct);

    public async Task<CompaniaErp?> ObtenerCompaniaAsync(int companiaId, CancellationToken ct) =>
        (await ListarCompaniasAsync(ct)).FirstOrDefault(c => c.Id == companiaId);

    public async Task<ProyectoErp?> ObtenerProyectoAsync(int companiaId, string proyectoErpId, CancellationToken ct) =>
        (await ListarProyectosAsync(companiaId, ct)).FirstOrDefault(p => MismoId(p.Id, proyectoErpId));

    public async Task<DimensionErp?> ObtenerDimensionAsync(int companiaId, string uegpId, CancellationToken ct) =>
        (await ListarDimensionesAsync(companiaId, ct)).FirstOrDefault(d => MismoId(d.UegpId, uegpId));

    public async Task<ActividadErp?> ObtenerActividadAsync(int companiaId, string proyectoErpId, string actividadId, CancellationToken ct) =>
        (await ListarActividadesAsync(companiaId, proyectoErpId, ct)).FirstOrDefault(a => MismoId(a.Id, actividadId));

    public async Task<HorarioErp?> ObtenerHorarioAsync(int codigo, CancellationToken ct) =>
        (await ListarHorariosAsync(ct)).FirstOrDefault(h => h.Codigo == codigo);

    private static bool MismoId(string a, string? b) =>
        string.Equals(a.Trim(), b?.Trim(), StringComparison.OrdinalIgnoreCase);
}
