using App.Application.Cronograma;
using App.Infrastructure.Consultas;

namespace App.Infrastructure.Tests.Consultas;

/// <summary>TAREA-20: las 7 consultas del cronograma se traducen a SQL (ToQueryString, sin conexión).</summary>
public class CronogramaSqlTests(ITestOutputHelper salida) : BaseSql(salida)
{
    private static readonly DateOnly Desde = new(2026, 10, 1), Hasta = new(2026, 10, 31);

    [Fact]
    public void MaxDias_DelParametro() =>
        Assert.Contains("CRONOGRAMA_MAX_DIAS", Sql(CronogramaConsultas.ConsultaMaxDias(Db)));

    [Fact]
    public void Dias_Gestor_ConTodosLosFiltros()
    {
        var sql = Sql(CronogramaConsultas.ConsultaDias(Db,
            new FiltroCronograma(Desde, Hasta, PropietarioUsuarioId: 3, ProyectoId: 14, DepartamentoId: 7, CodigoEkon: "12345")));

        Assert.Contains("FROM [dbo].[ProyectoAsignacionDia]", sql);
        Assert.Contains("[Eliminado] = CAST(0 AS bit)", sql);
        Assert.Contains("[PropietarioUsuarioId] = @", sql);
        Assert.Contains("[DepartamentoId] = @", sql);
        Assert.Contains("[CodigoEkon] = @", sql);
        Assert.Contains("[CargoAsignado]", sql);
        Assert.Contains("[NombreCompleto]", sql);
        Assert.DoesNotContain("[Cedula]", sql);
    }

    [Fact]
    public void Dias_AdminPorProyecto_SinFiltroDePropietario()
    {
        var sql = Sql(CronogramaConsultas.ConsultaDias(Db, new FiltroCronograma(Desde, Hasta, PropietarioUsuarioId: null, SoloProyectoId: 14)));
        Assert.Contains("[ProyectoId] = @", sql);
        Assert.Contains("[Fecha] >= @", sql);
    }

    [Fact]
    public void OtrosProyectos_SoloCantidades_SinDatosDelProyecto()
    {
        var sql = Sql(CronogramaConsultas.ConsultaOtrosProyectos(Db, new FiltroOtrosProyectos([1, 2], Desde, Hasta, 3, null)));

        Assert.Contains("GROUP BY", sql);
        Assert.Contains("COUNT(*)", sql);
        Assert.Contains("[RolAsignacionId] <> CAST(3 AS tinyint)", sql);
        Assert.Contains("[PropietarioUsuarioId] <> @", sql);
        Assert.DoesNotContain("[Codigo]", sql);
        Assert.DoesNotContain("[NombreVisual]", sql);
    }

    [Fact]
    public void OtrosProyectos_PorProyecto_ExcluyeEseProyecto() =>
        Assert.Contains("[Id] <> @", Sql(CronogramaConsultas.ConsultaOtrosProyectos(Db, new FiltroOtrosProyectos([1], Desde, Hasta, null, 14))));

    [Fact]
    public void Novedades_NoAnuladas_DelRango_ConSuTipo()
    {
        var sql = Sql(CronogramaConsultas.ConsultaNovedades(Db, [1, 2], [14], Desde, Hasta));

        Assert.Contains("FROM [dbo].[NovedadDia]", sql);
        Assert.Contains("[Anulada] = CAST(0 AS bit)", sql);
        Assert.Contains("[SiglaCronograma]", sql);
        Assert.Contains("[ColorHex]", sql);
        Assert.Contains("[OrigenNovedad]", sql);
    }

    [Fact]
    public void Actividades_QueSeSolapanConElRango()
    {
        var sql = Sql(CronogramaConsultas.ConsultaActividades(Db, [14], Desde, Hasta));
        Assert.Contains("FROM [dbo].[ProyectoActividad]", sql);
        Assert.Contains("[FechaInicio] <= @", sql);
        Assert.Contains("[FechaFin] >= @", sql);
    }

    [Fact]
    public void Proyectos_ConHorarioYAlmuerzo()
    {
        var sql = Sql(CronogramaConsultas.ConsultaProyectos(Db, [14]));
        Assert.Contains("[HoraEntrada]", sql);
        Assert.Contains("[RegresoAlmuerzo]", sql);
        Assert.Contains("[DimensionUegpId]", sql);
    }

    [Fact]
    public void RangoProyecto_NoEliminadoYVisible()
    {
        var sql = Sql(CronogramaConsultas.ConsultaRangoProyecto(Db, 14, 3));
        Assert.Contains("[Eliminado] = CAST(0 AS bit)", sql);
        Assert.Contains("[PropietarioUsuarioId] = @", sql);
    }
}
