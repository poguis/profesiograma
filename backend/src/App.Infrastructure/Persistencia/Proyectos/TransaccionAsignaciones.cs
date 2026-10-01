using System.Data;
using App.Application.Proyectos.Crear;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Persistencia.Proyectos;

/// <summary>
/// Transacción + bloqueo de aplicación exclusivo sobre 'profesiograma:asignaciones' (sp_getapplock, dueño = transacción).
/// Todos los que escriben asignaciones se serializan en este recurso: la verificación de cruces y la del código
/// dentro de la transacción no tienen condiciones de carrera. El bloqueo se libera al confirmar o revertir.
/// REGLA: toda escritura de asignaciones (crear y, en el futuro, editar o fecha de corte) debe usar esta clase.
/// </summary>
internal sealed class TransaccionAsignaciones(ProfesiogramaDbContext db, ILogger<TransaccionAsignaciones> logger) : ITransaccionAsignaciones
{
    public const string Recurso = "profesiograma:asignaciones";
    public const int EsperaMaximaMs = 15_000;

    public async Task<T> EjecutarAsync<T>(Func<CancellationToken, Task<T>> accion, Func<T, bool> confirmar, CancellationToken ct)
    {
        // Sin EnableRetryOnFailure: la transacción manual no necesita CreateExecutionStrategy.
        await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await ObtenerBloqueoAsync(ct);

        var resultado = await accion(ct);
        if (confirmar(resultado))
        {
            await transaccion.CommitAsync(ct);
        }
        else
        {
            await transaccion.RollbackAsync(ct);
        }

        // Si la acción lanza una excepción, DisposeAsync revierte la transacción y libera el bloqueo.
        return resultado;
    }

    private async Task ObtenerBloqueoAsync(CancellationToken ct)
    {
        var resultado = new SqlParameter("@resultado", SqlDbType.Int) { Direction = ParameterDirection.Output };

        await db.Database.ExecuteSqlRawAsync(
            """
            DECLARE @r int;
            EXEC @r = sp_getapplock @Resource = @recurso, @LockMode = N'Exclusive', @LockOwner = N'Transaction', @LockTimeout = @espera;
            SET @resultado = @r;
            """,
            [new SqlParameter("@recurso", SqlDbType.NVarChar, 255) { Value = Recurso },
             new SqlParameter("@espera", SqlDbType.Int) { Value = EsperaMaximaMs },
             resultado],
            ct);

        // 0 = concedido, 1 = concedido tras esperar; < 0 = timeout (-1), cancelado (-2), deadlock (-3) o error (-999).
        var codigo = resultado.Value is int valor ? valor : -999;
        if (codigo < 0)
        {
            logger.LogWarning("No se obtuvo el bloqueo {Recurso} (sp_getapplock = {Codigo}).", Recurso, codigo);
            throw new RegistroOcupadoException($"No se obtuvo el bloqueo de asignaciones (sp_getapplock = {codigo}).");
        }
    }
}
