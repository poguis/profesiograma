using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using App.Application.Empleados;
using App.Application.Erp;
using App.Infrastructure.Erp;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Tests.Erp;

/// <summary>
/// Cliente de EvolutionEmployee (TAREA-26b, conservado en la 26d como descarga de la caché) contra un HttpMessageHandler falso. Todos los datos son FICTICIOS; la forma
/// (envoltura y nombres de campos, incluidos los sensibles) es la observada en el sondeo de la TAREA-26a.
/// </summary>
public class FuenteEmpleadosErpHttpTests
{
    private const string Base7048 = "https://erp.prueba.local:7048";
    private static readonly CancellationToken Ct = CancellationToken.None;

    /// <summary>Campos sensibles de la API: nunca deben llegar al resultado (C31).</summary>
    private static readonly string[] CamposSensibles =
    [
        "cedula", "cedulaReportaA", "telefono", "mailPersonal", "provincia", "canton", "barrio", "callePrincipal",
        "calleSecundaria", "numeroCasa", "fechaNacimiento", "fechaAntiguedad", "salario", "bpr", "sexo", "nivelDireccion",
        "nombresReportaA", "reportaA",
    ];

    private const string JsonEmpleados = """
        {"statusCode":200,"isSuccess":true,"errorMessages":null,"result":[
          {"nombrecompleto":"PERSONA FICTICIA UNO","apellidos":"APELLIDO FICTICIO","nombres":"PERSONA UNO","codEmpresa":"EF1",
           "empresa":"EMPRESA FICTICIA","codPersona":"90001","mailEmpresa":"uno@ficticio.local","telefono":"SENSIBLE-TELEFONO",
           "mailPersonal":"SENSIBLE-MAIL","provincia":"SENSIBLE-PROVINCIA","canton":"SENSIBLE-CANTON","cedula":"SENSIBLE-CEDULA",
           "codPosicion":"X1","posicion":"POSICION FICTICIA","codPerfil":"F1","codPuesto":"P1","puesto":"PUESTO FICTICIO",
           "codDepartamento":"D1","departamento":"UNIDAD SISTEMA INTEGRADO DE GESTION","codPosicionJefe":"X0",
           "nombresReportaA":"SENSIBLE-JEFE","cedulaReportaA":"SENSIBLE-CEDULA-JEFE","reportaA":"SENSIBLE-REPORTA",
           "codFamilia":"F1","familiaPuesto":"OPERATIVO","codSeccion":"S1","seccion":"SECCIÓN FICTICIA","codArea":"A1",
           "area":"ÁREA FICTICIA","codUnidad":"U1","unidad":"UNIDAD FICTICIA","fechaNacimiento":"SENSIBLE-1900-01-01",
           "fechaAntiguedad":"SENSIBLE-ANTIGUEDAD","fechaIngreso":"2020-01-01T00:00:00","fechaSalida":null,
           "barrio":"SENSIBLE-BARRIO","callePrincipal":"SENSIBLE-CALLE","calleSecundaria":"SENSIBLE-CALLE2","numeroCasa":"SENSIBLE-CASA",
           "fechaIniContrato":"2020-01-01T00:00:00","fechaFinContrato":null,"tipoContrato":"INDEFINIDO","codNivelDir":"N1",
           "nivelDireccion":"SENSIBLE-NIVEL","codCargoTipo":"C1","cargoTipo":"OPERATIVO","salario":99999.99,"sexo":"SENSIBLE-SEXO",
           "estado":"A","bpr":12345},
          {"nombrecompleto":"PERSONA FICTICIA DOS","codPersona":90002,"codEmpresa":"EF2","estado":"A","codPuesto":7}]}
        """;

    [Fact]
    public async Task Mapeo_SoloCamposPermitidos_CodigosTextoONumero_YCuerpoFijo()
    {
        var (fuente, manejador) = Crear(_ => Json(JsonEmpleados));

        var empleados = await fuente.DescargarAsync(Ct);

        Assert.Equal(2, empleados.Count);
        Assert.Equal(new EmpleadoErp("90001", "PERSONA FICTICIA UNO", "APELLIDO FICTICIO", "PERSONA UNO", "uno@ficticio.local", "EF1",
            "EMPRESA FICTICIA", "P1", "PUESTO FICTICIO", "D1", "UNIDAD SISTEMA INTEGRADO DE GESTION", "U1", "UNIDAD FICTICIA", "A1",
            "ÁREA FICTICIA", "S1", "SECCIÓN FICTICIA", "OPERATIVO", "A"), empleados[0]);
        Assert.Equal(("90002", "PERSONA FICTICIA DOS", "7"), (empleados[1].CodigoEkon, empleados[1].NombreCompleto, empleados[1].CodPuesto));

        Assert.Equal($"{Base7048}/api/EvolutionEmployee/EmployeesEvolution", manejador.Uri!.ToString());
        Assert.Equal(HttpMethod.Post, manejador.Metodo);
        Assert.Equal("""{"parameter":"","estado":"A","codEmpresa":"","codDepartamento":""}""", manejador.Cuerpo);
    }

