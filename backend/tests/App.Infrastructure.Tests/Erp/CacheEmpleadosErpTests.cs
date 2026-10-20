using App.Application.Empleados;
using App.Application.Erp;
using App.Infrastructure.Erp;
using App.Infrastructure.Persistencia.DatosPrueba;
using Microsoft.Extensions.Logging.Abstractions;

namespace App.Infrastructure.Tests.Erp;

/// <summary>Caché de empleados (TAREA-26d: P3 TTL, descarga única, P4 lista anterior, P5 lectura fresca) y modo Simulado.</summary>
public class CacheEmpleadosErpTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static EmpleadoErp Erp(string codigo, string estado = "A") =>
        new(codigo, $"PERSONA {codigo}", null, null, null, null, null, null, "TECNICO", null, null, null, null, null, null, null, null, null, estado);

    private sealed class RelojMovible : TimeProvider
    {
        public DateTimeOffset Ahora { get; set; } = new(2026, 10, 8, 15, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Ahora;
    }

    private sealed class DescargaFalsa : IDescargaEmpleadosErp
    {
        public int Llamadas;
        public Exception? Error { get; set; }
        public TaskCompletionSource? Pausa { get; set; }
        public IReadOnlyList<EmpleadoErp> Respuesta { get; set; } = [Erp("1"), Erp("2"), Erp("3", estado: "I"), Erp(" ")];

        public async Task<IReadOnlyList<EmpleadoErp>> DescargarAsync(CancellationToken ct)
        {
            Interlocked.Increment(ref Llamadas);
            if (Pausa is not null)
            {
                await Pausa.Task;
            }

            return Error is null ? Respuesta : throw Error;
        }
    }

    private static (CacheEmpleadosErp Cache, DescargaFalsa Descarga, RelojMovible Reloj) Crear()
    {
        var descarga = new DescargaFalsa();
        var reloj = new RelojMovible();
        var opciones = new ServiciosExternosOpciones { CacheEmpleadosMinutos = 10, CacheEmpleadosMaxAntiguedadMinutos = 60 };
        return (new CacheEmpleadosErp(() => descarga, opciones, reloj, NullLogger<CacheEmpleadosErp>.Instance), descarga, reloj);
    }

    [Fact]
    public async Task Ttl_DentroNoDescarga_DespuesSi_SoloGuardaValidos()
    {
        var (cache, descarga, reloj) = Crear();

        var primera = await cache.ObtenerActivosAsync(Ct);
        reloj.Ahora = reloj.Ahora.AddMinutes(9);
        await cache.ObtenerActivosAsync(Ct);
        reloj.Ahora = reloj.Ahora.AddMinutes(2);
        await cache.ObtenerActivosAsync(Ct);

        Assert.Equal(2, descarga.Llamadas);
        Assert.Equal(["1", "2"], primera.Activos.Select(e => e.CodigoEkon)); // el inactivo y el sin código no entran
        Assert.False(primera.EsAnterior);
    }

    [Fact]
    public async Task DescargaUnica_ConLlamadasSimultaneas()
    {
        var (cache, descarga, _) = Crear();
        descarga.Pausa = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var tareas = Enumerable.Range(0, 10).Select(_ => Task.Run(() => cache.ObtenerActivosAsync(Ct))).ToList();
        await Task.Delay(100, TestContext.Current.CancellationToken);
        descarga.Pausa.SetResult();
        var listas = await Task.WhenAll(tareas);

        Assert.Equal(1, descarga.Llamadas);
        Assert.All(listas, l => Assert.Same(listas[0].Activos, l.Activos));
    }

    [Fact]
    public async Task P4_ApiCaida_ListaAnteriorConAviso_SinReintentarEnElMinuto_YMasDe60MinError()
    {
        var (cache, descarga, reloj) = Crear();
        await cache.ObtenerActivosAsync(Ct);
        descarga.Error = new ErpNoDisponibleException("empleados", "sin red");

        reloj.Ahora = reloj.Ahora.AddMinutes(30);
        var anterior = await cache.ObtenerActivosAsync(Ct);
        var otraVez = await cache.ObtenerActivosAsync(Ct); // menos de 1 min después del fallo: no reintenta

        Assert.True(anterior.EsAnterior);
        Assert.True(otraVez.EsAnterior);
        Assert.Equal(2, descarga.Llamadas);

        reloj.Ahora = reloj.Ahora.AddMinutes(31); // 61 min de antigüedad
        await Assert.ThrowsAsync<ErpNoDisponibleException>(() => cache.ObtenerActivosAsync(Ct));
    }

    [Fact]
    public async Task P5_Fresca_SiempreDescarga_NuncaUsaLaAnterior_YRenuevaLaCache()
    {
        var (cache, descarga, _) = Crear();
        await cache.ObtenerActivosAsync(Ct);

        await cache.ObtenerActivosFrescosAsync(Ct);
        await cache.ObtenerActivosAsync(Ct); // la fresca renovó la caché
        Assert.Equal(2, descarga.Llamadas);

        descarga.Error = new ErpNoDisponibleException("empleados", "sin red");
        await Assert.ThrowsAsync<ErpNoDisponibleException>(() => cache.ObtenerActivosFrescosAsync(Ct));
    }

    [Fact]
    public async Task SinValidos_NoDisponible()
    {
        var (cache, descarga, _) = Crear();
        descarga.Respuesta = [Erp("1", estado: "I")];

        var ex = await Assert.ThrowsAsync<ErpNoDisponibleException>(() => cache.ObtenerActivosAsync(Ct));

        Assert.Equal("empleados", ex.Operacion);
    }

    // ------------------------------------------------------------------ cédulas (TAREA-22-1, D15)

    /// <summary>Descarga con cédulas FICTICIAS (incluida la de un registro no válido, que no debe salir).</summary>
    private sealed class DescargaConCedulas : IDescargaEmpleadosErp
    {
        public Task<IReadOnlyList<EmpleadoErp>> DescargarAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<EmpleadoErp>>([Erp("1"), Erp("2"), Erp("3", estado: "I")]);

        public async Task<(IReadOnlyList<EmpleadoErp> Registros, CedulasEmpleados Cedulas)> DescargarConCedulasAsync(CancellationToken ct) =>
            (await DescargarAsync(ct), new CedulasEmpleados([("1", "0000000001"), ("3", "0000000003")]));
    }

    [Fact]
    public async Task Cedulas_SoloEnLaLecturaFresca_LaCacheQuedaSinEllas()
    {
        var reloj = new RelojMovible();
        var cache = new CacheEmpleadosErp(() => new DescargaConCedulas(),
            new ServiciosExternosOpciones { CacheEmpleadosMinutos = 10, CacheEmpleadosMaxAntiguedadMinutos = 60 }, reloj,
            NullLogger<CacheEmpleadosErp>.Instance);

        var fresca = await cache.ObtenerActivosFrescosAsync(Ct);
        var enCache = await cache.ObtenerActivosAsync(Ct); // dentro del TTL: la lista que dejó la lectura fresca
        reloj.Ahora = reloj.Ahora.AddMinutes(30);
        var anterior = await cache.ObtenerActivosAsync(Ct); // vencida: descarga normal, sin cédulas

        Assert.Equal((1, "0000000001", null), (fresca.Cedulas.Cantidad, fresca.Cedulas.De("1"), fresca.Cedulas.De("3"))); // "3" no es válido
        Assert.Equal(0, enCache.Cedulas.Cantidad);
        Assert.Equal(0, anterior.Cedulas.Cantidad);
        Assert.Equal(0, enCache.ComoAnterior().Cedulas.Cantidad);
    }

    [Fact]
    public async Task Simulado_CedulasFicticias_DevComoElSembrador_SimDe10DigitosEn00_Unicas()
    {
        var (lista, cedulas) = await ((IDescargaEmpleadosErp)new EmpleadosErpSimulado()).DescargarConCedulasAsync(Ct);

        var valores = lista.Select(e => cedulas.De(e.CodigoEkon)).ToList();
        Assert.Equal(208, cedulas.Cantidad);
        Assert.Equal(valores.Count, valores.Distinct().Count());
        for (var i = 1; i <= 8; i++)
        {
            Assert.Equal(EmpleadosPrueba.CrearDev(i, DateTime.MinValue).Cedula, cedulas.De($"DEV{i:000}")); // la alta puntual no cambia la fila
        }

        Assert.All(lista.Where(e => e.CodigoEkon!.StartsWith("SIM", StringComparison.Ordinal)).Select(e => cedulas.De(e.CodigoEkon)!),
            c => Assert.Matches("^00[0-9]{8}$", c));
    }

    // ------------------------------------------------------------------ Simulado (P12)

    [Fact]
    public async Task Simulado_DevIdenticosAlSembrador_Y200Ficticios_EnLosDepartamentosDeLaTabla()
    {
        var lista = await new EmpleadosErpSimulado().DescargarAsync(Ct);

        // DEV001–DEV008: la alta puntual con los datos del Simulado deja la fila igual a la del sembrador.
        for (var i = 1; i <= 8; i++)
        {
            var fila = EmpleadosPrueba.CrearDev(i, DateTime.MinValue);
            var datos = MapeoEmpleado.Datos(lista.Single(e => e.CodigoEkon == $"DEV{i:000}"));
            Assert.Equal(new DatosEmpleado(fila.NombreCompleto, fila.Apellidos, fila.Nombres, fila.CorreoEmpresa, fila.CodEmpresa, fila.Empresa,
                fila.CodPuesto, fila.Puesto, fila.CodDepartamento, fila.Departamento, fila.CodUnidad, fila.Unidad, fila.CodArea, fila.Area,
                fila.CodSeccion, fila.Seccion, fila.FamiliaPuesto), datos);
        }

        var simulados = lista.Where(e => e.CodigoEkon!.StartsWith("SIM", StringComparison.Ordinal)).ToList();
        string[] tabla =
        [
            "DEPARTAMENTO DE INFRAESTRUCTURA", "DEPARTAMENTO SEDEMI TELECOM", "DEPARTAMENTO SEDEMI PETROLEO Y GAS", "DEPARTAMENTO SEDEMI ENERGIA",
            "DEPARTAMENTO DE INFRAESTRUCTURA METALICA", "DEPARTAMENTO SEDEMI MINERIA", "UNIDAD SISTEMA INTEGRADO DE GESTION",
        ];
        Assert.Equal(200, simulados.Count);
        Assert.Equal("SIM001", simulados[0].CodigoEkon);
        Assert.All(lista, e => Assert.True(MapeoEmpleado.EsValido(e)));
        Assert.All(simulados, e => Assert.Contains(e.Departamento, tabla));
        Assert.True(simulados.Count(e => e.Departamento == "UNIDAD SISTEMA INTEGRADO DE GESTION") > 100); // la mayoría en SIG
        Assert.Equal(7, simulados.Select(e => e.Departamento).Distinct().Count());
    }
}
