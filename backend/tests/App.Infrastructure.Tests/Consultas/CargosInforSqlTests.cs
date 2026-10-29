using App.Application.Empleados;
using App.Infrastructure.Persistencia.Empleados;

namespace App.Infrastructure.Tests.Consultas;

/// <summary>
/// TAREA-25-3-1: traducción a SQL de las consultas de CargoInfor. El ExecuteUpdate (compare-and-set) y la importación
/// dentro del applock se verifican con backend/tests/manual/tarea25-3/cargos.cmd.
/// </summary>
public class CargosInforSqlTests(ITestOutputHelper salida) : BaseSql(salida)
{
    [Fact]
    public void Listado_SoloActivos_TextoEnCargoOCodigo_SinCodigo_YPersonasEnLaMismaConsulta()
    {
        var consulta = CargosInforRepositorio.ConsultaListado(Db, new CargoInforFiltro("TEC", FiltroCodigoInfor.SinCodigo, false, 1, 25));

        var total = Sql(consulta);
        var pagina = Sql(CargosInforRepositorio.PaginaListado(Db, consulta, 0, 25));

        Assert.Contains("[Activo] = CAST(1 AS bit)", total);
        Assert.Contains("[Cargo] LIKE", total);
        Assert.Contains("[CodigoInfor] LIKE", total);
        Assert.Contains("[CodigoInfor] IS NULL", total);
        Assert.Contains("COUNT(*)", pagina);          // personasGuardadas: subconsulta en la misma consulta (sin N+1)
        Assert.Contains("[Empleado]", pagina);
        Assert.Contains("[Puesto] = [c].[Cargo]", pagina);
        Assert.Contains("ORDER BY [c].[Cargo]", pagina);
        Assert.Contains("OFFSET", pagina);
    }

    [Fact]
    public void Listado_ConCodigo_EIncluirInactivos()
    {
        var sql = Sql(CargosInforRepositorio.ConsultaListado(Db, new CargoInforFiltro(null, FiltroCodigoInfor.ConCodigo, true, 1, 25)));

        Assert.Contains("[CodigoInfor] IS NOT NULL", sql);
        Assert.DoesNotContain("[Activo] = CAST(1 AS bit)", sql);
    }

    [Fact]
    public void Todos_Y_PorId_Traducen()
    {
        Assert.Contains("FROM [dbo].[CargoInfor]", Sql(CargosInforRepositorio.ConsultaTodos(Db)));
        Assert.Contains("[Id] = @", Sql(CargosInforRepositorio.ConsultaPorId(Db, 3)));
    }

    [Fact]
    public void CargoConValores_AnteriorNull_UsaIsNull()
    {
        var sql = Sql(CargosInforRepositorio.ConsultaCargoConValores(Db, 3, null, true));

        Assert.Contains("[CodigoInfor] IS NULL", sql);
        Assert.Contains("[Activo] = @", sql);
    }

    [Fact]
    public void CargoConValores_AnteriorNoNull_ComparaElParametro()
    {
        var sql = Sql(CargosInforRepositorio.ConsultaCargoConValores(Db, 3, "P0021", false));

        Assert.Contains("[CodigoInfor] = @", sql);
        Assert.Contains("[Activo] = @", sql);
    }
}
