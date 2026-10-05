using App.Infrastructure.Persistencia.Proyectos;

namespace App.Infrastructure.Tests.Consultas;

/// <summary>
/// Traducción a SQL de las consultas nuevas de la TAREA-17b (EdicionPersonalRepositorio):
/// actividad para reactivar (R8) y backs con descanso posterior guardado (H12). El resto de la reactivación reutiliza
/// consultas de la TAREA-14 y la TAREA-17 que ya tienen su prueba.
/// </summary>
public class ReactivacionSqlTests(ITestOutputHelper salida) : BaseSql(salida)
{
    [Fact]
    public void ActividadParaReactivar_VigenteEnLaFecha_MayorVersion_ConDescripcionYTipo()
    {
        var sql = Sql(EdicionPersonalRepositorio.ConsultaActividadParaReactivar(Db, 9, new DateOnly(2026, 10, 9)));
        Assert.Contains("FROM [dbo].[ProyectoActividad]", sql);
        Assert.Contains("[p].[FechaInicio] <= @", sql);
        Assert.Contains("[p].[FechaFin] >= @", sql);
        Assert.Contains("[ActividadDescripcion]", sql);
        Assert.Contains("[ActividadTipo]", sql);
        Assert.Contains("ORDER BY [p].[Version] DESC", sql);
    }

    [Fact]
    public void BacksConDescansoPosterior_DescansoDespuesDeLaFechaFinDelBack()
    {
        var sql = Sql(EdicionPersonalRepositorio.ConsultaBacksConDescansoPosterior(Db, 9));
        Assert.Contains("SELECT DISTINCT", sql);
        Assert.Contains("FROM [dbo].[ProyectoAsignacionDia]", sql);
        Assert.Contains("INNER JOIN [dbo].[ProyectoPersonal]", sql);
        Assert.Contains("[p].[RolAsignacionId] = CAST(3 AS tinyint)", sql);
        Assert.Contains("].[RolAsignacionId] = CAST(2 AS tinyint)", sql);
        Assert.Contains("[p].[Fecha] > [", sql);
    }
}
