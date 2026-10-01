using App.Infrastructure.Persistencia.Proyectos;

namespace App.Infrastructure.Tests.Consultas;

/// <summary>
/// Traducción a SQL de CambioEstadoRepositorio (TAREA-14). ExecuteDeleteAsync no tiene ToQueryString: se prueba el
/// IQueryable del filtro (ConsultaDiasAEliminar), que EF convierte en "DELETE FROM [p] FROM … WHERE …".
/// </summary>
public class CambioEstadoSqlTests(ITestOutputHelper salida) : BaseSql(salida)
{
    private static readonly DateOnly Fecha = new(2027, 3, 15);

    [Theory]
    [InlineData(null)]
    [InlineData(3)]
    public void Cabecera_NoEliminado_YVisibilidad(int? propietario)
    {
        var sql = Sql(CambioEstadoRepositorio.ConsultaCabecera(Db, 7, propietario));
        Assert.Contains("[Eliminado] = CAST(0 AS bit)", sql);
        Assert.Contains("[EstadoProyecto]", sql);
        Assert.Equal(propietario is not null, sql.Contains("[PropietarioUsuarioId] = @"));
    }

    [Fact]
    public void Personal_SinCedulaNiCorreo_ConJornada()
    {
        var sql = Sql(CambioEstadoRepositorio.ConsultaPersonal(Db, 7));
        Assert.Contains("FROM [dbo].[ProyectoPersonal]", sql);
        Assert.Contains("[CodigoEkon]", sql);
        Assert.Contains("LEFT JOIN [dbo].[Jornada]", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.DoesNotContain("[Cedula]", sql);
        Assert.DoesNotContain("[CorreoEmpresa]", sql);
    }

    [Fact]
    public void DiasPosteriores_SoloFechaMayorQueF()
    {
        var sql = Sql(CambioEstadoRepositorio.ConsultaDiasPosteriores(Db, 7, Fecha));
        Assert.Contains("FROM [dbo].[ProyectoAsignacionDia]", sql);
        Assert.Contains("[Fecha] > @", sql);
    }

    [Fact]
    public void DiasPosteriores_ConFechaMaxima_SeTraduce() =>
        Assert.Contains("[Fecha] > @", Sql(CambioEstadoRepositorio.ConsultaDiasPosteriores(Db, 7, DateOnly.MaxValue)));

    [Fact]
    public void Actividades() =>
        Assert.Contains("FROM [dbo].[ProyectoActividad]", Sql(CambioEstadoRepositorio.ConsultaActividades(Db, 7)));

    [Theory]
    [InlineData(new int[0])]
    [InlineData(new[] { 72 })]
    [InlineData(new[] { 72, 73 })]
    public void DiasAEliminar_FiltroDelDeleteMasivo(int[] personalEliminado)
    {
        var sql = Sql(CambioEstadoRepositorio.ConsultaDiasAEliminar(Db, 7, Fecha, personalEliminado));
        Assert.Contains("FROM [dbo].[ProyectoAsignacionDia]", sql);
        Assert.Contains("[ProyectoId] = @", sql);
        Assert.Contains("[Fecha] > @", sql);
        Assert.Contains("[ProyectoPersonalId]", sql);
    }

    [Fact]
    public void IdsDeCatalogo_PorCodigo()
    {
        Assert.Contains("FROM [dbo].[EstadoProyecto]", Sql(CambioEstadoRepositorio.ConsultaIdEstado(Db, "SUSPENDIDO")));
        Assert.Contains("FROM [dbo].[TipoMovimiento]", Sql(CambioEstadoRepositorio.ConsultaIdTipoMovimiento(Db, "SUSPENSION")));
    }

    [Fact]
    public void Seguimiento_ProyectoPersonalYActividades()
    {
        Assert.Contains("[RowVer]", Sql(CambioEstadoRepositorio.ConsultaProyectoSeguimiento(Db, 7)));
        Assert.Contains("FROM [dbo].[ProyectoPersonal]", Sql(CambioEstadoRepositorio.ConsultaPersonalSeguimiento(Db, 7)));
        Assert.Contains("FROM [dbo].[ProyectoActividad]", Sql(CambioEstadoRepositorio.ConsultaActividadesSeguimiento(Db, 7)));
    }

    [Fact]
    public void UltimaVersionEtapa_OrdenDescendente()
    {
        var sql = Sql(CambioEstadoRepositorio.ConsultaUltimaVersion(Db, 7));
        Assert.Contains("FROM [dbo].[ProyectoEtapa]", sql);
        Assert.Contains("ORDER BY [p].[Version] DESC", sql);
    }
}
