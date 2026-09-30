namespace App.Application.Erp;

/// <summary>
/// Catálogos del ERP (compañías, proyectos, dimensiones, actividades y horarios).
/// Los métodos Obtener* sirven para validar/leer por Id al registrar, sin confiar en lo que envía el navegador.
/// Errores de comunicación → <see cref="ErpNoDisponibleException"/>.
/// </summary>
public interface ICatalogoErp
{
    Task<IReadOnlyList<CompaniaErp>> ListarCompaniasAsync(CancellationToken ct);
    Task<CompaniaErp?> ObtenerCompaniaAsync(int companiaId, CancellationToken ct);

    /// <summary>Solo proyectos con status "Activo".</summary>
    Task<IReadOnlyList<ProyectoErp>> ListarProyectosAsync(int companiaId, CancellationToken ct);
    Task<ProyectoErp?> ObtenerProyectoAsync(int companiaId, string proyectoErpId, CancellationToken ct);

    Task<IReadOnlyList<DimensionErp>> ListarDimensionesAsync(int companiaId, CancellationToken ct);
    Task<DimensionErp?> ObtenerDimensionAsync(int companiaId, string uegpId, CancellationToken ct);

    Task<IReadOnlyList<ActividadErp>> ListarActividadesAsync(int companiaId, string proyectoErpId, CancellationToken ct);
    Task<ActividadErp?> ObtenerActividadAsync(int companiaId, string proyectoErpId, string actividadId, CancellationToken ct);

    /// <summary>Solo horarios con status "A".</summary>
    Task<IReadOnlyList<HorarioErp>> ListarHorariosAsync(CancellationToken ct);
    Task<HorarioErp?> ObtenerHorarioAsync(int codigo, CancellationToken ct);
}
