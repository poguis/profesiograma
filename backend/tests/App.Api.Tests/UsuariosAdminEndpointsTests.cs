using App.Api.Configuracion;
using App.Api.Endpoints;
using App.Api.Seguridad;
using App.Application.Comun;
using App.Application.Seguridad;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace App.Api.Tests;

/// <summary>
/// TAREA-25-1: /api/admin/usuarios con la autorización REAL (DevAuth y las políticas) en un pipeline EN MEMORIA, sin red
/// ni base (dobles). Datos FICTICIOS.
/// </summary>
public sealed class UsuariosAdminEndpointsTests
{
    [Theory]
    [InlineData("GET", "/api/admin/usuarios", null)]
    [InlineData("GET", "/api/admin/usuarios/5", null)]
    [InlineData("POST", "/api/admin/usuarios", """{"email":"persona@sedemi.com","nombreMostrar":"X"}""")]
    [InlineData("PUT", "/api/admin/usuarios/5", """{"nombreMostrar":"X","activo":false,"nombreAnterior":"X","activoAnterior":true}""")]
    [InlineData("PUT", "/api/admin/usuarios/5/departamentos", """{"departamentoIds":[1],"anteriores":[]}""")]
    public async Task Gestor_403_SinTocarElRepositorio(string metodo, string ruta, string? cuerpo)
    {
        var repo = new RepoFalso();

        var (estado, _) = await EnviarAsync("gestor", metodo, ruta, cuerpo, repo);

        Assert.Equal(StatusCodes.Status403Forbidden, estado);
        Assert.Equal(0, repo.Llamadas);
    }

    [Fact]
    public async Task Admin_Post_201_Y_Duplicado409ConUsuarioId()
    {
        var (creado, cuerpo) = await EnviarAsync("admin", "POST", "/api/admin/usuarios",
            """{"email":"Nueva@Sedemi.com","nombreMostrar":"PERSONA NUEVA"}""", new RepoFalso());
        var (duplicado, problema) = await EnviarAsync("admin", "POST", "/api/admin/usuarios",
            """{"email":"existente@sedemi.com","nombreMostrar":"X"}""", new RepoFalso());

        Assert.Equal((StatusCodes.Status201Created, """{"id":10}"""), (creado, cuerpo));
        Assert.Equal(StatusCodes.Status409Conflict, duplicado);
        Assert.Contains("Ya existe un usuario con ese correo.", problema);
        Assert.Contains("\"usuarioId\":5", problema);
    }

    [Fact]
    public async Task Admin_Post_DominioNoPermitido400_EnEmail()
    {
        var (estado, cuerpo) = await EnviarAsync("admin", "POST", "/api/admin/usuarios", """{"email":"persona@otro.com","nombreMostrar":"X"}""",
            new RepoFalso());

        Assert.Equal(StatusCodes.Status400BadRequest, estado);
        Assert.Contains("\"email\":[\"El correo debe ser de un dominio permitido: sedemi.com.\"]", cuerpo);
    }

    [Theory]
    [InlineData("GET", "/api/admin/usuarios/99", null)]
    [InlineData("GET", "/api/admin/usuarios/1", null)] // SISTEMA
    [InlineData("PUT", "/api/admin/usuarios/1", """{"nombreMostrar":"X","activo":true,"nombreAnterior":"X","activoAnterior":true}""")]
    [InlineData("PUT", "/api/admin/usuarios/99/departamentos", """{"departamentoIds":[],"anteriores":[]}""")]
    public async Task Admin_InexistenteOSistema_404(string metodo, string ruta, string? cuerpo)
    {
        var (estado, _) = await EnviarAsync("admin", metodo, ruta, cuerpo, new RepoFalso());

        Assert.Equal(StatusCodes.Status404NotFound, estado);
    }

    [Fact]
    public async Task Admin_PutDepartamentos_Desfasado409_YOk200()
    {
        var (conflicto, mensaje) = await EnviarAsync("admin", "PUT", "/api/admin/usuarios/5/departamentos",
            """{"departamentoIds":[1],"anteriores":[]}""", new RepoFalso(conflicto: true));
        var (ok, cuerpo) = await EnviarAsync("admin", "PUT", "/api/admin/usuarios/5/departamentos",
            """{"departamentoIds":[1],"anteriores":[]}""", new RepoFalso());

        Assert.Equal(StatusCodes.Status409Conflict, conflicto);
        Assert.Contains("Otro usuario cambió los departamentos; recargue.", mensaje);
        Assert.Equal((StatusCodes.Status200OK, """{"departamentos":[{"id":1,"nombreCorto":"A"}]}"""), (ok, cuerpo));
    }

