using App.Api.Configuracion;
using App.Api.Endpoints;
using App.Api.Seguridad;
using App.Application.Diagnostics;
using App.Application.Seguridad;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace App.Api.Tests;

/// <summary>
/// TAREA-7d-1: la SPA desde wwwroot (D2, J2, J3) y la salud anónima (D6), con el orden de Program.cs, en el entorno QA y
/// modo EntraId (IDs FICTICIOS), en un pipeline EN MEMORIA: sin Kestrel, sin red y sin base (dobles).
/// </summary>
public sealed class SpaSaludTests : IDisposable
{
    private const string IndexHtml = "<!doctype html><html><body><div id=\"root\"></div></body></html>";
    private const string Asset = "/assets/index-AbC123.js";
    private const string RedirectHtml = "<!doctype html><html><body><script type=\"module\" src=\"/assets/redirect-XyZ.js\"></script></body></html>";
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), "spa-prueba-" + Guid.NewGuid().ToString("N"));

    public SpaSaludTests()
    {
        Directory.CreateDirectory(Path.Combine(_raiz, "wwwroot", "assets"));
        File.WriteAllText(Path.Combine(_raiz, "wwwroot", "index.html"), IndexHtml);
        File.WriteAllText(Path.Combine(_raiz, "wwwroot", "assets", "index-AbC123.js"), "console.log(1)");
        File.WriteAllText(Path.Combine(_raiz, "wwwroot", "redirect.html"), RedirectHtml);
    }

    public void Dispose() => Directory.Delete(_raiz, recursive: true);

    [Theory]
    [InlineData("/")]
    [InlineData("/proyectos/14/cronograma")] // ruta de React Router
    public async Task RaizYRutaProfunda_IndexHtml_NoCache(string ruta)
    {
        var r = await EnviarAsync(ruta);

        Assert.Equal(StatusCodes.Status200OK, r.Estado);
        Assert.StartsWith("text/html", r.TipoContenido);
        Assert.Equal(SpaExtensions.CacheSinCache, r.Cache);
        Assert.Equal(IndexHtml, r.Cuerpo);
    }

    [Fact]
    public async Task AssetConHash_200_CacheLargaInmutable()
    {
        var r = await EnviarAsync(Asset);

        Assert.Equal(StatusCodes.Status200OK, r.Estado);
        Assert.Contains("javascript", r.TipoContenido);
        Assert.Equal(SpaExtensions.CacheAssets, r.Cache);
    }

    [Theory]
    [InlineData("/api/no-existe")]
    [InlineData("/api/proyectos/x/y/z")]
    public async Task ApiDesconocida_SinToken_404SinHtml(string ruta)
    {
        var r = await EnviarAsync(ruta);

        Assert.Equal(StatusCodes.Status404NotFound, r.Estado);
        Assert.DoesNotContain("<html", r.Cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ApiProtegida_SinToken_401_YConXDevUser_401()
    {
        Assert.Equal(StatusCodes.Status401Unauthorized, (await EnviarAsync("/api/usuarios/me")).Estado);
        Assert.Equal(StatusCodes.Status401Unauthorized, (await EnviarAsync("/api/usuarios/me", devUser: "admin")).Estado);
    }

    [Fact]
    public async Task Vivo_200_SinTocarLaBase()
    {
        var r = await EnviarAsync("/api/salud/vivo");

        Assert.Equal(StatusCodes.Status200OK, r.Estado);
        Assert.Equal("{\"estado\":\"vivo\"}", r.Cuerpo);
        Assert.Equal(0, r.Diagnostico.Llamadas);
    }

    [Theory]
    [InlineData(true, StatusCodes.Status200OK, "{\"estado\":\"listo\"}")]
    [InlineData(false, StatusCodes.Status503ServiceUnavailable, "")]
    public async Task Listo_SegunLaBase_SinDetalle(bool disponible, int estado, string cuerpo)
    {
        var r = await EnviarAsync("/api/salud/listo", baseDisponible: disponible);

        Assert.Equal((estado, cuerpo, 1), (r.Estado, r.Cuerpo, r.Diagnostico.Llamadas));
    }

    // ------------------------------------------------------------------ TAREA-7d-3: encabezados de seguridad (K1) y /assets/

    [Theory]
    [InlineData("/", StatusCodes.Status200OK)]
    [InlineData(Asset, StatusCodes.Status200OK)]
    [InlineData("/api/salud/vivo", StatusCodes.Status200OK)]
    [InlineData("/api/usuarios/me", StatusCodes.Status401Unauthorized)]
    [InlineData("/api/no-existe", StatusCodes.Status404NotFound)]
    [InlineData("/api/prueba/error", StatusCodes.Status500InternalServerError)]
    public async Task EncabezadosDeSeguridad_EnTodasLasRespuestas_SinHstsPorDefecto(string ruta, int estado)
    {
        var r = await EnviarAsync(ruta, esquema: "https");

        Assert.Equal(estado, r.Estado);
        foreach (var (nombre, valor) in EncabezadosSeguridadExtensions.Encabezados)
        {
            Assert.Equal(valor, r.Cabeceras[nombre].ToString());
        }
        Assert.Equal("SAMEORIGIN", r.Cabeceras.XFrameOptions.ToString()); // nunca DENY: ssoSilent usa un iframe del mismo origen
        Assert.False(r.Cabeceras.ContainsKey("Strict-Transport-Security"));
    }

    [Fact]
    public async Task Hsts_SoloConSeguridadHstsTrue_EnHttps()
    {
        var https = await EnviarAsync("/api/salud/vivo", hsts: true, esquema: "https");
        var http = await EnviarAsync("/api/salud/vivo", hsts: true, esquema: "http");

        Assert.StartsWith("max-age=", https.Cabeceras.StrictTransportSecurity.ToString());
        Assert.False(http.Cabeceras.ContainsKey("Strict-Transport-Security"));
    }

    [Fact]
    public async Task AssetsSinArchivo_IndexHtml_NoUnListado()
    {
        var r = await EnviarAsync("/assets/");

        Assert.Equal((StatusCodes.Status200OK, IndexHtml), (r.Estado, r.Cuerpo));
    }

    [Fact]
    public async Task AssetInexistente_404()
    {
        var r = await EnviarAsync("/assets/no-existe.js");

        Assert.Equal(StatusCodes.Status404NotFound, r.Estado);
        Assert.DoesNotContain("<html", r.Cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>TAREA-7d-4: la página del puente es un archivo (no el fallback), sin caché larga y enmarcable por el mismo origen.</summary>
    [Fact]
    public async Task RedirectHtml_ArchivoEstatico_NoCache_SameOrigin()
    {
        var r = await EnviarAsync("/redirect.html", esquema: "https");

        Assert.Equal((StatusCodes.Status200OK, RedirectHtml), (r.Estado, r.Cuerpo)); // no index.html
        Assert.StartsWith("text/html", r.TipoContenido);
        Assert.Equal(SpaExtensions.CacheSinCache, r.Cache);
        Assert.Equal("SAMEORIGIN", r.Cabeceras.XFrameOptions.ToString());
        Assert.Equal("frame-ancestors 'self'", r.Cabeceras.ContentSecurityPolicy.ToString());
    }

    // ------------------------------------------------------------------ pipeline (orden de Program.cs)

    private sealed record Respuesta(int Estado, string Cuerpo, string TipoContenido, string Cache, DiagnosticoFalso Diagnostico,
        IHeaderDictionary Cabeceras);

    private async Task<Respuesta> EnviarAsync(string ruta, string? devUser = null, bool baseDisponible = true, bool hsts = false,
        string esquema = "http", string host = "sdmapptest2.sedemi.local:7083")
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            EnvironmentName = "QA",
            ContentRootPath = _raiz,
            WebRootPath = Path.Combine(_raiz, "wwwroot"),
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Autenticacion:Modo"] = "EntraId",
            ["AzureAd:Instance"] = "https://login.microsoftonline.com/",
            ["AzureAd:TenantId"] = "aaaaaaaa-0000-0000-0000-000000000001",
            ["AzureAd:ClientId"] = "bbbbbbbb-0000-0000-0000-000000000002",
            ["AzureAd:Audience"] = "",
            [EncabezadosSeguridadExtensions.ClaveHsts] = hsts ? "true" : null, // QA no lo define (E3)
        });
        builder.Services.AddErroresApi();
        builder.AddSeguridadProfesiograma();
        var diagnostico = new DiagnosticoFalso(baseDisponible);
        builder.Services.AddSingleton<IDatabaseDiagnostics>(diagnostico);
        builder.Services.AddSingleton<IUsuarioProvisionamiento, ProvisionamientoNoUsado>();

        await using var app = builder.Build();
        app.UseEncabezadosSeguridad(); // K1: primero, como en Program.cs
        app.UseExceptionHandler();
        app.UseSpaEstaticos();
        app.UseRouting(); // después de los archivos, como en Program.cs (TAREA-7d-3)
        app.UseSeguridadProfesiograma();
        app.MapSaludEndpoints();
        app.MapUsuarioEndpoints();
        // Solo en el host de pruebas: un endpoint anónimo que lanza, para comprobar los encabezados del 500 (K1).
        app.MapGet("/api/prueba/error", IResult () => throw new InvalidOperationException("falla de prueba")).AllowAnonymous();
        app.MapSpa();
        app.UseEndpoints(_ => { });
        var pipeline = ((IApplicationBuilder)app).Build();

        var contexto = new DefaultHttpContext { RequestServices = app.Services.CreateScope().ServiceProvider };
        contexto.Request.Method = "GET";
        contexto.Request.Scheme = esquema;
        contexto.Request.Host = new HostString(host);
        contexto.Request.Path = ruta;
        if (devUser is not null) contexto.Request.Headers["X-Dev-User"] = devUser;
        var respuesta = new RespuestaConInicio();
        contexto.Features.Set<IHttpResponseFeature>(respuesta);
        var salida = new MemoryStream();
        contexto.Response.Body = salida;

        await pipeline(contexto);
        await respuesta.IniciarAsync(); // sin servidor nadie dispara OnStarting: se simula el inicio de la respuesta

        return new Respuesta(contexto.Response.StatusCode, System.Text.Encoding.UTF8.GetString(salida.ToArray()),
            contexto.Response.ContentType ?? "", contexto.Response.Headers.CacheControl.ToString(), diagnostico, contexto.Response.Headers);
    }

    /// <summary>Guarda los OnStarting y los ejecuta como el servidor (en orden inverso al registro).</summary>
    private sealed class RespuestaConInicio : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Accion, object Estado)> _alIniciar = [];

        public override void OnStarting(Func<object, Task> callback, object state) => _alIniciar.Add((callback, state));

        public async Task IniciarAsync()
        {
            for (var i = _alIniciar.Count - 1; i >= 0; i--)
            {
                await _alIniciar[i].Accion(_alIniciar[i].Estado);
            }
        }
    }

    private sealed class DiagnosticoFalso(bool disponible) : IDatabaseDiagnostics
    {
        public int Llamadas { get; private set; }

        public Task<DatabaseInfo> GetInfoAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> EstaDisponibleAsync(CancellationToken cancellationToken = default)
        {
            Llamadas++;
            return Task.FromResult(disponible);
        }
    }

    private sealed class ProvisionamientoNoUsado : IUsuarioProvisionamiento
    {
        public Task<UsuarioResuelto> ResolverAsync(Guid? entraObjectId, string? upn, string correo, string nombreMostrar, CancellationToken ct)
            => throw new InvalidOperationException("Sin token no hay alta.");

        public void Invalidar(string email, Guid? entraObjectId) { }
    }
}
