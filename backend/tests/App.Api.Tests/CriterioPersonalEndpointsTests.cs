using App.Api.Configuracion;
using App.Api.Endpoints;
using App.Api.Seguridad;
using App.Application.Empleados;
using App.Application.Proyectos.Crear;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace App.Api.Tests;

/// <summary>
/// TAREA-62: GET /api/admin/empleados/familias y PUT /api/admin/empleados/familias-asignables con la autorización REAL
/// (DevAuth y las políticas) en un pipeline EN MEMORIA, sin red ni base (dobles). Datos FICTICIOS.
/// </summary>
public sealed class CriterioPersonalEndpointsTests
{
    private const string RutaGet = "/api/admin/empleados/familias";
    private const string RutaPut = "/api/admin/empleados/familias-asignables";

    [Theory]
    [InlineData("GET", RutaGet, null)]
    [InlineData("PUT", RutaPut, """{"familias":["ADMINISTRATIVO"],"valorAnterior":"ADMINISTRATIVO"}""")]
    public async Task Gestor_403_SinLeerNiEscribir(string metodo, string ruta, string? cuerpo)
    {
        var repo = new RepositorioFalso("ADMINISTRATIVO");

        var (estado, _) = await EnviarAsync("gestor", metodo, ruta, cuerpo, repo);

        Assert.Equal(StatusCodes.Status403Forbidden, estado);
        Assert.Equal((0, null), (repo.Lecturas, repo.Actualizado));
    }

    [Fact]
    public async Task Admin_Get_ValorVacio_SinCriterio()
    {
        var (estado, cuerpo) = await EnviarAsync("admin", "GET", RutaGet, null, new RepositorioFalso(""));

        Assert.Equal(StatusCodes.Status200OK, estado);
        Assert.Equal("""{"valorActual":"","sinCriterio":true,"familias":[{"familia":"ADMINISTRATIVO","activos":1,"asignable":false},{"familia":"OPERATIVO","activos":1,"asignable":false}]}""",
            cuerpo);
        Assert.DoesNotContain("PERSONA", cuerpo); // sin datos personales
    }

    [Fact]
    public async Task Admin_Put_Vacio400_EnFamilias()
    {
        var repo = new RepositorioFalso("ADMINISTRATIVO");

        var (estado, cuerpo) = await EnviarAsync("admin", "PUT", RutaPut, """{"familias":[],"valorAnterior":"ADMINISTRATIVO"}""", repo);

        Assert.Equal(StatusCodes.Status400BadRequest, estado);
        Assert.Contains("Seleccione al menos una familia.", cuerpo);
        Assert.Null(repo.Actualizado);
    }

    [Fact]
    public async Task Admin_Put_ValorAnteriorDesfasado409_YOk200()
    {
        var (conflicto, cuerpo) = await EnviarAsync("admin", "PUT", RutaPut, """{"familias":["OPERATIVO"],"valorAnterior":"VIEJO"}""",
            new RepositorioFalso("ADMINISTRATIVO", filas: 0));
        Assert.Equal(StatusCodes.Status409Conflict, conflicto);
        Assert.Contains("Otro usuario cambió el criterio; recargue.", cuerpo);

        var (ok, valor) = await EnviarAsync("admin", "PUT", RutaPut,
            """{"familias":["ADMINISTRATIVO","OPERATIVO"],"valorAnterior":"ADMINISTRATIVO"}""", new RepositorioFalso("ADMINISTRATIVO"));
        Assert.Equal(StatusCodes.Status200OK, ok);
        Assert.Equal("""{"valorActual":"ADMINISTRATIVO, OPERATIVO"}""", valor);
    }

    // ------------------------------------------------------------------ pipeline en memoria

    private static async Task<(int Estado, string Cuerpo)> EnviarAsync(string usuario, string metodo, string ruta, string? cuerpo,
        RepositorioFalso repo)
    {
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Autenticacion:Modo"] = "DevAuth",
            ["DevAuth:Usuarios:0:Clave"] = "admin",
            ["DevAuth:Usuarios:0:Email"] = "admin.dev@profesiograma.local",
            ["DevAuth:Usuarios:0:Roles:0"] = "Admin",
            ["DevAuth:Usuarios:1:Clave"] = "gestor",
            ["DevAuth:Usuarios:1:Email"] = "gestor.dev@profesiograma.local",
            ["DevAuth:Usuarios:1:Roles:0"] = "Gestor",
        });
        builder.AddSeguridadProfesiograma();
        builder.Services.AddErroresApi();
        builder.Services.AddSingleton<IFuenteEmpleadosErp>(new FuenteFalsa());
        builder.Services.AddSingleton<ICriterioPersonalRepositorio>(repo);
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<CriterioPersonalServicio>();
        // Solo para enlazar los demás endpoints de empleados (no se llaman); con fábrica no se construyen.
        builder.Services.AddScoped<EmpleadoConsultaServicio>(_ => throw new InvalidOperationException("No se usa en esta prueba."));
        builder.Services.AddScoped<CompletarCedulasServicio>(_ => throw new InvalidOperationException("No se usa en esta prueba."));

        await using var app = builder.Build();
        app.UseExceptionHandler();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapEmpleadoEndpoints();
        app.UseEndpoints(_ => { });
        var pipeline = ((IApplicationBuilder)app).Build();

        var contexto = new DefaultHttpContext { RequestServices = app.Services.CreateScope().ServiceProvider };
        contexto.Request.Method = metodo;
        contexto.Request.Path = ruta;
        contexto.Request.Headers["X-Dev-User"] = usuario;
        if (cuerpo is not null)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(cuerpo);
            contexto.Request.ContentType = "application/json";
            contexto.Request.ContentLength = bytes.Length;
            contexto.Request.Body = new MemoryStream(bytes);
            contexto.Features.Set<IHttpRequestBodyDetectionFeature>(new CuerpoDetectable()); // como Kestrel: hay cuerpo
        }

        var salida = new MemoryStream();
        contexto.Response.Body = salida;

        await pipeline(contexto);

        return (contexto.Response.StatusCode, System.Text.Encoding.UTF8.GetString(salida.ToArray()));
    }

    private sealed class CuerpoDetectable : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }

    private sealed class FuenteFalsa : IFuenteEmpleadosErp
    {
        private static ListaEmpleadosErp Lista() =>
            new([Persona("900001", "ADMINISTRATIVO"), Persona("900002", "OPERATIVO")], DateTimeOffset.UnixEpoch);

        private static EmpleadoErp Persona(string codigo, string familia) =>
            new(codigo, $"PERSONA {codigo}", null, null, null, null, null, null, "TECNICO", null, null, null, null, null, null, null, null,
                familia, "A");

        public Task<ListaEmpleadosErp> ObtenerActivosAsync(CancellationToken ct) => Task.FromResult(Lista());

        public Task<ListaEmpleadosErp> ObtenerActivosFrescosAsync(CancellationToken ct) => Task.FromResult(Lista());
    }

    private sealed class RepositorioFalso(string? valor, int filas = 1) : ICriterioPersonalRepositorio
    {
        public int Lecturas { get; private set; }
        public string? Actualizado { get; private set; }

        public Task<CriterioGuardado> ObtenerAsync(CancellationToken ct)
        {
            Lecturas++;
            return Task.FromResult(new CriterioGuardado(valor,
                valor?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? []));
        }

        public Task<int> ActualizarAsync(string valorAnterior, string valorNuevo, int usuarioId, DateTime fechaUtc, CancellationToken ct)
        {
            Actualizado = valorNuevo;
            return Task.FromResult(filas);
        }
    }
}
