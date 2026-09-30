using App.Application.Diagnostics;
using Microsoft.Data.SqlClient;

namespace App.Infrastructure.Diagnostics;

/// <summary>
/// Consulta versión, compatibilidad y permisos de SQL Server.
/// Requisito del modelo (Fase 2): SQL Server 2017+ (versión mayor 14) y nivel de compatibilidad ≥ 130.
/// </summary>
public sealed class SqlDatabaseDiagnostics(string connectionString) : IDatabaseDiagnostics
{
    private const int VersionMayorMinima = 14;   // SQL Server 2017
    private const int CompatibilidadMinima = 130; // ISJSON, OPENJSON

    private const string Query = """
        SELECT
            CAST(SERVERPROPERTY('ServerName')     AS nvarchar(128)) AS Servidor,
            DB_NAME()                                                AS BaseDatos,
            SUSER_SNAME()                                            AS Usuario,
            CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)) AS VersionProducto,
            CAST(SERVERPROPERTY('ProductLevel')   AS nvarchar(128)) AS NivelProducto,
            CAST(SERVERPROPERTY('Edition')        AS nvarchar(128)) AS Edicion,
            (SELECT compatibility_level FROM sys.databases WHERE name = DB_NAME()) AS Compatibilidad,
            CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS nvarchar(128)) AS Collation,
            HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'CREATE TABLE')  AS PuedeCrearTablas,
            HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'CREATE SCHEMA') AS PuedeCrearEsquemas,
            IS_ROLEMEMBER('db_owner')                                 AS EsDbOwner;
        """;

    public async Task<DatabaseInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(Query, connection) { CommandTimeout = 30 };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("La consulta de diagnóstico no devolvió resultados.");

        var versionProducto = reader.GetString(3);
        var versionMayor = int.Parse(versionProducto.Split('.')[0]);
        var compatibilidad = Convert.ToInt32(reader.GetValue(6));
        var puedeCrearTablas = ToBool(reader.GetValue(8));
        var puedeCrearEsquemas = ToBool(reader.GetValue(9));
        var esDbOwner = ToBool(reader.GetValue(10));

        var observaciones = new List<string>();
        if (versionMayor < VersionMayorMinima)
            observaciones.Add($"Versión {NombreVersion(versionMayor)} inferior a SQL Server 2017: el script de Fase 2 debe adaptarse (STRING_AGG).");
        if (compatibilidad < CompatibilidadMinima)
            observaciones.Add($"Nivel de compatibilidad {compatibilidad} < {CompatibilidadMinima}: ISJSON/OPENJSON no disponibles.");
        if (!puedeCrearTablas)
            observaciones.Add("El usuario no tiene permiso CREATE TABLE.");
        if (!puedeCrearEsquemas)
            observaciones.Add("El usuario no tiene permiso CREATE SCHEMA (necesario para el esquema stg de la carga masiva).");
        if (observaciones.Count == 0)
            observaciones.Add("La base cumple los requisitos del modelo de la Fase 2.");

        return new DatabaseInfo(
            Servidor: reader.GetString(0),
            BaseDatos: reader.GetString(1),
            UsuarioConectado: reader.GetString(2),
            VersionProducto: versionProducto,
            VersionMayor: versionMayor,
            NombreVersion: NombreVersion(versionMayor),
            NivelProducto: reader.GetString(4),
            Edicion: reader.GetString(5),
            NivelCompatibilidad: compatibilidad,
            Collation: reader.GetString(7),
            PuedeCrearTablas: puedeCrearTablas,
            PuedeCrearEsquemas: puedeCrearEsquemas,
            EsDbOwner: esDbOwner,
            CumpleRequisitos: versionMayor >= VersionMayorMinima
                              && compatibilidad >= CompatibilidadMinima
                              && puedeCrearTablas,
            Observaciones: observaciones);
    }

    private static bool ToBool(object value) => value is not DBNull && Convert.ToInt32(value) == 1;

    private static string NombreVersion(int mayor) => mayor switch
    {
        11 => "SQL Server 2012",
        12 => "SQL Server 2014",
        13 => "SQL Server 2016",
        14 => "SQL Server 2017",
        15 => "SQL Server 2019",
        16 => "SQL Server 2022",
        17 => "SQL Server 2025",
        _ => $"Versión {mayor}"
    };
}
