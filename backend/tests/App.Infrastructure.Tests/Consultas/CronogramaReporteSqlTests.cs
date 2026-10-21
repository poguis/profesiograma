using App.Application.Cronograma;
using App.Infrastructure.Consultas;

namespace App.Infrastructure.Tests.Consultas;

/// <summary>TAREA-22-2: consulta (6) de las personas del reporte (cédula y D14) y que las 7 de la TAREA-20 siguen sin [Cedula].</summary>
public class CronogramaReporteSqlTests(ITestOutputHelper salida) : BaseSql(salida)
{
    private static readonly DateOnly Desde = new(2026, 10, 1), Hasta = new(2026, 10, 31);

    [Fact]
    public void EmpleadosReporte_ConCedula_YCodigoInforPorPuesto_SinUpper()
    {
        var sql = Sql(CronogramaConsultas.ConsultaEmpleadosReporte(Db, [6, 7]));

        Assert.Contains("FROM [dbo].[Empleado]", sql);
        Assert.Contains("[Cedula]", sql); // la única consulta del cronograma que la lee
        Assert.Contains("LEFT JOIN", sql);
        Assert.Contains("[dbo].[CargoInfor]", sql);
        Assert.Contains("[Activo] = CAST(1 AS bit)", sql);
        // D14: igualdad directa sobre la columna (collation de la base, sin UPPER/LOWER): puede usar UQ_CargoInfor_Cargo.
        Assert.Matches(@"\[e\]\.\[Puesto\] = \[\w+\]\.\[Cargo\]", sql);
        Assert.DoesNotContain("UPPER", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LOWER", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LasSieteConsultasDeLaTarea20_SiguenSinCedula()
    {
        string[] consultas =
        [
            Sql(CronogramaConsultas.ConsultaMaxDias(Db)),
            Sql(CronogramaConsultas.ConsultaDias(Db, new FiltroCronograma(Desde, Hasta, 3, ProyectoId: 14, DepartamentoId: 7, CodigoEkon: "12345"))),
            Sql(CronogramaConsultas.ConsultaOtrosProyectos(Db, new FiltroOtrosProyectos([1, 2], Desde, Hasta, null, 14))),
            Sql(CronogramaConsultas.ConsultaNovedades(Db, [1, 2], [14], Desde, Hasta)),
            Sql(CronogramaConsultas.ConsultaActividades(Db, [14], Desde, Hasta)),
            Sql(CronogramaConsultas.ConsultaProyectos(Db, [14])),
            Sql(CronogramaConsultas.ConsultaRangoProyecto(Db, 14, 3)),
        ];

        Assert.All(consultas, sql => Assert.DoesNotContain("[Cedula]", sql));
    }
}
