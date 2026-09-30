using System.Net;
using System.Text;
using App.Application.Erp;
using App.Infrastructure.Erp;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace App.Infrastructure.Tests.Erp;

/// <summary>
/// CatalogoErpHttp contra un HttpMessageHandler falso. Todos los datos son FICTICIOS; la forma del JSON
/// (envoltura / arreglo directo y nombres de campos) es la del ERP real (ConsultarProyecto.json + verificación TAREA-11).
/// </summary>
public class CatalogoErpHttpTests
{
    private const string Base7048 = "https://erp.prueba.local:7048";
    private const string Base7055 = "https://erp.prueba.local:7055";
    private static readonly CancellationToken Ct = CancellationToken.None;

    private const string JsonCompanias = """
        {"statusCode":200,"isSuccess":true,"errorMessages":null,"result":[
          {"companyId":9001,"name":"PRUEBA","tradeName":"COMPAÑÍA DE PRUEBA S.A.","ruc":"0999999999001"},
          {"companyId":9002,"name":"OTRA","tradeName":null,"ruc":null}]}
        """;

    private const string JsonProyectos = """
        [{"projectId":"DEV-ERP-001","companyId":9001,"description":"PROYECTO ERP DE PRUEBA","status":"Activo","sectorId":"S1","sectorDescription":null,"technicalOfficeId":"T1","technicalOfficeName":"X","uploadDate":"2026-01-01"},
         {"projectId":"DEV-ERP-002","companyId":9001,"description":"PROYECTO INACTIVO","status":"Inactivo","sectorId":"S1","sectorDescription":null,"technicalOfficeId":"T1","technicalOfficeName":"X","uploadDate":"2026-01-01"},
         {"projectId":"DEV-ERP-003","companyId":9001,"description":"OTRO ACTIVO","status":"ACTIVO","sectorId":"S1","sectorDescription":null,"technicalOfficeId":"T1","technicalOfficeName":"X","uploadDate":"2026-01-01"}]
        """;

    private const string JsonDimensiones = """
        [{"companyId":9001,"uegpId":"DEV-DIM-01","description":"PLANTA DE PRUEBA","uploadDate":"2026-01-01"},
         {"companyId":9001,"uegpId":"DEV-DIM-02","description":"OFICINAS DE PRUEBA","uploadDate":"2026-01-01"}]
        """;

    private const string JsonActividades = """
        {"statusCode":200,"isSuccess":true,"errorMessages":null,"result":[
          {"projectId":"DEV-ERP-001","activityId":"DEV.01","description":"ACTIVIDAD DE PRUEBA","activityType":"PRUEBA"}]}
        """;

    private const string JsonHorarios = """
        {"statusCode":200,"isSuccess":true,"errorMessages":null,"result":[
          {"codHorario":1,"descripcion":"07:00 - 18:00 (PRUEBA)","horaEntrada":"0700","horaSalida":"1800","horas":"1100","horasTrab":"1000","tipoHorario":"M","status":"A"},
          {"codHorario":3,"descripcion":"INACTIVO","horaEntrada":"0600","horaSalida":"1400","horas":"0800","horasTrab":"0700","tipoHorario":"D","status":"I"}]}
        """;

    [Fact]
    public async Task Companias_NombreEsTradeNameONameYNombreCortoEsName()
    {
        var (catalogo, manejador) = Crear(_ => Json(JsonCompanias));

        var companias = await catalogo.ListarCompaniasAsync(Ct);

        Assert.Equal(
            [new CompaniaErp(9001, "COMPAÑÍA DE PRUEBA S.A.", "PRUEBA", "0999999999001"), new CompaniaErp(9002, "OTRA", "OTRA", null)],
            companias);
        Assert.Equal($"{Base7048}/api/Company/list_company", manejador.Solicitudes.Single().ToString());
    }

