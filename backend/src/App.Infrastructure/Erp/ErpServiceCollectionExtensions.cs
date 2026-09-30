using System.Globalization;
using App.Application.Erp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace App.Infrastructure.Erp;

public static class ErpServiceCollectionExtensions
{
    /// <summary>
    /// Registra ICatalogoErp según "ServiciosExternos:Modo":
    ///  - Http: APIs reales (HttpClient tipado con timeout + caché en memoria).
    ///  - Simulado: datos fijos; SOLO en Development (en otro entorno la API no arranca, igual que DevAuth).
    /// </summary>
    public static IServiceCollection AddCatalogoErp(this IServiceCollection services, IConfiguration configuration, bool esDesarrollo)
        => services.AddCatalogoErp(LeerOpciones(configuration), esDesarrollo);

    internal static IServiceCollection AddCatalogoErp(this IServiceCollection services, ServiciosExternosOpciones opciones, bool esDesarrollo)
    {
        if (string.Equals(opciones.Modo, ServiciosExternosOpciones.ModoSimulado, StringComparison.OrdinalIgnoreCase))
        {
            if (!esDesarrollo)
            {
                throw new InvalidOperationException(
                    $"{ServiciosExternosOpciones.Seccion}:Modo = {ServiciosExternosOpciones.ModoSimulado} solo está permitido en Development.");
            }

            services.AddSingleton<ICatalogoErp, CatalogoErpSimulado>();
            return services;
        }

        if (!string.Equals(opciones.Modo, ServiciosExternosOpciones.ModoHttp, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{ServiciosExternosOpciones.Seccion}:Modo = '{opciones.Modo}' no es válido (use Http o Simulado).");
        }

        ValidarBase(opciones.ErpBase7048, nameof(opciones.ErpBase7048));
        ValidarBase(opciones.ErpBase7055, nameof(opciones.ErpBase7055));
        if (opciones.TimeoutSegundos is < 1 or > 120)
        {
            throw new InvalidOperationException(
                $"{ServiciosExternosOpciones.Seccion}:TimeoutSegundos debe estar entre 1 y 120 (actual: {opciones.TimeoutSegundos}).");
        }

        services.AddMemoryCache();
        services.AddSingleton(opciones);
        services.AddHttpClient<ICatalogoErp, CatalogoErpHttp>(cliente => cliente.Timeout = TimeSpan.FromSeconds(opciones.TimeoutSegundos));
        return services;
    }

    internal static ServiciosExternosOpciones LeerOpciones(IConfiguration configuration)
    {
        var seccion = configuration.GetSection(ServiciosExternosOpciones.Seccion);
        var timeout = seccion[nameof(ServiciosExternosOpciones.TimeoutSegundos)];

        return new ServiciosExternosOpciones
        {
            Modo = seccion[nameof(ServiciosExternosOpciones.Modo)] ?? ServiciosExternosOpciones.ModoHttp,
            ErpBase7048 = seccion[nameof(ServiciosExternosOpciones.ErpBase7048)] ?? string.Empty,
            ErpBase7055 = seccion[nameof(ServiciosExternosOpciones.ErpBase7055)] ?? string.Empty,
            TimeoutSegundos = int.TryParse(timeout, NumberStyles.Integer, CultureInfo.InvariantCulture, out var segundos) ? segundos : 15,
        };
    }

    private static void ValidarBase(string valor, string nombre)
    {
        if (!Uri.TryCreate(valor, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException(
                $"{ServiciosExternosOpciones.Seccion}:{nombre} debe ser una URL https absoluta (actual: '{valor}').");
        }
    }
}
