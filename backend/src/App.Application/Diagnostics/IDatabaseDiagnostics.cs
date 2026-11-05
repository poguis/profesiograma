namespace App.Application.Diagnostics;

/// <summary>Contrato para verificar la conexión y la versión de la base de datos.</summary>
public interface IDatabaseDiagnostics
{
    Task<DatabaseInfo> GetInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>TAREA-7d-1 (D6): true si la base responde a un SELECT 1. Nunca lanza por un fallo de conexión.</summary>
    Task<bool> EstaDisponibleAsync(CancellationToken cancellationToken = default);
}
