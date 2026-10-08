using System.Diagnostics;
using System.Text;
using System.Text.Json;
using App.Api.Configuracion;
using App.Application.Proyectos.Crear;
using App.Application.Proyectos.Personal;
using App.Application.Proyectos.Reactivacion;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace App.Api.Tests;

/// <summary>
/// TAREA-26d-1, defecto V3 (08/10/2026 09:04): POST /api/proyectos/previsualizar con "actividadId": 20 (número; el campo
/// es texto) respondía 500. En Development el enlace del cuerpo lanza BadHttpRequestException (ThrowOnBadRequest) y
/// ningún manejador la atendía → 500 genérico. Pipeline EN MEMORIA (UseExceptionHandler + enlace real del cuerpo con
/// RequestDelegateFactory): sin Kestrel, sin red, sin llamar a ninguna API.
/// </summary>
public sealed class SolicitudIlegibleTests
{
    /// <summary>Cuerpo de la verificación V3 con códigos EKON ficticios (900001, 900002).</summary>
    private const string CuerpoV3 = """
        { "companiaId": "1001", "grupo": "CAMPO", "proyectoErpId": "120260053", "actividadId": 20,
          "dimensionUegpId": null, "fechaInicio": "2026-11-02", "fechaFin": "2026-11-27", "horarioCodigo": 7,
          "salidaAlmuerzo": "13:00", "regresoAlmuerzo": "14:00", "departamentoId": null,
          "principales": [ { "codigoEkon": "900001", "jornada": "TIPO_2", "fechaInicio": "2026-11-02",
                             "fechaFin": "2026-11-27", "cargo": null } ],
          "backs": [ { "codigoEkon": "900002", "tipoRegistro": "JORNADA", "diasDescanso": 2, "fechaInicio": "2026-11-09",
                       "fechaFin": "2026-11-12", "principalRelacionado": 1, "observacion": null, "cargo": null } ] }
        """;

    private static readonly string CuerpoV3Corregido =
        CuerpoV3.Replace("\"actividadId\": 20", "\"actividadId\": \"20\"", StringComparison.Ordinal);

    [Fact]
    public async Task V3_ActividadNumerica_400EnActividadId_No500()
    {
        var r = await EnviarAsync(Crear, CuerpoV3);

        Assert.Equal(StatusCodes.Status400BadRequest, r.Estado);
        Assert.Equal("La solicitud no tiene el formato esperado.", r.Titulo);
        Assert.Equal(["Debe ser texto entre comillas (p. ej. \"20\")."], r.Errores["actividadId"]);
    }

    [Fact]
    public async Task V3_ConActividadComoTexto_ElCuerpoSeLee_YCompaniaComoTextoSeAcepta()
    {
        var r = await EnviarAsync(Crear, CuerpoV3Corregido);

        Assert.Equal(StatusCodes.Status200OK, r.Estado);
        var s = Assert.IsType<CrearProyectoSolicitud>(r.Leido);
        Assert.Equal((1001, "20", "900001", "900002", 1),
            (s.CompaniaId, s.ActividadId, s.Principales![0].CodigoEkon, s.Backs![0].CodigoEkon, s.Backs[0].PrincipalRelacionado));
    }

    [Theory]
    [InlineData("\"fechaInicio\": \"2026-11-02\",\n", "\"fechaInicio\": \"02/11/2026\",\n",
        "principales[0].fechaInicio", "Debe ser una fecha con formato aaaa-mm-dd entre comillas.")]
    [InlineData("\"diasDescanso\": 2", "\"diasDescanso\": \"dos\"", "backs[0].diasDescanso", "Debe ser un número entero sin comillas.")]
    [InlineData("\"backs\": [", "\"backs\": {\"x\": 1}, \"otro\": [", "backs", "Debe ser una lista ([ … ]).")]
    public async Task TipoIncorrecto_400ConLaRutaDelCampo(string buscar, string poner, string clave, string mensaje)
    {
        var cuerpo = CuerpoV3Corregido.Replace(buscar, poner, StringComparison.Ordinal);
        Assert.NotEqual(CuerpoV3Corregido, cuerpo);

        var r = await EnviarAsync(Crear, cuerpo);

        Assert.Equal(StatusCodes.Status400BadRequest, r.Estado);
        Assert.Equal([mensaje], r.Errores[clave]);
    }

    [Fact]
    public async Task JsonMalFormado_400EnCuerpo_ConLinea()
    {
        var r = await EnviarAsync(Crear, "{ \"grupo\": \"CAMPO\",\n  \"fechaInicio\": }");

        Assert.Equal(StatusCodes.Status400BadRequest, r.Estado);
        Assert.Equal(["El cuerpo no es un JSON válido (línea 2): revise comas, comillas y llaves."], r.Errores["cuerpo"]);
    }

