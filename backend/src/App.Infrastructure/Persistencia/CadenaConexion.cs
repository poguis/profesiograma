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
}
