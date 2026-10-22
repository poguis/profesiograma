using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using App.Api.Configuracion;
using App.Api.Endpoints;
using App.Api.Seguridad;
using App.Application.Empleados;
using App.Application.Novedades;
using App.Application.Novedades.Sincronizacion;
using App.Application.Proyectos.Crear;
using App.Application.Seguridad;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace App.Api.Tests;

/// <summary>
/// TAREA-23-3: POST /api/admin/novedades/sincronizar-permisos-medicos con la autorización REAL (DevAuth + Politica.Admin)
/// y el servicio REAL con puertos falsos, en un pipeline EN MEMORIA (sin Kestrel, red ni base).
/// </summary>
public sealed class SincronizacionEndpointsTests
{
    private const string Ruta = "/api/admin/novedades/sincronizar-permisos-medicos";

    [Fact]
    public async Task Admin_200_ConConteos_Gestor_403_SinSesion_401()
    {
        var admin = await EnviarAsync("admin", Ruta, new FuenteVacia(true));
        var gestor = await EnviarAsync("gestor", Ruta, new FuenteVacia(true));
        var anonimo = await EnviarAsync("anonimo", Ruta, new FuenteVacia(true));

        Assert.Equal(StatusCodes.Status200OK, admin.Estado);
        var raiz = admin.Json!.RootElement;
        Assert.Equal(("Falsa", 0, 0), (raiz.GetProperty("fuente").GetString(), raiz.GetProperty("leidas").GetInt32(), raiz.GetProperty("nuevas").GetInt32()));
        Assert.True(raiz.TryGetProperty("ausentesEnOrigen", out _));
        Assert.Equal(StatusCodes.Status403Forbidden, gestor.Estado);
        Assert.Equal(StatusCodes.Status401Unauthorized, anonimo.Estado);
    }

    [Fact]
    public async Task FuenteNoConfigurada_503()
    {
        var r = await EnviarAsync("admin", Ruta, new FuenteVacia(false) { Error = FuenteNovedadesNoDisponibleException.NoConfigurada() });

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, r.Estado);
        Assert.Equal("Fuente de novedades externas no configurada", r.Json!.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Escenario_ConFuenteQueNoLoAdmite_400()
    {
        var r = await EnviarAsync("admin", Ruta + "?escenario=base", new FuenteVacia(false));

        Assert.Equal(StatusCodes.Status400BadRequest, r.Estado);
        Assert.Equal(SincronizacionPermisosMedicosServicio.MensajeEscenarioFuente,
            r.Json!.RootElement.GetProperty("errors").GetProperty("escenario")[0].GetString());
    }

    // ------------------------------------------------------------------ pipeline en memoria

    private sealed record Respuesta(int Estado, JsonDocument? Json);

    private static async Task<Respuesta> EnviarAsync(string usuario, string ruta, IFuenteNovedadesExternas fuente)
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
        builder.Services.AddSingleton(fuente);
        builder.Services.AddSingleton<ISincronizacionNovedades>(NoUsar<ISincronizacionNovedades>(vacia: true));
        builder.Services.AddSingleton(NoUsar<IDatosReferenciaProyecto>());
        builder.Services.AddSingleton(NoUsar<IFuenteEmpleadosErp>());
        builder.Services.AddSingleton(NoUsar<ITransaccionAsignaciones>());
        builder.Services.AddScoped<SincronizacionPermisosMedicosServicio>();
        // Solo para enlazar las demás rutas de novedades (no se llaman).
        builder.Services.AddScoped<NovedadConsultaServicio>(_ => throw new InvalidOperationException("No se usa en esta prueba."));
        builder.Services.AddScoped<OpcionesFormularioNovedad>(_ => throw new InvalidOperationException("No se usa en esta prueba."));
        builder.Services.AddScoped<NovedadEscrituraServicio>(_ => throw new InvalidOperationException("No se usa en esta prueba."));
        builder.Services.AddScoped<NovedadModificacionServicio>(_ => throw new InvalidOperationException("No se usa en esta prueba."));

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
        contexto.Request.Method = HttpMethods.Post;
        contexto.Request.Path = partes[0];
        contexto.Request.QueryString = partes.Length > 1 ? new QueryString("?" + partes[1]) : QueryString.Empty;
        contexto.Request.Headers["X-Dev-User"] = usuario;
        contexto.RequestServices.GetRequiredService<IHttpContextAccessor>().HttpContext = contexto;
        var salida = new MemoryStream();
        contexto.Response.Body = salida;

        await pipeline(contexto);

        return new Respuesta(contexto.Response.StatusCode, salida.Length > 0 ? JsonDocument.Parse(salida.ToArray()) : null);
    }

    /// <summary>Fuente sin filas (o que lanza <see cref="Error"/>).</summary>
    private sealed class FuenteVacia(bool escenarios) : IFuenteNovedadesExternas
    {
        public Exception? Error { get; init; }
        public string Fuente => "Falsa";
        public bool AdmiteEscenarios => escenarios;

        public Task<LecturaNovedadesExternas> LeerPermisosMedicosAsync(string? escenario, CancellationToken ct) =>
            Error is null ? Task.FromResult(new LecturaNovedadesExternas(Fuente, [], 0)) : Task.FromException<LecturaNovedadesExternas>(Error);
    }

    /// <summary>
    /// Proxy que falla si se llama; con <paramref name="vacia"/>, las lecturas de ISincronizacionNovedades devuelven
    /// colecciones vacías (sin filas no hay nada que escribir).
    /// </summary>
    private static T NoUsar<T>(bool vacia = false) where T : class
    {
        var proxy = DispatchProxy.Create<T, Lanzador>();
        ((Lanzador)(object)proxy).Vacia = vacia;
        return proxy;
    }

    public class Lanzador : DispatchProxy
    {
        public bool Vacia { get; set; }

        protected override object? Invoke(MethodInfo? metodo, object?[]? args)
        {
            if (Vacia && metodo?.Name.StartsWith("Obtener", StringComparison.Ordinal) == true)
            {
                var tipo = metodo.ReturnType.GetGenericArguments()[0]; // Task<T>
                object vacio = tipo.IsGenericType && tipo.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>)
                    ? Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(tipo.GetGenericArguments()))!
                    : Activator.CreateInstance(typeof(List<>).MakeGenericType(tipo.GetGenericArguments()))!;
                return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(tipo).Invoke(null, [vacio]);
            }

            throw new InvalidOperationException($"No se usa en esta prueba: {metodo?.Name}.");
        }
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
}
