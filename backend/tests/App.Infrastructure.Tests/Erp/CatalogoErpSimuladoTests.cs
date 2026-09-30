using App.Application.Erp;
using App.Infrastructure.Erp;
using Microsoft.Extensions.DependencyInjection;

namespace App.Infrastructure.Tests.Erp;

/// <summary>Datos del modo Simulado (coherentes con PRY-DEV-0001..0003) y registro según ServiciosExternos:Modo.</summary>
public class CatalogoErpSimuladoTests
{
    private readonly CatalogoErpSimulado catalogo = new();
    private static readonly CancellationToken Ct = CancellationToken.None;

    [Fact]
    public async Task Compania9001_ComoEnLaBase() =>
        Assert.Equal([new CompaniaErp(9001, "COMPAÑÍA DE PRUEBA S.A.", "PRUEBA", "0999999999001")], await catalogo.ListarCompaniasAsync(Ct));

    [Fact]
    public async Task Proyectos9001_SoloElActivo() =>
        Assert.Equal([new ProyectoErp("DEV-ERP-001", "PROYECTO ERP DE PRUEBA", "Activo")], await catalogo.ListarProyectosAsync(9001, Ct));

    [Fact]
    public async Task Actividades_DevErp001() =>
        Assert.Equal([new ActividadErp("DEV.01", "ACTIVIDAD DE PRUEBA", "PRUEBA")], await catalogo.ListarActividadesAsync(9001, "DEV-ERP-001", Ct));

    [Fact]
    public async Task Dimensiones9001_Dos() =>
        Assert.Equal(
            [new DimensionErp("DEV-DIM-01", "PLANTA DE PRUEBA"), new DimensionErp("DEV-DIM-02", "OFICINAS DE PRUEBA")],
            await catalogo.ListarDimensionesAsync(9001, Ct));

    [Fact]
    public async Task Horarios_SoloActivos_ElPrimeroComoPryDev0001() =>
        Assert.Equal(
        [
            new HorarioErp(1, "07:00 - 18:00 (PRUEBA)", new TimeOnly(7, 0), new TimeOnly(18, 0), 660, 600, null),
            new HorarioErp(2, "08:00 - 17:00 (PRUEBA)", new TimeOnly(8, 0), new TimeOnly(17, 0), 540, 480, "M"),
        ],
        await catalogo.ListarHorariosAsync(Ct));

    [Fact]
    public async Task CompaniaInexistente_ListasVacias()
    {
        Assert.Null(await catalogo.ObtenerCompaniaAsync(1234, Ct));
        Assert.Empty(await catalogo.ListarProyectosAsync(1234, Ct));
        Assert.Empty(await catalogo.ListarDimensionesAsync(1234, Ct));
    }

    // ------------------------------------------------------------------ registro (AddCatalogoErp)

    [Fact]
    public void Registro_SimuladoFueraDeDevelopment_Lanza()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddCatalogoErp(new ServiciosExternosOpciones { Modo = "Simulado" }, esDesarrollo: false));
        Assert.Contains("solo está permitido en Development", ex.Message);
    }

    [Fact]
    public void Registro_SimuladoEnDevelopment_UsaCatalogoSimulado()
    {
        using var proveedor = new ServiceCollection()
            .AddCatalogoErp(new ServiciosExternosOpciones { Modo = "simulado" }, esDesarrollo: true)
            .BuildServiceProvider();
        Assert.IsType<CatalogoErpSimulado>(proveedor.GetRequiredService<ICatalogoErp>());
    }

    [Fact]
    public void Registro_Http_UsaCatalogoHttp()
    {
        var servicios = new ServiceCollection().AddLogging();
        servicios.AddCatalogoErp(new ServiciosExternosOpciones
        {
            Modo = "Http", ErpBase7048 = "https://erp.prueba.local:7048", ErpBase7055 = "https://erp.prueba.local:7055",
        }, esDesarrollo: false);

        using var proveedor = servicios.BuildServiceProvider();
        Assert.IsType<CatalogoErpHttp>(proveedor.GetRequiredService<ICatalogoErp>());
    }

    [Theory]
    [InlineData("Otro", "https://a:1", "https://b:2", 15)]
    [InlineData("Http", "", "https://b:2", 15)]
    [InlineData("Http", "http://sin-tls:7048", "https://b:2", 15)]
    [InlineData("Http", "https://a:1", "https://b:2", 0)]
    public void Registro_ConfiguracionInvalida_Lanza(string modo, string base7048, string base7055, int timeout) =>
        Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddCatalogoErp(new ServiciosExternosOpciones
        {
            Modo = modo, ErpBase7048 = base7048, ErpBase7055 = base7055, TimeoutSegundos = timeout,
        }, esDesarrollo: true));
}
