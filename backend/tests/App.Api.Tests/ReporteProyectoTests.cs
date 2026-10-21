using System.Security.Claims;
using System.Text;
using System.Text.Json;
using App.Api.Configuracion;
using App.Api.Endpoints;
using App.Api.Seguridad;
using App.Application.Cronograma;
using App.Application.Cronograma.Reporte;
using App.Application.Proyectos;
using App.Application.Seguridad;
using App.Domain.Catalogos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace App.Api.Tests;

/// <summary>
/// TAREA-22-2: GET /api/proyectos/{id}/reporte y …/reporte/resumen con la autorización REAL (DevAuth + políticas) y el
/// servicio REAL, en un pipeline EN MEMORIA: sin Kestrel, sin red, sin base (consultas falsas) y con un escritor falso
/// (el libro lo prueban App.Infrastructure.Tests). Datos FICTICIOS: proyecto 1 del admin (Id 1), proyecto 2 de otro usuario.
/// </summary>
public sealed class ReporteProyectoTests
{
    private const string CedulaFicticia = "0000000006";
    private static readonly byte[] ArchivoFalso = [0x50, 0x4B, 0x03, 0x04];

    [Fact]
    public async Task Reporte_200_Xlsx_ContentDisposition_NoStore_YFilas()
    {
        var r = await EnviarAsync("admin", "/api/proyectos/1/reporte?desde=2026-10-01&hasta=2026-10-31");

        Assert.Equal(StatusCodes.Status200OK, r.Estado);
        Assert.Equal(CronogramaEndpoints.TipoXlsx, r.Contexto.Response.ContentType);
        var disposicion = r.Contexto.Response.Headers.ContentDisposition.ToString();
        Assert.Contains("attachment", disposicion);
        Assert.Contains("filename=Reporte_PRY-1_20261001_20261031.xlsx", disposicion);
        Assert.Contains("filename*=UTF-8''Reporte_PRY-1_20261001_20261031.xlsx", disposicion);
        Assert.Equal("no-store", r.Contexto.Response.Headers.CacheControl.ToString());
        Assert.Equal("1", r.Contexto.Response.Headers[CronogramaEndpoints.EncabezadoFilas].ToString());
        Assert.Equal(ArchivoFalso, r.Bytes);
    }

    [Theory]
    [InlineData("/api/proyectos/1/reporte?hasta=2026-10-31", "desde")]
    [InlineData("/api/proyectos/1/reporte?desde=2026-10-31&hasta=2026-10-01", "hasta")]
    [InlineData("/api/proyectos/1/reporte/resumen?desde=01-10-2026&hasta=2026-10-31", "desde")]
    public async Task Reporte_YResumen_400EnSuCampo(string ruta, string campo)
    {
        var r = await EnviarAsync("admin", ruta);

        Assert.Equal(StatusCodes.Status400BadRequest, r.Estado);
        using var doc = JsonDocument.Parse(r.Bytes);
        Assert.True(doc.RootElement.GetProperty("errors").TryGetProperty(campo, out _));
    }

    [Fact]
    public async Task Gestor_ProyectoAjeno_404_YSinUsuario_401()
    {
        Assert.Equal(StatusCodes.Status404NotFound, (await EnviarAsync("gestor", "/api/proyectos/2/reporte?desde=2026-10-01&hasta=2026-10-31")).Estado);
        Assert.Equal(StatusCodes.Status404NotFound, (await EnviarAsync("gestor", "/api/proyectos/2/reporte/resumen?desde=2026-10-01&hasta=2026-10-31")).Estado);
        Assert.Equal(StatusCodes.Status401Unauthorized, (await EnviarAsync("anonimo", "/api/proyectos/1/reporte?desde=2026-10-01&hasta=2026-10-31")).Estado);
    }

    [Fact]
    public async Task Resumen_SoloConteos_SinCedulasNiNombres()
    {
        var r = await EnviarAsync("admin", "/api/proyectos/1/reporte/resumen?desde=2026-10-01&hasta=2026-10-31");
        var cuerpo = Encoding.UTF8.GetString(r.Bytes);

        Assert.Equal(StatusCodes.Status200OK, r.Estado);
        Assert.Equal("""{"filas":1,"personas":1,"personasSinCedula":0,"personasSinCodigoInfor":1,"filasSinActividad":1,"diasConOtroProyecto":0}""", cuerpo);
        Assert.DoesNotContain(CedulaFicticia, cuerpo);
        Assert.DoesNotContain("PERSONA", cuerpo);
    }

    // ------------------------------------------------------------------ pipeline en memoria

    private sealed record Respuesta(int Estado, byte[] Bytes, HttpContext Contexto);

