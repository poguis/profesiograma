using App.Application.Comun;
using App.Application.Empleados;
using App.Application.Erp;
using App.Application.Proyectos.Crear;
using App.Application.Proyectos.Personal;
using App.Application.Proyectos.Reactivacion;
using App.Application.Tests.Proyectos.Crear;
using App.Application.Tests.Proyectos.Personal;
using App.Application.Tests.Proyectos.Reactivacion;

namespace App.Application.Tests.Empleados;

/// <summary>
/// TAREA-26d (opción C): empleados desde la API. Mapeo, búsqueda en memoria, catálogo de la solicitud (transición
/// empleadoId / codigoEkon, Id temporales) y su uso en crear, personal y reactivación. Todos los datos son FICTICIOS.
/// </summary>
public class EmpleadosApiTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static EmpleadoErp Erp(string? codigo, string? nombre = "PERSONA FICTICIA", string? estado = "A", string? puesto = "TECNICO",
        string? departamento = "UNIDAD SISTEMA INTEGRADO DE GESTION", string? unidad = null, string? apellidos = null, string? codEmpresa = "EF1") =>
        new(codigo, nombre, apellidos, null, null, codEmpresa, null, null, puesto, null, departamento, null, unidad, null, null, null, null, null, estado);

    // ------------------------------------------------------------------ MapeoEmpleado

    [Fact]
    public void Mapeo_Validez()
    {
        Assert.True(MapeoEmpleado.EsValido(Erp("900001")));
        Assert.False(MapeoEmpleado.EsValido(Erp(null)));
        Assert.False(MapeoEmpleado.EsValido(Erp("  ")));
        Assert.False(MapeoEmpleado.EsValido(Erp(new string('9', 21))));
        Assert.False(MapeoEmpleado.EsValido(Erp("900001", nombre: " ")));
        Assert.False(MapeoEmpleado.EsValido(Erp("900001", estado: "I")));
        Assert.False(MapeoEmpleado.EsValido(Erp("900001", codEmpresa: "CODIGO-MUY-LARGO")));
    }

    [Fact]
    public void Mapeo_DatosNormalizadosYRecortados_CargoEnMayusculas()
    {
        var erp = new EmpleadoErp(" 101 ", "  PERSONA  ", " ", null, "x@ficticio.local", "EF1", "EMPRESA", " P1 ", new string('p', 230),
            "D1", "DEPTO", null, "", "A1", "AREA", "S1", "SECCION", "OPERATIVO", "A");

        var d = MapeoEmpleado.Datos(erp);

        Assert.Equal(("PERSONA", null, "P1", 200, null), (d.NombreCompleto, d.Apellidos, d.CodPuesto, d.Puesto!.Length, d.Unidad));
        Assert.Equal("TÉCNICO", MapeoEmpleado.NormalizarCargo(" Técnico "));
        Assert.Null(MapeoEmpleado.NormalizarCargo("  "));
        Assert.Equal("101", MapeoEmpleado.Normalizado(erp).CodigoEkon);
    }

    // ------------------------------------------------------------------ BusquedaEmpleados (P13, paginación, departamentos)

    private static ListaEmpleadosErp Lista(bool anterior = false) => new(
    [
        Erp("1", "ÑANDÚ PÉREZ JOSÉ", apellidos: "ÑANDÚ PÉREZ"),
        Erp("2", "ALVAREZ MARIA", departamento: "DEPARTAMENTO SEDEMI ENERGIA"),
        Erp("3", "BENITEZ ANA", departamento: "OTRO", unidad: "UNIDAD SISTEMA INTEGRADO DE GESTION"),
        Erp("40", "CARDENAS LUIS", departamento: "OTRO"),
    ], new DateTimeOffset(2026, 10, 8, 15, 30, 0, TimeSpan.Zero), anterior);

    [Fact]
    public void Busqueda_SinTildesNiMayusculas_EnNombreApellidosYCodigo()
    {
        Assert.Equal(["1"], BusquedaEmpleados.Buscar(Lista(), new EmpleadoFiltro("nandu perez", null, 1, 20)).Items.Select(i => i.CodigoEkon));
        Assert.Equal(["1"], BusquedaEmpleados.Buscar(Lista(), new EmpleadoFiltro("JOSE", null, 1, 20)).Items.Select(i => i.CodigoEkon));
        Assert.Equal(["40"], BusquedaEmpleados.Buscar(Lista(), new EmpleadoFiltro("40", null, 1, 20)).Items.Select(i => i.CodigoEkon));
    }

    [Fact]
    public void Busqueda_OrdenPorNombre_PaginaYTotal_SinDatosSensibles()
    {
        var r = BusquedaEmpleados.Buscar(Lista(), new EmpleadoFiltro(null, null, 2, 2));

        Assert.Equal((4, 2, 2, null), (r.Total, r.Pagina, r.Tamano, r.AvisoErp));
        Assert.Equal(["40", "1"], r.Items.Select(i => i.CodigoEkon)); // ALVAREZ, BENITEZ | CARDENAS, ÑANDÚ
        Assert.Equal(new EmpleadoBusquedaDto("40", "CARDENAS LUIS", "TECNICO", "OTRO", null), r.Items[0]);
    }

    [Fact]
    public void Busqueda_SoloMisDepartamentos_PorDepartamentoOUnidad()
    {
        var r = BusquedaEmpleados.Buscar(Lista(), new EmpleadoFiltro(null, ["unidad sistema integrado de gestion"], 1, 20));

        Assert.Equal(["3", "1"], r.Items.Select(i => i.CodigoEkon));
    }

    [Fact]
    public void Busqueda_ListaAnterior_AvisoConHoraDeEcuador()
    {
        var r = BusquedaEmpleados.Buscar(Lista(anterior: true), new EmpleadoFiltro(null, null, 1, 20));

        Assert.Equal("Lista de empleados de las 10:30; el ERP no respondió.", r.AvisoErp); // 15:30 UTC = 10:30 en Ecuador
    }

    // ------------------------------------------------------------------ CatalogoEmpleados (P2: transición, Id temporales, P5)

    private static Task<CatalogoEmpleados> Catalogo(EmpleadosErpFalsos fuente, bool fresco, params ReferenciaEmpleado[] refs) =>
        CatalogoEmpleados.CargarAsync(new DatosFalsos(), fuente, refs, fresco, Ct);

    [Fact]
    public async Task Catalogo_PorCodigo_IdRealSiTieneFila_TemporalNegativoSiNo_ConDatosDeLaApi()
    {
        var fuente = new EmpleadosErpFalsos(EmpleadosErpFalsos.Activo("900002", "PERSONA FICTICIA 2", "PUESTO X"),
            EmpleadosErpFalsos.Activo("900001", "PERSONA FICTICIA 1", null));
        var c = await Catalogo(fuente, false, new(null, "DEV006"), new(null, "900002"), new(null, "900001"));
        var e = new Dictionary<string, List<string>>();

        var conFila = c.Validar(null, " DEV006 ", "principales[0]", e)!;
        var nuevo1 = c.Validar(null, "900001", "principales[1]", e)!;
        var nuevo2 = c.Validar(null, "900002", "backs[0]", e)!;

        Assert.Empty(e);
        Assert.Equal((6, "DEV006", "EMPLEADO PRUEBA 06"), (conFila.Id, conFila.CodigoEkon, conFila.NombreCompleto));
        Assert.Equal((-1, "PERSONA FICTICIA 1"), (nuevo1.Id, nuevo1.NombreCompleto)); // orden determinista por código
        Assert.Equal((-2, "PUESTO X"), (nuevo2.Id, nuevo2.Puesto));
        Assert.NotNull(nuevo2.Erp);
        Assert.Equal((1, 0), (fuente.Lecturas, fuente.LecturasFrescas)); // vista previa: caché
    }

    [Fact]
    public async Task Catalogo_Transicion_EmpleadoIdOCodigo_AmbosONinguno400()
    {
        var c = await Catalogo(new EmpleadosErpFalsos(), true, new(6, null), new(7, "DEV007"), new(9, null), new(null, "DEV009"));
        var e = new Dictionary<string, List<string>>();

        Assert.Equal(6, c.Validar(6, null, "principales[0]", e)!.Id);
        Assert.Null(c.Validar(7, "DEV007", "principales[1]", e));
        Assert.Null(c.Validar(null, null, "principales[2]", e));
        Assert.Null(c.Validar(9, null, "backs[0]", e));          // fila existe pero no está activa en la API
        Assert.Null(c.Validar(null, "DEV009", "backs[1]", e));
        Assert.Null(c.Validar(99, null, "backs[2]", e));          // no existe

        Assert.Equal(["Indique codigoEkon o empleadoId, no ambos."], e["principales[1].codigoEkon"]);
        Assert.Equal(["El empleado es obligatorio."], e["principales[2].empleadoId"]);
        Assert.Equal(["El empleado 9 no existe o no está activo."], e["backs[0].empleadoId"]);
        Assert.Equal(["El empleado DEV009 no existe o no está activo."], e["backs[1].codigoEkon"]);
        Assert.Equal(["El empleado 99 no existe o no está activo."], e["backs[2].empleadoId"]);
    }

    [Fact]
    public async Task Catalogo_SinReferencias_NoConsultaLaApi_YApiCaidaSePropaga()
    {
        var fuente = new EmpleadosErpFalsos { Error = new ErpNoDisponibleException("empleados", "sin red") };

        Assert.Same(CatalogoEmpleados.Vacio, await Catalogo(fuente, true, new ReferenciaEmpleado(null, " ")));
        Assert.Equal(0, fuente.Lecturas + fuente.LecturasFrescas);
        await Assert.ThrowsAsync<ErpNoDisponibleException>(() => Catalogo(fuente, true, new ReferenciaEmpleado(null, "DEV006")));
    }

    // ------------------------------------------------------------------ Crear proyecto (validación fresca, Id temporales, cruces)

    private static (CrearProyectoServicio Servicio, EmpleadosErpFalsos Fuente, RepositorioFalso Repo, TransaccionFalsa Tx, CrucesExternosFalsos Cruces)
        Crear(params EmpleadoErp[] extra)
    {
        var fuente = new EmpleadosErpFalsos(extra);
        var repo = new RepositorioFalso();
        var tx = new TransaccionFalsa();
        var cruces = new CrucesExternosFalsos();
        var validador = new CrearProyectoValidador(new DatosFalsos(), new ErpFalso(), new UsuarioFalso(), fuente);
        return (new CrearProyectoServicio(validador, cruces, repo, tx, new RelojFijo(new DateTimeOffset(2026, 12, 1, 15, 0, 0, TimeSpan.Zero))),
            fuente, repo, tx, cruces);
    }

    private static CrearProyectoSolicitud ConCodigos() => Dobles.SolicitudCampo() with
    {
        Principales =
        [
            new PrincipalSolicitud(null, "TIPO_2", new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), null, "900001"),
            new PrincipalSolicitud(7, "TIPO_3", new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), null),
        ],
        Backs = [new BackSolicitud(null, "JORNADA", 2, new DateOnly(2026, 12, 12), new DateOnly(2026, 12, 15), 1, null, null, "DEV008")],
    };

    [Fact]
    public async Task Crear_VistaPrevia_ConCodigoSinFila_IdTemporalEnTramos_SinBuscarCrucesExternosDeEse()
    {
        var c = Crear(EmpleadosErpFalsos.Activo("900001", "PERSONA FICTICIA 1", "PUESTO X"));

        var r = await c.Servicio.PrevisualizarAsync(ConCodigos(), Ct);

        Assert.Equal(EstadoCrearProyecto.Previsualizado, r.Estado);
        Assert.Contains(r.Previsualizacion!.Tramos, t => t.EmpleadoId == -1 && t.CodigoEkon == "900001" && t.NombreEmpleado == "PERSONA FICTICIA 1");
        Assert.Equal((1, 0), (c.Fuente.Lecturas, c.Fuente.LecturasFrescas));
        Assert.Equal(1, c.Cruces.Llamadas); // se consultan los cruces de los que tienen fila (7 y 8), no del temporal
    }

    [Fact]
    public async Task Crear_Registro_LecturaFresca_YAltaConDatosDeLaApi()
    {
        var c = Crear(EmpleadosErpFalsos.Activo("900001", "PERSONA FICTICIA 1", "PUESTO X"));

        var r = await c.Servicio.RegistrarAsync(ConCodigos(), Ct);

        Assert.Equal(EstadoCrearProyecto.Creado, r.Estado);
        Assert.Equal((0, 1), (c.Fuente.Lecturas, c.Fuente.LecturasFrescas)); // P5: fresca, una sola vez
        var principales = c.Repo.Agregado!.Value.Proyecto.Datos.Principales;
        Assert.Equal((-1, "900001", "PUESTO X"), (principales[0].Empleado.Id, principales[0].Empleado.CodigoEkon, principales[0].Cargo));
        Assert.NotNull(principales[0].Empleado.Erp); // la alta puntual recibe los datos de la API
        Assert.Equal(7, principales[1].Empleado.Id);  // transición por empleadoId: Id real
    }

    [Fact]
    public async Task Crear_Registro_CodigoInactivo400_ApiCaida503SinTransaccion()
    {
        var c = Crear(); // 900001 no está en la API
        var invalido = await c.Servicio.RegistrarAsync(ConCodigos(), Ct);

        Assert.Equal(EstadoCrearProyecto.Invalido, invalido.Estado);
        Assert.Equal(["El empleado 900001 no existe o no está activo."], invalido.Errores!["principales[0].codigoEkon"]);

        var caida = Crear();
        caida.Fuente.Error = new ErpNoDisponibleException("empleados", "sin red");
        await Assert.ThrowsAsync<ErpNoDisponibleException>(() => caida.Servicio.RegistrarAsync(ConCodigos(), Ct));
        Assert.Equal(0, caida.Tx.Iniciadas);
        Assert.Null(caida.Repo.Agregado);
    }

    // ------------------------------------------------------------------ Actualizar personal y reactivar

    [Fact]
    public async Task Personal_NuevaPorCodigo_FrescaUnaVez_AltaEnElCambio()
    {
        var repo = new RepositorioEdicionFalso(DoblesPersonal.Proyecto());
        var fuente = new EmpleadosErpFalsos(EmpleadosErpFalsos.Activo("900001", "PERSONA FICTICIA 1", "PUESTO X"));
        var tx = new TransaccionFalsa();
        var servicio = new EdicionPersonalServicio(repo, new DatosFalsos(), new CrucesEdicionFalsos(), new EdicionPersonalValidador(), tx,
            new UsuarioFalso(), new RelojFijo(DoblesPersonal.Ahora), fuente);
        var nuevo = new BackEdicionSolicitud("kn", null, null, "JORNADA", new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 12), 0, "p1",
            null, null, "900001");

        var r = await servicio.RegistrarAsync(1,
            new ActualizarPersonalSolicitud([DoblesPersonal.SolP1()], [DoblesPersonal.SolK1(), nuevo]).ConVersion(repo), Ct);

        Assert.Equal(EstadoEdicion.Realizado, r.Estado);
        Assert.Equal((0, 1), (fuente.Lecturas, fuente.LecturasFrescas)); // dentro del applock no se vuelve a llamar a la API
        var alta = Assert.Single(repo.Aplicado!.AltasEmpleados!);
        Assert.Equal((-1, "900001"), (alta.Id, alta.CodigoEkon));
        Assert.Equal(-1, Assert.Single(repo.Aplicado.Nuevas).EmpleadoId); // el repositorio lo convierte en Id real
    }

    [Fact]
    public async Task Reactivacion_PropuestoSegunLaApi_ApiCaidaAvisoSinBloquear()
    {
        var fuente = new EmpleadosErpFalsos { Error = new ErpNoDisponibleException("empleados", "sin red") };
        var servicio = new ReactivacionServicio(new RepositorioEdicionFalso(DoblesReactivacion.Proyecto()),
            new RepositorioReactivacionFalso(null), new DatosFalsos(), new CrucesEdicionFalsos(), new ReactivacionValidador(),
            new TransaccionFalsa(), new UsuarioFalso(), new RelojFijo(DoblesReactivacion.Ahora), fuente);

        var d = (await servicio.ObtenerAsync(DoblesReactivacion.ProyectoId, Ct))!;

        Assert.True(d.PrincipalPropuesto!.Empleado.Activo);
        Assert.Equal([ReactivacionServicio.AdvertenciaErpNoVerificado], d.Advertencias);
    }
}
