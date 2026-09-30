using App.Application.Erp;

namespace App.Application.Tests.Erp;

/// <summary>404 para compañía o proyecto ERP inexistente; lista (posiblemente vacía) si existen.</summary>
public class CatalogoErpConsultaServicioTests
{
    private static readonly CatalogoFalso Catalogo = new();
    private readonly CatalogoErpConsultaServicio servicio = new(Catalogo);
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Proyectos_CompaniaExistente_DevuelveLista()
    {
        var r = await servicio.ListarProyectosAsync(9001, Ct);
        Assert.Null(r.NoEncontrado);
        Assert.Equal([new ProyectoErp("DEV-ERP-001", "PROYECTO ERP DE PRUEBA", "Activo")], r.Valor);
    }

    [Fact]
    public async Task Proyectos_CompaniaInexistente_NoEncontrado()
    {
        var r = await servicio.ListarProyectosAsync(1234, Ct);
        Assert.Equal(CatalogoErpConsultaServicio.CompaniaNoEncontrada, r.NoEncontrado);
        Assert.Null(r.Valor);
    }

    [Fact]
    public async Task Dimensiones_CompaniaInexistente_NoEncontrado() =>
        Assert.Equal(CatalogoErpConsultaServicio.CompaniaNoEncontrada, (await servicio.ListarDimensionesAsync(1234, Ct)).NoEncontrado);

    [Fact]
    public async Task Actividades_ProyectoExistente_DevuelveLista()
    {
        var r = await servicio.ListarActividadesAsync(9001, "DEV-ERP-001", Ct);
        Assert.Equal([new ActividadErp("DEV.01", "ACTIVIDAD DE PRUEBA", "PRUEBA")], r.Valor);
    }

    [Fact]
    public async Task Actividades_CompaniaInexistente_NoEncontradoCompania() =>
        Assert.Equal(CatalogoErpConsultaServicio.CompaniaNoEncontrada,
            (await servicio.ListarActividadesAsync(1234, "DEV-ERP-001", Ct)).NoEncontrado);

    [Fact]
    public async Task Actividades_ProyectoInexistente_NoEncontradoProyecto() =>
        Assert.Equal(CatalogoErpConsultaServicio.ProyectoNoEncontrado,
            (await servicio.ListarActividadesAsync(9001, "NO-EXISTE", Ct)).NoEncontrado);

    private sealed class CatalogoFalso : ICatalogoErp
    {
        private static readonly CompaniaErp Compania = new(9001, "COMPAÑÍA DE PRUEBA S.A.", "PRUEBA", "0999999999001");
        private static readonly ProyectoErp Proyecto = new("DEV-ERP-001", "PROYECTO ERP DE PRUEBA", "Activo");

        public Task<IReadOnlyList<CompaniaErp>> ListarCompaniasAsync(CancellationToken ct) => Lista(Compania);
        public Task<CompaniaErp?> ObtenerCompaniaAsync(int companiaId, CancellationToken ct) =>
            Task.FromResult(companiaId == Compania.Id ? Compania : null);
        public Task<IReadOnlyList<ProyectoErp>> ListarProyectosAsync(int companiaId, CancellationToken ct) => Lista(Proyecto);
        public Task<ProyectoErp?> ObtenerProyectoAsync(int companiaId, string proyectoErpId, CancellationToken ct) =>
            Task.FromResult(proyectoErpId == Proyecto.Id ? Proyecto : null);
        public Task<IReadOnlyList<DimensionErp>> ListarDimensionesAsync(int companiaId, CancellationToken ct) => Lista<DimensionErp>();
        public Task<DimensionErp?> ObtenerDimensionAsync(int companiaId, string uegpId, CancellationToken ct) => Task.FromResult<DimensionErp?>(null);
        public Task<IReadOnlyList<ActividadErp>> ListarActividadesAsync(int companiaId, string proyectoErpId, CancellationToken ct) =>
            Lista(new ActividadErp("DEV.01", "ACTIVIDAD DE PRUEBA", "PRUEBA"));
        public Task<ActividadErp?> ObtenerActividadAsync(int companiaId, string proyectoErpId, string actividadId, CancellationToken ct) =>
            Task.FromResult<ActividadErp?>(null);
        public Task<IReadOnlyList<HorarioErp>> ListarHorariosAsync(CancellationToken ct) => Lista<HorarioErp>();
        public Task<HorarioErp?> ObtenerHorarioAsync(int codigo, CancellationToken ct) => Task.FromResult<HorarioErp?>(null);

        private static Task<IReadOnlyList<T>> Lista<T>(params T[] items) => Task.FromResult<IReadOnlyList<T>>(items);
    }
}
