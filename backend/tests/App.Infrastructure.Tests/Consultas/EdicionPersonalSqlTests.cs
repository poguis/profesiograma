using App.Infrastructure.Persistencia.Proyectos;

namespace App.Infrastructure.Tests.Consultas;

/// <summary>
/// Traducción a SQL de EdicionPersonalRepositorio (TAREA-17). ExecuteDeleteAsync no tiene ToQueryString: se prueba el
/// IQueryable del filtro (ConsultaDiasDesdeCorte). El resto (cabecera, actividades, última versión, tipo de movimiento,
/// personal con seguimiento) reutiliza consultas de CambioEstadoRepositorio que ya tienen su prueba.
/// </summary>
public class EdicionPersonalSqlTests(ITestOutputHelper salida) : BaseSql(salida)
{
    private static readonly DateOnly Corte = new(2026, 10, 2);

    [Fact]
    public void PersonalEdicion_SinCedulaNiCorreo_ConJornadaYOrden()
    {
        var sql = Sql(EdicionPersonalRepositorio.ConsultaPersonalEdicion(Db, 9));
        Assert.Contains("FROM [dbo].[ProyectoPersonal]", sql);
        Assert.Contains("INNER JOIN [dbo].[Empleado]", sql);
        Assert.Contains("LEFT JOIN [dbo].[Jornada]", sql);
        Assert.Contains("[EsPrincipalInicial]", sql);
        Assert.Contains("ORDER BY [p].[RolAsignacionId], [p].[Numero]", sql);
        Assert.DoesNotContain("[Cedula]", sql);
        Assert.DoesNotContain("[CorreoEmpresa]", sql);
    }

    [Fact]
    public void DiasBase_SoloAnterioresAlCorte()
    {
        var sql = Sql(EdicionPersonalRepositorio.ConsultaDiasBase(Db, 9, Corte));
        Assert.Contains("FROM [dbo].[ProyectoAsignacionDia]", sql);
        Assert.Contains("[p].[ProyectoId] = @", sql);
        Assert.Contains("[p].[Fecha] < @", sql);
        Assert.Contains("[TipoAsignacion]", sql);
    }

    [Fact]
    public void DiasDesdeCorte_FiltroDelDeleteMasivo()
    {
        var sql = Sql(EdicionPersonalRepositorio.ConsultaDiasDesdeCorte(Db, 9, Corte));
        Assert.Contains("FROM [dbo].[ProyectoAsignacionDia]", sql);
        Assert.Contains("[p].[Fecha] >= @", sql);
    }
}
