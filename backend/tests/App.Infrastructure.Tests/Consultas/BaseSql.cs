using App.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Tests.Consultas;

/// <summary>
/// Base para pruebas de traducción a SQL (regla de CLAUDE.md "Reglas de pruebas").
/// ToQueryString traduce la consulta completa sin abrir conexión a la base: si EF no puede traducirla, lanza
/// InvalidOperationException (lo que las pruebas con dobles no detectan).
/// </summary>
public abstract class BaseSql(ITestOutputHelper salida) : IDisposable
{
    protected ProfesiogramaDbContext Db { get; } = new(new DbContextOptionsBuilder<ProfesiogramaDbContext>()
        .UseSqlServer("Server=sin-conexion;Database=sin-conexion;Integrated Security=true;TrustServerCertificate=true",
            sql => sql.UseCompatibilityLevel(150))
        .Options);

    /// <summary>Traduce, imprime el SQL en la salida de la prueba y lo devuelve para las aserciones.</summary>
    protected string Sql<T>(IQueryable<T> consulta)
    {
        var sql = consulta.ToQueryString();
        salida.WriteLine(sql);
        Assert.False(string.IsNullOrWhiteSpace(sql));
        return sql;
    }

    public void Dispose()
    {
        Db.Dispose();
        GC.SuppressFinalize(this);
    }
}
