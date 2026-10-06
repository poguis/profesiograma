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

    /// <summary>TAREA-19y: PROYECTO_EXIGE_PRINCIPAL ("1" o "true" = exige; otro valor o ausente = no, el inicial es 0).</summary>
    [Theory]
    [InlineData("1", true)]
    [InlineData("true", true)]
    [InlineData(" TRUE ", true)]
    [InlineData("0", false)]
    [InlineData("false", false)]
    [InlineData("SI", false)]
    [InlineData(null, false)]
    public void ExigePrincipal_SeLeeDelParametro(string? valor, bool esperado) =>
        Assert.Equal(esperado, DatosReferenciaProyecto.LeerExigePrincipal(valor));
}
