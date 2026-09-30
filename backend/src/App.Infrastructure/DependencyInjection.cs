using App.Application.Diagnostics;
using App.Infrastructure.Diagnostics;
using App.Infrastructure.Persistencia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace App.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Profesiograma";

    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"Falta la cadena de conexión 'ConnectionStrings:{ConnectionStringName}'. Configúrela con user-secrets (ver README).");

        services.AddSingleton<IDatabaseDiagnostics>(_ => new SqlDatabaseDiagnostics(connectionString));
        services.AddPersistencia(connectionString);   // [PASO 2] EF Core + auditoría + consultas + sembrador
        return services;
    }
}
