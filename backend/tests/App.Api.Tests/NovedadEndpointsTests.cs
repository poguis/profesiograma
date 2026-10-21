using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using App.Api.Configuracion;
using App.Api.Endpoints;
using App.Api.Seguridad;
using App.Application.Comun;
using App.Application.Empleados;
using App.Application.Novedades;
using App.Application.Proyectos.Crear;
using App.Application.Seguridad;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace App.Api.Tests;

/// <summary>
/// TAREA-23-1: /api/novedades con la autorización REAL (DevAuth + políticas), los servicios REALES y puertos falsos, en
/// un pipeline EN MEMORIA: sin Kestrel, sin red y sin base. Datos ficticios.
/// </summary>
public sealed class NovedadEndpointsTests
{
    private const string General = """{ "tipoAplicacion": "GENERAL", "tipoNovedad": "FERIADO", "fechaInicio": "2026-12-25", "fechaFin": "2026-12-25" }""";

    [Fact]
    public async Task General_ComoGestor_403_ComoAdmin_201ConLocation()
    {
        var gestor = await EnviarAsync("gestor", HttpMethods.Post, "/api/novedades", General);
        var admin = await EnviarAsync("admin", HttpMethods.Post, "/api/novedades", General);

        Assert.Equal(StatusCodes.Status403Forbidden, gestor.Estado);
        Assert.Equal(ResultadoCrearNovedad.MensajeProhibido, gestor.Json!.RootElement.GetProperty("title").GetString());
        Assert.Equal(StatusCodes.Status201Created, admin.Estado);
        Assert.Equal("/api/novedades/77", admin.Contexto.Response.Headers.Location.ToString());
        Assert.Equal("NOV-", admin.Json!.RootElement.GetProperty("codigo").GetString()![..4]);
    }

    [Fact]
    public async Task Campos_400EnSuCampo()
    {
        var r = await EnviarAsync("gestor", HttpMethods.Post, "/api/novedades",
            """{ "tipoAplicacion": "PERSONA", "tipoNovedad": "FERIADO", "codigoEkon": "DEV001" }""");

        Assert.Equal(StatusCodes.Status400BadRequest, r.Estado);
        var errores = r.Json!.RootElement.GetProperty("errors");
        Assert.Equal("El tipo de novedad no aplica a PERSONA.", errores.GetProperty("tipoNovedad")[0].GetString());
        Assert.True(errores.TryGetProperty("fechaInicio", out _));
        Assert.True(errores.TryGetProperty("fechaFin", out _));
    }

    [Fact]
    public async Task CuerpoIlegible_400EnElCampo()
    {
        var r = await EnviarAsync("gestor", HttpMethods.Post, "/api/novedades",
            """{ "tipoAplicacion": "GENERAL", "tipoNovedad": "FERIADO", "fechaInicio": "25-12-2026", "fechaFin": "2026-12-25" }""");

        Assert.Equal(StatusCodes.Status400BadRequest, r.Estado);
        Assert.Equal(SolicitudIlegibleExceptionHandler.Titulo, r.Json!.RootElement.GetProperty("title").GetString());
        Assert.True(r.Json.RootElement.GetProperty("errors").TryGetProperty("fechaInicio", out _));
    }

    [Fact]
    public async Task Detalle_NoVisible_404_Listado_400_SinSesion_401()
    {
        Assert.Equal(StatusCodes.Status404NotFound, (await EnviarAsync("gestor", HttpMethods.Get, "/api/novedades/5")).Estado);
        Assert.Equal(StatusCodes.Status400BadRequest, (await EnviarAsync("gestor", HttpMethods.Get, "/api/novedades?aplicacion=TODAS")).Estado);
        Assert.Equal(StatusCodes.Status401Unauthorized, (await EnviarAsync("anonimo", HttpMethods.Get, "/api/novedades")).Estado);
    }

    [Fact]
    public async Task Opciones_GestorSinGeneral()
    {
        var r = await EnviarAsync("gestor", HttpMethods.Get, "/api/novedades/opciones-formulario");

        Assert.Equal(StatusCodes.Status200OK, r.Estado);
        Assert.False(r.Json!.RootElement.GetProperty("puedeRegistrarGeneral").GetBoolean());
        Assert.Equal(0, r.Json.RootElement.GetProperty("tiposPorAplicacion").GetProperty("GENERAL").GetArrayLength());
    }

    // ------------------------------------------------------------------ pipeline en memoria

    private sealed record Respuesta(int Estado, JsonDocument? Json, HttpContext Contexto);

