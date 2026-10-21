using System.Diagnostics;
using System.Text.Json;
using App.Api.Configuracion;
using App.Application.Comun;
using App.Application.Erp;
using App.Application.Proyectos.Crear;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace App.Api.Tests;

/// <summary>
/// TAREA-22c: base de datos caída → 503 "Base de datos no disponible". Pipeline EN MEMORIA (UseExceptionHandler +
/// AddErroresApi, como Program.cs): sin Kestrel, sin red y sin base.
/// </summary>
public sealed class BaseDatosNoDisponibleTests
{
    private static BaseDatosNoDisponibleException Caida(FaseFalloBaseDatos fase = FaseFalloBaseDatos.AlAbrir)
        => new(fase, 10061, 12, new InvalidOperationException("Server=SERVIDOR_FICTICIO;Database=BD_FICTICIA_22C"));

    [Fact]
    public async Task Get_AlAbrir_503ConTextoDeConsulta()
    {
        // Cache-Control: no-cache,no-store lo pone ExceptionHandlerMiddleware en OnStarting, que DefaultHttpContext no
        // dispara: se comprueba en la verificación manual (bd-caida.cmd).
        var r = await EnviarAsync(HttpMethods.Get, Caida());

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, r.Estado);
        Assert.Equal(("Base de datos no disponible", "No se pudo consultar la base de datos. Intente nuevamente en un minuto."), (r.Titulo, r.Detalle));
    }

    [Fact]
    public async Task Get_EnComando_TambienTextoDeConsulta()
    {
        var r = await EnviarAsync(HttpMethods.Get, Caida(FaseFalloBaseDatos.EnComando));

        Assert.Equal((503, BaseDatosNoDisponibleExceptionHandler.DetalleConsulta), (r.Estado, r.Detalle));
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    public async Task Escritura_AlAbrir_AfirmaQueNoSeGuardo(string metodo)
    {
        var r = await EnviarAsync(metodo, Caida());

        Assert.Equal((503, "La base de datos no respondió y no se guardó ningún cambio. Intente nuevamente en un minuto."),
            (r.Estado, r.Detalle));
    }

    [Fact]
    public async Task Escritura_EnComando_EnvueltaEnDbUpdateException_TextoCauteloso()
    {
        var r = await EnviarAsync(HttpMethods.Post, new DbUpdateException("guardar", Caida(FaseFalloBaseDatos.EnComando)));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, r.Estado);
        Assert.Equal(
            "La base de datos dejó de responder durante el registro. Vuelva a cargar el proyecto para comprobar si el cambio se guardó antes de intentarlo de nuevo.",
            r.Detalle);
    }

    [Fact]
    public async Task Escritura_EnConfirmacion_EnAggregateYEnInvalidOperation_TextoCauteloso()
    {
        var envuelta = new AggregateException(new TimeoutException("otra"),
            new InvalidOperationException("x", Caida(FaseFalloBaseDatos.EnConfirmacion)));

        var r = await EnviarAsync(HttpMethods.Post, envuelta);

        Assert.Equal((503, BaseDatosNoDisponibleExceptionHandler.DetalleIncierto), (r.Estado, r.Detalle));
    }

    [Fact]
    public async Task ExcepcionNoClasificada_Sigue500()
    {
        var r = await EnviarAsync(HttpMethods.Get, new InvalidOperationException("otra cosa", new TimeoutException()));

        Assert.Equal(StatusCodes.Status500InternalServerError, r.Estado);
    }

    [Fact]
    public async Task RegistroOcupado_Y_Erp_SinCambios()
    {
        var ocupado = await EnviarAsync(HttpMethods.Post, new RegistroOcupadoException("applock"));
        var erp = await EnviarAsync(HttpMethods.Get, new ErpNoDisponibleException("empleados", "caído"));

        Assert.Equal((503, "Registro ocupado", "Hay otro registro de asignaciones en curso. Intente nuevamente en unos segundos."),
            (ocupado.Estado, ocupado.Titulo, ocupado.Detalle));
        Assert.Equal((503, "Servicio de empleados no disponible"), (erp.Estado, erp.Titulo));
    }

    [Fact]
    public async Task Log_UnAvisoCada30s_ConNumeroYRuta_SinElMensajeDelProveedor()
    {
        var log = new LogEnMemoria();
        await using var sp = Servicios(log);

        await EnviarAsync(HttpMethods.Get, Caida(), sp);
        await EnviarAsync(HttpMethods.Get, Caida(), sp);
        await EnviarAsync(HttpMethods.Get, Caida(), sp);

        var avisos = log.Mensajes.Where(m => m.Contains("Base de datos no disponible", StringComparison.Ordinal)).ToList();
        var aviso = Assert.Single(avisos);
        Assert.Contains("10061", aviso);
        Assert.Contains("/api/prueba", aviso);
        Assert.DoesNotContain(log.Mensajes, m => m.Contains("FICTICI", StringComparison.Ordinal));
    }

    private sealed record Respuesta(int Estado, string? Titulo, string? Detalle);

    private static ServiceProvider Servicios(LogEnMemoria? log = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => { if (log is not null) b.AddProvider(log); });
        services.AddMetrics();
        services.AddSingleton(new DiagnosticListener("App.Api.Tests"));
        services.AddErroresApi();
        return services.BuildServiceProvider();
    }

    private static async Task<Respuesta> EnviarAsync(string metodo, Exception excepcion, ServiceProvider? compartido = null)
    {
        var sp = compartido ?? Servicios();
        try
        {
            var app = new ApplicationBuilder(sp);
            app.UseExceptionHandler();
            app.Run(_ => throw excepcion);
            var pipeline = app.Build();

            var contexto = new DefaultHttpContext { RequestServices = sp };
            contexto.Request.Method = metodo;
            contexto.Request.Path = "/api/prueba";
            var salida = new MemoryStream();
            contexto.Response.Body = salida;

            await pipeline(contexto);

            string? titulo = null, detalle = null;
            if (salida.Length > 0)
            {
                using var doc = JsonDocument.Parse(salida.ToArray());
                titulo = doc.RootElement.TryGetProperty("title", out var t) ? t.GetString() : null;
                detalle = doc.RootElement.TryGetProperty("detail", out var d) ? d.GetString() : null;
            }

            return new Respuesta(contexto.Response.StatusCode, titulo, detalle);
        }
        finally
        {
            if (compartido is null)
            {
                await sp.DisposeAsync();
            }
        }
    }

    private sealed class LogEnMemoria : ILoggerProvider
    {
        public List<string> Mensajes { get; } = [];

        public ILogger CreateLogger(string categoryName) => new Registrador(this);

        public void Dispose() { }

        private sealed class Registrador(LogEnMemoria destino) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                lock (destino.Mensajes)
                {
                    destino.Mensajes.Add($"{logLevel}: {formatter(state, exception)} {exception}");
                }
            }
        }
    }
}
