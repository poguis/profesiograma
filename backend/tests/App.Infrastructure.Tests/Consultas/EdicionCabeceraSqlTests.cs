using App.Infrastructure.Persistencia.Proyectos;

namespace App.Infrastructure.Tests.Consultas;

/// <summary>
/// Traducción a SQL de las consultas nuevas de EdicionCabeceraRepositorio (TAREA-18). El resto (personal, días
/// posteriores, filtro del DELETE, última versión, tipo de movimiento, proyecto y actividades con seguimiento) reutiliza
/// consultas que ya tienen su prueba.
/// </summary>
public class EdicionCabeceraSqlTests(ITestOutputHelper salida) : BaseSql(salida)
{
    [Fact]
    public void CabeceraEdicion_VisibilidadGrupoHorarioAlmuerzoYRowVer()
    {
        var sql = Sql(EdicionCabeceraRepositorio.ConsultaCabeceraEdicion(Db, 9, 3));
        Assert.Contains("FROM [dbo].[Proyecto]", sql);
        Assert.Contains("[p].[Eliminado] = CAST(0 AS bit)", sql);
        Assert.Contains("[p].[PropietarioUsuarioId] = @", sql);
        Assert.Contains("[RequiereProyectoErp]", sql);
        Assert.Contains("[HorarioCodigo]", sql);
        Assert.Contains("[SalidaAlmuerzo]", sql);
        Assert.Contains("[RowVer]", sql);
        Assert.DoesNotContain("[Cedula]", sql);
    }

    [Fact]
    public void CabeceraEdicion_Admin_SinFiltroDePropietario() =>
        Assert.DoesNotContain("[PropietarioUsuarioId]", Sql(EdicionCabeceraRepositorio.ConsultaCabeceraEdicion(Db, 9, null)));

    [Fact]
    public void ActividadesEdicion_ConDescripcionTipoYTipoDeMovimiento()
    {
        var sql = Sql(EdicionCabeceraRepositorio.ConsultaActividadesEdicion(Db, 9));
        Assert.Contains("FROM [dbo].[ProyectoActividad]", sql);
        Assert.Contains("INNER JOIN [dbo].[TipoMovimiento]", sql);
        Assert.Contains("[ActividadDescripcion]", sql);
        Assert.Contains("ORDER BY [p].[Version]", sql);
    }
}
