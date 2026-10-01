using App.Domain.Proyectos;

namespace App.Domain.Tests.Proyectos;

/// <summary>RN01/P6 (código) y nombre visual (FASE_5 §3).</summary>
public class ReglasProyectoTests
{
    [Fact]
    public void Codigo_FechaYUltimosSeisDelUidEnMinusculas()
    {
        var uid = Guid.Parse("0E1F2A3B-4C5D-6E7F-8091-A2B3C4D5E6F7");
        Assert.Equal("PRY-20261201-d5e6f7", GeneradorCodigoProyecto.Generar(new DateOnly(2026, 12, 1), uid));
    }

    [Fact]
    public void Codigo_FormatoYLargo()
    {
        var codigo = GeneradorCodigoProyecto.Generar(new DateOnly(2026, 1, 5), Guid.NewGuid());
        Assert.Matches("^PRY-20260105-[0-9a-f]{6}$", codigo);
        Assert.True(codigo.Length <= 30); // Proyecto.Codigo varchar(30)
    }

    [Theory]
    [InlineData("PROYECTO ERP DE PRUEBA", "PLANTA DE PRUEBA", "PROYECTO ERP DE PRUEBA")]
    [InlineData(null, "PLANTA DE PRUEBA", "PLANTA - PLANTA DE PRUEBA")]
    [InlineData("  ", " OFICINAS DE PRUEBA ", "PLANTA - OFICINAS DE PRUEBA")]
    public void NombreVisual_ErpOPlantaDimension(string? erp, string? dimension, string esperado) =>
        Assert.Equal(esperado, NombreVisualProyecto.Calcular(erp, dimension));

    [Fact]
    public void NombreVisual_SinErpNiDimension_Lanza() =>
        Assert.Throws<ArgumentException>(() => NombreVisualProyecto.Calcular(null, " "));
}
