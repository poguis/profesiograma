using App.Infrastructure.Consultas;
using App.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Tests.Consultas;

/// <summary>
/// Regresión TAREA-12: la consulta de departamentos del usuario debe ser traducible a SQL.
/// (La primera versión ordenaba después de proyectar a un record con constructor → InvalidOperationException → 500.)
/// ToQueryString no abre conexión a la base.
/// </summary>
public class DatosReferenciaProyectoSqlTests(ITestOutputHelper salida)
{
    [Fact]
    public void DepartamentosDeUsuario_SeTraduceASql_ConDistinctYOrden()
    {
        var opciones = new DbContextOptionsBuilder<ProfesiogramaDbContext>()
            .UseSqlServer("Server=sin-conexion;Database=sin-conexion;Integrated Security=true;TrustServerCertificate=true",
                sql => sql.UseCompatibilityLevel(150))
            .Options;
        using var db = new ProfesiogramaDbContext(opciones);

        var sql = DatosReferenciaProyecto.ConsultaDepartamentosDeUsuario(db, usuarioId: 3).ToQueryString();
        salida.WriteLine(sql);

        Assert.Contains("SELECT DISTINCT", sql);
        Assert.Contains("FROM [dbo].[UsuarioDepartamento]", sql);
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("[Activo] = CAST(1 AS bit)", sql);
    }
}
