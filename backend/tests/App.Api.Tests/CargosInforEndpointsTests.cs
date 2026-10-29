using App.Api.Configuracion;
using App.Api.Endpoints;
using App.Api.Seguridad;
using App.Application.Comun;
using App.Application.Empleados;
using App.Application.Erp;
using App.Application.Proyectos.Crear;
using App.Application.Proyectos.Estados;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace App.Api.Tests;

/// <summary>
/// TAREA-25-3-1: /api/admin/cargos-infor con la autorización REAL (DevAuth y las políticas) en un pipeline EN MEMORIA,
/// sin red ni base (dobles). Puestos y códigos FICTICIOS.
/// </summary>
public sealed class CargosInforEndpointsTests
{
    private const string Ruta = "/api/admin/cargos-infor";

    [Theory]
    [InlineData("GET", Ruta, null)]
    [InlineData("GET", Ruta + "/importacion", null)]
    [InlineData("POST", Ruta + "/importacion", null)]
    [InlineData("PUT", Ruta + "/3", """{"codigoInfor":"P1","activo":true,"codigoInforAnterior":null,"activoAnterior":true}""")]
    public async Task Gestor_403_SinTocarLaApiNiLaBase(string metodo, string ruta, string? cuerpo)
    {
        var repo = new RepoFalso();
        var fuente = new FuenteFalsa();

        var (estado, _) = await EnviarAsync("gestor", metodo, ruta, cuerpo, repo, fuente);

        Assert.Equal(StatusCodes.Status403Forbidden, estado);
        Assert.Equal((0, 0), (repo.Llamadas, fuente.Lecturas));
    }

    [Fact]
    public async Task Admin_Put_404_Y_409()
    {
        const string cuerpo = """{"codigoInfor":"p1","activo":true,"codigoInforAnterior":null,"activoAnterior":true}""";

        var (noExiste, mensaje404) = await EnviarAsync("admin", "PUT", Ruta + "/99", cuerpo, new RepoFalso(filas: 0, existe: false), new FuenteFalsa());
        var (cambiado, mensaje409) = await EnviarAsync("admin", "PUT", Ruta + "/3", cuerpo, new RepoFalso(filas: 0, existe: true), new FuenteFalsa());
        var (ok, cuerpoOk) = await EnviarAsync("admin", "PUT", Ruta + "/3", cuerpo, new RepoFalso(), new FuenteFalsa());

        Assert.Equal(StatusCodes.Status404NotFound, noExiste);
        Assert.Contains("El cargo no existe.", mensaje404);
        Assert.Equal(StatusCodes.Status409Conflict, cambiado);
        Assert.Contains("Otro usuario cambió el cargo; recargue.", mensaje409);
        Assert.Equal((StatusCodes.Status200OK, """{"id":3,"codigoInfor":"P1","activo":true}"""), (ok, cuerpoOk));
    }

    [Fact]
    public async Task Admin_Put_400_EnCodigoInfor()
    {
        var (estado, cuerpo) = await EnviarAsync("admin", "PUT", Ruta + "/3",
            """{"codigoInfor":"P 1","activo":true,"codigoInforAnterior":null,"activoAnterior":true}""", new RepoFalso(), new FuenteFalsa());

        Assert.Equal(StatusCodes.Status400BadRequest, estado);
        Assert.Contains("El código Infor solo admite letras, números, punto, guion y guion bajo.", cuerpo);
    }

    [Fact]
    public async Task Admin_Importar_409_PorElIndiceUnico()
    {
        var (estado, cuerpo) = await EnviarAsync("admin", "POST", Ruta + "/importacion", null, new RepoFalso(fallarAlInsertar: true), new FuenteFalsa());

        Assert.Equal(StatusCodes.Status409Conflict, estado);
        Assert.Contains("No se pudo importar: otro registro se guardó al mismo tiempo", cuerpo);
    }