    [Fact]
    public async Task Admin_Put_Desfasado409()
    {
        var (estado, cuerpo) = await EnviarAsync("admin", "PUT", "/api/admin/usuarios/5",
            """{"nombreMostrar":"X","activo":false,"nombreAnterior":"VIEJO","activoAnterior":true}""", new RepoFalso(filas: 0));

        Assert.Equal(StatusCodes.Status409Conflict, estado);
        Assert.Contains("Otro usuario cambió el registro; recargue.", cuerpo);
    }

    // ------------------------------------------------------------------ pipeline en memoria (como CriterioPersonalEndpointsTests)

    private static async Task<(int Estado, string Cuerpo)> EnviarAsync(string usuario, string metodo, string ruta, string? cuerpo, RepoFalso repo)
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
        builder.Services.AddSingleton<IUsuariosAdminRepositorio>(repo);
        builder.Services.AddSingleton<IUsuarioProvisionamiento>(new ProvisionamientoFalso());
        builder.Services.AddSingleton(new ConfiguracionUsuarios(["sedemi.com"]));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<UsuariosAdminServicio>();

        await using var app = builder.Build();
        app.UseExceptionHandler();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapUsuarioEndpoints();
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

    private sealed class ProvisionamientoFalso : IUsuarioProvisionamiento
    {
        public Task<UsuarioResuelto> ResolverAsync(Guid? entraObjectId, string email, string nombreMostrar, CancellationToken ct) =>
            throw new NotSupportedException();

        public void Invalidar(string email, Guid? entraObjectId) { }
    }

    private sealed class RepoFalso(int filas = 1, bool conflicto = false) : IUsuariosAdminRepositorio
    {
        private static readonly UsuarioRegistro[] Usuarios =
        [
            new(1, "sistema@prueba.local", null, "SISTEMA", true, true),
            new(5, "existente@sedemi.com", null, "X", true, false),
        ];

        public int Llamadas { get; private set; }

        public Task<PaginaResultado<UsuarioAdminDto>> ListarAsync(UsuarioAdminFiltro filtro, CancellationToken ct)
        {
            Llamadas++;
            return Task.FromResult(new PaginaResultado<UsuarioAdminDto>([], 1, 25, 0));
        }

        public Task<UsuarioAdminDto?> ObtenerAsync(int id, CancellationToken ct)
        {
            Llamadas++;
            return Task.FromResult(id == 5 ? new UsuarioAdminDto(5, "existente@sedemi.com", "X", true, false, []) : null);
        }

        public Task<UsuarioRegistro?> ObtenerRegistroAsync(int id, CancellationToken ct)
        {
            Llamadas++;
            return Task.FromResult(Usuarios.FirstOrDefault(u => u.Id == id));
        }

        public Task<UsuarioRegistro?> BuscarPorEmailAsync(string email, CancellationToken ct)
        {
            Llamadas++;
            return Task.FromResult(Usuarios.FirstOrDefault(u => u.Email == email));
        }

        public Task<IReadOnlyList<DepartamentoRegistro>> ObtenerDepartamentosAsync(CancellationToken ct)
        {
            Llamadas++;
            return Task.FromResult<IReadOnlyList<DepartamentoRegistro>>([new(1, "DEPARTAMENTO A", "A", true, 1)]);
        }

        public Task<int?> CrearAsync(string email, string nombreMostrar, CancellationToken ct)
        {
            Llamadas++;
            return Task.FromResult<int?>(10);
        }

        public Task<int> ActualizarAsync(int id, string nombreAnterior, bool activoAnterior, string nombre, bool activo, int usuarioId,
            DateTime fechaUtc, CancellationToken ct)
        {
            Llamadas++;
            return Task.FromResult(filas);
        }

        public Task<CambioDepartamentos> AsignarDepartamentosAsync(int usuarioId, IReadOnlySet<int> anteriores, IReadOnlySet<int> nuevos,
            CancellationToken ct)
        {
            Llamadas++;
            return Task.FromResult(new CambioDepartamentos(conflicto, conflicto ? [] : [1], []));
        }
    }
}
