using App.Infrastructure.Persistencia;
using Microsoft.Data.SqlClient;

namespace App.Infrastructure.Tests.Persistencia;

/// <summary>TAREA-22c (decisión 1b): Connect Timeout en código; si la cadena trae uno explícito, gana la cadena. Cadenas ficticias.</summary>
public sealed class CadenaConexionTests
{
    private const string Base = "Server=SERVIDOR_FICTICIO;Database=BD_FICTICIA_22C;User Id=usuario_ficticio;Password=clave_ficticia";

    private static int Timeout(string cadena) => new SqlConnectionStringBuilder(cadena).ConnectTimeout;

    [Fact]
    public void SinValorEnLaCadena_SeAplicaElConfigurado()
    {
        var resultado = CadenaConexion.ConTiempoEspera(Base, 5);

        Assert.Equal(5, Timeout(resultado));
        var b = new SqlConnectionStringBuilder(resultado);
        Assert.Equal(("SERVIDOR_FICTICIO", "BD_FICTICIA_22C", "usuario_ficticio", "clave_ficticia"),
            (b.DataSource, b.InitialCatalog, b.UserID, b.Password));
    }

    [Theory]
    [InlineData("Connect Timeout=30", 30)]
    [InlineData("Connection Timeout=20", 20)]
    [InlineData("Timeout=7", 7)]
    [InlineData("connect timeout = 12", 12)]
    [InlineData("Connect Timeout=15", 15)] // explícito e igual al valor por defecto de SqlClient: también gana
    public void ConValorExplicito_GanaLaCadena(string parte, int esperado)
    {
        var cadena = $"{Base};{parte}";

        var resultado = CadenaConexion.ConTiempoEspera(cadena, 5);

        Assert.Equal(cadena, resultado);
        Assert.Equal(esperado, Timeout(resultado));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ValorConfiguradoInvalido_Lanza(int segundos)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CadenaConexion.ConTiempoEspera(Base, segundos));
    }
}