    [Fact]
    public async Task Admin_Importar_503_ConElErpCaido_SinTocarLaBase()
    {
        var repo = new RepoFalso();

        var (estado, cuerpo) = await EnviarAsync("admin", "POST", Ruta + "/importacion", null, repo, new FuenteFalsa(caida: true));

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, estado);
        Assert.Contains("Servicio de empleados no disponible", cuerpo);
        Assert.Equal(0, repo.Llamadas);
    }

    [Fact]
    public async Task Admin_VistaPrevia_200_SoloConteos()
    {
        var (estado, cuerpo) = await EnviarAsync("admin", "GET", Ruta + "/importacion", null, new RepoFalso(), new FuenteFalsa());

        Assert.Equal(StatusCodes.Status200OK, estado);
        Assert.StartsWith("""{"puestosApi":1,"nuevos":1,"yaExistentes":0,"existentesInactivos":0,"fusionados":0,"gruposVariantes":0,"variantes":[]""", cuerpo);
        Assert.DoesNotContain("PERSONA", cuerpo);
    }

    // ------------------------------------------------------------------ pipeline en memoria (como CriterioPersonalEndpointsTests)

    private static async Task<(int Estado, string Cuerpo)> EnviarAsync(string usuario, string metodo, string ruta, string? cuerpo,
        RepoFalso repo, FuenteFalsa fuente)
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
        builder.Services.AddSingleton<ICargosInforRepositorio>(repo);
        builder.Services.AddSingleton<IFuenteEmpleadosErp>(fuente);
        builder.Services.AddSingleton<ITransaccionAsignaciones, TransaccionFalsa>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<CargosInforServicio>();

        await using var app = builder.Build();
        app.UseExceptionHandler();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapCargoInforEndpoints();
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
            contexto.Features.Set<IHttpRequestBodyDetectionFeature>(new CuerpoDetectable());
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

    private sealed class FuenteFalsa(bool caida = false) : IFuenteEmpleadosErp
    {
        public int Lecturas { get; private set; }

        private ListaEmpleadosErp Lista()
        {
            Lecturas++;
            return caida
                ? throw new ErpNoDisponibleException("empleados", "caído")
                : new ListaEmpleadosErp([new EmpleadoErp("900001", "PERSONA 900001", null, null, null, null, null, null, "AYUDANTE", null, null,
                    null, null, null, null, null, null, "ADMINISTRATIVO", "A")], DateTimeOffset.UnixEpoch);
        }

        public Task<ListaEmpleadosErp> ObtenerActivosAsync(CancellationToken ct) => Task.FromResult(Lista());

        public Task<ListaEmpleadosErp> ObtenerActivosFrescosAsync(CancellationToken ct) => Task.FromResult(Lista());
    }

    private sealed class TransaccionFalsa : ITransaccionAsignaciones
    {
        public Task<T> EjecutarAsync<T>(Func<CancellationToken, Task<T>> accion, Func<T, bool> confirmar, CancellationToken ct) => accion(ct);
    }

    private sealed class RepoFalso(int filas = 1, bool existe = true, bool fallarAlInsertar = false) : ICargosInforRepositorio
    {
        public int Llamadas { get; private set; }

        public Task<PaginaResultado<CargoInforFila>> ListarAsync(CargoInforFiltro filtro, CancellationToken ct)
        {
            Llamadas++;
            return Task.FromResult(new PaginaResultado<CargoInforFila>([], 1, 25, 0));
        }

        public Task<IReadOnlyList<CargoRegistro>> ObtenerTodosAsync(CancellationToken ct)
        {
            Llamadas++;
            return Task.FromResult<IReadOnlyList<CargoRegistro>>([]);
        }

        public Task InsertarAsync(IReadOnlyList<string> cargos, CancellationToken ct)
        {
            Llamadas++;
            return fallarAlInsertar ? throw new ConflictoConcurrenciaException("UQ") : Task.CompletedTask;
        }

        public Task<int> ActualizarAsync(int id, string? codigoAnterior, bool activoAnterior, string? codigo, bool activo, int usuarioId,
            DateTime fechaUtc, CancellationToken ct)
        {
            Llamadas++;
            return Task.FromResult(filas);
        }

        public Task<bool> ExisteAsync(int id, CancellationToken ct)
        {
            Llamadas++;
            return Task.FromResult(existe);
        }
    }
}
