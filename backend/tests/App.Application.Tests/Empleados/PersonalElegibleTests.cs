using App.Application.Empleados;
using App.Application.Novedades;
using App.Application.Novedades.Sincronizacion;
using App.Application.Proyectos.Crear;
using App.Application.Proyectos.Personal;
using App.Application.Proyectos.Reactivacion;
using App.Application.Seguridad;
using App.Application.Tests.Novedades;
using App.Application.Tests.Novedades.Sincronizacion;
using App.Application.Tests.Proyectos.Crear;
using App.Application.Tests.Proyectos.Personal;
using App.Application.Tests.Proyectos.Reactivacion;

namespace App.Application.Tests.Empleados;

/// <summary>
/// TAREA-62: criterio de personal elegible (familiaPuesto contra EMPLEADO_FAMILIAS_ASIGNABLES). Regla, buscador, catálogo
/// de personas nuevas con la exención del mismo proyecto, novedades, sincronización (sin filtro) y administración.
/// Todos los datos son FICTICIOS.
/// </summary>
public class PersonalElegibleTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly string[] Administrativo = ["ADMINISTRATIVO"];

    private static EmpleadoErp Erp(string codigo, string? familia, string departamento = "UNIDAD SISTEMA INTEGRADO DE GESTION") =>
        new(codigo, $"PERSONA {codigo}", null, null, null, null, null, null, "TECNICO", null, departamento, null, null, null, null, null,
            null, familia, "A");

    // ------------------------------------------------------------------ regla

    [Theory]
    [InlineData("ADMINISTRATIVO", true)]
    [InlineData("administrativo", true)]
    [InlineData("  Administrativo  ", true)]
    [InlineData("ADMINÍSTRATIVO", true)] // sin tildes
    [InlineData("OPERATIVO", false)]
    [InlineData(null, false)]            // sin familia, con filtro: no asignable
    [InlineData("   ", false)]
    public void Regla_SinMayusculasNiTildesNiEspacios_SinFamiliaNoAsignable(string? familia, bool esperado) =>
        Assert.Equal(esperado, ReglasPersonalElegible.EsAsignable(Erp("1", familia), Administrativo));

    [Fact]
    public void Regla_ListaVaciaTodos_VariasFamilias_EspaciosInternosColapsados()
    {
        Assert.True(ReglasPersonalElegible.EsAsignable(Erp("1", null), []));
        Assert.True(ReglasPersonalElegible.EsAsignable(Erp("1", "OPERATIVO"), ["ADMINISTRATIVO", "operativo"]));
        Assert.True(ReglasPersonalElegible.EsAsignable(Erp("1", "MANDO   MEDIO"), ["mando medio"]));
        Assert.False(ReglasPersonalElegible.EsAsignable(Erp("1", "TECNICO"), ["ADMINISTRATIVO", "OPERATIVO"]));
        Assert.Equal("El empleado SIM010 no pertenece a un grupo asignable.", ReglasPersonalElegible.MensajeNoAsignable("SIM010"));
    }

    // ------------------------------------------------------------------ buscador

    private static ListaEmpleadosErp Lista() => new(
        [Erp("1", "ADMINISTRATIVO"), Erp("2", "OPERATIVO"), Erp("3", "Administrativo", "OTRO"), Erp("4", null)], DateTimeOffset.UnixEpoch);

    [Fact]
    public void Buscador_FiltraAntesDePaginar_SeSumaAlDeDepartamento_YDevuelveElCriterio()
    {
        var todos = BusquedaEmpleados.Buscar(Lista(), new EmpleadoFiltro(null, null, 1, 20), Administrativo);
        Assert.Equal(["1", "3"], todos.Items.Select(i => i.CodigoEkon));
        Assert.Equal(2, todos.Total);
        Assert.Equal(Administrativo, todos.FamiliasAsignables);

        var conDepartamento = BusquedaEmpleados.Buscar(Lista(), new EmpleadoFiltro(null, ["UNIDAD SISTEMA INTEGRADO DE GESTION"], 1, 20),
            Administrativo);
        Assert.Equal(["1"], conDepartamento.Items.Select(i => i.CodigoEkon));

        var sinCriterio = BusquedaEmpleados.Buscar(Lista(), new EmpleadoFiltro(null, null, 1, 20));
        Assert.Equal(4, sinCriterio.Total);
        Assert.Empty(sinCriterio.FamiliasAsignables!);
    }

    // ------------------------------------------------------------------ catálogo de personas nuevas

    private static Task<CatalogoEmpleados> Catalogo(DatosFalsos datos, IEnumerable<string>? exentos, params string[] codigos) =>
        CatalogoEmpleados.CargarAsync(datos, new EmpleadosErpFalsos(Erp("900001", "ADMINISTRATIVO"), Erp("900002", "OPERATIVO")),
            codigos, fresco: true, Ct, exentos);

    [Fact]
    public async Task Catalogo_NoAsignable400_DespuesDeNoActivo_YExentos()
    {
        var datos = new DatosFalsos { FamiliasAsignables = [.. Administrativo] };
        var c = await Catalogo(datos, ["DEV006"], "900001", "900002", "X99", "DEV006", "DEV007");
        var e = new Dictionary<string, List<string>>();

        Assert.NotNull(c.Validar("900001", "principales[0]", e));
        Assert.Null(c.Validar("900002", "principales[1]", e));
        Assert.Null(c.Validar("X99", "backs[0]", e));             // no existe: su mensaje de siempre
        Assert.NotNull(c.Validar("DEV006", "backs[1]", e));       // DEV sin familia, pero exento (ya está en el proyecto)
        Assert.Null(c.Validar("dev007", "backs[2]", e));          // DEV sin familia, no exento

        Assert.Equal(["El empleado 900002 no pertenece a un grupo asignable."], e["principales[1].codigoEkon"]);
        Assert.Equal(["El empleado X99 no existe o no está activo."], e["backs[0].codigoEkon"]);
        Assert.Equal(["El empleado dev007 no pertenece a un grupo asignable."], e["backs[2].codigoEkon"]); // el código tal como llegó, como «no existe»
        Assert.True(c.NoEsAsignable(" 900002 "));
        Assert.False(c.NoEsAsignable("DEV006"));
        Assert.Equal(1, datos.LecturasFamilias);
    }

    [Fact]
    public async Task Catalogo_SinCriterio_NoFiltra_YSinActivosNoLeeElParametro()
    {
        var sinCriterio = new DatosFalsos();
        var c = await Catalogo(sinCriterio, null, "900002");
        Assert.NotNull(c.Validar("900002", "principales[0]", new Dictionary<string, List<string>>()));

        var datos = new DatosFalsos { FamiliasAsignables = [.. Administrativo] };
        await Catalogo(datos, null, "X99");
        Assert.Equal(0, datos.LecturasFamilias);
    }

    // ------------------------------------------------------------------ crear proyecto: sin exención

    [Fact]
    public async Task Crear_SinExencion_PersonaConFilaDeOtraFamilia400()
    {
        var datos = new DatosFalsos { FamiliasAsignables = [.. Administrativo] };
        var validador = new CrearProyectoValidador(datos, new ErpFalso(), new UsuarioFalso(), new EmpleadosErpFalsos());
        var servicio = new CrearProyectoServicio(validador, new CrucesExternosFalsos(), new RepositorioFalso(), new TransaccionFalsa(),
            new RelojFijo(new DateTimeOffset(2026, 12, 1, 15, 0, 0, TimeSpan.Zero)));
        var s = Dobles.SolicitudCampo() with
        {
            Principales = [new PrincipalSolicitud("DEV007", "TIPO_3", new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), null)],
            Backs = [],
        };

        var r = await servicio.PrevisualizarAsync(s, Ct);

        Assert.Equal(EstadoCrearProyecto.Invalido, r.Estado);
        Assert.Equal(["El empleado DEV007 no pertenece a un grupo asignable."], r.Errores!["principales[0].codigoEkon"]);
    }

    // ------------------------------------------------------------------ actualizar personal y reactivar: exención del mismo proyecto

    [Fact]
    public async Task Personal_ExentoQuienYaEstaEnElProyecto_FiltradoElNuevo()
    {
        var datos = new DatosFalsos { FamiliasAsignables = [.. Administrativo] };
        var repo = new RepositorioEdicionFalso(DoblesPersonal.Proyecto()); // DEV006 (P1) y DEV007 (P2, K1) ya están
        var servicio = new EdicionPersonalServicio(repo, datos, new CrucesEdicionFalsos(), new EdicionPersonalValidador(),
            new TransaccionFalsa(), new UsuarioFalso(), new RelojFijo(DoblesPersonal.Ahora), new EmpleadosErpFalsos());
        var s = new ActualizarPersonalSolicitud([DoblesPersonal.SolP1()], [DoblesPersonal.SolK1(),
            DoblesPersonal.BackNuevo("kn", 6, "2026-10-10", "2026-10-12", "p1"),   // DEV006: exento
            DoblesPersonal.BackNuevo("ko", 1, "2026-10-10", "2026-10-12", "p1")]); // DEV001: no está en el proyecto

        var r = await servicio.PrevisualizarAsync(1, s.ConVersion(repo), Ct);

        Assert.Equal(EstadoEdicion.Invalido, r.Estado);
        Assert.Equal(["El empleado DEV001 no pertenece a un grupo asignable."], r.Errores!["backs[2].codigoEkon"]);
        Assert.DoesNotContain("backs[1].codigoEkon", r.Errores.Keys);
    }

    [Fact]
    public async Task Reactivacion_ExentoElPropuestoYElPersonalGuardado_FiltradoElNuevo()
    {
        var datos = new DatosFalsos { FamiliasAsignables = [.. Administrativo] };
        Task<ResultadoReactivacion> Previsualizar(ReactivarProyectoSolicitud cuerpo) =>
            new ReactivacionServicio(new RepositorioEdicionFalso(DoblesReactivacion.Proyecto()), new RepositorioReactivacionFalso(null), datos,
                new CrucesEdicionFalsos(), new ReactivacionValidador(), new TransaccionFalsa(), new UsuarioFalso(),
                new RelojFijo(DoblesReactivacion.Ahora), new EmpleadosErpFalsos())
            .PrevisualizarAsync(DoblesReactivacion.ProyectoId, cuerpo, Ct);

        var propuesto = await Previsualizar(DoblesReactivacion.Cuerpo()); // DEV007 (R7): ya estaba en el proyecto
        Assert.NotEqual(EstadoEdicion.Invalido, propuesto.Estado);

        var nuevo = await Previsualizar(DoblesReactivacion.Cuerpo(backs:
            [DoblesReactivacion.Back("k9", 1, DoblesReactivacion.R, DoblesReactivacion.R.AddDays(2), "p1")]));
        Assert.Equal(["El empleado DEV001 no pertenece a un grupo asignable."], nuevo.Errores!["backs[0].codigoEkon"]);
    }

    // ------------------------------------------------------------------ novedades PERSONA: sin exención

    [Fact]
    public async Task Novedad_Persona_NoAsignable400_ConSuMensaje_YNoActivoConElDeSiempre()
    {
        var e = new EscenarioNovedad(UsuarioNovedad.Gestor());
        e.Datos.FamiliasAsignables = [.. Administrativo];

        var noAsignable = await e.Servicio.CrearAsync(NovedadPrueba.Persona("DEV001"), Ct); // DEV sin familia
        var inactivo = await e.Servicio.CrearAsync(NovedadPrueba.Persona("DEV999"), Ct);

        Assert.Equal(["El empleado DEV001 no pertenece a un grupo asignable."], noAsignable.Errores!["codigoEkon"]);
        Assert.Equal(["El empleado no existe o no está activo."], inactivo.Errores!["codigoEkon"]);
        Assert.Equal(0, e.Transaccion.Iniciadas);
    }

    [Fact]
    public async Task Novedad_FormaInvalida_NoLeeElParametro()
    {
        var e = new EscenarioNovedad(UsuarioNovedad.Gestor());
        e.Datos.FamiliasAsignables = [.. Administrativo];

        await e.Servicio.CrearAsync(NovedadPrueba.Persona("DEV001") with { FechaFin = new DateOnly(2000, 1, 1) }, Ct);

        Assert.Equal(0, e.Datos.LecturasFamilias);
    }

    // ------------------------------------------------------------------ sincronización de PERMISO MEDICO: sin filtro (P2)

    [Fact]
    public async Task Sincronizacion_NoFiltraPorFamilia()
    {
        var datos = new DatosFalsos { FamiliasAsignables = [.. Administrativo] };
        var eventos = new List<string>();
        var api = new FuenteEmpleadosConCedulas(); // SIM001 activo, sin familia: no asignable con el criterio
        var servicio = new SincronizacionPermisosMedicosServicio(new FuenteFalsa(JsonFicticio.Valida(1, "SIM001")) { Eventos = eventos },
            new SincronizacionFalsa(eventos), datos, api, new TransaccionFalsa(), NovedadPrueba.Reloj);

        var r = (await servicio.SincronizarAsync(null, Ct)).Valor!;

        Assert.Equal(1, r.Nuevas);
        Assert.Empty(r.Omitidas);
        Assert.Equal(0, datos.LecturasFamilias);
    }

    // ------------------------------------------------------------------ administración del criterio

    private sealed class RepositorioCriterioFalso(string? valor, int filas = 1) : ICriterioPersonalRepositorio
    {
        public (string Anterior, string Nuevo, int Usuario, DateTime Fecha)? Actualizado { get; private set; }

        public Task<CriterioGuardado> ObtenerAsync(CancellationToken ct) =>
            Task.FromResult(new CriterioGuardado(valor,
                valor?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? []));

        public Task<int> ActualizarAsync(string valorAnterior, string valorNuevo, int usuarioId, DateTime fechaUtc, CancellationToken ct)
        {
            Actualizado = (valorAnterior, valorNuevo, usuarioId, fechaUtc);
            return Task.FromResult(filas);
        }
    }

    private static CriterioPersonalServicio Criterio(ICriterioPersonalRepositorio repo) =>
        new(repo, new EmpleadosErpFalsos(Erp("1", "ADMINISTRATIVO"), Erp("2", "Administrativo"), Erp("3", "OPERATIVO"), Erp("4", null)),
            new UsuarioFalso(), new RelojFijo(new DateTimeOffset(2026, 10, 27, 15, 30, 45, 600, TimeSpan.Zero)));

    [Fact]
    public async Task Obtener_AgrupaSinMayusculas_AgregaLasDelParametroSinActivos_YMarcaSinCriterio()
    {
        var conCriterio = await Criterio(new RepositorioCriterioFalso("ADMINISTRATIVO, MANDO MEDIO")).ObtenerAsync(Ct);

        Assert.Equal("ADMINISTRATIVO, MANDO MEDIO", conCriterio.ValorActual);
        Assert.False(conCriterio.SinCriterio);
        Assert.Equal(
            [new FamiliaCriterioDto("ADMINISTRATIVO", 2, true), new FamiliaCriterioDto("OPERATIVO", 1, false), new FamiliaCriterioDto("MANDO MEDIO", 0, true)],
            conCriterio.Familias);
        // DatosFalsos: 8 DEV sin familia (no cuentan en ningún grupo).

        var vacio = await Criterio(new RepositorioCriterioFalso("")).ObtenerAsync(Ct);
        Assert.True(vacio.SinCriterio);
        Assert.All(vacio.Familias, f => Assert.False(f.Asignable));

        var sinFila = await Criterio(new RepositorioCriterioFalso(null)).ObtenerAsync(Ct);
        Assert.Equal(("", true), (sinFila.ValorActual, sinFila.SinCriterio));
    }

    [Fact]
    public async Task Guardar_Ok_ConAuditoriaExplicitaYValorNormalizado()
    {
        var repo = new RepositorioCriterioFalso("ADMINISTRATIVO");

        var r = await Criterio(repo).GuardarAsync(new GuardarCriterioSolicitud([" administrativo ", "OPERATIVO"], "ADMINISTRATIVO"), Ct);

        Assert.Equal(EstadoGuardarCriterio.Guardado, r.Estado);
        Assert.Equal("administrativo, OPERATIVO", r.Guardado!.ValorActual);
        Assert.Equal(("ADMINISTRATIVO", "administrativo, OPERATIVO", 3, new DateTime(2026, 10, 27, 15, 30, 45, DateTimeKind.Utc)), // UsuarioFalso = 3
            repo.Actualizado);
    }

    [Theory]
    [InlineData(new string[0], "familias", "Seleccione al menos una familia.")]
    [InlineData(new[] { "  " }, "familias", "La familia no puede estar vacía.")]
    [InlineData(new[] { "ADMINISTRATIVO,OPERATIVO" }, "familias", "La familia ADMINISTRATIVO,OPERATIVO no puede contener comas.")]
    [InlineData(new[] { "ADMINISTRATIVO", "administrativo" }, "familias", "La familia administrativo está repetida.")]
    [InlineData(new[] { "INEXISTENTE" }, "familias", "La familia INEXISTENTE no existe.")]
    [InlineData(new[] { "INEXISTENTE", "A,B" }, "familias", "La familia INEXISTENTE no existe.")] // todos los errores de una vez
    [InlineData(new[] { "INEXISTENTE", "A,B" }, "familias", "La familia A,B no puede contener comas.")]
    public async Task Guardar_Validaciones400(string[] familias, string campo, string mensaje)
    {
        var repo = new RepositorioCriterioFalso("ADMINISTRATIVO");

        var r = await Criterio(repo).GuardarAsync(new GuardarCriterioSolicitud(familias, "ADMINISTRATIVO"), Ct);

        Assert.Equal(EstadoGuardarCriterio.Invalido, r.Estado);
        Assert.Contains(mensaje, r.Errores![campo]);
        Assert.Null(repo.Actualizado);
    }

    [Fact]
    public async Task Guardar_ValorAnteriorObligatorio_LargoMaximo_Y409()
    {
        var sinAnterior = await Criterio(new RepositorioCriterioFalso("ADMINISTRATIVO"))
            .GuardarAsync(new GuardarCriterioSolicitud(["ADMINISTRATIVO"], null), Ct);
        Assert.Equal([CriterioPersonalServicio.MensajeValorAnterior], sinAnterior.Errores!["valorAnterior"]);

        var vacioPermitido = await Criterio(new RepositorioCriterioFalso(""))
            .GuardarAsync(new GuardarCriterioSolicitud(["ADMINISTRATIVO"], ""), Ct); // presente aunque sea vacío
        Assert.Equal(EstadoGuardarCriterio.Guardado, vacioPermitido.Estado);

        var largas = Enumerable.Range(1, 40).Select(i => $"FAMILIA NUMERO {i:00}").ToArray(); // 40 × 17 + separadores > 400
        var largo = await Criterio(new RepositorioCriterioFalso(string.Join(",", largas)))
            .GuardarAsync(new GuardarCriterioSolicitud(largas, ""), Ct);
        Assert.Equal([CriterioPersonalServicio.MensajeLargo], largo.Errores!["familias"]);

        var conflicto = await Criterio(new RepositorioCriterioFalso("ADMINISTRATIVO", filas: 0))
            .GuardarAsync(new GuardarCriterioSolicitud(["OPERATIVO"], "VALOR VIEJO"), Ct);
        Assert.Equal(EstadoGuardarCriterio.Conflicto, conflicto.Estado);
    }
}
