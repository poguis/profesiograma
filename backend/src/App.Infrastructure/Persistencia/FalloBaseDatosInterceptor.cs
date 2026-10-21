using System.Data.Common;
using App.Application.Comun;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace App.Infrastructure.Persistencia;

/// <summary>
/// ¿El error de SQL Server significa "la base no responde"? (TAREA-22c, lógica pura). No cuentan las violaciones de
/// restricción (547, 2601, 2627), el deadlock (1205), el login fallido (18456, error de configuración) ni los de sintaxis.
/// </summary>
internal static class ClasificadorFalloSql
{
    /// <summary>
    /// -1 servidor o instancia no encontrados; 2 y 53 red; 64, 233, 10053 y 10054 conexión cortada; 258 espera agotada;
    /// 4060 no se pudo abrir la base (también si el nombre está mal: se registra con su número); 10060 sin respuesta;
    /// 10061 rechazada; 11001 host desconocido (DNS).
    /// </summary>
    private static readonly HashSet<int> NoDisponible = [-1, 2, 53, 64, 233, 258, 4060, 10053, 10054, 10060, 10061, 11001];

    /// <summary>Timeout (-2): solo al abrir la conexión. En un comando es una consulta lenta y sigue siendo 500.</summary>
    public const int Timeout = -2;

    public static bool EsNoDisponible(int numero, FaseFalloBaseDatos fase)
        => NoDisponible.Contains(numero) || (numero == Timeout && fase == FaseFalloBaseDatos.AlAbrir);
}

/// <summary>
/// Convierte los fallos de conexión de SQL Server en <see cref="BaseDatosNoDisponibleException"/> (503 en la API).
/// Lanzar desde *Failed reemplaza la excepción original (comprobado en FalloBaseDatosInterceptorTests). Sin estado.
/// </summary>
internal sealed class FalloBaseDatosInterceptor : IDbConnectionInterceptor, IDbCommandInterceptor, IDbTransactionInterceptor
{
    public static readonly FalloBaseDatosInterceptor Instancia = new();

    public void ConnectionFailed(DbConnection connection, ConnectionErrorEventData eventData)
        => Clasificar(eventData.Exception, FaseFalloBaseDatos.AlAbrir, eventData.Duration);

    public Task ConnectionFailedAsync(DbConnection connection, ConnectionErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Clasificar(eventData.Exception, FaseFalloBaseDatos.AlAbrir, eventData.Duration);
        return Task.CompletedTask;
    }

    public void CommandFailed(DbCommand command, CommandErrorEventData eventData)
        => Clasificar(eventData.Exception, FaseFalloBaseDatos.EnComando, eventData.Duration);

    public Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Clasificar(eventData.Exception, FaseFalloBaseDatos.EnComando, eventData.Duration);
        return Task.CompletedTask;
    }

    public void TransactionFailed(DbTransaction transaction, TransactionErrorEventData eventData)
        => Clasificar(eventData.Exception, FaseTransaccion(eventData.Action), TimeSpan.Zero);

    public Task TransactionFailedAsync(DbTransaction transaction, TransactionErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Clasificar(eventData.Exception, FaseTransaccion(eventData.Action), TimeSpan.Zero);
        return Task.CompletedTask;
    }

    private static FaseFalloBaseDatos FaseTransaccion(string accion)
        => string.Equals(accion, "Commit", StringComparison.OrdinalIgnoreCase) ? FaseFalloBaseDatos.EnConfirmacion : FaseFalloBaseDatos.EnComando;

    private static void Clasificar(Exception excepcion, FaseFalloBaseDatos fase, TimeSpan duracion)
    {
        if (excepcion is SqlException sql && ClasificadorFalloSql.EsNoDisponible(sql.Number, fase))
        {
            throw new BaseDatosNoDisponibleException(fase, sql.Number, duracion.TotalMilliseconds, sql);
        }
    }
}