    [Fact]
    public async Task Proyectos_SoloActivos_SinDistinguirMayusculas()
    {
        var (catalogo, manejador) = Crear(_ => Json(JsonProyectos));

        var proyectos = await catalogo.ListarProyectosAsync(9001, Ct);

        Assert.Equal(
            [new ProyectoErp("DEV-ERP-001", "PROYECTO ERP DE PRUEBA", "Activo"), new ProyectoErp("DEV-ERP-003", "OTRO ACTIVO", "ACTIVO")],
            proyectos);
        Assert.Equal($"{Base7048}/api/Project/GetProject/9001", manejador.Solicitudes.Single().ToString());
    }

    [Fact]
    public async Task Dimensiones_Mapeo()
    {
        var (catalogo, manejador) = Crear(_ => Json(JsonDimensiones));

        Assert.Equal(
            [new DimensionErp("DEV-DIM-01", "PLANTA DE PRUEBA"), new DimensionErp("DEV-DIM-02", "OFICINAS DE PRUEBA")],
            await catalogo.ListarDimensionesAsync(9001, Ct));
        Assert.Equal($"{Base7048}/api/Uegp/GetUegpCompany/9001", manejador.Solicitudes.Single().ToString());
    }

    [Fact]
    public async Task Actividades_RutaProyectoLuegoCompania_YMapeo()
    {
        var (catalogo, manejador) = Crear(_ => Json(JsonActividades));

        Assert.Equal([new ActividadErp("DEV.01", "ACTIVIDAD DE PRUEBA", "PRUEBA")], await catalogo.ListarActividadesAsync(9001, "DEV-ERP-001", Ct));
        Assert.Equal($"{Base7055}/api/Activity/GetActivitiesProject/DEV-ERP-001/9001", manejador.Solicitudes.Single().ToString());
    }

    [Fact]
    public async Task Actividades_404_ListaVacia_ComoElFlujoOriginal()
    {
        var (catalogo, _) = Crear(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        Assert.Empty(await catalogo.ListarActividadesAsync(9001, "DEV-ERP-001", Ct));
    }

    [Fact]
    public async Task Horarios_SoloStatusA_YHorasConvertidas()
    {
        var (catalogo, manejador) = Crear(_ => Json(JsonHorarios));

        var horarios = await catalogo.ListarHorariosAsync(Ct);

        Assert.Equal([new HorarioErp(1, "07:00 - 18:00 (PRUEBA)", new TimeOnly(7, 0), new TimeOnly(18, 0), 660, 600, "M")], horarios);
        Assert.Equal($"{Base7055}/api/PayrollSchedule/ListPayrollSchedule", manejador.Solicitudes.Single().ToString());
    }

    [Fact]
    public async Task Cache_DosLlamadas_UnaSolaSolicitudHttp()
    {
        var (catalogo, manejador) = Crear(_ => Json(JsonCompanias));

        await catalogo.ListarCompaniasAsync(Ct);
        await catalogo.ListarCompaniasAsync(Ct);
        Assert.NotNull(await catalogo.ObtenerCompaniaAsync(9001, Ct));

        Assert.Single(manejador.Solicitudes);
    }

    [Fact]
    public async Task ObtenerPorId_UsaElListado()
    {
        var (catalogo, _) = Crear(req => req.RequestUri!.AbsolutePath switch
        {
            var p when p.Contains("GetProject") => Json(JsonProyectos),
            var p when p.Contains("GetUegpCompany") => Json(JsonDimensiones),
            var p when p.Contains("GetActivitiesProject") => Json(JsonActividades),
            var p when p.Contains("ListPayrollSchedule") => Json(JsonHorarios),
            _ => Json(JsonCompanias),
        });

        Assert.Equal("PRUEBA", (await catalogo.ObtenerCompaniaAsync(9001, Ct))!.NombreCorto);
        Assert.Null(await catalogo.ObtenerCompaniaAsync(1234, Ct));
        Assert.NotNull(await catalogo.ObtenerProyectoAsync(9001, "dev-erp-001", Ct));   // sin distinguir mayúsculas
        Assert.Null(await catalogo.ObtenerProyectoAsync(9001, "DEV-ERP-002", Ct));      // inactivo
        Assert.Equal("OFICINAS DE PRUEBA", (await catalogo.ObtenerDimensionAsync(9001, "DEV-DIM-02", Ct))!.Descripcion);
        Assert.Equal("DEV.01", (await catalogo.ObtenerActividadAsync(9001, "DEV-ERP-001", "DEV.01", Ct))!.Id);
        Assert.NotNull(await catalogo.ObtenerHorarioAsync(1, Ct));
        Assert.Null(await catalogo.ObtenerHorarioAsync(3, Ct));                          // inactivo
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.BadRequest)]
    public async Task RespuestaNoExitosa_ErpNoDisponible(HttpStatusCode estado)
    {
        var (catalogo, _) = Crear(_ => new HttpResponseMessage(estado));
        var ex = await Assert.ThrowsAsync<ErpNoDisponibleException>(() => catalogo.ListarCompaniasAsync(Ct));
        Assert.Contains($"HTTP {(int)estado}", ex.Message);
    }

