using App.Infrastructure.Persistencia.Vistas;

namespace App.Infrastructure.Tests.Consultas;

/// <summary>Definición de dbo.vwProyectoResumen V2 (TAREA-18b, O3): regla común de actividad vigente.</summary>
public class VistasSqlTests
{
    [Fact]
    public void VwProyectoResumen_V2_FiltroDeCobertura_SinRespaldoPorVersion()
    {
        var sql = VistasSql.VwProyectoResumen_V2;

        Assert.Contains("CREATE OR ALTER VIEW dbo.vwProyectoResumen", sql);
        Assert.Contains("AT TIME ZONE 'SA Pacific Standard Time'", sql);
        Assert.Contains("WHEN hoy.Hoy < p.FechaInicio THEN p.FechaInicio", sql);
        Assert.Contains("WHEN hoy.Hoy > p.FechaFin THEN p.FechaFin", sql);
        Assert.Contains("a.FechaInicio <= ref.Fecha AND a.FechaFin >= ref.Fecha", sql);
        Assert.Contains("ORDER BY a.Version DESC", sql);
        Assert.DoesNotContain("ELSE 1 END", sql); // respaldo de V1: "la de mayor versión" si ninguna cubre hoy
        Assert.DoesNotContain("SWITCHOFFSET", sql);
    }

    [Fact]
    public void VwProyectoResumen_V1_NoCambia() =>
        Assert.Contains("ELSE 1 END", VistasSql.VwProyectoResumen_V1); // la usan la migración Vistas y el Down de la V2
}
