using App.Infrastructure.Consultas;
using App.Infrastructure.Persistencia.DatosPrueba;
using App.Infrastructure.Persistencia.Empleados;

namespace App.Infrastructure.Tests.Consultas;

/// <summary>
/// TAREA-62: traducción a SQL de las consultas del criterio de personal elegible y familias del modo Simulado. El
/// ExecuteUpdate del PUT (compare-and-set) se verifica con el script manual backend/tests/manual/tarea62/criterio.cmd.
/// </summary>
public class PersonalElegibleSqlTests(ITestOutputHelper salida) : BaseSql(salida)
{
    [Fact]
    public void FamiliasAsignables_LeeSoloElValorDelParametro()
    {
        var sql = Sql(DatosReferenciaProyecto.ConsultaFamiliasAsignables(Db));

        Assert.Contains("FROM [dbo].[Parametro]", sql);
        Assert.Contains("EMPLEADO_FAMILIAS_ASIGNABLES", sql);
        Assert.Contains("[Valor]", sql);
    }

    [Fact]
    public void CriterioConValor_FiltraPorClaveYValorAnterior()
    {
        var sql = Sql(CriterioPersonalRepositorio.ConsultaCriterioConValor(Db, "ADMINISTRATIVO"));

        Assert.Contains("FROM [dbo].[Parametro]", sql);
        Assert.Contains("EMPLEADO_FAMILIAS_ASIGNABLES", sql);
        Assert.Contains("[Valor] = @", sql); // el valor anterior viaja como parámetro (compare-and-set)
    }

    [Fact]
    public void ModoSimulado_SimMultiplosDeDiez_Operativo_DevSinCambios()
    {
        var lista = EmpleadosPrueba.ListaSimulada();

        var operativos = lista.Where(e => e.FamiliaPuesto == EmpleadosPrueba.FamiliaNoAsignable).Select(e => e.CodigoEkon).ToList();
        Assert.Equal(20, operativos.Count);
        Assert.Contains("SIM010", operativos);
        Assert.Contains("SIM200", operativos);
        Assert.All(lista.Where(e => e.CodigoEkon!.StartsWith("DEV", StringComparison.Ordinal)),
            e => Assert.Equal(EmpleadosPrueba.FamiliaPuesto, e.FamiliaPuesto));
        Assert.Equal(EmpleadosPrueba.FamiliaPuesto, lista.Single(e => e.CodigoEkon == "SIM011").FamiliaPuesto);
    }
}
