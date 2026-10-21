using App.Infrastructure;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace App.Api.Tests;

/// <summary>
/// TAREA-22c-b (ajuste 2): BaseDatos:ForzarTcp ausente = true; la variable de entorno BaseDatos__ForzarTcp=false lo
/// desactiva (mismo mapeo "__" → ":" que usa el host). Cadenas ficticias; no se conecta a nada.
/// </summary>
public sealed class CadenaConexionConfiguracionTests
{
    private const string Cadena = @"Server=NIQUEL\SSDEV;Database=BD_FICTICIA_22C;User Id=usuario_ficticio;Password=clave_ficticia";

    private static SqlConnectionStringBuilder Efectiva(IConfiguration configuracion) =>
        new(DependencyInjection.CadenaConexionEfectiva(configuracion));

    private static IConfigurationRoot EnMemoria(params (string Clave, string Valor)[] valores) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(valores.Select(v => new KeyValuePair<string, string?>(v.Clave, v.Valor))
                .Append(new KeyValuePair<string, string?>("ConnectionStrings:Profesiograma", Cadena)))
            .Build();

    [Fact]
    public void SinLaClave_ForzarTcpEsTrue_YTimeoutPorDefecto5()
    {
        var b = Efectiva(EnMemoria());

        Assert.Equal((@"tcp:NIQUEL\SSDEV", 5), (b.DataSource, b.ConnectTimeout));
    }

    [Fact]
    public void ForzarTcpFalse_NoAntepone()
    {
        var b = Efectiva(EnMemoria(("BaseDatos:ForzarTcp", "false"), ("BaseDatos:TiempoEsperaConexionSegundos", "8")));

        Assert.Equal((@"NIQUEL\SSDEV", 8), (b.DataSource, b.ConnectTimeout));
    }

    [Fact]
    public void VariableDeEntorno_BaseDatos__ForzarTcp_False_LoDesactiva()
    {
        // Prefijo propio para no afectar a otras pruebas del proceso; AddEnvironmentVariables lo quita y convierte "__" en ":".
        const string prefijo = "PRUEBA22CB_";
        Environment.SetEnvironmentVariable(prefijo + "BaseDatos__ForzarTcp", "false");
        try
        {
            var configuracion = new ConfigurationBuilder()
                .AddInMemoryCollection([new KeyValuePair<string, string?>("ConnectionStrings:Profesiograma", Cadena),
                                        new KeyValuePair<string, string?>("BaseDatos:ForzarTcp", "true")]) // como appsettings.json
                .AddEnvironmentVariables(prefijo)
                .Build();

            Assert.Equal(@"NIQUEL\SSDEV", Efectiva(configuracion).DataSource);
        }
        finally
        {
            Environment.SetEnvironmentVariable(prefijo + "BaseDatos__ForzarTcp", null);
        }
    }
}
