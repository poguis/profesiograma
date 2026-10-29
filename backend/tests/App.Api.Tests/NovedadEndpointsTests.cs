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

    // ------------------------------------------------------------------ TAREA-23-2: editar y anular

    private const string EdicionGeneral = """{ "version": "AAAAAAAAB9E=", "tipoNovedad": "FERIADO", "fechaInicio": "2032-01-01", "fechaFin": "2032-01-02", "diasExcluidos": [] }""";

    [Fact]
    public async Task Put_VersionIlegible_400_Y_SinSesion_401()
    {
        var ilegible = await EnviarAsync("admin", HttpMethods.Put, "/api/novedades/5", EdicionGeneral.Replace("AAAAAAAAB9E=", "no-es-base64!", StringComparison.Ordinal));
        var anonimo = await EnviarAsync("anonimo", HttpMethods.Put, "/api/novedades/5", EdicionGeneral);

        Assert.Equal(StatusCodes.Status400BadRequest, ilegible.Estado);
        Assert.Equal(VersionNovedad.MensajeInvalida, ilegible.Json!.RootElement.GetProperty("errors").GetProperty("version")[0].GetString());
        Assert.Equal(StatusCodes.Status401Unauthorized, anonimo.Estado);
    }

    [Fact]
    public async Task Put_Y_Anular_GeneralDelAdmin_ComoGestor_403_Inexistente_404()
    {
        var put = await EnviarAsync("gestor", HttpMethods.Put, "/api/novedades/5", EdicionGeneral);
        var anular = await EnviarAsync("gestor", HttpMethods.Post, "/api/novedades/5/anular", """{ "version": "AAAAAAAAB9E=", "motivo": "Prueba de anulación" }""");
        var inexistente = await EnviarAsync("admin", HttpMethods.Put, "/api/novedades/6", EdicionGeneral);

        Assert.Equal(StatusCodes.Status403Forbidden, put.Estado);
        Assert.Equal(PermisosNovedad.MensajeSinPermiso, put.Json!.RootElement.GetProperty("title").GetString());
        Assert.Equal(StatusCodes.Status403Forbidden, anular.Estado);
        Assert.Equal(StatusCodes.Status404NotFound, inexistente.Estado);
    }

    [Fact]
    public async Task Put_Y_Anular_ComoAdmin_200ConVersionNueva()
    {
        var put = await EnviarAsync("admin", HttpMethods.Put, "/api/novedades/5", EdicionGeneral);
        var anular = await EnviarAsync("admin", HttpMethods.Post, "/api/novedades/5/anular", """{ "version": "AAAAAAAAB9E=", "motivo": "Prueba de anulación" }""");

        Assert.Equal(StatusCodes.Status200OK, put.Estado);
        Assert.Equal("AAAAAAAAB9I=", put.Json!.RootElement.GetProperty("version").GetString());
        Assert.Equal((200, 5), (anular.Estado, anular.Json!.RootElement.GetProperty("id").GetInt32()));
    }

    // ------------------------------------------------------------------ TAREA-27: tipoConflicto en los 409

    private static string GeneralEl(string fecha) =>
        $$"""{ "tipoAplicacion": "GENERAL", "tipoNovedad": "FERIADO", "fechaInicio": "{{fecha}}", "fechaFin": "{{fecha}}" }""";

    private static (string? Titulo, string? Tipo, string? Codigo) Conflicto(Respuesta r)
    {
        Assert.Equal(StatusCodes.Status409Conflict, r.Estado);
        var raiz = r.Json!.RootElement;
        return (raiz.GetProperty("title").GetString(), raiz.GetProperty("tipoConflicto").GetString(),
            raiz.TryGetProperty("codigo", out var codigo) ? codigo.GetString() : null);
    }

    [Fact]
    public async Task Crear_Duplicado_409Duplicado_ConCodigoSoloSiEsVisible()
    {
        var visible = Conflicto(await EnviarAsync("admin", HttpMethods.Post, "/api/novedades", GeneralEl("2026-12-31")));
        var oculto = Conflicto(await EnviarAsync("admin", HttpMethods.Post, "/api/novedades", GeneralEl("2026-12-30")));

        Assert.Equal((ResultadoCrearNovedad.MensajeDuplicado, "duplicado", "NOV-20261231-dup001"), visible);
        Assert.Equal((ResultadoCrearNovedad.MensajeDuplicado, "duplicado", null), oculto);
    }

    [Fact]
    public async Task Crear_AltaConcurrente_409RegistroConcurrente()
    {
        var r = Conflicto(await EnviarAsync("admin", HttpMethods.Post, "/api/novedades", GeneralEl("2026-12-29")));

        Assert.Equal((ResultadoCrearNovedad.MensajeCambiado, "registroConcurrente", null), r);
    }

    [Fact]
    public async Task Editar_Anulada_Cambio_Duplicado_ConSuTipo()
    {
        var anulada = Conflicto(await EnviarAsync("admin", HttpMethods.Put, "/api/novedades/8", EdicionGeneral));
        var cambio = Conflicto(await EnviarAsync("admin", HttpMethods.Put, "/api/novedades/5",
            EdicionGeneral.Replace("AAAAAAAAB9E=", "AAAAAAAACQk=", StringComparison.Ordinal)));
        var duplicado = Conflicto(await EnviarAsync("admin", HttpMethods.Put, "/api/novedades/5",
            EdicionGeneral.Replace("2032-01-01", "2032-02-01", StringComparison.Ordinal).Replace("2032-01-02", "2032-02-01", StringComparison.Ordinal)));

        Assert.Equal((PermisosNovedad.MensajeAnulada, "anulada", null), anulada);
        Assert.Equal((VersionNovedad.MensajeCambio, "cambio", null), cambio);
        Assert.Equal((ResultadoCrearNovedad.MensajeDuplicado, "duplicado", "NOV-20320201-dup002"), duplicado);
    }

    [Fact]
    public async Task Anular_YaAnulada_Y_Cambio_ConSuTipo()
    {
        var ya = Conflicto(await EnviarAsync("admin", HttpMethods.Post, "/api/novedades/8/anular", """{ "version": "AAAAAAAAB9E=", "motivo": "Prueba de anulación" }"""));
        var cambio = Conflicto(await EnviarAsync("admin", HttpMethods.Post, "/api/novedades/5/anular", """{ "version": "AAAAAAAACQk=", "motivo": "Prueba de anulación" }"""));

        Assert.Equal((ResultadoModificacionNovedad.MensajeYaAnulada, "anulada", null), ya);
        Assert.Equal((VersionNovedad.MensajeCambio, "cambio", null), cambio);
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
        builder.Services.AddSingleton<INovedadModificacion, ModificacionFalsa>(); // TAREA-23-2
        builder.Services.AddScoped<NovedadModificacionServicio>();

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

    /// <summary>
    /// TAREA-27: duplicado visible el 31/12/2026 y el 01/02/2032 (edición), oculto el 30/12/2026; el alta del 29/12/2026
    /// choca con otro registro (ConflictoConcurrenciaException).
    /// </summary>
    private sealed class RepositorioFalso : INovedadRepositorio
    {
        public Task<DuplicadoNovedad?> BuscarDuplicadoAsync(CriterioDuplicado criterio, int? visibleParaUsuarioId, CancellationToken ct) =>
            Task.FromResult(criterio.Desde switch
            {
                { Year: 2026, Month: 12, Day: 31 } => new DuplicadoNovedad("NOV-20261231-dup001", true),
                { Year: 2026, Month: 12, Day: 30 } => new DuplicadoNovedad("NOV-20261230-oculta", false),
                { Year: 2032, Month: 2, Day: 1 } => new DuplicadoNovedad("NOV-20320201-dup002", true),
                _ => (DuplicadoNovedad?)null,
            });

        public Task<bool> ExisteCodigoOUidAsync(string codigo, Guid uid, CancellationToken ct) => Task.FromResult(false);

        public Task<NovedadCreadaDto> AgregarAsync(NuevaNovedad nueva, CancellationToken ct) =>
            nueva.Datos.FechaInicio == new DateOnly(2026, 12, 29)
                ? throw new App.Application.Proyectos.Estados.ConflictoConcurrenciaException("Alta concurrente (prueba).")
                : Task.FromResult(new NovedadCreadaDto(77, nueva.Codigo, "AAAAAAAAB9E="));
    }

    /// <summary>
    /// Novedad 5: GENERAL FERIADO del admin (Id 1), 3 días, versión AAAAAAAAB9E=. Novedad 8 (TAREA-27): la misma, anulada.
    /// Cualquier otra: no existe.
    /// </summary>
    private sealed class ModificacionFalsa : INovedadModificacion
    {
        public Task<NovedadParaModificar?> ObtenerParaModificarAsync(int id, int? visibleParaUsuarioId, CancellationToken ct) =>
            Task.FromResult(id is 5 or 8
                ? new NovedadParaModificar(id, 3, 3, null, null, "PROFESIOGRAMA", true, null, id == 8, 1, null,
                    [new(2032, 1, 1), new(2032, 1, 2), new(2032, 1, 3)], [0, 0, 0, 0, 0, 0, 7, 209])
                : null);

        public Task<string> EditarAsync(EdicionNovedad edicion, CancellationToken ct) => Task.FromResult("AAAAAAAAB9I=");

        public Task<string> AnularAsync(AnulacionNovedad anulacion, CancellationToken ct) => Task.FromResult("AAAAAAAAB9M=");
    }

    private sealed class TransaccionFalsa : ITransaccionAsignaciones
    {
        public Task<T> EjecutarAsync<T>(Func<CancellationToken, Task<T>> accion, Func<T, bool> confirmar, CancellationToken ct) => accion(ct);
    }
}
