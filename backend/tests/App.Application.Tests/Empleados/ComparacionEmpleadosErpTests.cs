using App.Application.Empleados;
using App.Application.Erp;
using App.Application.Proyectos.Personal;
using App.Application.Proyectos.Reactivacion;
using App.Application.Tests.Proyectos.Crear;
using App.Application.Tests.Proyectos.Personal;

namespace App.Application.Tests.Empleados;

/// <summary>
/// TAREA-26d-3: avisos "guardado frente al ERP" por persona (solo informan, D1) y el servicio del endpoint
/// GET /api/proyectos/{id}/personal/comparacion-erp. Datos ficticios.
/// </summary>
public class ComparacionEmpleadosErpTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static EmpleadoGuardado Guardado(string codigo = "900001", string nombre = "PERSONA FICTICIA", string? puesto = "TECNICO",
        string? departamento = "OPERACIONES", string? unidad = "TALLER") =>
        new(1, codigo, nombre, puesto, departamento, unidad);

    private static EmpleadoErp Erp(string codigo = "900001", string nombre = "PERSONA FICTICIA", string? puesto = "TECNICO",
        string? departamento = "OPERACIONES", string? unidad = "TALLER") =>
        new(codigo, nombre, null, null, null, null, null, null, puesto, null, departamento, null, unidad, null, null, null, null, null, "A");

    private static ListaEmpleadosErp Lista(bool anterior = false, params EmpleadoErp[] activos) =>
        new(activos, new DateTimeOffset(2026, 10, 13, 15, 30, 0, TimeSpan.Zero), anterior);

    // ------------------------------------------------------------------ lógica pura

    [Fact]
    public void SinDiferencias_IgnoraTildesMayusculasYEspacios()
    {
        var guardado = Guardado(nombre: "  José   Núñez ", puesto: "Técnico SSA", departamento: "Operaciones", unidad: "taller");
        var erp = Erp(nombre: "JOSE NUNEZ", puesto: "TECNICO  SSA", departamento: "OPERACIONES ", unidad: "TALLER");

        Assert.Empty(ComparacionEmpleadosErp.Avisos(guardado, erp));
    }

    [Fact]
    public void NoEstaEnLaLista_NoActivo()
    {
        var aviso = Assert.Single(ComparacionEmpleadosErp.Avisos(Guardado(), null));

        Assert.Equal(new AvisoErpDto("NO_ACTIVO", "Ya no está activo en el ERP.", null, null), aviso);
    }

    [Fact]
    public void CambiosDeNombreCargoYDepartamento_UnAvisoPorTipo_ConValores()
    {
        var avisos = ComparacionEmpleadosErp.Avisos(Guardado(), Erp(nombre: "PERSONA FICTICIA DOS", puesto: "Supervisor", departamento: "LOGISTICA"));

        Assert.Equal(["CAMBIO_NOMBRE", "CAMBIO_CARGO", "CAMBIO_DEPARTAMENTO"], avisos.Select(a => a.Tipo));
        Assert.Equal("El nombre en el ERP es distinto: PERSONA FICTICIA → PERSONA FICTICIA DOS.", avisos[0].Mensaje);
        Assert.Equal(("TECNICO", "SUPERVISOR"), (avisos[1].Guardado, avisos[1].Erp)); // puesto normalizado como en CargoInfor
        Assert.Equal("El departamento o la unidad en el ERP cambió: OPERACIONES · TALLER → LOGISTICA · TALLER.", avisos[2].Mensaje);
    }

    [Fact]
    public void Unidad_DentroDelAvisoDeDepartamento_YUnidadIgualAlDepartamentoNoCuenta()
    {
        var cambioUnidad = Assert.Single(ComparacionEmpleadosErp.Avisos(Guardado(), Erp(unidad: "BODEGA")));
        Assert.Equal(("CAMBIO_DEPARTAMENTO", "OPERACIONES · TALLER", "OPERACIONES · BODEGA"), (cambioUnidad.Tipo, cambioUnidad.Guardado, cambioUnidad.Erp));

        Assert.Empty(ComparacionEmpleadosErp.Avisos(Guardado(unidad: null), Erp(unidad: "OPERACIONES")));
    }

    [Fact]
    public void ValorVacioFrenteAValor_EsCambio()
    {
        var aviso = Assert.Single(ComparacionEmpleadosErp.Avisos(Guardado(puesto: null), Erp(puesto: "AUDITOR")));

        Assert.Equal("El cargo en el ERP cambió: (vacío) → AUDITOR.", aviso.Mensaje);
        Assert.Empty(ComparacionEmpleadosErp.Avisos(Guardado(puesto: " "), Erp(puesto: null)));
    }

    [Fact]
    public void Comparar_SoloPersonasConAvisos_SinRepetir_OrdenPorCodigo()
    {
        var guardados = new[] { Guardado("900002"), Guardado("900001", puesto: "OTRO"), Guardado("900002"), Guardado("900003") };

        var r = ComparacionEmpleadosErp.Comparar(guardados, Lista(false, Erp("900001"), Erp("900003")));

        Assert.Equal(["900001", "900002"], r.Personas.Select(p => p.CodigoEkon));
        Assert.Equal("CAMBIO_CARGO", Assert.Single(r.Personas[0].Avisos).Tipo);
        Assert.Equal("NO_ACTIVO", Assert.Single(r.Personas[1].Avisos).Tipo);
        Assert.Null(r.AvisoGeneral);
        Assert.Equal(new DateTimeOffset(2026, 10, 13, 15, 30, 0, TimeSpan.Zero), r.ComparadoUtc);
    }

    [Fact]
    public void ListaAnterior_AvisoGeneralConHoraDeEcuador_YNoComparado()
    {
        var r = ComparacionEmpleadosErp.Comparar([Guardado()], Lista(true, Erp()));
        Assert.Equal("Comparado con la lista de las 10:30; el ERP no respondió.", r.AvisoGeneral); // 15:30 UTC = 10:30 en Ecuador

        var noComparado = ComparacionEmpleadosErp.NoComparado();
        Assert.Equal((null, "No se pudo comparar con el ERP.", 0), (noComparado.ComparadoUtc, noComparado.AvisoGeneral, noComparado.Personas.Count));
        Assert.Equal(ComparacionEmpleadosErp.MensajeNoComparado, ReactivacionServicio.AdvertenciaErpNoVerificado); // D4: un solo texto
    }

    // ------------------------------------------------------------------ servicio (GET …/personal/comparacion-erp)

    private static (ComparacionErpServicio Servicio, DatosFalsos Datos, EmpleadosErpFalsos Fuente, RepositorioEdicionFalso Repo) Servicio(
        int? usuarioId = 3, bool existe = true)
    {
        var datos = new DatosFalsos();
        var fuente = new EmpleadosErpFalsos();
        var repo = new RepositorioEdicionFalso(existe ? DoblesPersonal.Proyecto() : null);
        return (new ComparacionErpServicio(repo, datos, fuente, new UsuarioFalso(usuarioId), new RelojFijo(DoblesPersonal.Ahora)), datos, fuente, repo);
    }

    [Fact]
    public async Task Servicio_SinDiferencias_ListaEnCache_NuncaFresca()
    {
        var s = Servicio();

        var r = (await s.Servicio.CompararAsync(1, Ct))!;

        Assert.Empty(r.Personas);
        Assert.Null(r.AvisoGeneral);
        Assert.Equal((1, 0), (s.Fuente.Lecturas, s.Fuente.LecturasFrescas));
        Assert.Equal(1, s.Datos.LecturasGuardados);
    }

    [Fact]
    public async Task Servicio_CambioDePuestoGuardado_AvisoPorCodigo_SinEscribir()
    {
        var s = Servicio();
        s.Datos.Guardados[6] = s.Datos.Guardados[6] with { Puesto = "PUESTO ANTERIOR" };

        var r = (await s.Servicio.CompararAsync(1, Ct))!;

        var persona = Assert.Single(r.Personas);
        Assert.Equal(("DEV006", "CAMBIO_CARGO"), (persona.CodigoEkon, Assert.Single(persona.Avisos).Tipo));
        Assert.Null(s.Repo.Aplicado); // D1: solo informa
    }

    [Fact]
    public async Task Servicio_ErpCaido_AvisoGeneral_No503()
    {
        var s = Servicio();
        s.Fuente.Error = new ErpNoDisponibleException("empleados", "sin red");

        var r = await s.Servicio.CompararAsync(1, Ct);

        Assert.Equal((null, ComparacionEmpleadosErp.MensajeNoComparado, 0), (r!.ComparadoUtc, r.AvisoGeneral, r.Personas.Count));
        Assert.Equal(0, s.Datos.LecturasGuardados);
    }

    [Fact]
    public async Task Servicio_NoVisibleOInexistente_Null404()
    {
        Assert.Null(await Servicio(usuarioId: null).Servicio.CompararAsync(1, Ct));
        var inexistente = Servicio(existe: false);
        Assert.Null(await inexistente.Servicio.CompararAsync(1, Ct));
        Assert.Equal(0, inexistente.Fuente.Lecturas);
    }
}
