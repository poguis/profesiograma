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

    // ---------------------------------------------------------------- TAREA-22c-b: "tcp:" (decisiones 1a y 2a)

    private static string Cadena(string clave, string origen) =>
        $"{clave}={origen};Database=BD_FICTICIA_22C;User Id=usuario_ficticio;Password=clave_ficticia";

    [Theory]
    [InlineData("Server", @"NIQUEL\SSDEV", @"tcp:NIQUEL\SSDEV")] // forma real
    [InlineData("Data Source", @"NIQUEL\SSDEV", @"tcp:NIQUEL\SSDEV")]
    [InlineData("Address", "SERVIDOR_FICTICIO", "tcp:SERVIDOR_FICTICIO")]
    [InlineData("Server", @"NiQuEl\SsDeV", @"tcp:NiQuEl\SsDeV")]
    [InlineData("Server", @"  NIQUEL\SSDEV  ", @"tcp:NIQUEL\SSDEV")]
    [InlineData("Server", "SERVIDOR_FICTICIO,1433", "tcp:SERVIDOR_FICTICIO,1433")]
    [InlineData("Server", "192.0.2.1", "tcp:192.0.2.1")]
    [InlineData("Server", @"192.0.2.1\SSDEV", @"tcp:192.0.2.1\SSDEV")]
    [InlineData("Server", "localhost", "tcp:localhost")]
    public void SinProtocolo_AnteponeTcp_YConservaElResto(string clave, string origen, string esperado)
    {
        var b = new SqlConnectionStringBuilder(CadenaConexion.ConTcp(Cadena(clave, origen)));

        Assert.Equal((esperado, "BD_FICTICIA_22C", "usuario_ficticio", "clave_ficticia"), (b.DataSource, b.InitialCatalog, b.UserID, b.Password));
    }

    [Theory]
    [InlineData(".")]
    [InlineData("(local)")]
    [InlineData("(LOCAL)")]
    [InlineData(@".\SQLEXPRESS")]
    [InlineData(@"(local)\SQLEXPRESS")]
    [InlineData(@"(localdb)\MSSQLLocalDB")]
    [InlineData(@"(LocalDB)\MSSQLLocalDB")]
    [InlineData(@"np:\\SERVIDOR_FICTICIO\pipe\sql\query")]
    [InlineData("lpc:(local)")]
    [InlineData("tcp:SERVIDOR_FICTICIO")]
    [InlineData(@"TCP:NIQUEL\SSDEV")]
    [InlineData("Tcp:SERVIDOR_FICTICIO,1433")]
    [InlineData("admin:SERVIDOR_FICTICIO")]
    [InlineData("ADMIN:SERVIDOR_FICTICIO")]
    public void LocalesYConPrefijo_QuedanIgual(string origen)
    {
        var cadena = Cadena("Server", origen);

        Assert.Equal(cadena, CadenaConexion.ConTcp(cadena));
    }

    [Fact]
    public void SinDataSource_QuedaIgual()
    {
        const string cadena = "Database=BD_FICTICIA_22C;User Id=usuario_ficticio;Password=clave_ficticia";

        Assert.Equal(cadena, CadenaConexion.ConTcp(cadena));
    }

    [Fact]
    public void NetworkLibrary_SqlClientLaRechazaEnNet()
    {
        // Por eso ConTcp no la excluye aparte: una cadena así no se puede usar en .NET (tampoco antes de la TAREA-22c-b).
        Assert.Throws<NotSupportedException>(() => CadenaConexion.ConTcp("Server=SERVIDOR_FICTICIO;Network Library=dbmssocn"));
    }

    [Fact]
    public void Ajustar_ComponeTimeoutYTcp_ElTimeoutExplicitoSigueGanando()
    {
        var explicito = new SqlConnectionStringBuilder(CadenaConexion.Ajustar(Cadena("Server", @"NIQUEL\SSDEV") + ";Connect Timeout=30", 5, true));
        var sinValor = new SqlConnectionStringBuilder(CadenaConexion.Ajustar(Cadena("Server", @"NIQUEL\SSDEV"), 5, true));
        var sinTcp = new SqlConnectionStringBuilder(CadenaConexion.Ajustar(Cadena("Server", @"NIQUEL\SSDEV"), 5, false));

        Assert.Equal((@"tcp:NIQUEL\SSDEV", 30, "clave_ficticia"), (explicito.DataSource, explicito.ConnectTimeout, explicito.Password));
        Assert.Equal((@"tcp:NIQUEL\SSDEV", 5), (sinValor.DataSource, sinValor.ConnectTimeout));
        Assert.Equal((@"NIQUEL\SSDEV", 5), (sinTcp.DataSource, sinTcp.ConnectTimeout));
    }
}
