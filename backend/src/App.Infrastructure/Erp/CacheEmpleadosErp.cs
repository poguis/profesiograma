using App.Application.Empleados;
using App.Application.Erp;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Erp;

/// <summary>
/// Lista de empleados activos en memoria (TAREA-26d, opción C; singleton). Nada se guarda en la base.
///  - TTL ServiciosExternos:CacheEmpleadosMinutos (P3, 10): dentro del TTL no se llama al ERP.
///  - Descarga única: un SemaphoreSlim serializa las descargas; las peticiones simultáneas esperan la misma.
///  - P4: si la API no responde y la lista anterior tiene menos de CacheEmpleadosMaxAntiguedadMinutos (60), el buscador
///    y la vista previa la usan (EsAnterior = true). Tras un fallo no se reintenta durante <see cref="EsperaTrasFallo"/>
///    (el buscador no espera el timeout en cada tecla).
///  - Lectura fresca (registro, P5): siempre descarga; nunca usa la lista anterior.
/// Solo se guardan los registros válidos y normalizados (MapeoEmpleado); ~1633 registros, unos 1–2 MB.
/// </summary>
internal sealed class CacheEmpleadosErp(
    Func<IDescargaEmpleadosErp> descarga,
    ServiciosExternosOpciones opciones,
    TimeProvider reloj,
    ILogger<CacheEmpleadosErp> logger) : IFuenteEmpleadosErp, IDisposable
{
    public static readonly TimeSpan EsperaTrasFallo = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim cerrojo = new(1, 1);
    private ListaEmpleadosErp? actual;
    private DateTimeOffset? ultimoFallo;

    private TimeSpan Ttl => TimeSpan.FromMinutes(opciones.CacheEmpleadosMinutos);
    private TimeSpan MaxAntiguedad => TimeSpan.FromMinutes(opciones.CacheEmpleadosMaxAntiguedadMinutos);

    public async Task<ListaEmpleadosErp> ObtenerActivosAsync(CancellationToken ct)
    {
        if (Vigente(Volatile.Read(ref actual)) is { } enCache)
        {
            return enCache;
        }

        await cerrojo.WaitAsync(ct);
        try
        {
            // Otra petición pudo descargarla mientras se esperaba el cerrojo.
            if (Vigente(actual) is { } descargada)
            {
                return descargada;
            }

            if (Anterior() is { } reciente && ultimoFallo is { } fallo && reloj.GetUtcNow() - fallo < EsperaTrasFallo)
            {
                return reciente; // falló hace menos de 1 min: no se espera otro timeout
            }

            try
            {
                return await DescargarAsync(ct);
            }
            catch (ErpNoDisponibleException) when (Anterior() is not null)
            {
                ultimoFallo = reloj.GetUtcNow();
                logger.LogWarning("Empleados del ERP no disponibles: se usa la lista anterior ({Minutos} min).",
                    (int)(reloj.GetUtcNow() - actual!.ObtenidaUtc).TotalMinutes);
                return Anterior()!;
            }
            catch (ErpNoDisponibleException)
            {
                ultimoFallo = reloj.GetUtcNow();
                throw;
            }
        }
        finally
        {
            cerrojo.Release();
        }
    }

    public async Task<ListaEmpleadosErp> ObtenerActivosFrescosAsync(CancellationToken ct)
    {
        await cerrojo.WaitAsync(ct);
        try
        {
            return await DescargarAsync(ct);
        }
        catch (ErpNoDisponibleException)
        {
            ultimoFallo = reloj.GetUtcNow();
            throw;
        }
        finally
        {
            cerrojo.Release();
        }
    }

    public void Dispose() => cerrojo.Dispose();

    private async Task<ListaEmpleadosErp> DescargarAsync(CancellationToken ct)
    {
        var registros = await descarga().DescargarAsync(ct);
        var validos = registros.Where(MapeoEmpleado.EsValido).Select(MapeoEmpleado.Normalizado).ToList();
        if (validos.Count == 0)
        {
            throw new ErpNoDisponibleException("empleados", "la respuesta no trae empleados activos válidos");
        }

        var lista = new ListaEmpleadosErp(validos, reloj.GetUtcNow());
        Volatile.Write(ref actual, lista);
        ultimoFallo = null;
        return lista;
    }

    private ListaEmpleadosErp? Vigente(ListaEmpleadosErp? lista) =>
        lista is not null && reloj.GetUtcNow() - lista.ObtenidaUtc < Ttl ? lista : null;

    private ListaEmpleadosErp? Anterior() =>
        actual is { } lista && reloj.GetUtcNow() - lista.ObtenidaUtc < MaxAntiguedad ? lista.ComoAnterior() : null;
}