    [Fact]
    public async Task CuerpoVacio_400_YSinContentTypeJson_415()
    {
        var vacio = await EnviarAsync(Crear, "");
        Assert.Equal(StatusCodes.Status400BadRequest, vacio.Estado);
        Assert.True(vacio.Errores.ContainsKey("solicitud"));

        var texto = await EnviarAsync(Crear, "{}", "text/plain");
        Assert.Equal(StatusCodes.Status415UnsupportedMediaType, texto.Estado);
        Assert.True(texto.Errores.ContainsKey("cuerpo"));
    }

    [Fact]
    public async Task CampoDesconocido_SeIgnora_NoEsError()
    {
        var r = await EnviarAsync(Crear, "{ \"grupo\": \"CAMPO\", \"campoQueNoExiste\": 1 }");

        Assert.Equal(StatusCodes.Status200OK, r.Estado);
    }

    [Fact]
    public async Task PersonalYReactivacion_TipoIncorrecto_400EnSuCampo()
    {
        var personal = await EnviarAsync(Personal,
            "{ \"principales\": [ { \"clave\": \"n1\", \"codigoEkon\": 900001, \"jornada\": \"TIPO_2\" } ], \"backs\": [] }");
        Assert.Equal(StatusCodes.Status400BadRequest, personal.Estado);
        Assert.Equal(["Debe ser texto entre comillas (p. ej. \"20\")."], personal.Errores["principales[0].codigoEkon"]);

        var reactivacion = await EnviarAsync(Reactivar, "{ \"fecha\": \"2026-11-02\", \"versionProyecto\": \"siete\" }");
        Assert.Equal(StatusCodes.Status400BadRequest, reactivacion.Estado);
        Assert.Equal(["Debe ser un número entero sin comillas."], reactivacion.Errores["versionProyecto"]);
    }

    [Fact]
    public void ThrowOnBadRequest_EnTodosLosEntornos_ParaQueEl400LleveElMensaje()
    {
        using var sp = Servicios();
        Assert.True(sp.GetRequiredService<IOptions<RouteHandlerOptions>>().Value.ThrowOnBadRequest);
    }

    // ------------------------------------------------------------------ pipeline en memoria

    private sealed record Respuesta(int Estado, string? Titulo, Dictionary<string, string[]> Errores, object? Leido);

    private static Delegate Crear(Action<object> leer) => (CrearProyectoSolicitud s) => { leer(s); return Results.Ok(); };

    private static Delegate Personal(Action<object> leer) => (ActualizarPersonalSolicitud s) => { leer(s); return Results.Ok(); };

    private static Delegate Reactivar(Action<object> leer) => (ReactivarProyectoSolicitud s) => { leer(s); return Results.Ok(); };

    private static ServiceProvider Servicios()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMetrics();
        services.AddSingleton(new DiagnosticListener("App.Api.Tests"));
        services.AddErroresApi();
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// UseExceptionHandler + enlace del cuerpo como en un endpoint mínimo, con ThrowOnBadRequest = true (lo que hace
    /// Development por defecto y, desde la corrección, todos los entornos).
    /// </summary>
    private static async Task<Respuesta> EnviarAsync(Func<Action<object>, Delegate> manejador, string cuerpo,
        string contentType = "application/json")
    {
        await using var sp = Servicios();
        object? leido = null;
        var endpoint = RequestDelegateFactory.Create(manejador(s => leido = s),
            new RequestDelegateFactoryOptions { ServiceProvider = sp, ThrowOnBadRequest = true });

        var app = new ApplicationBuilder(sp);
        app.UseExceptionHandler();
        app.Run(endpoint.RequestDelegate);
        var pipeline = app.Build();

        var bytes = Encoding.UTF8.GetBytes(cuerpo);
        var contexto = new DefaultHttpContext { RequestServices = sp };
        contexto.Request.Method = HttpMethods.Post;
        contexto.Request.Path = "/api/proyectos/previsualizar";
        contexto.Request.ContentType = contentType;
        contexto.Request.ContentLength = bytes.Length;
        contexto.Request.Body = new MemoryStream(bytes);
        contexto.Features.Set<IHttpRequestBodyDetectionFeature>(new CuerpoDetectable(bytes.Length > 0));
        var salida = new MemoryStream();
        contexto.Response.Body = salida;

        await pipeline(contexto);

        var errores = new Dictionary<string, string[]>();
        string? titulo = null;
        if (salida.Length > 0 && contexto.Response.StatusCode >= 400)
        {
            using var doc = JsonDocument.Parse(salida.ToArray());
            titulo = doc.RootElement.TryGetProperty("title", out var t) ? t.GetString() : null;
            if (doc.RootElement.TryGetProperty("errors", out var e))
            {
                foreach (var p in e.EnumerateObject())
                {
                    errores[p.Name] = p.Value.EnumerateArray().Select(v => v.GetString()!).ToArray();
                }
            }
        }

        return new Respuesta(contexto.Response.StatusCode, titulo, errores, leido);
    }

    /// <summary>Como Kestrel: una solicitud con Content-Length 0 no tiene cuerpo.</summary>
    private sealed class CuerpoDetectable(bool tiene) : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => tiene;
    }
}