    [Fact]
    public async Task Actividades_500_ErpNoDisponible()
    {
        var (catalogo, _) = Crear(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        await Assert.ThrowsAsync<ErpNoDisponibleException>(() => catalogo.ListarActividadesAsync(9001, "DEV-ERP-001", Ct));
    }

    [Fact]
    public async Task ErrorDeRed_ErpNoDisponible()
    {
        var (catalogo, _) = Crear(_ => throw new HttpRequestException("sin conexión"));
        var ex = await Assert.ThrowsAsync<ErpNoDisponibleException>(() => catalogo.ListarHorariosAsync(Ct));
        Assert.IsType<HttpRequestException>(ex.InnerException);
    }

    [Fact]
    public async Task Timeout_ErpNoDisponible()
    {
        var (catalogo, _) = Crear(_ => throw new TaskCanceledException("timeout"));
        var ex = await Assert.ThrowsAsync<ErpNoDisponibleException>(() => catalogo.ListarHorariosAsync(Ct));
        Assert.Contains("tiempo de espera", ex.Message);
    }

    [Fact]
    public async Task CancelacionDelLlamador_NoSeConvierteEnErpNoDisponible()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var (catalogo, _) = Crear(_ => Json(JsonCompanias));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => catalogo.ListarCompaniasAsync(cts.Token));
    }

    [Theory]
    [InlineData("no es json")]
    [InlineData("")]
    [InlineData("null")]
    public async Task JsonInvalido_ErpNoDisponible(string cuerpo)
    {
        var (catalogo, _) = Crear(_ => Json(cuerpo));
        await Assert.ThrowsAsync<ErpNoDisponibleException>(() => catalogo.ListarProyectosAsync(9001, Ct));
    }

    [Fact]
    public async Task ErrorNoSeCachea_ElSiguienteIntentoVuelveAConsultar()
    {
        var intentos = 0;
        var (catalogo, manejador) = Crear(_ => ++intentos == 1 ? new HttpResponseMessage(HttpStatusCode.InternalServerError) : Json(JsonCompanias));

        await Assert.ThrowsAsync<ErpNoDisponibleException>(() => catalogo.ListarCompaniasAsync(Ct));
        Assert.Equal(2, (await catalogo.ListarCompaniasAsync(Ct)).Count);
        Assert.Equal(2, manejador.Solicitudes.Count);
    }

    // ------------------------------------------------------------------ utilidades

    private static (CatalogoErpHttp Catalogo, ManejadorFalso Manejador) Crear(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var manejador = new ManejadorFalso(responder);
        var opciones = new ServiciosExternosOpciones { Modo = "Http", ErpBase7048 = Base7048, ErpBase7055 = Base7055 + "/" };
        var catalogo = new CatalogoErpHttp(new HttpClient(manejador), new MemoryCache(new MemoryCacheOptions()), opciones,
            NullLogger<CatalogoErpHttp>.Instance);
        return (catalogo, manejador);
    }

    private static HttpResponseMessage Json(string cuerpo) =>
        new(HttpStatusCode.OK) { Content = new StringContent(cuerpo, Encoding.UTF8, "application/json") };

    private sealed class ManejadorFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<Uri> Solicitudes { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Solicitudes.Add(request.RequestUri!);
            return Task.FromResult(responder(request));
        }
    }
}
