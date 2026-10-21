using App.Application.Diagnostics;
using App.Infrastructure.Diagnostics;
using App.Infrastructure.Erp;
using App.Infrastructure.Persistencia;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace App.Infrastructure;

public static class DependencyInjection
{
    public const string ConnectionStringName = "Profesiograma";

    /// <param name="esDesarrollo">Entorno Development (permite ServiciosExternos:Modo = Simulado).</param>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration, bool esDesarrollo)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"Falta la cadena de conexión 'ConnectionStrings:{ConnectionStringName}'. Configúrela con user-secrets (ver README).");

        // TAREA-22c: Connect Timeout corto para EF y el diagnóstico (si la cadena trae uno explícito, gana la cadena).
        var segundos = configuration[CadenaConexion.ClaveTiempoEspera] is { } valor
            ? int.Parse(valor, System.Globalization.CultureInfo.InvariantCulture)
            : CadenaConexion.TiempoEsperaPorDefecto;
        connectionString = CadenaConexion.ConTiempoEspera(connectionString, segundos);

        services.AddSingleton<IDatabaseDiagnostics>(_ => new SqlDatabaseDiagnostics(connectionString));
        services.AddPersistencia(connectionString);   // [PASO 2] EF Core + auditoría + consultas + sembrador
        services.AddCatalogoErp(configuration, esDesarrollo);   // ERP: Http o Simulado (TAREA-11)
        return services;
    }
}