    [Fact]
    public async Task DatosSensibles_NoExistenEnElResultado_NiEnLosTiposDelContrato()
    {
        var (fuente, _) = Crear(_ => Json(JsonEmpleados));

        var empleados = await fuente.DescargarAsync(Ct);

        // Ningún valor sensible ficticio llega al resultado.
        var serializado = JsonSerializer.Serialize(empleados);
        Assert.DoesNotContain("SENSIBLE", serializado);

        // Ni el registro de Application ni el DTO interno del cliente tienen propiedades sensibles.
        foreach (var tipo in new[] { typeof(EmpleadoErp), typeof(DatosEmpleado), typeof(FuenteEmpleadosErpHttp.EmpleadoJson) })
        {
            var propiedades = tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name).ToList();
            foreach (var sensible in CamposSensibles)
            {
                Assert.DoesNotContain(propiedades, p => string.Equals(p, sensible, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Theory]
    [InlineData("""{"statusCode":200,"isSuccess":false,"errorMessages":["x"],"result":null}""")]
    [InlineData("""{"statusCode":200,"isSuccess":true,"errorMessages":null,"result":null}""")]
    [InlineData("""{"statusCode":200,"isSuccess":true,"errorMessages":null,"result":[]}""")] // TAREA-26d: vacío = no disponible
    [InlineData("""{"statusCode":200,"isSuccess":true,"errorMessages":null,"result":[{"codPersona":{"objeto":1}}]}""")]
    [InlineData("""no es json""")]
    public async Task RespuestaSinResultadosOInvalida_NoDisponible_SinElCuerpoEnElMensaje(string cuerpo)
    {
        var logger = new LoggerFalso();
        var (fuente, _) = Crear(_ => Json(cuerpo), logger);

        var ex = await Assert.ThrowsAsync<ErpNoDisponibleException>(() => fuente.DescargarAsync(Ct));

        Assert.Equal("empleados", ex.Operacion);
        Assert.Null(ex.InnerException);
        Assert.DoesNotContain("objeto", ex.Message);
        Assert.All(logger.Mensajes, m => Assert.DoesNotContain("objeto", m));
    }

    [Fact]
    public async Task Http500_NoDisponible_YLogDebugSinCuerpoNiUrl()
    {
        var logger = new LoggerFalso();
        var (fuente, _) = Crear(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError) { Content = new StringContent("SENSIBLE") }, logger);

        var ex = await Assert.ThrowsAsync<ErpNoDisponibleException>(() => fuente.DescargarAsync(Ct));

        Assert.Contains("HTTP 500", ex.Message);
        Assert.Contains(logger.Mensajes, m => m.StartsWith("ERP empleados: HTTP 500 en ", StringComparison.Ordinal));
        Assert.All(logger.Mensajes, m => Assert.DoesNotContain("SENSIBLE", m));
        Assert.All(logger.Mensajes, m => Assert.DoesNotContain("erp.prueba.local", m));
    }

    [Fact]
    public async Task ErrorDeRed_NoDisponible()
    {
        var (fuente, _) = Crear(_ => throw new HttpRequestException("sin red"));

        var ex = await Assert.ThrowsAsync<ErpNoDisponibleException>(() => fuente.DescargarAsync(Ct));

        Assert.Contains("error de red", ex.Message);
    }

    [Fact]
    public void Opciones_PorDefecto()
    {
        var opciones = new ServiciosExternosOpciones();

        Assert.Equal((60, 10, 60), (opciones.TimeoutEmpleadosSegundos, opciones.CacheEmpleadosMinutos, opciones.CacheEmpleadosMaxAntiguedadMinutos));
    }

    // ------------------------------------------------------------------ utilidades

    private static (FuenteEmpleadosErpHttp Fuente, ManejadorFalso Manejador) Crear(
        Func<HttpRequestMessage, HttpResponseMessage> responder, ILogger<FuenteEmpleadosErpHttp>? logger = null)
    {
        var manejador = new ManejadorFalso(responder);
        var opciones = new ServiciosExternosOpciones { Modo = "Http", ErpBase7048 = Base7048, ErpBase7055 = "https://erp.prueba.local:7055" };
        return (new FuenteEmpleadosErpHttp(new HttpClient(manejador), opciones, logger ?? new LoggerFalso()), manejador);
    }

    private static HttpResponseMessage Json(string cuerpo) =>
        new(HttpStatusCode.OK) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };

    private sealed class ManejadorFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public Uri? Uri { get; private set; }
        public HttpMethod? Metodo { get; private set; }
        public string? Cuerpo { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Uri = request.RequestUri;
            Metodo = request.Method;
            Cuerpo = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return responder(request);
        }
    }

    private sealed class LoggerFalso : ILogger<FuenteEmpleadosErpHttp>
    {
        public List<string> Mensajes { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Mensajes.Add(formatter(state, exception) + (exception is null ? string.Empty : " " + exception.Message));
    }
}
