using System.Net;
using System.Text;
using App.Application.Erp;
using App.Infrastructure.Erp;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Tests.Erp;

/// <summary>
/// TAREA-26a: log Debug por llamada real al ERP (operación, código HTTP y milisegundos) y log del modo al arrancar.
/// Datos FICTICIOS. El log nunca lleva el cuerpo ni la URL.
/// </summary>
public class CatalogoErpHttpLogTests
{
    private const string Base7048 = "https://erp.prueba.local:7048";
    private const string Base7055 = "https://erp.prueba.local:7055";
    private static readonly CancellationToken Ct = CancellationToken.None;

    private const string JsonCompanias = """
        {"statusCode":200,"isSuccess":true,"errorMessages":null,"result":[
          {"companyId":9001,"name":"PRUEBA","tradeName":"COMPAÑÍA DE PRUEBA S.A.","ruc":"0999999999001"}]}
        """;

    [Fact]
    public async Task LlamadaReal_UnDebugConOperacionCodigoYMilisegundos_SinCuerpoNiUrl_YLaCacheNoRegistra()
    {
        var logger = new LoggerFalso();
        var catalogo = Crear(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonCompanias, Encoding.UTF8, "application/json") }, logger);

        await catalogo.ListarCompaniasAsync(Ct);
        await catalogo.ListarCompaniasAsync(Ct); // en caché: no llama al ERP ni registra

        var entrada = Assert.Single(logger.Entradas);
        Assert.Equal(LogLevel.Debug, entrada.Nivel);
        Assert.Matches(@"^ERP compañías: HTTP 200 en \d+ ms$", entrada.Mensaje);
        Assert.DoesNotContain("erp.prueba.local", entrada.Mensaje);
        Assert.DoesNotContain("PRUEBA", entrada.Mensaje);
    }

    [Fact]
    public async Task RespuestaDeError_TambienRegistraElCodigo()
    {
        var logger = new LoggerFalso();
        var catalogo = Crear(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError), logger);

        await Assert.ThrowsAsync<ErpNoDisponibleException>(() => catalogo.ListarCompaniasAsync(Ct));

        Assert.Contains(logger.Entradas, e => e.Nivel == LogLevel.Debug && e.Mensaje.StartsWith("ERP compañías: HTTP 500 en ", StringComparison.Ordinal));
        Assert.Contains(logger.Entradas, e => e.Nivel == LogLevel.Warning); // el aviso de siempre (no disponible)
    }

    [Theory]
    [InlineData("Http", "Http")]
    [InlineData("http", "Http")]
    [InlineData("Simulado", "Simulado")]
    [InlineData("SIMULADO", "Simulado")]
    public void ModoEfectivo_Normalizado(string configurado, string esperado) =>
        Assert.Equal(esperado, ErpServiceCollectionExtensions.ModoEfectivo(new ServiciosExternosOpciones { Modo = configurado }));

    // ------------------------------------------------------------------ utilidades

    private static CatalogoErpHttp Crear(Func<HttpRequestMessage, HttpResponseMessage> responder, ILogger<CatalogoErpHttp> logger)
    {
        var opciones = new ServiciosExternosOpciones { Modo = "Http", ErpBase7048 = Base7048, ErpBase7055 = Base7055 };
        return new CatalogoErpHttp(new HttpClient(new ManejadorFalso(responder)), new MemoryCache(new MemoryCacheOptions()), opciones, logger);
    }

    private sealed record Entrada(LogLevel Nivel, string Mensaje);

    private sealed class LoggerFalso : ILogger<CatalogoErpHttp>
    {
        public List<Entrada> Entradas { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entradas.Add(new Entrada(logLevel, formatter(state, exception)));
    }

    private sealed class ManejadorFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
