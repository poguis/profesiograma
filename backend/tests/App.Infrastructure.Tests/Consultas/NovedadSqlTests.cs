using App.Application.Novedades;
using App.Domain.Catalogos;
using App.Infrastructure.Consultas;
using App.Infrastructure.Persistencia.Novedades;

namespace App.Infrastructure.Tests.Consultas;

/// <summary>TAREA-23-1: cada consulta nueva de novedades se traduce a SQL (ToQueryString, sin conexión).</summary>
public class NovedadSqlTests(ITestOutputHelper salida) : BaseSql(salida)
{
    private static readonly DateOnly Desde = new(2026, 10, 1), Hasta = new(2026, 10, 31);

    private static NovedadFiltro Filtro(int? visibleParaUsuarioId, bool incluirAnuladas = false) => new(
        CatalogoIds.TipoAplicacionNovedad.Persona, "VACACIONES", "PROFESIOGRAMA", "NOV", Desde, Hasta, incluirAnuladas, 2, 20, visibleParaUsuarioId);

    [Fact]
    public void Listado_Gestor_ConVisibilidadYTodosLosFiltros()
    {
        var sql = Sql(NovedadConsultas.PaginaListado(NovedadConsultas.ConsultaListado(Db, Filtro(3)), 20, 20));

        Assert.Contains("FROM [dbo].[Novedad]", sql);
        Assert.Contains("[OrigenNovedadId] <> CAST(2 AS tinyint)", sql); // J2: nunca PERMISOS_MEDICOS para un gestor
        Assert.Contains("[CreadoPorId] = @", sql);
        Assert.Contains("[PropietarioUsuarioId] = @", sql);
        Assert.Contains("[Anulada] = CAST(0 AS bit)", sql);
        Assert.Contains("LIKE", sql);
        Assert.Contains("[FechaInicio] <= @", sql);
        Assert.Contains("[FechaFin] >= @", sql);
        Assert.Contains("COUNT(*)", sql); // TotalDias en la misma consulta (sin N+1)
        Assert.Contains("OFFSET", sql);
        Assert.DoesNotContain("[Cedula]", sql);
        Assert.DoesNotContain("[DetalleOrigenJson]", sql);
    }

    [Fact]
    public void Listado_Admin_SinVisibilidad_ConAnuladas_YConteo()
    {
        var consulta = NovedadConsultas.ConsultaListado(Db, Filtro(null, incluirAnuladas: true));
        var sql = Sql(consulta);
        Sql(consulta.Select(n => n.Id)); // base del CountAsync

        Assert.DoesNotContain("[CreadoPorId] = @", sql);
        Assert.DoesNotContain("[Anulada] = CAST(0 AS bit)", sql);
    }

    [Fact]
    public void Detalle_Gestor_Y_Admin()
    {
        var gestor = Sql(NovedadConsultas.ConsultaDetalle(Db, 5, 3));
        var admin = Sql(NovedadConsultas.ConsultaDetalle(Db, 5, null));

        Assert.Contains("[RowVer]", gestor);
        Assert.Contains("[RegistradoPorNombre]", gestor);
        Assert.Contains("[CreadoPorId] = @", gestor);
        Assert.DoesNotContain("[CreadoPorId] = @", admin);
        Assert.DoesNotContain("[Cedula]", admin);
        Assert.DoesNotContain("[DetalleOrigenJson]", admin);
    }

    [Fact]
    public void Dias_Ordenados() =>
        Assert.Contains("ORDER BY [n].[Fecha]", Sql(NovedadConsultas.ConsultaDias(Db, 5)));

    [Fact]
    public void Tipos_Activos() =>
        Assert.Contains("[SeleccionableManual]", Sql(NovedadConsultas.ConsultaTipos(Db)));

    [Fact]
    public void ProyectoElegible_VigenteNoEliminado_YDelPropietario()
    {
        var gestor = Sql(NovedadConsultas.ConsultaProyectoElegible(Db, 10, 3));
        var admin = Sql(NovedadConsultas.ConsultaProyectoElegible(Db, 10, null));

        Assert.Contains("[EsVigente] = CAST(1 AS bit)", gestor);
        Assert.Contains("[Eliminado] = CAST(0 AS bit)", gestor);
        Assert.Contains("[PropietarioUsuarioId] = @", gestor);
        Assert.DoesNotContain("[PropietarioUsuarioId] = @", admin);
    }

    [Theory]
    [InlineData(CatalogoIds.TipoAplicacionNovedad.Persona, 1, null)]
    [InlineData(CatalogoIds.TipoAplicacionNovedad.Proyecto, null, 10)]
    [InlineData(CatalogoIds.TipoAplicacionNovedad.General, null, null)]
    public void Duplicado_PorSujetoTipoYDias(byte aplicacion, int? empleadoId, int? proyectoId)
    {
        var sql = Sql(NovedadRepositorio.ConsultaDuplicado(Db,
            new CriterioDuplicado(aplicacion, CatalogoIds.TipoNovedad.Vacaciones, empleadoId, proyectoId, Desde, Hasta)));

        Assert.Contains("EXISTS", sql);
        Assert.Contains("[NovedadDia]", sql);
        Assert.Contains("[Anulada] = CAST(0 AS bit)", sql);
        Assert.Contains("[TipoNovedadId] = @", sql);
        Assert.Equal(empleadoId is not null, sql.Contains("[EmpleadoId] = @", StringComparison.Ordinal));
        Assert.Equal(proyectoId is not null, sql.Contains("[ProyectoId] = @", StringComparison.Ordinal));
    }

    [Fact]
    public void Duplicado_Visible_MismaReglaQueElListado() =>
        Assert.Contains("[OrigenNovedadId] <> CAST(2 AS tinyint)", Sql(NovedadRepositorio.ConsultaVisible(Db, 5, 3).Select(n => n.Id)));

    [Fact]
    public void CodigoOUid() =>
        Assert.Contains("[Uid] = @", Sql(NovedadRepositorio.ConsultaCodigoOUid(Db, "NOV-20261021-abc123", Guid.Empty).Select(n => n.Id)));
}
