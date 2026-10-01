using App.Infrastructure.Consultas;
using App.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Tests.Consultas;

/// <summary>
/// SQL real que genera EF para los cruces externos (ToQueryString; no abre conexión a la base).
/// El texto se imprime en la salida de la prueba para documentarlo en el reporte.
/// </summary>
public class ConsultaCrucesExternosSqlTests(ITestOutputHelper salida)
{
    [Fact]
    public void Sql_FiltraOtrosProyectosVigentesNoEliminadosYRolDistintoDeDescanso()
    {
        var opciones = new DbContextOptionsBuilder<ProfesiogramaDbContext>()
            .UseSqlServer("Server=sin-conexion;Database=sin-conexion;Integrated Security=true;TrustServerCertificate=true",
                sql => sql.UseCompatibilityLevel(150))
            .Options;
        using var db = new ProfesiogramaDbContext(opciones);

        var sql = ConsultaCrucesExternos.Consulta(db, [6, 7], new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), excluirProyectoId: null)
            .ToQueryString();
        salida.WriteLine(sql);

        Assert.Contains("FROM [dbo].[ProyectoAsignacionDia]", sql);
        Assert.Contains("INNER JOIN [dbo].[Proyecto]", sql);
        Assert.Contains("[EsVigente] = CAST(1 AS bit)", sql);
        Assert.Contains("[Eliminado] = CAST(0 AS bit)", sql);
        Assert.Contains("[RolAsignacionId] <> CAST(3 AS tinyint)", sql);
        Assert.Contains("[Fecha] >= @desde", sql);
        Assert.Contains("[Fecha] <= @hasta", sql);
        Assert.Contains("[EmpleadoId] IN (", sql);
        Assert.DoesNotContain("Propietario", sql);   // solo código, nombre y estado del proyecto existente
    }
}
