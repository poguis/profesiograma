using System.Globalization;
using App.Application.Empleados;
using App.Application.Erp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

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
            services.AddSingleton<IDescargaEmpleadosErp, EmpleadosErpSimulado>(); // TAREA-26d (P12)
            AddCacheEmpleados(services, opciones);
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

        if (opciones.TimeoutEmpleadosSegundos is < 1 or > 600)
        {
            throw new InvalidOperationException(
                $"{ServiciosExternosOpciones.Seccion}:TimeoutEmpleadosSegundos debe estar entre 1 y 600 (actual: {opciones.TimeoutEmpleadosSegundos}).");
        }

        services.AddMemoryCache();
        services.AddSingleton(opciones);
        services.AddHttpClient<ICatalogoErp, CatalogoErpHttp>(cliente => cliente.Timeout = TimeSpan.FromSeconds(opciones.TimeoutSegundos));
        services.AddHttpClient<IDescargaEmpleadosErp, FuenteEmpleadosErpHttp>(
            cliente => cliente.Timeout = TimeSpan.FromSeconds(opciones.TimeoutEmpleadosSegundos)); // TAREA-26d
        AddCacheEmpleados(services, opciones);
        return services;
    }

    /// <summary>
    /// TAREA-26d: lista de empleados en memoria (singleton, descarga única). La descarga se resuelve en cada llamada (el
    /// cliente HTTP tipado es transitorio; su HttpMessageHandler lo administra IHttpClientFactory).
    /// </summary>
    private static void AddCacheEmpleados(IServiceCollection services, ServiciosExternosOpciones opciones)
    {
        if (opciones.CacheEmpleadosMinutos is < 1 or > 1440
            || opciones.CacheEmpleadosMaxAntiguedadMinutos < opciones.CacheEmpleadosMinutos
            || opciones.CacheEmpleadosMaxAntiguedadMinutos > 1440)
        {
            throw new InvalidOperationException(
                $"{ServiciosExternosOpciones.Seccion}: CacheEmpleadosMinutos debe estar entre 1 y 1440 y " +
                "CacheEmpleadosMaxAntiguedadMinutos entre CacheEmpleadosMinutos y 1440.");
        }

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<IFuenteEmpleadosErp>(sp => new CacheEmpleadosErp(
            () => sp.GetRequiredService<IDescargaEmpleadosErp>(), opciones, sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<ILogger<CacheEmpleadosErp>>()));
    }

    /// <summary>
    /// Modo del ERP que usará la API ("Http" o "Simulado"), para el log del arranque (TAREA-26a). Sin URL ni secretos.
    /// Un modo inválido no llega aquí: AddCatalogoErp ya impidió el arranque.
    /// </summary>
    public static string ModoEfectivo(IConfiguration configuration) => ModoEfectivo(LeerOpciones(configuration));

    internal static string ModoEfectivo(ServiciosExternosOpciones opciones) =>
        string.Equals(opciones.Modo, ServiciosExternosOpciones.ModoSimulado, StringComparison.OrdinalIgnoreCase)
            ? ServiciosExternosOpciones.ModoSimulado
            : ServiciosExternosOpciones.ModoHttp;

    internal static ServiciosExternosOpciones LeerOpciones(IConfiguration configuration)
    {
        var seccion = configuration.GetSection(ServiciosExternosOpciones.Seccion);
        var timeout = seccion[nameof(ServiciosExternosOpciones.TimeoutSegundos)];
        var timeoutEmpleados = seccion[nameof(ServiciosExternosOpciones.TimeoutEmpleadosSegundos)];
        var cacheEmpleados = seccion[nameof(ServiciosExternosOpciones.CacheEmpleadosMinutos)];
        var antiguedadEmpleados = seccion[nameof(ServiciosExternosOpciones.CacheEmpleadosMaxAntiguedadMinutos)];

        return new ServiciosExternosOpciones
        {
            Modo = seccion[nameof(ServiciosExternosOpciones.Modo)] ?? ServiciosExternosOpciones.ModoHttp,
            ErpBase7048 = seccion[nameof(ServiciosExternosOpciones.ErpBase7048)] ?? string.Empty,
            ErpBase7055 = seccion[nameof(ServiciosExternosOpciones.ErpBase7055)] ?? string.Empty,
            TimeoutSegundos = int.TryParse(timeout, NumberStyles.Integer, CultureInfo.InvariantCulture, out var segundos) ? segundos : 15,
            TimeoutEmpleadosSegundos = int.TryParse(timeoutEmpleados, NumberStyles.Integer, CultureInfo.InvariantCulture, out var segundosEmpleados)
                ? segundosEmpleados
                : 60,
            CacheEmpleadosMinutos = int.TryParse(cacheEmpleados, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minutos)
                ? minutos
                : 10,
            CacheEmpleadosMaxAntiguedadMinutos = int.TryParse(antiguedadEmpleados, NumberStyles.Integer, CultureInfo.InvariantCulture,
                out var antiguedad)
                ? antiguedad
                : 60,
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
