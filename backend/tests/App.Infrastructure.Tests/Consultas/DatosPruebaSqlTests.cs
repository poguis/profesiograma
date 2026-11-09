using App.Infrastructure.Persistencia.DatosPrueba;

namespace App.Infrastructure.Tests.Consultas;

/// <summary>
/// TAREA-8 (pendiente 15): traducción a SQL de las consultas del sembrador de datos de prueba (solo Development).
/// Datos FICTICIOS; no se conecta a ninguna base.
/// </summary>
public class DatosPruebaSqlTests(ITestOutputHelper salida) : BaseSql(salida)
{
    [Fact]
    public void ProyectosPrueba_PorPrefijoDelCodigo()
    {
        var sql = Sql(DatosPruebaSembrador.ConsultaProyectosPrueba(Db));

        Assert.Contains("[Proyecto]", sql);
        Assert.Contains("[Codigo] LIKE", sql); // StartsWith traduce a LIKE (prefijo PRY-DEV-)
    }

    [Fact]
    public void CompaniaPrueba_PorId()
    {
        var sql = Sql(DatosPruebaSembrador.ConsultaCompaniaPrueba(Db));

        Assert.Contains("[Compania]", sql);
        Assert.Contains("[Id] = 9001", sql);
    }

    [Fact]
    public void DepartamentoPrueba_PorNombre()
    {
        Assert.Contains("[Nombre] = N'UNIDAD SISTEMA INTEGRADO DE GESTION'", Sql(DatosPruebaSembrador.ConsultaDepartamentoPrueba(Db)));
    }

    [Fact]
    public void UsuarioDepartamento_PorUsuarioYDepartamento_Parametrizado()
    {
        var sql = Sql(DatosPruebaSembrador.ConsultaUsuarioDepartamento(Db, 2, 7));

        Assert.Contains("[UsuarioId] = @", sql);
        Assert.Contains("[DepartamentoId] = @", sql);
    }

    [Fact]
    public void UsuarioPorEmail_Parametrizado()
    {
        Assert.Contains("[Email] = @", Sql(DatosPruebaSembrador.ConsultaUsuarioPorEmail(Db, "admin.dev@profesiograma.local")));
    }

    [Fact]
    public void EmpleadoPorCodigo_Parametrizado()
    {
        Assert.Contains("[CodigoEkon] = @", Sql(DatosPruebaSembrador.ConsultaEmpleadoPorCodigo(Db, "DEV001")));
    }
}
