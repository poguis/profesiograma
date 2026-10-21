using App.Application.Comun;
using App.Infrastructure.Persistencia;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Tests.Persistencia;

/// <summary>
/// TAREA-22c: clasificación de "base no disponible" y su interceptor de EF. La prueba de conexión usa solo loopback
/// (127.0.0.1, puerto 1, nadie escucha) con datos ficticios: no toca ninguna base real. Tarda ~2–3 s.
/// </summary>
public sealed class FalloBaseDatosInterceptorTests
{
    [Fact]
    public async Task AlAbrir_LanzarDesdeElInterceptor_ReemplazaLaExcepcion_ConLaSqlExceptionComoInterna()
    {
        await using var db = new ProfesiogramaDbContext(new DbContextOptionsBuilder<ProfesiogramaDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=BD_FICTICIA_22C;User Id=usuario_ficticio;Password=clave_ficticia;Connect Timeout=1;TrustServerCertificate=true")
            .AddInterceptors(FalloBaseDatosInterceptor.Instancia)
            .Options);

        var ex = await Assert.ThrowsAsync<BaseDatosNoDisponibleException>(
            () => db.Usuarios.AnyAsync(TestContext.Current.CancellationToken));

        var sql = Assert.IsType<SqlException>(ex.InnerException);
        Assert.Equal((FaseFalloBaseDatos.AlAbrir, sql.Number), (ex.Fase, ex.NumeroError));
        Assert.DoesNotContain("127.0.0.1", ex.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(53)]
    [InlineData(64)]
    [InlineData(233)]
    [InlineData(258)]
    [InlineData(4060)]
    [InlineData(10053)]
    [InlineData(10054)]
    [InlineData(10060)]
    [InlineData(10061)]
    [InlineData(11001)]
    public void NumerosDeConexion_CuentanEnCualquierFase(int numero)
    {
        Assert.All(Enum.GetValues<FaseFalloBaseDatos>(), fase => Assert.True(ClasificadorFalloSql.EsNoDisponible(numero, fase)));
    }

    [Fact]
    public void Timeout_SoloCuentaAlAbrir()
    {
        Assert.True(ClasificadorFalloSql.EsNoDisponible(-2, FaseFalloBaseDatos.AlAbrir));
        Assert.False(ClasificadorFalloSql.EsNoDisponible(-2, FaseFalloBaseDatos.EnComando));
        Assert.False(ClasificadorFalloSql.EsNoDisponible(-2, FaseFalloBaseDatos.EnConfirmacion));
    }

    [Theory]
    [InlineData(547)]   // restricción
    [InlineData(2601)]  // índice único
    [InlineData(2627)]  // clave única
    [InlineData(1205)]  // deadlock
    [InlineData(18456)] // login fallido (configuración)
    [InlineData(102)]   // sintaxis
    [InlineData(0)]
    public void OtrosErrores_NoCuentan(int numero)
    {
        Assert.All(Enum.GetValues<FaseFalloBaseDatos>(), fase => Assert.False(ClasificadorFalloSql.EsNoDisponible(numero, fase)));
    }
}
