using System.Text.Json;
using App.Application.Comun;
using App.Application.Empleados;
using App.Application.Erp;
using App.Application.Novedades.Sincronizacion;
using App.Application.Tests.Novedades;
using App.Application.Tests.Proyectos.Crear;
using App.Domain.Catalogos;

namespace App.Application.Tests.Novedades.Sincronizacion;

/// <summary>TAREA-23-3: mapeador (puro), plan (puro) y servicio con dobles. JSON y filas FICTICIOS (A6).</summary>
public sealed class SincronizacionTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    // ------------------------------------------------------------------ mapeador

    private static MapeoPermisoMedico.Resultado Mapear(string? json, string estado = "ACTIVA", string? codigo = "NOV-FICT-0001", string? uid = null, string? correoColumna = null) =>
        MapeoPermisoMedico.Mapear(new FilaNovedadExterna(1001, codigo, uid ?? JsonFicticio.Uid(1).ToString(), estado, json, correoColumna));

    [Fact]
    public void Mapeo_FechasEkon_PersonaEkon_YSoloCorreo()
    {
        var fechas = Mapear(JsonFicticio.Json(1, dias: ["2070-03-02", "2070-03-01", "2070-03-02"], fechasEkon: "DEV001", personaEkon: "DEV009")).Mapeado!;
        var persona = Mapear(JsonFicticio.Json(1, dias: ["2070-03-01"], personaEkon: " DEV005 ")).Mapeado!;
        var correo = Mapear(JsonFicticio.Json(1, dias: ["2070-03-01"], correo: " Empleado04.DEV@profesiograma.local ")).Mapeado!;
        var columna = Mapear(JsonFicticio.Json(1, dias: ["2070-03-01"]), correoColumna: "otro.ficticio@profesiograma.local").Mapeado!;

        Assert.Equal("DEV001", fechas.Ekon); // Fechas.Ekon gana
        Assert.Equal([new(2070, 3, 1), new(2070, 3, 2)], fechas.Dias); // ordenados y sin repetir
        Assert.Equal(("DEV005", (string?)null), (persona.Ekon, persona.Correo));
        Assert.Equal(((string?)null, "empleado04.dev@profesiograma.local"), (correo.Ekon, correo.Correo));
        Assert.Equal("otro.ficticio@profesiograma.local", columna.Correo); // CORREO_PERSONA como respaldo
        Assert.DoesNotContain("empleado04", correo.ToString()); // el correo de la persona no se imprime
    }

    [Theory]
    [InlineData("2.448", "2448")]   // puntos de miles (C3) [SUPUESTO]
    [InlineData("12.345", "12345")]
    [InlineData(" 9940 ", "9940")]
    [InlineData("DEV.01", "DEV.01")] // no es un número con miles: queda igual
    public void Mapeo_EkonNormalizado(string ekon, string esperado) =>
        Assert.Equal(esperado, Mapear(JsonFicticio.Json(1, dias: ["2070-03-01"], fechasEkon: ekon)).Mapeado!.Ekon);

    [Theory]
    [InlineData("{ no es json", null, "jsonInvalido")]
    [InlineData("[1, 2]", null, "jsonInvalido")]
    [InlineData(null, "VACACIONES", "tipoNoSoportado")]
    [InlineData(null, "ORIGEN", "otroOrigen")]
    [InlineData(null, "PROYECTO", "aplicacionNoSoportada")]
    [InlineData(null, "SIN_EMPLEADO", "sinEmpleado")]
    [InlineData(null, "FECHA_ILEGIBLE", "fechasInvalidas")]
    [InlineData(null, "RANGO_367", "fechasInvalidas")]
    [InlineData(null, "SIN_FECHAS", "fechasInvalidas")]
    public void Mapeo_Omisiones(string? jsonCrudo, string? caso, string categoria)
    {
        var json = jsonCrudo ?? caso switch
        {
            "VACACIONES" => JsonFicticio.Json(1, dias: ["2070-03-01"], fechasEkon: "DEV001", tipo: "VACACIONES"),
            "ORIGEN" => JsonFicticio.Json(1, dias: ["2070-03-01"], fechasEkon: "DEV001", origenPermiso: "aplicacionProyectos"),
            "PROYECTO" => JsonFicticio.Json(1, dias: ["2070-03-01"], fechasEkon: "DEV001", aplicacion: "PROYECTO"),
            "SIN_EMPLEADO" => JsonFicticio.Json(1, dias: ["2070-03-01"]),
            "FECHA_ILEGIBLE" => JsonFicticio.Json(1, dias: ["01/03/2070"], fechasEkon: "DEV001"),
            "RANGO_367" => JsonFicticio.Json(1, dias: null, fechasEkon: "DEV001", inicio: "2070-01-01", fin: "2071-01-02"),
            _ => JsonFicticio.Json(1, dias: null, fechasEkon: "DEV001"),
        };

        var r = Mapear(json);

        Assert.Null(r.Mapeado);
        Assert.Equal(new OmisionSincronizacion(categoria, "NOV-FICT-0001"), r.Omision);
    }

    [Fact]
    public void Mapeo_UidIlegible_YReferenciaSinCodigo()
    {
        var r = Mapear(JsonFicticio.Json(1, dias: ["2070-03-01"], fechasEkon: "DEV001"), codigo: null, uid: "no-es-guid");

        Assert.Equal(new OmisionSincronizacion("uidInvalido", "fila ID 1001"), r.Omision);
    }

    [Fact]
    public void Mapeo_SinDias_ExpandeElRango_TipoConGuionYTilde_EstadoNoActiva()
    {
        var m = Mapear(JsonFicticio.Json(1, dias: null, fechasEkon: "DEV006", inicio: "2070-04-10", fin: "2070-04-12", tipo: "permiso_médico"),
            estado: "  anulada ").Mapeado!;

        Assert.Equal([new(2070, 4, 10), new(2070, 4, 11), new(2070, 4, 12)], m.Dias);
        Assert.Equal((new DateOnly(2070, 4, 10), new DateOnly(2070, 4, 12), false), (m.FechaInicio, m.FechaFin, m.Activa));
        Assert.True(Mapear(JsonFicticio.Json(1, dias: ["2070-04-10"], fechasEkon: "DEV006"), estado: " activa ").Mapeado!.Activa);
    }

    [Fact]
    public void Mapeo_FechaRegistro_EcuadorAUtc_EIlegibleNull_YRegistrante()
    {
        var m = Mapear(JsonFicticio.Json(1, dias: ["2070-03-01"], fechasEkon: "DEV001", fechaRegistro: "2070-02-01 22:30:15")).Mapeado!;
        var sin = Mapear(JsonFicticio.Json(1, dias: ["2070-03-01"], fechasEkon: "DEV001", fechaRegistro: "01/02/2070")).Mapeado!;

        Assert.Equal(new DateTime(2070, 2, 2, 3, 30, 15, DateTimeKind.Utc), m.FechaRegistroUtc); // UTC-5
        Assert.Null(sin.FechaRegistroUtc);
        Assert.Equal(("REGISTRANTE FICTICIO", "registro.ficticio@profesiograma.local", "PLANTA"), (m.RegistradoPorNombre, m.RegistradoPorCorreo, m.OrigenArea));
    }

    [Fact]
    public void Mapeo_SinTitulo_CodigoGeneradoDeterminista()
    {
        var a = Mapear(JsonFicticio.Json(1, dias: ["2070-03-01"], fechasEkon: "DEV001"), codigo: " ").Mapeado!;
        var b = Mapear(JsonFicticio.Json(1, dias: ["2070-03-01"], fechasEkon: "DEV001"), codigo: null).Mapeado!;

        Assert.Equal("NOV-20700201-000001", a.Codigo); // fecha de registro (Ecuador) + 6 hex del Uid
        Assert.Equal(a.Codigo, b.Codigo);
    }

    [Fact]
    public void Mapeo_DetalleOrigen_ListaBlancaSinPersona_YOrdenEstable()
    {
        var m = Mapear(JsonFicticio.Json(1, dias: ["2070-03-01"], fechasEkon: "DEV001", correo: "secreto.ficticio@profesiograma.local")).Mapeado!;
        var otra = Mapear(JsonFicticio.Json(1, dias: ["2070-03-01"], fechasEkon: "DEV001", correo: "secreto.ficticio@profesiograma.local")).Mapeado!;

        using var doc = JsonDocument.Parse(m.DetalleOrigenJson);
        Assert.Equal(["NovedadUID", "TipoAplicacion", "TipoNovedad", "OrigenPermiso", "Origen", "Fechas", "Dias", "Registro"],
            doc.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(["FechaInicio", "FechaFin", "Ekon"], doc.RootElement.GetProperty("Fechas").EnumerateObject().Select(p => p.Name));
        Assert.Equal(["FechaRegistro"], doc.RootElement.GetProperty("Registro").EnumerateObject().Select(p => p.Name));
        Assert.DoesNotContain("PERSONA FICTICIA", m.DetalleOrigenJson);
        Assert.DoesNotContain("secreto.ficticio", m.DetalleOrigenJson);
        Assert.DoesNotContain("REGISTRANTE", m.DetalleOrigenJson);
        Assert.Equal(m.DetalleOrigenJson, otra.DetalleOrigenJson);
    }

    // ------------------------------------------------------------------ plan

    private static PermisoMedicoMapeado M(int n, string ekon = "DEV001", bool activa = true, string[]? dias = null, string? codigo = null) =>
        MapeoPermisoMedico.Mapear(JsonFicticio.Fila(n, JsonFicticio.Json(n, dias: dias ?? ["2070-03-01"], fechasEkon: ekon),
            activa ? "ACTIVA" : "ANULADA", codigo)).Mapeado!;

    private static NovedadExistente E(PermisoMedicoMapeado m, int empleadoId = 1, bool anulada = false, byte origen = CatalogoIds.OrigenNovedad.PermisosMedicos) =>
        new(50 + m.LegacyId!.Value, m.Uid, origen, m.Codigo, m.LegacyId, empleadoId, m.FechaInicio, m.FechaFin, m.Dias,
            m.OrigenArea, m.RegistradoPorNombre, m.RegistradoPorCorreo, m.DetalleOrigenJson, anulada);

    [Fact]
    public void Clasificar_Nueva_SinCambios_CambioDeFechas_Anular_Reactivar_NoActivaNueva()
    {
        var m = M(1);

        Assert.Equal(AccionSincronizacion.Nueva, PlanSincronizacion.Clasificar(m, 1, null));
        Assert.Equal(AccionSincronizacion.NoActivaSinRegistro, PlanSincronizacion.Clasificar(M(1, activa: false), 1, null));
        Assert.Equal(AccionSincronizacion.SinCambios, PlanSincronizacion.Clasificar(m, 1, E(m)));
        Assert.Equal(AccionSincronizacion.Actualizar, PlanSincronizacion.Clasificar(M(1, dias: ["2070-03-01", "2070-03-02"]), 1, E(m)));
        Assert.Equal(AccionSincronizacion.Actualizar, PlanSincronizacion.Clasificar(m, 2, E(m))); // otro empleado
        Assert.Equal(AccionSincronizacion.Anular, PlanSincronizacion.Clasificar(M(1, activa: false), 1, E(m)));
        Assert.Equal(AccionSincronizacion.SinCambios, PlanSincronizacion.Clasificar(M(1, activa: false), 1, E(m, anulada: true)));
        Assert.Equal(AccionSincronizacion.Reactivar, PlanSincronizacion.Clasificar(m, 1, E(m, anulada: true)));
    }

    [Fact]
    public void Armar_Conflictos_Ausentes_Y_SinEmpleado()
    {
        var nueva = M(1);
        var estable = M(2);
        var codigoAjeno = M(3);
        var legacyAjeno = M(4);
        var sinEmpleado = M(5, ekon: "ZZ999");
        var repetida = M(2);
        var dePlanta = M(6);
        var duenosCodigo = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase) { [codigoAjeno.Codigo] = Guid.NewGuid() };
        var duenosLegacy = new Dictionary<int, Guid> { [legacyAjeno.LegacyId!.Value] = Guid.NewGuid() };
        var existentes = new Dictionary<Guid, NovedadExistente>
        {
            [estable.Uid] = E(estable),
            [dePlanta.Uid] = E(dePlanta, origen: CatalogoIds.OrigenNovedad.Profesiograma),
        };

        var plan = PlanSincronizacion.Armar([nueva, estable, codigoAjeno, legacyAjeno, sinEmpleado, repetida, dePlanta, M(7, activa: false)],
            existentes, duenosCodigo, duenosLegacy, m => m.Ekon == "ZZ999" ? null : 1);

        Assert.Equal(1, plan.SinCambios);
        Assert.Equal([(nueva.Uid, AccionSincronizacion.Nueva)], plan.Escrituras.Select(e => (e.Item.Mapeado.Uid, e.Accion)));
        Assert.Equal(["conflictoCodigo", "conflictoLegacyId", "sinEmpleado", "uidInvalido", "conflictoCodigo", "noActivaSinRegistro"],
            plan.Omisiones.Select(o => o.Categoria));
    }

    // ------------------------------------------------------------------ servicio (dobles)

    private sealed class Escenario
    {
        public Escenario(params FilaNovedadExterna[] filas)
        {
            Fuente = new FuenteFalsa(filas) { Eventos = Eventos };
            Base = new SincronizacionFalsa(Eventos);
            Servicio = new SincronizacionPermisosMedicosServicio(Fuente, Base, new DatosFalsos(), Api, Transaccion, NovedadPrueba.Reloj);
        }

        public List<string> Eventos { get; } = [];
        public FuenteFalsa Fuente { get; }
        public SincronizacionFalsa Base { get; }
        public EmpleadosErpFalsos Api { get; } = new();
        public TransaccionFalsa Transaccion { get; } = new();
        public SincronizacionPermisosMedicosServicio Servicio { get; }

        public async Task<ResultadoSincronizacionDto> Sincronizar(string? escenario = null) =>
            (await Servicio.SincronizarAsync(escenario, Ct)).Valor!;
    }

    [Fact]
    public async Task R3_FuenteYBaseAntesDeLaTransaccion_SinApiSiTodosTienenFila()
    {
        var e = new Escenario(JsonFicticio.Valida(1), JsonFicticio.Valida(2, "DEV002"));

        var r = await e.Sincronizar();

        Assert.Equal((2, 0, 0), (r.Nuevas, r.SinCambios, e.Api.LecturasFrescas));
        Assert.Equal("fuente", e.Eventos[0]);
        Assert.Equal("lote", e.Eventos[^1]);
        Assert.Equal(1, e.Transaccion.Iniciadas);
        Assert.Equal([1, 2], e.Base.Lotes[0].Items.Select(i => i.EmpleadoId));
        Assert.Empty(e.Base.Lotes[0].Altas);
    }

    [Fact]
    public async Task FuenteCaidaONoConfigurada_SeLanzaSinEscribir()
    {
        var e = new Escenario(JsonFicticio.Valida(1));
        e.Fuente.Error = FuenteNovedadesNoDisponibleException.NoConfigurada();

        var ex = await Assert.ThrowsAsync<FuenteNovedadesNoDisponibleException>(() => e.Servicio.SincronizarAsync(null, Ct));

        Assert.Equal(FuenteNovedadesNoDisponibleException.TituloNoConfigurada, ex.Titulo);
        Assert.Equal(0, e.Transaccion.Iniciadas);
        Assert.Equal(["fuente"], e.Eventos);
    }

    [Fact]
    public async Task MasDeMaxFilas_503()
    {
        var e = new Escenario(Enumerable.Range(1, FuenteNovedades.MaxFilas + 1).Select(n => JsonFicticio.Fila(n, "{}")).ToArray());

        var ex = await Assert.ThrowsAsync<FuenteNovedadesNoDisponibleException>(() => e.Servicio.SincronizarAsync(null, Ct));

        Assert.Equal(FuenteNovedadesNoDisponibleException.TituloNoDisponible, ex.Titulo);
        Assert.Equal(0, e.Transaccion.Iniciadas);
    }

    [Fact]
    public async Task ApiCaida_CuandoHaceFalta_503SinEscribir()
    {
        var e = new Escenario(JsonFicticio.Valida(1), JsonFicticio.Valida(2, "SIM001")); // SIM001 sin fila: hace falta la API
        e.Api.Error = new ErpNoDisponibleException("empleados", "caída");

        await Assert.ThrowsAsync<ErpNoDisponibleException>(() => e.Servicio.SincronizarAsync(null, Ct));

        Assert.Equal(0, e.Transaccion.Iniciadas);
        Assert.DoesNotContain("lote", e.Eventos);
    }

    [Fact]
    public async Task A4_ApiUnaSolaVez_AltaPuntualConCedula_YSinFilaInactivo_SinEmpleado()
    {
        var e = new Escenario(JsonFicticio.Valida(1, "SIM001"), JsonFicticio.Valida(2, "SIM001"), JsonFicticio.Valida(3, "ZZ999"));
        var api = new FuenteEmpleadosConCedulas(); // SIM001 activo, con cédula ficticia en la lectura fresca
        var servicio = new SincronizacionPermisosMedicosServicio(e.Fuente, e.Base, new DatosFalsos(), api, e.Transaccion, NovedadPrueba.Reloj);

        var r = (await servicio.SincronizarAsync(null, Ct)).Valor!;

        Assert.Equal(1, api.LecturasFrescas);
        Assert.Equal(2, r.Nuevas);
        Assert.Equal(1, r.Omitidas["sinEmpleado"]); // ZZ999: sin fila y no activo en la API
        var lote = e.Base.Lotes.Single();
        Assert.Equal("SIM001", Assert.Single(lote.Altas).CodigoEkon);
        Assert.All(lote.Items, i => Assert.Equal(lote.Altas[0].Id, i.EmpleadoId)); // mismo Id temporal
        Assert.Equal(FuenteEmpleadosConCedulas.CedulaFicticia, lote.Cedulas.De("SIM001"));
    }

    [Fact]
    public async Task Idempotencia_SegundaCorridaSinCambios_NoAbreTransaccion()
    {
        var filas = new[] { JsonFicticio.Valida(1), JsonFicticio.Valida(2, "DEV002") };
        var e = new Escenario(filas);
        foreach (var f in filas)
        {
            var m = MapeoPermisoMedico.Mapear(f).Mapeado!;
            e.Base.Existentes[m.Uid] = E(m, f == filas[0] ? 1 : 2);
        }

        var r = await e.Sincronizar();

        Assert.Equal((0, 2, 0), (r.Nuevas, r.SinCambios, e.Transaccion.Iniciadas));
        Assert.Empty(e.Base.Lotes);
    }

    [Fact]
    public async Task LotesDe100()
    {
        var e = new Escenario(Enumerable.Range(1, 250).Select(n => JsonFicticio.Valida(n)).ToArray());

        var r = await e.Sincronizar();

        Assert.Equal([100, 100, 50], e.Base.Lotes.Select(l => l.Items.Count));
        Assert.Equal((250, 3, 3), (r.Nuevas, e.Transaccion.Iniciadas, e.Transaccion.Confirmadas));
        Assert.Equal(r, e.Servicio.Parcial);
    }

    [Fact]
    public async Task A1_UidNuevoQueYaExisteAlEscribir_NoSeDuplica()
    {
        var fila = JsonFicticio.Valida(1);
        var m = MapeoPermisoMedico.Mapear(fila).Mapeado!;
        var e = new Escenario(fila);
        e.Base.EnBase[m.Uid] = E(m); // otra sincronización la insertó entre la lectura y la transacción

        var r = await e.Sincronizar();

        Assert.Equal((0, 1), (r.Nuevas, r.SinCambios));
    }

    [Fact]
    public async Task P2_SoloCorreo_FilaUnica_Ambigua_Y_Api()
    {
        var unica = JsonFicticio.Fila(1, JsonFicticio.Json(1, dias: ["2070-03-01"], correo: "empleado04.dev@profesiograma.local"));
        var ambigua = JsonFicticio.Fila(2, JsonFicticio.Json(2, dias: ["2070-03-02"], correo: "repetido.ficticio@profesiograma.local"));
        var porApi = JsonFicticio.Fila(3, JsonFicticio.Json(3, dias: ["2070-03-03"], correo: "solo.api.ficticio@profesiograma.local"));
        var e = new Escenario(unica, ambigua, porApi);
        e.Base.PorCorreo.AddRange([
            new EmpleadoPorCorreo(4, "DEV004", "EMPLEADO04.DEV@profesiograma.local"),
            new EmpleadoPorCorreo(5, "DEV005", "repetido.ficticio@profesiograma.local"),
            new EmpleadoPorCorreo(6, "DEV006", "repetido.ficticio@profesiograma.local")]);
        var api = new ApiConCorreo();
        var servicio = new SincronizacionPermisosMedicosServicio(e.Fuente, e.Base, new DatosFalsos(), api, e.Transaccion, NovedadPrueba.Reloj);

        var r = (await servicio.SincronizarAsync(null, Ct)).Valor!;

        Assert.Equal((2, 1), (r.Nuevas, r.Omitidas["sinEmpleado"]));
        Assert.Equal([4, 7], e.Base.Lotes.Single().Items.Select(i => i.EmpleadoId)); // fila por correo; API → DEV007, que tiene fila
        Assert.Equal(1, api.Lecturas);
    }

    /// <summary>API con DEV007 cuyo correo es solo.api.ficticio@… (en la base, DEV007 tiene otro correo).</summary>
    private sealed class ApiConCorreo : IFuenteEmpleadosErp
    {
        public int Lecturas { get; private set; }
        public Task<ListaEmpleadosErp> ObtenerActivosAsync(CancellationToken ct) => throw new InvalidOperationException("Solo lectura fresca.");
        public Task<ListaEmpleadosErp> ObtenerActivosFrescosAsync(CancellationToken ct)
        {
            Lecturas++;
            return Task.FromResult(new ListaEmpleadosErp(
                [new EmpleadoErp("DEV007", "EMPLEADO PRUEBA 07", null, null, "Solo.Api.Ficticio@profesiograma.local", null, null, null, "PUESTO 7",
                    null, null, null, null, null, null, null, null, null, "A")], DateTimeOffset.UnixEpoch));
        }
    }

    [Fact]
    public async Task Resultado_Omitidas_FilasIlegibles_Ausentes_YCodigos()
    {
        var e = new Escenario(JsonFicticio.Valida(1), JsonFicticio.Fila(2, "{ roto"), JsonFicticio.Fila(3, JsonFicticio.Json(3, dias: ["2070-03-03"], fechasEkon: "DEV001", tipo: "VACACIONES")));
        e.Fuente.FilasIlegibles = 2;
        e.Base.UidsPermisosMedicos.AddRange([JsonFicticio.Uid(1), JsonFicticio.Uid(2), Guid.NewGuid()]); // la última no viene en la fuente

        var r = await e.Sincronizar();

        Assert.Equal((5, 1, 1), (r.Leidas, r.Nuevas, r.AusentesEnOrigen));
        Assert.Equal(new Dictionary<string, int> { ["jsonInvalido"] = 1, ["tipoNoSoportado"] = 1, ["filaIlegible"] = 2 }, r.Omitidas);
        Assert.Equal(["NOV-FICT-0002", "NOV-FICT-0003"], r.CodigosOmitidos);
        Assert.Equal(new DateTime(2026, 10, 22, 3, 0, 0, DateTimeKind.Utc), r.SincronizadaUtc);
    }

    [Theory]
    [InlineData("otro", true, "El escenario debe ser base, fechas, anuladas.")]
    [InlineData("base", false, "El escenario solo se admite con la fuente Simulada.")]
    public async Task Escenario_400(string escenario, bool admite, string mensaje)
    {
        var e = new Escenario(JsonFicticio.Valida(1));
        e.Fuente.Escenarios = admite;

        var r = await e.Servicio.SincronizarAsync(escenario, Ct);

        Assert.Equal([mensaje], r.Errores!["escenario"]);
        Assert.Empty(e.Eventos); // ni siquiera se lee la fuente
    }

    [Fact]
    public async Task Escenario_SeNormalizaYSePasaALaFuente()
    {
        var e = new Escenario(JsonFicticio.Valida(1));

        await e.Sincronizar(" FECHAS ");

        Assert.Equal("fechas", e.Fuente.EscenarioRecibido);
    }
}
