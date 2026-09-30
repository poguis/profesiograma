namespace App.Application.Diagnostics;

/// <summary>Contrato para verificar la conexión y la versión de la base de datos.</summary>
public interface IDatabaseDiagnostics
{
    Task<DatabaseInfo> GetInfoAsync(CancellationToken cancellationToken = default);
}