    private static async Task<Respuesta> EnviarAsync(string usuario, string metodo, string ruta, string? cuerpo = null)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Autenticacion:Modo"] = "DevAuth",
            ["DevAuth:UsuarioPorDefecto"] = "anonimo",
            ["DevAuth:Usuarios:0:Clave"] = "admin",
            ["DevAuth:Usuarios:0:Email"] = "admin.dev@profesiograma.local",
            ["DevAuth:Usuarios:0:Roles:0"] = "Admin",
            ["DevAuth:Usuarios:1:Clave"] = "gestor",
            ["DevAuth:Usuarios:1:Email"] = "gestor.dev@profesiograma.local",
            ["DevAuth:Usuarios:1:Roles:0"] = "Gestor",
        });
        builder.AddSeguridadProfesiograma();
        builder.Services.AddErroresApi();
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<IUsuarioActual, UsuarioDesdeClaims>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<INovedadConsultas, ConsultasFalsas>();
        builder.Services.AddSingleton<INovedadRepositorio, RepositorioFalso>();
        builder.Services.AddSingleton<ITransaccionAsignaciones, TransaccionFalsa>();
        // PERSONA con la API no se ejercita aquí (lo cubre App.Application.Tests): si se llamara, la prueba falla.
        builder.Services.AddSingleton(NoUsar<IDatosReferenciaProyecto>());
        builder.Services.AddSingleton(NoUsar<IFuenteEmpleadosErp>());
        builder.Services.AddScoped<NovedadConsultaServicio>();
        builder.Services.AddScoped<NovedadValidador>();
        builder.Services.AddScoped<NovedadEscrituraServicio>();
        builder.Services.AddScoped<OpcionesFormularioNovedad>();

        await using var app = builder.Build();
        app.UseExceptionHandler();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapNovedadEndpoints();
        app.UseEndpoints(_ => { });
        var pipeline = ((IApplicationBuilder)app).Build();

        var partes = ruta.Split('?', 2);
        var contexto = new DefaultHttpContext { RequestServices = app.Services.CreateScope().ServiceProvider };
        contexto.Request.Method = metodo;
        contexto.Request.Path = partes[0];
        contexto.Request.QueryString = partes.Length > 1 ? new QueryString("?" + partes[1]) : QueryString.Empty;
        contexto.Request.Headers["X-Dev-User"] = usuario;
        if (cuerpo is not null)
        {
            var bytes = Encoding.UTF8.GetBytes(cuerpo);
            contexto.Request.ContentType = "application/json";
            contexto.Request.ContentLength = bytes.Length;
            contexto.Request.Body = new MemoryStream(bytes);
            contexto.Features.Set<IHttpRequestBodyDetectionFeature>(new CuerpoDetectable()); // como Kestrel: hay cuerpo
        }

        contexto.RequestServices.GetRequiredService<IHttpContextAccessor>().HttpContext = contexto;
        var salida = new MemoryStream();
        contexto.Response.Body = salida;

        await pipeline(contexto);

        return new Respuesta(contexto.Response.StatusCode, salida.Length > 0 ? JsonDocument.Parse(salida.ToArray()) : null, contexto);
    }

    private static T NoUsar<T>() where T : class => DispatchProxy.Create<T, Lanzador>();

    private sealed class CuerpoDetectable : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }

    /// <summary>Proxy que falla si se llama: marca dependencias que estas pruebas no deben usar.</summary>
    public class Lanzador : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            throw new InvalidOperationException($"No se usa en esta prueba: {targetMethod?.Name}.");
    }

    private sealed class UsuarioDesdeClaims(IHttpContextAccessor http) : IUsuarioActual
    {
        private ClaimsPrincipal? Principal => http.HttpContext?.User;
        public int? UsuarioId => Email switch { "admin.dev@profesiograma.local" => 1, "gestor.dev@profesiograma.local" => 2, _ => null };
        public string? Email => Principal?.FindFirst(ClaimNames.PreferredUsername)?.Value;
        public string? NombreMostrar => null;
        public IReadOnlyCollection<string> Roles => Principal?.FindAll(ClaimNames.Roles).Select(c => c.Value).ToList() ?? [];
        public bool EstaAutenticado => UsuarioId.HasValue;
        public bool TieneRol(string rol) => Roles.Contains(rol, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class ConsultasFalsas : INovedadConsultas
    {
        public Task<PaginaResultado<NovedadResumenDto>> ListarAsync(NovedadFiltro filtro, CancellationToken ct) =>
            Task.FromResult(new PaginaResultado<NovedadResumenDto>([], filtro.Pagina, filtro.Tamano, 0));

        public Task<NovedadDetalleDatos?> ObtenerDetalleAsync(int id, int? visibleParaUsuarioId, CancellationToken ct) =>
            Task.FromResult<NovedadDetalleDatos?>(null);

        public Task<IReadOnlyList<TipoNovedadRef>> ObtenerTiposAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<TipoNovedadRef>>(
            [
                new(3, "FERIADO", "FERIADO", "F", "#D13438", false, true, true, true),
                new(6, "VACACIONES", "VACACIONES", "V", "#0277BD", true, false, false, true),
            ]);

        public Task<bool> ProyectoElegibleAsync(int proyectoId, int? propietarioUsuarioId, CancellationToken ct) => Task.FromResult(false);
    }

    private sealed class RepositorioFalso : INovedadRepositorio
    {
        public Task<DuplicadoNovedad?> BuscarDuplicadoAsync(CriterioDuplicado criterio, int? visibleParaUsuarioId, CancellationToken ct) =>
            Task.FromResult<DuplicadoNovedad?>(null);

        public Task<bool> ExisteCodigoOUidAsync(string codigo, Guid uid, CancellationToken ct) => Task.FromResult(false);

        public Task<NovedadCreadaDto> AgregarAsync(NuevaNovedad nueva, CancellationToken ct) =>
            Task.FromResult(new NovedadCreadaDto(77, nueva.Codigo, "AAAAAAAAB9E="));
    }

    private sealed class TransaccionFalsa : ITransaccionAsignaciones
    {
        public Task<T> EjecutarAsync<T>(Func<CancellationToken, Task<T>> accion, Func<T, bool> confirmar, CancellationToken ct) => accion(ct);
    }
}
