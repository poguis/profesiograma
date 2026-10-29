using App.Application.Comun;
using App.Application.Empleados;
using App.Application.Erp;
using App.Application.Proyectos.Crear;
using App.Application.Proyectos.Estados;
using App.Application.Seguridad;

namespace App.Application.Tests.Empleados;

/// <summary>
/// TAREA-25-3-1: reglas de CargoInfor (comparador de la collation, plan de importación, código Infor) y servicio con
/// dobles. Las variantes se prueban aquí (F5: el modo Simulado no se modifica). Puestos y códigos FICTICIOS.
/// </summary>
public sealed class CargosInforTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ------------------------------------------------------------------ comparador (imita Modern_Spanish_CI_AS)

    [Theory]
    [InlineData("TECNICO SSA", "tecnico ssa", true)]          // mayúsculas y minúsculas → iguales
    [InlineData("TECNICO SSA", "ＴＥＣＮＩＣＯ SSA", true)]     // ancho completo frente a normal → iguales
    [InlineData("TECNICO SSA", "TÉCNICO SSA", false)]         // tildes → distintos
    [InlineData("TECNICO SSA", "TECNICO  SSA", false)]        // espacios internos dobles → distintos
    public void Collation_ComoLaBase(string a, string b, bool iguales)
    {
        Assert.Equal(iguales, ReglasCargoInfor.Collation.Equals(a, b));
        if (iguales)
        {
            Assert.Equal(ReglasCargoInfor.Collation.GetHashCode(a), ReglasCargoInfor.Collation.GetHashCode(b));
        }
    }

    // ------------------------------------------------------------------ plan de importación

    [Fact]
    public void Planificar_Conteos_NuevosExistentesInactivosYFusionados()
    {
        var activos = Activos("  Tecnico SSA ", "TECNICO SSA", "Paramedico", "AYUDANTE", null, "   ", "INSPECTOR");
        var existentes = new[] { Cargo(1, "PARAMEDICO"), Cargo(2, "INSPECTOR", activo: false), Cargo(3, "SIN USO") };

        var plan = ReglasCargoInfor.Planificar(activos, existentes);

        Assert.Equal(4, plan.PuestosApi);                 // TECNICO SSA, PARAMEDICO, AYUDANTE, INSPECTOR
        Assert.Equal(["TECNICO SSA", "AYUDANTE"], plan.Nuevos);
        Assert.Equal((1, 1, 1), (plan.YaExistentes, plan.ExistentesInactivos, plan.Fusionados)); // «Tecnico SSA» se fusiona
        Assert.Empty(plan.Variantes);
    }

    [Fact]
    public void Planificar_TildesYEspaciosInternos_SonVariantes_ConLosGuardados()
    {
        var activos = Activos("TÉCNICO SSA", "TECNICO  SSA", "AYUDANTE");
        var existentes = new[] { Cargo(1, "TECNICO SSA"), Cargo(2, "AYUDANTÉ VIEJO"), Cargo(3, "SOLO GUARDADO"), Cargo(4, "SÓLO GUARDADO") };

        var plan = ReglasCargoInfor.Planificar(activos, existentes);

        Assert.Equal(["TÉCNICO SSA", "TECNICO  SSA", "AYUDANTE"], plan.Nuevos); // cada variante es una fila distinta
        var grupo = Assert.Single(plan.Variantes); // los grupos solo de guardados no cuentan
        Assert.Equal(["TECNICO  SSA", "TECNICO SSA", "TÉCNICO SSA"], grupo);
    }

    [Fact]
    public void Planificar_RecorteA200_PuedeFusionar()
    {
        var largo = new string('A', 200);

        var plan = ReglasCargoInfor.Planificar(Activos(largo + "X", largo + "Y"), []);

        Assert.Equal((1, 1), (plan.PuestosApi, plan.Fusionados));
        Assert.Equal(largo, Assert.Single(plan.Nuevos));
    }

    // ------------------------------------------------------------------ código Infor (F2)

    [Theory]
    [InlineData("  p0021 ", "P0021", null)]
    [InlineData("   ", null, null)]
    [InlineData(null, null, null)]
    [InlineData("A.B-C_1", "A.B-C_1", null)]
    [InlineData("P 0021", "P 0021", ReglasCargoInfor.MensajeCodigoFormato)]
    [InlineData("PÑ1", "PÑ1", ReglasCargoInfor.MensajeCodigoFormato)]
    [InlineData("123456789012345678901", "123456789012345678901", ReglasCargoInfor.MensajeCodigoLargo)]
    public void Codigo_NormalizaYValida(string? entrada, string? normalizado, string? error)
    {
        var codigo = ReglasCargoInfor.NormalizarCodigo(entrada);

        Assert.Equal(normalizado, codigo);
        Assert.Equal(error, ReglasCargoInfor.ValidarCodigo(codigo));
    }

    // ------------------------------------------------------------------ servicio

    [Fact]
    public async Task Editar_200_ConCodigoNormalizado_YAnteriorVacioComoNull()
    {
        var repo = new RepoFalso();

        var r = await Servicio(repo).EditarAsync(5, new EditarCargoInforSolicitud(" p0021 ", true, "", true), Ct);

        Assert.Equal(EstadoCargoInfor.Ok, r.Estado);
        Assert.Equal((5, null, true, "P0021", true, 2), repo.Actualizado);
    }

    [Fact]
    public async Task Editar_CeroFilas_404SiNoExiste_409SiExiste()
    {
        var noExiste = await Servicio(new RepoFalso(filas: 0, existe: false)).EditarAsync(9, new EditarCargoInforSolicitud("X", true, null, true), Ct);
        var cambiado = await Servicio(new RepoFalso(filas: 0, existe: true)).EditarAsync(9, new EditarCargoInforSolicitud("X", true, "Y", true), Ct);

        Assert.Equal(EstadoCargoInfor.NoEncontrado, noExiste.Estado);
        Assert.Equal(EstadoCargoInfor.Conflicto, cambiado.Estado);
    }

    [Fact]
    public async Task Editar_400_EnCodigoActivoYAnterior()
    {
        var repo = new RepoFalso();

        var r = await Servicio(repo).EditarAsync(5, new EditarCargoInforSolicitud("P 1", null, null, null), Ct);

        Assert.Equal(["activo", "anterior", "codigoInfor"], r.Errores!.Keys.Order());
        Assert.Null(repo.Actualizado);
    }

    [Fact]
    public async Task Importar_InsertaSoloFaltantes_NoReactiva_EsIdempotente()
    {
        var repo = new RepoFalso(Cargo(1, "PARAMEDICO", activo: false));
        var servicio = Servicio(repo, Activos("PARAMEDICO", "AYUDANTE"));

        var primera = await servicio.ImportarAsync(Ct);
        var segunda = await servicio.ImportarAsync(Ct);

        Assert.Equal(new ImportacionCargosDto(2, 1, 0, 1), primera.Valor);
        Assert.Equal(new ImportacionCargosDto(2, 0, 1, 1), segunda.Valor);
        Assert.False(repo.Tabla.Single(c => c.Cargo == "PARAMEDICO").Activo);
        Assert.Equal(1, repo.Inserciones); // la segunda no llama a InsertarAsync
    }

    [Fact]
    public async Task Importar_UnicoDuplicado_409_YErpCaido_503SinTransaccion()
    {
        var conflicto = await Servicio(new RepoFalso { FallarAlInsertar = true }, Activos("AYUDANTE")).ImportarAsync(Ct);
        var transaccion = new TransaccionFalsa();
        var servicio = new CargosInforServicio(new RepoFalso(), new FuenteFalsa([], caida: true), transaccion, new AdminActual(), TimeProvider.System);

        Assert.Equal(EstadoCargoInfor.Conflicto, conflicto.Estado);
        await Assert.ThrowsAsync<ErpNoDisponibleException>(() => servicio.ImportarAsync(Ct));
        Assert.Equal(0, transaccion.Llamadas);
    }

    [Fact]
    public async Task Listar_ActivosErp_PorCargo_YNullConErpCaido()
    {
        var repo = new RepoFalso(Cargo(1, "PARAMEDICO"), Cargo(2, "INSPECTOR"));
        var con = await Servicio(repo, Activos("Paramedico", "PARAMEDICO ", "AYUDANTE")).ListarAsync(new CargoInforListadoSolicitud(null, null, null, null, null), Ct);
        var sin = await new CargosInforServicio(repo, new FuenteFalsa([], caida: true), new TransaccionFalsa(), new AdminActual(), TimeProvider.System)
            .ListarAsync(new CargoInforListadoSolicitud(null, "SINCODIGO", null, null, null), Ct);

        Assert.Equal(new (int, int?)[] { (1, 2), (2, 0) }, con.Valor!.Items.Select(i => (i.Id, i.ActivosErp)));
        Assert.All(sin.Valor!.Items, i => Assert.Null(i.ActivosErp));
        Assert.Equal(FiltroCodigoInfor.SinCodigo, repo.UltimoFiltro!.Filtro);
    }

    [Fact]
    public async Task Listar_FiltroInvalido_400()
    {
        var r = await Servicio(new RepoFalso()).ListarAsync(new CargoInforListadoSolicitud(null, "otro", null, null, "101"), Ct);

        Assert.Equal(["filtro", "tamano"], r.Errores!.Keys.Order());
    }

    // ------------------------------------------------------------------ dobles

    private static CargoRegistro Cargo(int id, string cargo, bool activo = true) => new(id, cargo, null, activo);

    private static List<EmpleadoErp> Activos(params string?[] puestos) =>
        puestos.Select((p, i) => new EmpleadoErp($"9{i:00000}", $"PERSONA {i}", null, null, null, null, null, null, p, null, null, null, null,
            null, null, null, null, "ADMINISTRATIVO", "A")).ToList();

    private static CargosInforServicio Servicio(RepoFalso repo, List<EmpleadoErp>? activos = null) =>
        new(repo, new FuenteFalsa(activos ?? []), new TransaccionFalsa(), new AdminActual(), TimeProvider.System);

    private sealed class RepoFalso(params CargoRegistro[] iniciales) : ICargosInforRepositorio
    {
        private readonly int filas = 1;
        private readonly bool existe = true;

        public RepoFalso(int filas, bool existe) : this()
        {
            this.filas = filas;
            this.existe = existe;
        }

        public List<CargoRegistro> Tabla { get; } = [.. iniciales];
        public bool FallarAlInsertar { get; init; }
        public int Inserciones { get; private set; }
        public CargoInforFiltro? UltimoFiltro { get; private set; }
        public (int, string?, bool, string?, bool, int)? Actualizado { get; private set; }

        public Task<PaginaResultado<CargoInforFila>> ListarAsync(CargoInforFiltro filtro, CancellationToken ct)
        {
            UltimoFiltro = filtro;
            return Task.FromResult(new PaginaResultado<CargoInforFila>(
                Tabla.Select(c => new CargoInforFila(c.Id, c.Cargo, c.CodigoInfor, c.Activo, 0)).ToList(), 1, 25, Tabla.Count));
        }

        public Task<IReadOnlyList<CargoRegistro>> ObtenerTodosAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CargoRegistro>>(Tabla.ToList());

        public Task InsertarAsync(IReadOnlyList<string> cargos, CancellationToken ct)
        {
            if (FallarAlInsertar)
            {
                throw new ConflictoConcurrenciaException("UQ");
            }

            Inserciones++;
            Tabla.AddRange(cargos.Select((c, i) => new CargoRegistro(100 + Tabla.Count + i, c, null, true)));
            return Task.CompletedTask;
        }

        public Task<int> ActualizarAsync(int id, string? codigoAnterior, bool activoAnterior, string? codigo, bool activo, int usuarioId,
            DateTime fechaUtc, CancellationToken ct)
        {
            Actualizado = (id, codigoAnterior, activoAnterior, codigo, activo, usuarioId);
            return Task.FromResult(filas);
        }

        public Task<bool> ExisteAsync(int id, CancellationToken ct) => Task.FromResult(existe);
    }

    private sealed class FuenteFalsa(List<EmpleadoErp> activos, bool caida = false) : IFuenteEmpleadosErp
    {
        private ListaEmpleadosErp Lista() =>
            caida ? throw new ErpNoDisponibleException("empleados", "caído") : new ListaEmpleadosErp(activos, DateTimeOffset.UnixEpoch);

        public Task<ListaEmpleadosErp> ObtenerActivosAsync(CancellationToken ct) => Task.FromResult(Lista());

        public Task<ListaEmpleadosErp> ObtenerActivosFrescosAsync(CancellationToken ct) => Task.FromResult(Lista());
    }

    private sealed class TransaccionFalsa : ITransaccionAsignaciones
    {
        public int Llamadas { get; private set; }

        public Task<T> EjecutarAsync<T>(Func<CancellationToken, Task<T>> accion, Func<T, bool> confirmar, CancellationToken ct)
        {
            Llamadas++;
            return accion(ct);
        }
    }

    private sealed class AdminActual : IUsuarioActual
    {
        public int? UsuarioId => 2;
        public string? Email => "admin@prueba.local";
        public string? NombreMostrar => "ADMIN";
        public IReadOnlyCollection<string> Roles => [RolesApp.Admin];
        public bool EstaAutenticado => true;
        public bool TieneRol(string rol) => Roles.Contains(rol);
    }
}
