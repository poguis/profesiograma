using App.Application.Novedades.Sincronizacion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace App.Infrastructure.NovedadesExternas;

/// <summary>
/// Fuente de las novedades externas según "NovedadesExternas:Modo" (TAREA-23-3, H1), con el criterio de
/// ServiciosExternos:Modo: Simulado y ArchivoLocal SOLO en Development (en otro entorno la API no arranca); sin modo o
/// Graph (23-4, aún no implementado) → la API arranca y el endpoint responde 503 «Fuente de novedades externas no
/// configurada». Otro valor → la API no arranca. La ruta del CSV va SOLO en user-secrets (NovedadesExternas:RutaArchivo).
/// </summary>
public static class NovedadesExternasServiceCollectionExtensions
{
    public const string Seccion = "NovedadesExternas";
    public const string ModoSimulado = "Simulado", ModoArchivoLocal = "ArchivoLocal", ModoGraph = "Graph";

    public static IServiceCollection AddNovedadesExternas(this IServiceCollection services, IConfiguration configuration, bool esDesarrollo)
    {
        var modo = configuration[$"{Seccion}:Modo"]?.Trim();
        var ruta = configuration[$"{Seccion}:RutaArchivo"];

        if (Es(modo, ModoSimulado) || Es(modo, ModoArchivoLocal))
        {
            if (!esDesarrollo)
            {
                throw new InvalidOperationException($"{Seccion}:Modo = {modo} solo está permitido en Development.");
            }

            return Es(modo, ModoSimulado)
                ? services.AddSingleton<IFuenteNovedadesExternas, FuenteNovedadesSimulada>()
                : services.AddSingleton<IFuenteNovedadesExternas>(_ => new FuenteNovedadesArchivoLocal(ruta));
        }

        if (string.IsNullOrEmpty(modo) || Es(modo, ModoGraph))
        {
            return services.AddSingleton<IFuenteNovedadesExternas, FuenteNovedadesNoConfigurada>();
        }

        throw new InvalidOperationException($"{Seccion}:Modo = '{modo}' no es válido (use {ModoSimulado}, {ModoArchivoLocal} o {ModoGraph}).");
    }

    private static bool Es(string? modo, string valor) => string.Equals(modo, valor, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Sin fuente configurada (o Graph, que llega en la 23-4): 503.</summary>
internal sealed class FuenteNovedadesNoConfigurada : IFuenteNovedadesExternas
{
    public string Fuente => "NoConfigurada";
    public bool AdmiteEscenarios => false;

    public Task<LecturaNovedadesExternas> LeerPermisosMedicosAsync(string? escenario, CancellationToken ct) =>
        throw FuenteNovedadesNoDisponibleException.NoConfigurada();
}
