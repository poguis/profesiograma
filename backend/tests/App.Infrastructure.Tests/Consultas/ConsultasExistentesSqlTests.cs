using App.Application.Empleados;
using App.Application.Proyectos;
using App.Infrastructure.Consultas;
using App.Infrastructure.Persistencia.Proyectos;

namespace App.Infrastructure.Tests.Consultas;

/// <summary>Traducción a SQL de ProyectoConsultas (TAREA-07).</summary>
public class ProyectoConsultasSqlTests(ITestOutputHelper salida) : BaseSql(salida)
{
    [Fact]
    public void Listado_ConTodosLosFiltros_YPagina()
    {
        var filtro = new ProyectoFiltro("ACTIVO", "CAMPO", "0001", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), 2, 20, 3);
        var consulta = ProyectoConsultas.ConsultaListado(Db, filtro);

        var conteo = Sql(consulta);
        Assert.Contains("FROM dbo.vwProyectoResumen", conteo);
        Assert.Contains("[PropietarioUsuarioId] = @", conteo);
        Assert.Contains("LIKE", conteo);

        var pagina = Sql(ProyectoConsultas.PaginaListado(consulta, 20, 20));
        Assert.Contains("ORDER BY", pagina);
        Assert.Contains("OFFSET", pagina);
    }

    [Fact]
    public void Listado_SinFiltros() =>
        Assert.DoesNotContain("WHERE", Sql(ProyectoConsultas.ConsultaListado(Db, new ProyectoFiltro(null, null, null, null, null, 1, 20, null))));

    [Theory]
    [InlineData(null)]
    [InlineData(3)]
    public void Detalle_Cabecera(int? propietario)
    {
        var sql = Sql(ProyectoConsultas.ConsultaCabecera(Db, 1, propietario));
        Assert.Contains("[Eliminado] = CAST(0 AS bit)", sql);
        Assert.Equal(propietario is not null, sql.Contains("[PropietarioUsuarioId] = @"));
    }

    [Fact]
    public void Detalle_Personal_SinDatosSensibles()
    {
        var sql = Sql(ProyectoConsultas.ConsultaPersonal(Db, 1));
        Assert.DoesNotContain("Cedula", sql);
        Assert.DoesNotContain("CorreoEmpresa", sql);
    }

    [Fact]
    public void Detalle_Etapas_SinSnapshot() => Assert.DoesNotContain("SnapshotPersonal", Sql(ProyectoConsultas.ConsultaEtapas(Db, 1)));

    [Fact]
    public void Detalle_ActividadVigente()
    {
        // O3 (TAREA-18b): filtro de cobertura de la fecha de referencia y mayor versión; sin CASE ni respaldo.
        var sql = Sql(ProyectoConsultas.ConsultaActividadVigente(Db, 1, new DateOnly(2026, 10, 1)));
        Assert.Contains("[p].[FechaInicio] <= @", sql);
        Assert.Contains("[p].[FechaFin] >= @", sql);
        Assert.Contains("ORDER BY [p].[Version] DESC", sql);
        Assert.DoesNotContain("CASE", sql);
    }
}

/// <summary>
/// Traducción a SQL de EmpleadoConsultas (TAREA-11). TAREA-26d: la búsqueda ya no consulta la base (lee la API en
/// memoria); se retiraron sus pruebas de traducción y queda la de los departamentos del usuario.
/// </summary>
public class EmpleadoConsultasSqlTests(ITestOutputHelper salida) : BaseSql(salida)
{
    [Fact]
    public void DepartamentosDeUsuario() => Assert.Contains("SELECT DISTINCT", Sql(EmpleadoConsultas.ConsultaDepartamentosDeUsuario(Db, 3)));
}

/// <summary>Traducción a SQL de CatalogoConsultas (Paso 2).</summary>
public class CatalogoConsultasSqlTests(ITestOutputHelper salida) : BaseSql(salida)
{
    [Fact]
    public void Catalogos_TodosSeTraducen()
    {
        Assert.Contains("EstadoProyecto", Sql(CatalogoConsultas.Estados(Db)));
        Assert.Contains("TipoMovimiento", Sql(CatalogoConsultas.TiposMovimiento(Db)));
        Assert.Contains("GrupoProyecto", Sql(CatalogoConsultas.Grupos(Db)));
        Assert.Contains("Jornada", Sql(CatalogoConsultas.Jornadas(Db)));
        Assert.Contains("RolAsignacion", Sql(CatalogoConsultas.Roles(Db)));
        Assert.Contains("TipoAplicacionNovedad", Sql(CatalogoConsultas.TiposAplicacion(Db)));
        Assert.Contains("OrigenNovedad", Sql(CatalogoConsultas.Origenes(Db)));
        Assert.Contains("TipoNovedad", Sql(CatalogoConsultas.TiposNovedad(Db)));
        Assert.Contains("Departamento", Sql(CatalogoConsultas.Departamentos(Db)));
        Assert.Contains("Parametro", Sql(CatalogoConsultas.Parametros(Db)));
    }
}

/// <summary>Traducción a SQL de las demás consultas nuevas de la TAREA-12.</summary>
public class CrearProyectoConsultasSqlTests(ITestOutputHelper salida) : BaseSql(salida)
{
    [Fact]
    public void DatosReferencia_GrupoJornadasLimitesEmpleados()
    {
        Assert.Contains("[Codigo] = @", Sql(DatosReferenciaProyecto.ConsultaGrupo(Db, "CAMPO")));
        Assert.Contains("[Activo] = CAST(1 AS bit)", Sql(DatosReferenciaProyecto.ConsultaJornadas(Db)));
        var limites = Sql(DatosReferenciaProyecto.ConsultaLimites(Db));
        Assert.Contains("[Clave] IN (", limites);
        Assert.Contains("PROYECTO_EXIGE_PRINCIPAL", limites); // TAREA-19y
        // TAREA-26d: por Id sin filtro de estado (si está activo lo decide la API).
        var empleados = Sql(DatosReferenciaProyecto.ConsultaEmpleadosPorIds(Db, [6, 7, 8]));
        Assert.DoesNotContain("[EstadoErp]", empleados);
        Assert.DoesNotContain("[Cedula]", empleados);
    }

    [Fact]
    public void Repositorio_CodigoYCompania()
    {
        Assert.Contains("[Codigo] = @", Sql(ProyectoRepositorio.ConsultaCodigo(Db, "PRY-20261001-abcdef")));
        Assert.Contains("FROM [dbo].[Compania]", Sql(ProyectoRepositorio.ConsultaCompania(Db, 9001)));
    }
}
