using App.Application.Diagnostics;
using App.Application.Seguridad;
using App.Infrastructure.Diagnostics;
using App.Infrastructure.Erp;
using App.Infrastructure.NovedadesExternas;
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
        var connectionString = CadenaConexionEfectiva(configuration);

        services.AddSingleton<IDatabaseDiagnostics>(_ => new SqlDatabaseDiagnostics(connectionString));
        services.AddPersistencia(connectionString);   // [PASO 2] EF Core + auditoría + consultas + sembrador
        services.AddCatalogoErp(configuration, esDesarrollo);   // ERP: Http o Simulado (TAREA-11)
        services.AddNovedadesExternas(configuration, esDesarrollo); // PERMISO MEDICO: Simulado / ArchivoLocal / sin fuente (TAREA-23-3)
        // TAREA-25-1: dominios de correo del alta de usuarios (sin valor → sedemi.com).
        services.AddSingleton(new ConfiguracionUsuarios(configuration.GetSection("Usuarios:DominiosPermitidos").GetChildren()
            .Select(c => c.Value?.Trim().ToLowerInvariant()).OfType<string>().Where(d => d.Length > 0).ToList()));
        return services;
    }

    /// <summary>
    /// Cadena que usan EF y el diagnóstico. TAREA-22c: Connect Timeout corto (BaseDatos:TiempoEsperaConexionSegundos, 5;
    /// gana uno explícito de la cadena). TAREA-22c-b: "tcp:" si BaseDatos:ForzarTcp no es false (sin la clave, true).
    /// </summary>
    public static string CadenaConexionEfectiva(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                $"Falta la cadena de conexión 'ConnectionStrings:{ConnectionStringName}'. Configúrela con user-secrets (ver README).");

        var segundos = configuration[CadenaConexion.ClaveTiempoEspera] is { } valor
            ? int.Parse(valor, System.Globalization.CultureInfo.InvariantCulture)
            : CadenaConexion.TiempoEsperaPorDefecto;
        var forzarTcp = configuration[CadenaConexion.ClaveForzarTcp] is not { } texto || bool.Parse(texto);
        return CadenaConexion.Ajustar(connectionString, segundos, forzarTcp);
    }
}
