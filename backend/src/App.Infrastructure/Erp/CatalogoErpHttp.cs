using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using App.Application.Erp;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Erp;

/// <summary>
/// Catálogos del ERP real (https://backstack.sedemi.com, puertos 7048/7055, sin autenticación).
/// Contrato de campos tomado del flujo ConsultarProyecto (Power Automate) y verificado contra el ERP (TAREA-11).
/// Caché en memoria: compañías y horarios 10 min; resto 5 min. Solo se cachean respuestas correctas.
/// No se modifica la validación de certificados.
/// </summary>
internal sealed class CatalogoErpHttp(
    HttpClient http,
    IMemoryCache cache,
    ServiciosExternosOpciones opciones,
    ILogger<CatalogoErpHttp> logger) : CatalogoErpBase
{
    internal static readonly TimeSpan DuracionLarga = TimeSpan.FromMinutes(10);
    internal static readonly TimeSpan DuracionCorta = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public override Task<IReadOnlyList<CompaniaErp>> ListarCompaniasAsync(CancellationToken ct) =>
        EnCacheAsync("erp:companias", DuracionLarga, async () =>
        {
            var respuesta = await LeerAsync<RespuestaErp<CompaniaJson>>(Uri7048("api/Company/list_company"), "compañías", ct);
            return respuesta!.Result.OrEmpty()
                .Select(c => new CompaniaErp(c.CompanyId, ReglasErp.NombreCompania(c.Name, c.TradeName), c.Name?.Trim(), c.Ruc?.Trim()))
                .ToList();
        });

    public override Task<IReadOnlyList<ProyectoErp>> ListarProyectosAsync(int companiaId, CancellationToken ct) =>
        EnCacheAsync($"erp:proyectos:{companiaId}", DuracionCorta, async () =>
        {
            var proyectos = await LeerAsync<List<ProyectoJson>>(Uri7048($"api/Project/GetProject/{companiaId}"), "proyectos", ct);
            return proyectos.OrEmpty()
                .Where(p => !string.IsNullOrWhiteSpace(p.ProjectId) && ReglasErp.EsProyectoActivo(p.Status))
                .Select(p => new ProyectoErp(p.ProjectId!.Trim(), p.Description?.Trim() ?? string.Empty, p.Status!.Trim()))
                .ToList();
        });

    public override Task<IReadOnlyList<DimensionErp>> ListarDimensionesAsync(int companiaId, CancellationToken ct) =>
        EnCacheAsync($"erp:dimensiones:{companiaId}", DuracionCorta, async () =>
        {
            var dimensiones = await LeerAsync<List<DimensionJson>>(Uri7048($"api/Uegp/GetUegpCompany/{companiaId}"), "dimensiones", ct);
            return dimensiones.OrEmpty()
                .Where(d => !string.IsNullOrWhiteSpace(d.UegpId))
                .Select(d => new DimensionErp(d.UegpId!.Trim(), d.Description?.Trim() ?? string.Empty))
                .ToList();
        });

    public override Task<IReadOnlyList<ActividadErp>> ListarActividadesAsync(int companiaId, string proyectoErpId, CancellationToken ct) =>
        EnCacheAsync($"erp:actividades:{companiaId}:{proyectoErpId.Trim().ToUpperInvariant()}", DuracionCorta, async () =>
        {
            // Orden de la ruta del ERP: {projectId}/{companyId}. 404 → sin actividades (como el flujo original).
            var uri = Uri7055($"api/Activity/GetActivitiesProject/{Uri.EscapeDataString(proyectoErpId.Trim())}/{companiaId}");
            var respuesta = await LeerAsync<RespuestaErp<ActividadJson>>(uri, "actividades", ct, noEncontradoEsVacio: true);
            return respuesta?.Result.OrEmpty()
                .Where(a => !string.IsNullOrWhiteSpace(a.ActivityId))
                .Select(a => new ActividadErp(a.ActivityId!.Trim(), a.Description?.Trim() ?? string.Empty, a.ActivityType?.Trim()))
                .ToList() ?? [];
        });

    public override Task<IReadOnlyList<HorarioErp>> ListarHorariosAsync(CancellationToken ct) =>
        EnCacheAsync("erp:horarios", DuracionLarga, async () =>
        {
            var respuesta = await LeerAsync<RespuestaErp<HorarioJson>>(Uri7055("api/PayrollSchedule/ListPayrollSchedule"), "horarios", ct);
            return respuesta!.Result.OrEmpty()
                .Where(h => ReglasErp.EsHorarioActivo(h.Status))
                .Select(h => new HorarioErp(h.CodHorario, h.Descripcion?.Trim() ?? string.Empty,
                    ReglasErp.AHora(h.HoraEntrada), ReglasErp.AHora(h.HoraSalida),
                    ReglasErp.AMinutos(h.Horas), ReglasErp.AMinutos(h.HorasTrab), h.TipoHorario?.Trim()))
                .ToList();
        });

    // ------------------------------------------------------------------ infraestructura HTTP

    private async Task<IReadOnlyList<T>> EnCacheAsync<T>(string clave, TimeSpan duracion, Func<Task<List<T>>> obtener)
    {
        if (cache.TryGetValue(clave, out IReadOnlyList<T>? enCache) && enCache is not null)
        {
            return enCache;
        }

        IReadOnlyList<T> valor = await obtener();
        cache.Set(clave, valor, duracion);
        return valor;
    }

    /// <summary>GET + JSON. Red, timeout, 5xx, otro código inesperado o JSON inválido → ErpNoDisponibleException.</summary>
    private async Task<T?> LeerAsync<T>(Uri uri, string operacion, CancellationToken ct, bool noEncontradoEsVacio = false)
        where T : class
    {
        HttpResponseMessage respuesta;
        var cronometro = Stopwatch.StartNew();
        try
        {
            respuesta = await http.GetAsync(uri, ct);
        }
        catch (HttpRequestException ex)
        {
            throw Registrar(new ErpNoDisponibleException(operacion, "error de red o de conexión segura", ex));
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw Registrar(new ErpNoDisponibleException(operacion, "tiempo de espera agotado", ex));
        }

        // TAREA-26a: una línea por llamada real al ERP (las respuestas en caché no llegan aquí). Nunca el cuerpo ni la URL.
        logger.LogDebug("ERP {Operacion}: HTTP {Codigo} en {Milisegundos} ms", operacion, (int)respuesta.StatusCode,
            cronometro.ElapsedMilliseconds);

        using (respuesta)
        {
            if (noEncontradoEsVacio && respuesta.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!respuesta.IsSuccessStatusCode)
            {
                throw Registrar(new ErpNoDisponibleException(operacion, $"respuesta HTTP {(int)respuesta.StatusCode}"));
            }

            try
            {
                return await respuesta.Content.ReadFromJsonAsync<T>(Json, ct)
                       ?? throw new JsonException("Respuesta vacía.");
            }
            catch (JsonException ex)
            {
                throw Registrar(new ErpNoDisponibleException(operacion, "respuesta con formato inválido", ex));
            }
        }
    }

    private ErpNoDisponibleException Registrar(ErpNoDisponibleException ex)
    {
        logger.LogWarning(ex.InnerException, "{Mensaje}", ex.Message);
        return ex;
    }

    private Uri Uri7048(string ruta) => Combinar(opciones.ErpBase7048, ruta);
    private Uri Uri7055(string ruta) => Combinar(opciones.ErpBase7055, ruta);

    private static Uri Combinar(string baseUrl, string ruta) => new(new Uri(baseUrl.TrimEnd('/') + "/"), ruta);

    // ------------------------------------------------------------------ contratos JSON del ERP

    /// <summary>Envoltura de list_company, GetActivitiesProject y ListPayrollSchedule.</summary>
    internal sealed record RespuestaErp<T>(int StatusCode, bool IsSuccess, List<T>? Result);

    internal sealed record CompaniaJson(int CompanyId, string? Name, string? TradeName, string? Ruc);

    internal sealed record ProyectoJson(string? ProjectId, int CompanyId, string? Description, string? Status);

    internal sealed record DimensionJson(int CompanyId, string? UegpId, string? Description);

    internal sealed record ActividadJson(string? ProjectId, string? ActivityId, string? Description, string? ActivityType);

    internal sealed record HorarioJson(
        int CodHorario, string? Descripcion, string? HoraEntrada, string? HoraSalida,
        string? Horas, string? HorasTrab, string? TipoHorario, string? Status);
}

internal static class ListaExtensions
{
    public static IEnumerable<T> OrEmpty<T>(this List<T>? lista) => lista ?? [];
}
