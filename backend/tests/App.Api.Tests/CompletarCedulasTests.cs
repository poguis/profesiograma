using App.Api.Configuracion;
using App.Api.Endpoints;
using App.Api.Seguridad;
using App.Application.Empleados;
using App.Application.Proyectos.Crear;
using App.Domain.Maestros;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace App.Api.Tests;

/// <summary>
/// TAREA-22-1: POST /api/empleados/completar-cedulas con la autorización REAL (DevAuth y las políticas de
/// AddSeguridadProfesiograma) en un pipeline EN MEMORIA: sin Kestrel, sin red, sin base ni API (dobles). Cédulas FICTICIAS.
/// </summary>
public sealed class CompletarCedulasTests
{
    private const string Ruta = "/api/empleados/completar-cedulas";
    private const string CedulaFicticia = "0000000001";

    [Fact]
    public async Task Gestor_403_SinTocarLaApiNiLaBase()
    {
        var fuente = new FuenteFalsa();

        var (estado, _) = await EnviarAsync("gestor", fuente);

        Assert.Equal(StatusCodes.Status403Forbidden, estado);
        Assert.Equal(0, fuente.Lecturas);
    }

    [Fact]
    public async Task Admin_200_SoloConteos_SinCedulasNiCodigos()
    {
        var (estado, cuerpo) = await EnviarAsync("admin", new FuenteFalsa());

        Assert.Equal(StatusCodes.Status200OK, estado);
        Assert.Equal("""{"revisadas":2,"completadas":1,"sinCedulaEnApi":0,"noEncontradasEnApi":1}""", cuerpo);
        Assert.DoesNotContain(CedulaFicticia, cuerpo);
        Assert.DoesNotContain("900001", cuerpo);
    }

    // ------------------------------------------------------------------ pipeline en memoria

    private static async Task<(int Estado, string Cuerpo)> EnviarAsync(string usuario, FuenteFalsa fuente)
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
        builder.Services.AddSingleton<IFuenteEmpleadosErp>(fuente);
        builder.Services.AddSingleton<ITransaccionAsignaciones, TransaccionFalsa>();
        builder.Services.AddSingleton<ICompletarCedulasRepositorio>(
            new RepositorioFalso(new Empleado { CodigoEkon = "900001" }, new Empleado { CodigoEkon = "900099" }));
        builder.Services.AddScoped<CompletarCedulasServicio>();
        // Solo para enlazar GET /api/empleados (no se llama); con fábrica, la validación de Development no lo construye.
        builder.Services.AddScoped<EmpleadoConsultaServicio>(_ => throw new InvalidOperationException("No se usa en esta prueba."));

        await using var app = builder.Build();
        app.UseExceptionHandler();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapEmpleadoEndpoints();
        app.UseEndpoints(_ => { });
        var pipeline = ((IApplicationBuilder)app).Build();

        var contexto = new DefaultHttpContext { RequestServices = app.Services.CreateScope().ServiceProvider };
        contexto.Request.Method = HttpMethods.Post;
        contexto.Request.Path = Ruta;
        contexto.Request.Headers["X-Dev-User"] = usuario;
        var salida = new MemoryStream();
        contexto.Response.Body = salida;

        await pipeline(contexto);

        return (contexto.Response.StatusCode, System.Text.Encoding.UTF8.GetString(salida.ToArray()));
    }

    private sealed class FuenteFalsa : IFuenteEmpleadosErp
    {
        public int Lecturas { get; private set; }

        private static ListaEmpleadosErp Lista(CedulasEmpleados cedulas) =>
            new([new EmpleadoErp("900001", "PERSONA 900001", null, null, null, null, null, null, "TECNICO", null, null, null, null, null,
                null, null, null, null, "A")], DateTimeOffset.UnixEpoch, cedulas: cedulas);

        public Task<ListaEmpleadosErp> ObtenerActivosAsync(CancellationToken ct) => Task.FromResult(Lista(CedulasEmpleados.Vacio));

        public Task<ListaEmpleadosErp> ObtenerActivosFrescosAsync(CancellationToken ct)
        {
            Lecturas++;
            return Task.FromResult(Lista(new CedulasEmpleados([("900001", CedulaFicticia)])));
        }
    }

    private sealed class TransaccionFalsa : ITransaccionAsignaciones
    {
        public Task<T> EjecutarAsync<T>(Func<CancellationToken, Task<T>> accion, Func<T, bool> confirmar, CancellationToken ct) => accion(ct);
    }

    private sealed class RepositorioFalso(params Empleado[] filas) : ICompletarCedulasRepositorio
    {
        public Task<IReadOnlyList<Empleado>> ObtenerSinCedulaAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<Empleado>>(filas.Where(f => f.Cedula is null).ToList());

        public Task GuardarAsync(CancellationToken ct) => Task.CompletedTask;
    }
}
