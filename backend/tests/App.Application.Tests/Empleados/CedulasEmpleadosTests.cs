using System.Reflection;
using App.Application.Empleados;
using App.Application.Erp;
using App.Application.Proyectos.Crear;
using App.Application.Tests.Proyectos.Crear;
using App.Domain.Maestros;

namespace App.Application.Tests.Empleados;

/// <summary>
/// TAREA-22-1 (D15): la cédula viaja aparte (CedulasEmpleados), solo con la lectura fresca, y la acción «completar
/// cédulas». Todas las cédulas son FICTICIAS (10 dígitos que empiezan en "00").
/// </summary>
public class CedulasEmpleadosTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static readonly CedulasEmpleados Cedulas = new([("900001", "0000000001"), ("900002", "0000000002"), ("900003", null)]);

    // ------------------------------------------------------------------ tipo y tipos sin cédula

    [Fact]
    public void Tipos_QueSePuedenLoguearOSerializar_NoTienenCedula()
    {
        foreach (var tipo in new[] { typeof(EmpleadoErp), typeof(DatosEmpleado), typeof(EmpleadoAsignable), typeof(ListaEmpleadosErp) })
        {
            var propiedades = tipo.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(p => p.Name);
            Assert.DoesNotContain(propiedades, p => p.Equals("Cedula", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void CedulasEmpleados_ToStringSinValores_NormalizaYSoloLasPedidas()
    {
        var c = new CedulasEmpleados([(" 900001 ", " 0000000001 "), ("900002", "   "), ("900003", new string('0', 25)), (null, "0000000004")]);

        Assert.Equal("CedulasEmpleados(2)", c.ToString());
        Assert.DoesNotContain("0000000001", c.ToString());
        Assert.Equal("0000000001", c.De("900001"));
        Assert.Null(c.De("900002"));                      // vacía o solo espacios → sin dato
        Assert.Equal(20, c.De("900003")!.Length);         // recortada a Empleado.Cedula
        var solo = c.Solo(["900003", "999999"]);
        Assert.Equal((1, null, "CedulasEmpleados(1)"), (solo.Cantidad, solo.De("900001"), solo.ToString()));
    }

    // ------------------------------------------------------------------ catálogo: solo con la lectura fresca

    [Fact]
    public async Task Catalogo_Fresco_TraeSoloLasCedulasDeLosCodigosPedidos_VistaPreviaSinCedulas()
    {
        var fuente = new FuenteConCedulas();

        var fresco = await CatalogoEmpleados.CargarAsync(new DatosFalsos(), fuente, ["900001", " 900003 "], fresco: true, Ct);
        var previa = await CatalogoEmpleados.CargarAsync(new DatosFalsos(), fuente, ["900001"], fresco: false, Ct);

        Assert.Equal((1, "0000000001", null), (fresco.Cedulas.Cantidad, fresco.Cedulas.De("900001"), fresco.Cedulas.De("900002")));
        Assert.Equal(0, previa.Cedulas.Cantidad);
    }

    // ------------------------------------------------------------------ completar cédulas (lógica pura)

    [Fact]
    public void Aplicar_SoloLlenaLosNull_YCuentaSinCedulaYNoEncontradas()
    {
        var conCedula = new Empleado { CodigoEkon = "900002", Cedula = "0000000099" };
        var completable = new Empleado { CodigoEkon = "900001" };
        var sinEnApi = new Empleado { CodigoEkon = "900003" };
        var noEncontrada = new Empleado { CodigoEkon = "DEV001" };

        var r = CompletarCedulas.Aplicar([completable, conCedula, sinEnApi, noEncontrada], Lista());

        Assert.Equal(new ResultadoCompletarCedulasDto(Revisadas: 3, Completadas: 1, SinCedulaEnApi: 1, NoEncontradasEnApi: 1), r);
        Assert.Equal("0000000001", completable.Cedula);
        Assert.Equal("0000000099", conCedula.Cedula); // nunca se sobrescribe una no nula
        Assert.Null(sinEnApi.Cedula);
        Assert.Null(noEncontrada.Cedula);
    }

    // ------------------------------------------------------------------ servicio

    [Fact]
    public async Task Servicio_CompletaDentroDeLaTransaccion_YLaSegundaVezNoHaceNada()
    {
        var repositorio = new RepositorioFalso(new Empleado { CodigoEkon = "900001" }, new Empleado { CodigoEkon = "900003" });
        var transaccion = new TransaccionFalsa();
        var servicio = new CompletarCedulasServicio(new FuenteConCedulas(), transaccion, repositorio);

        var primera = await servicio.CompletarAsync(Ct);
        var segunda = await servicio.CompletarAsync(Ct);

        Assert.Equal(new ResultadoCompletarCedulasDto(2, 1, 1, 0), primera);
        Assert.Equal(new ResultadoCompletarCedulasDto(1, 0, 1, 0), segunda); // idempotente
        Assert.Equal((2, 2, 1), (transaccion.Iniciadas, transaccion.Confirmadas, repositorio.Guardados));
    }

    [Fact]
    public async Task Servicio_ErpCaido_503SinAbrirLaTransaccionNiEscribir()
    {
        var repositorio = new RepositorioFalso(new Empleado { CodigoEkon = "900001" });
        var transaccion = new TransaccionFalsa();
        var fuente = new FuenteConCedulas { Error = new ErpNoDisponibleException("empleados", "prueba") };

        await Assert.ThrowsAsync<ErpNoDisponibleException>(() => new CompletarCedulasServicio(fuente, transaccion, repositorio).CompletarAsync(Ct));

        Assert.Equal((0, 0, 0), (transaccion.Iniciadas, repositorio.Lecturas, repositorio.Guardados));
    }

    // ------------------------------------------------------------------ dobles

    private static ListaEmpleadosErp Lista(CedulasEmpleados? cedulas = null) =>
        new(new[] { "900001", "900002", "900003" }.Select(c => EmpleadosErpFalsos.Activo(c, $"PERSONA {c}", "TECNICO")).ToList(),
            DateTimeOffset.UnixEpoch, cedulas: cedulas ?? Cedulas);

    /// <summary>API falsa: la lectura fresca trae cédulas; la de caché, no (como CacheEmpleadosErp).</summary>
    private sealed class FuenteConCedulas : IFuenteEmpleadosErp
    {
        public Exception? Error { get; init; }

        public Task<ListaEmpleadosErp> ObtenerActivosAsync(CancellationToken ct) => Task.FromResult(Lista(CedulasEmpleados.Vacio));

        public Task<ListaEmpleadosErp> ObtenerActivosFrescosAsync(CancellationToken ct) =>
            Error is null ? Task.FromResult(Lista()) : Task.FromException<ListaEmpleadosErp>(Error);
    }

    private sealed class RepositorioFalso(params Empleado[] filas) : ICompletarCedulasRepositorio
    {
        public int Lecturas { get; private set; }
        public int Guardados { get; private set; }

        public Task<IReadOnlyList<Empleado>> ObtenerSinCedulaAsync(CancellationToken ct)
        {
            Lecturas++;
            return Task.FromResult<IReadOnlyList<Empleado>>(filas.Where(f => f.Cedula is null).ToList());
        }

        public Task GuardarAsync(CancellationToken ct)
        {
            Guardados++;
            return Task.CompletedTask;
        }
    }
}