    private static async Task<Respuesta> EnviarAsync(string usuario, string ruta)
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
        // Sin UsuarioActualMiddleware (consulta dbo.Usuario): el usuario sale de las claims (admin = 1, gestor = 2).
        builder.Services.AddScoped<IUsuarioActual, UsuarioDesdeClaims>();
        builder.Services.AddSingleton<ICronogramaConsultas, ConsultasFalsas>();
        builder.Services.AddSingleton<IEscritorReporteExcel, EscritorFalso>();
        builder.Services.AddScoped<ReporteProyectoServicio>();
        // Solo para enlazar las demás rutas del cronograma (no se llaman).
        builder.Services.AddScoped<CronogramaConsultaServicio>(_ => throw new InvalidOperationException("No se usa en esta prueba."));

        await using var app = builder.Build();
        app.UseExceptionHandler();
        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapCronogramaEndpoints();
        app.UseEndpoints(_ => { });
        var pipeline = ((IApplicationBuilder)app).Build();

        var partes = ruta.Split('?', 2);
        var contexto = new DefaultHttpContext { RequestServices = app.Services.CreateScope().ServiceProvider };
        contexto.Request.Method = HttpMethods.Get;
        contexto.Request.Path = partes[0];
        contexto.Request.QueryString = partes.Length > 1 ? new QueryString("?" + partes[1]) : QueryString.Empty;
        contexto.Request.Headers["X-Dev-User"] = usuario;
        contexto.RequestServices.GetRequiredService<IHttpContextAccessor>().HttpContext = contexto;
        var salida = new MemoryStream();
        contexto.Response.Body = salida;

        await pipeline(contexto);

        return new Respuesta(contexto.Response.StatusCode, salida.ToArray(), contexto);
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

    private sealed class EscritorFalso : IEscritorReporteExcel
    {
        public byte[] Escribir(IReadOnlyList<FilaReporte> filas) => ArchivoFalso;
    }

    /// <summary>Proyecto 1 (del usuario 1) con un día de la persona 6; proyecto 2 de otro usuario.</summary>
    private sealed class ConsultasFalsas : ICronogramaConsultas
    {
        private static readonly DateOnly Dia = new(2026, 10, 2);

        public Task<int?> ObtenerMaxDiasAsync(CancellationToken ct) => Task.FromResult<int?>(90);

        public Task<IReadOnlyList<DiaCronograma>> ObtenerDiasAsync(FiltroCronograma filtro, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DiaCronograma>>(filtro.SoloProyectoId == 1
                ? [new DiaCronograma(1, 6, "900006", "PERSONA 06", Dia, CatalogoIds.RolAsignacion.Principal, "PRINCIPAL", null)]
                : []);

        public Task<IReadOnlyList<DiaOtrosProyectos>> ObtenerOtrosProyectosAsync(FiltroOtrosProyectos filtro, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DiaOtrosProyectos>>([]);

        public Task<IReadOnlyList<NovedadDiaCronograma>> ObtenerNovedadesAsync(
            IReadOnlyCollection<int> empleadoIds, IReadOnlyCollection<int> proyectoIds, DateOnly desde, DateOnly hasta, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<NovedadDiaCronograma>>([]);

        public Task<IReadOnlyList<ActividadCronograma>> ObtenerActividadesAsync(
            IReadOnlyCollection<int> proyectoIds, DateOnly desde, DateOnly hasta, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ActividadCronograma>>([]);

        public Task<IReadOnlyList<ProyectoCronogramaDto>> ObtenerProyectosAsync(IReadOnlyCollection<int> proyectoIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ProyectoCronogramaDto>>(proyectoIds.Select(id => new ProyectoCronogramaDto(id, $"PRY-{id}",
                $"PROYECTO {id}", "ACTIVO", "CAMPO", 9001, "120260053", null,
                ArmadoCronograma.Horario(1, "07:00 - 18:00", new TimeOnly(7, 0), new TimeOnly(18, 0)), new AlmuerzoDto(null, null))).ToList());

        public Task<RangoProyecto?> ObtenerRangoProyectoAsync(int proyectoId, int? propietarioUsuarioId, CancellationToken ct) =>
            Task.FromResult(proyectoId switch
            {
                1 when propietarioUsuarioId is null or 1 => new RangoProyecto(1, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)),
                2 when propietarioUsuarioId is null or 99 => new RangoProyecto(2, new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)),
                _ => (RangoProyecto?)null,
            });

        public Task<IReadOnlyList<EmpleadoReporte>> ObtenerEmpleadosReporteAsync(IReadOnlyCollection<int> empleadoIds, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EmpleadoReporte>>([new EmpleadoReporte(6, "900006", "PERSONA 06", CedulaFicticia, null)]);
    }
}
