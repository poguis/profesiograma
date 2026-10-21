using Microsoft.Data.SqlClient;

namespace App.Infrastructure.Persistencia;

/// <summary>Ajustes de la cadena de conexión hechos en código (no se editan los user-secrets).</summary>
internal static class CadenaConexion
{
    public const string ClaveTiempoEspera = "BaseDatos:TiempoEsperaConexionSegundos";
    public const int TiempoEsperaPorDefecto = 5;

    /// <summary>
    /// TAREA-22c: Connect Timeout corto (el de SqlClient es 15 s) para responder 503 rápido con la base caída. Si la
    /// cadena ya trae un valor explícito (Connect Timeout, Connection Timeout o Timeout), gana la cadena.
    /// </summary>
    public static string ConTiempoEspera(string cadena, int segundos)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(segundos);
        var builder = new SqlConnectionStringBuilder(cadena);

        // ShouldSerialize resuelve los alias y solo es true si el valor vino en la cadena (no por el valor por defecto).
        if (builder.ShouldSerialize("Connect Timeout"))
        {
            return cadena;
        }

        builder.ConnectTimeout = segundos;
        return builder.ConnectionString;
    }

    public const string ClaveForzarTcp = "BaseDatos:ForzarTcp";

    private static readonly string[] Prefijos = ["tcp:", "np:", "lpc:", "admin:"];

    /// <summary>
    /// TAREA-22c-b: antepone "tcp:" al Data Source. Sin protocolo, SqlClient prueba TCP y luego Named Pipes, y esa segunda
    /// fase no respeta el Connect Timeout contra un host que no responde (27 s medidos con 5 s). Quedan igual: el Data
    /// Source vacío, uno con prefijo (tcp:, np:, lpc:, admin:) y los locales (".", "(local)", "(localdb)", con o sin
    /// instancia: shared memory y LocalDB). "Network Library" no hace falta excluirla: en .NET SqlClient la rechaza
    /// (NotSupportedException) al leer la cadena.
    /// </summary>
    public static string ConTcp(string cadena)
    {
        var builder = new SqlConnectionStringBuilder(cadena);
        var origen = builder.DataSource.Trim();
        if (origen.Length == 0 || Prefijos.Any(p => origen.StartsWith(p, StringComparison.OrdinalIgnoreCase)) || EsLocal(origen))
        {
            return cadena;
        }

        builder.DataSource = "tcp:" + origen;
        return builder.ConnectionString;
    }

    private static bool EsLocal(string origen)
    {
        var host = origen.Split('\\', ',')[0].Trim();
        return host == "." || host.Equals("(local)", StringComparison.OrdinalIgnoreCase) || host.Equals("(localdb)", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Cadena final de la aplicación: Connect Timeout (gana uno explícito) y, si <paramref name="forzarTcp"/>, "tcp:".</summary>
    public static string Ajustar(string cadena, int segundos, bool forzarTcp)
    {
        var conTiempo = ConTiempoEspera(cadena, segundos);
        return forzarTcp ? ConTcp(conTiempo) : conTiempo;
    }
}
