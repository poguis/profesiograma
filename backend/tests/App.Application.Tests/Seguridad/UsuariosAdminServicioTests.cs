using App.Application.Comun;
using App.Application.Seguridad;

namespace App.Application.Tests.Seguridad;

/// <summary>TAREA-25-1: reglas de la administración de usuarios y departamentos (repositorio falso). Datos FICTICIOS.</summary>
public sealed class UsuariosAdminServicioTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private const int AdminId = 2, OtroId = 5;

    // ------------------------------------------------------------------ alta

    [Theory]
    [InlineData(null, UsuariosAdminServicio.MensajeEmailVacio)]
    [InlineData("   ", UsuariosAdminServicio.MensajeEmailVacio)]
    [InlineData("sin-arroba", UsuariosAdminServicio.MensajeEmailFormato)]
    [InlineData("Nombre <persona@sedemi.com>", UsuariosAdminServicio.MensajeEmailFormato)]
    [InlineData("persona@otro.com", "El correo debe ser de un dominio permitido: sedemi.com, prueba.local.")]
    public async Task Crear_EmailInvalido_400(string? email, string mensaje)
    {
        var e = new Escenario();

        var r = await e.Servicio.CrearAsync(new CrearUsuarioSolicitud(email, "PERSONA FICTICIA"), Ct);

        Assert.Equal(EstadoAdminUsuario.Invalido, r.Estado);
        Assert.Equal([mensaje], r.Errores!["email"]);
        Assert.Null(e.Repo.Creado);
    }

    [Fact]
    public async Task Crear_EmailLargo_400()
    {
        var r = await new Escenario().Servicio.CrearAsync(new CrearUsuarioSolicitud(new string('a', 250) + "@sedemi.com", "X"), Ct);

        Assert.Equal([UsuariosAdminServicio.MensajeEmailLargo], r.Errores!["email"]);
    }

    [Theory]
    [InlineData(null, UsuariosAdminServicio.MensajeNombreVacio)]
    [InlineData("  ", UsuariosAdminServicio.MensajeNombreVacio)]
    [InlineData("201", UsuariosAdminServicio.MensajeNombreLargo)]
    public async Task Crear_NombreInvalido_400(string? nombre, string mensaje)
    {
        var valor = nombre == "201" ? new string('N', 201) : nombre;

        var r = await new Escenario().Servicio.CrearAsync(new CrearUsuarioSolicitud("persona@sedemi.com", valor), Ct);

        Assert.Equal([mensaje], r.Errores!["nombreMostrar"]);
    }

    [Fact]
    public async Task Crear_NormalizaCorreo_201_EInvalidaCache()
    {
        var e = new Escenario();

        var r = await e.Servicio.CrearAsync(new CrearUsuarioSolicitud("  Persona.Nueva@SEDEMI.com ", "  PERSONA NUEVA "), Ct);

        Assert.Equal(EstadoAdminUsuario.Ok, r.Estado);
        Assert.Equal(("persona.nueva@sedemi.com", "PERSONA NUEVA"), e.Repo.Creado);
        Assert.Equal(["persona.nueva@sedemi.com|"], e.Cache.Invalidados);
    }

    [Fact]
    public async Task Crear_CorreoExistente_409ConUsuarioId_SistemaSinId()
    {
        var e = new Escenario();

        var r = await e.Servicio.CrearAsync(new CrearUsuarioSolicitud("OTRO@sedemi.com", "X"), Ct);
        var sistema = await e.Servicio.CrearAsync(new CrearUsuarioSolicitud("sistema@prueba.local", "X"), Ct);

        Assert.Equal((EstadoAdminUsuario.Conflicto, OtroId), (r.Estado, r.UsuarioExistenteId));
        Assert.Equal((EstadoAdminUsuario.Conflicto, (int?)null), (sistema.Estado, sistema.UsuarioExistenteId));
        Assert.Null(e.Repo.Creado);
    }

    [Fact]
    public async Task Crear_SinConfiguracion_SoloSedemi()
    {
        var e = new Escenario(dominios: []);

        var r = await e.Servicio.CrearAsync(new CrearUsuarioSolicitud("persona@prueba.local", "X"), Ct);

        Assert.Equal(["El correo debe ser de un dominio permitido: sedemi.com."], r.Errores!["email"]);
    }

    // ------------------------------------------------------------------ edición, baja y reactivación

    [Fact]
    public async Task Editar_Baja_200_InvalidaPorOidYCorreo()
    {
        var e = new Escenario();

        var r = await e.Servicio.EditarAsync(OtroId, new EditarUsuarioSolicitud(" OTRO ", false, "OTRO", true), Ct);

        Assert.Equal(EstadoAdminUsuario.Ok, r.Estado);
        Assert.Equal((OtroId, "OTRO", true, "OTRO", false, AdminId), e.Repo.Actualizado);
        Assert.Equal([$"otro@sedemi.com|{Escenario.OidOtro}"], e.Cache.Invalidados);
    }

    [Fact]
    public async Task Editar_ValoresAnterioresDesfasados_409_SinInvalidar()
    {
        var e = new Escenario(filasActualizadas: 0);

        var r = await e.Servicio.EditarAsync(OtroId, new EditarUsuarioSolicitud("OTRO", false, "VIEJO", true), Ct);

        Assert.Equal(EstadoAdminUsuario.Conflicto, r.Estado);
        Assert.Empty(e.Cache.Invalidados);
    }

    [Fact]
    public async Task Editar_BajaPropia_400_EnActivo()
    {
        var e = new Escenario();

        var r = await e.Servicio.EditarAsync(AdminId, new EditarUsuarioSolicitud("ADMIN", false, "ADMIN", true), Ct);

        Assert.Equal([UsuariosAdminServicio.MensajeBajaPropia], r.Errores!["activo"]);
        Assert.Null(e.Repo.Actualizado);
    }

    [Fact]
    public async Task Editar_PropioNombre_200()
    {
        var r = await new Escenario().Servicio.EditarAsync(AdminId, new EditarUsuarioSolicitud("ADMIN 2", true, "ADMIN", true), Ct);

        Assert.Equal(EstadoAdminUsuario.Ok, r.Estado);
    }

    [Theory]
    [InlineData(1)]   // SISTEMA
    [InlineData(99)]  // inexistente
    public async Task Editar_SistemaOInexistente_404(int id)
    {
        var e = new Escenario();

        var r = await e.Servicio.EditarAsync(id, new EditarUsuarioSolicitud("X", true, "X", true), Ct);

        Assert.Equal(EstadoAdminUsuario.NoEncontrado, r.Estado);
        Assert.Null(e.Repo.Actualizado);
    }

    [Fact]
    public async Task Editar_FaltanDatos_400()
    {
        var r = await new Escenario().Servicio.EditarAsync(OtroId, new EditarUsuarioSolicitud("", null, null, null), Ct);

        Assert.Equal(["activo", "anterior", "nombreMostrar"], r.Errores!.Keys.Order());
    }

    // ------------------------------------------------------------------ departamentos

    [Fact]
    public async Task Departamentos_RepetidoInexistenteEInactivoNuevo_400()
    {
        var e = new Escenario();

        var r = await e.Servicio.AsignarDepartamentosAsync(OtroId, new AsignarDepartamentosSolicitud([1, 1, 50, 9], [1]), Ct);

        Assert.Equal([UsuariosAdminServicio.MensajeDepartamentoRepetido(1), UsuariosAdminServicio.MensajeDepartamentoInvalido(50),
            UsuariosAdminServicio.MensajeDepartamentoInvalido(9)], r.Errores!["departamentoIds"]);
        Assert.Null(e.Repo.Asignado);
    }

    [Fact]
    public async Task Departamentos_InactivoConservado_Y_Vacio_200()
    {
        var e = new Escenario();

        var conservado = await e.Servicio.AsignarDepartamentosAsync(OtroId, new AsignarDepartamentosSolicitud([9, 2], [9]), Ct);
        var vacio = await e.Servicio.AsignarDepartamentosAsync(OtroId, new AsignarDepartamentosSolicitud([], [9, 2]), Ct);

        Assert.Equal([2, 9], conservado.Valor!.Departamentos.Select(d => d.Id)); // por Orden
        Assert.Equal(EstadoAdminUsuario.Ok, vacio.Estado);
        Assert.Empty(vacio.Valor!.Departamentos);
    }

    [Fact]
    public async Task Departamentos_ConjuntoCambiado_409()
    {
        var r = await new Escenario(conflictoDepartamentos: true).Servicio
            .AsignarDepartamentosAsync(OtroId, new AsignarDepartamentosSolicitud([1], []), Ct);

        Assert.Equal(EstadoAdminUsuario.Conflicto, r.Estado);
    }

    [Fact]
    public async Task Departamentos_SinListas_400_YSistema404()
    {
        var e = new Escenario();

        var r = await e.Servicio.AsignarDepartamentosAsync(OtroId, new AsignarDepartamentosSolicitud(null, null), Ct);
        var sistema = await e.Servicio.AsignarDepartamentosAsync(1, new AsignarDepartamentosSolicitud([1], []), Ct);

        Assert.Equal(["anteriores", "departamentoIds"], r.Errores!.Keys.Order());
        Assert.Equal(EstadoAdminUsuario.NoEncontrado, sistema.Estado);
    }

    [Fact]
    public async Task Detalle_DisponiblesActivosMasInactivoAsignado_YUsuarioActual()
    {
        var e = new Escenario(departamentosDelOtro: [new DepartamentoUsuarioDto(9, "VIEJO")]);

        var otro = await e.Servicio.ObtenerDetalleAsync(OtroId, Ct);
        var propio = await e.Servicio.ObtenerDetalleAsync(AdminId, Ct);

        Assert.Equal([(1, true, false), (2, true, false), (9, false, true)],
            otro!.DepartamentosDisponibles.Select(d => (d.Id, d.Activo, d.Asignado)));
        Assert.False(otro.EsUsuarioActual);
        Assert.Equal([1, 2], propio!.DepartamentosDisponibles.Select(d => d.Id)); // el inactivo no asignado no aparece
        Assert.True(propio.EsUsuarioActual);
        Assert.Null(await e.Servicio.ObtenerDetalleAsync(1, Ct));
    }

    [Fact]
    public async Task Listar_TamanoMayorA100_400()
    {
        var r = await new Escenario().Servicio.ListarAsync(new UsuarioAdminListadoSolicitud(null, "si", "0", "101"), Ct);

        Assert.Equal(["incluirInactivos", "pagina", "tamano"], r.Errores!.Keys.Order());
    }

    // ------------------------------------------------------------------ dobles

    private sealed class Escenario
    {
        public static readonly Guid OidOtro = Guid.Parse("55555555-5555-5555-5555-555555555555");

        public Escenario(IReadOnlyList<string>? dominios = null, int filasActualizadas = 1, bool conflictoDepartamentos = false,
            IReadOnlyList<DepartamentoUsuarioDto>? departamentosDelOtro = null)
        {
            Repo = new RepoFalso(filasActualizadas, conflictoDepartamentos, departamentosDelOtro ?? []);
            Servicio = new UsuariosAdminServicio(Repo, Cache, new AdminActual(),
                new ConfiguracionUsuarios(dominios ?? ["sedemi.com", "prueba.local"]), TimeProvider.System);
        }

        public RepoFalso Repo { get; }
        public CacheFalsa Cache { get; } = new();
        public UsuariosAdminServicio Servicio { get; }
    }

    private sealed class RepoFalso(int filas, bool conflicto, IReadOnlyList<DepartamentoUsuarioDto> departamentosDelOtro) : IUsuariosAdminRepositorio
    {
        private readonly UsuarioRegistro[] usuarios =
        [
            new(1, "sistema@prueba.local", null, "SISTEMA", true, true),
            new(AdminId, "admin@prueba.local", null, "ADMIN", true, false),
            new(OtroId, "otro@sedemi.com", Escenario.OidOtro, "OTRO", true, false),
        ];

        public (string Email, string Nombre)? Creado { get; private set; }
        public (int, string, bool, string, bool, int)? Actualizado { get; private set; }
        public (IReadOnlySet<int>, IReadOnlySet<int>)? Asignado { get; private set; }

        public Task<PaginaResultado<UsuarioAdminDto>> ListarAsync(UsuarioAdminFiltro filtro, CancellationToken ct) =>
            Task.FromResult(new PaginaResultado<UsuarioAdminDto>([], filtro.Pagina, filtro.Tamano, 0));

        public Task<UsuarioAdminDto?> ObtenerAsync(int id, CancellationToken ct) =>
            Task.FromResult(usuarios.FirstOrDefault(u => u.Id == id && !u.EsSistema) is { } u
                ? new UsuarioAdminDto(u.Id, u.Email, u.NombreMostrar, u.Activo, u.EntraObjectId is not null, u.Id == OtroId ? departamentosDelOtro : [])
                : null);

        public Task<UsuarioRegistro?> ObtenerRegistroAsync(int id, CancellationToken ct) =>
            Task.FromResult(usuarios.FirstOrDefault(u => u.Id == id));

        public Task<UsuarioRegistro?> BuscarPorEmailAsync(string email, CancellationToken ct) =>
            Task.FromResult(usuarios.FirstOrDefault(u => string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)));

        public Task<IReadOnlyList<DepartamentoRegistro>> ObtenerDepartamentosAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<DepartamentoRegistro>>(
                [new(9, "DEPARTAMENTO VIEJO", "VIEJO", false, 9), new(2, "DEPARTAMENTO B", "B", true, 2), new(1, "DEPARTAMENTO A", "A", true, 1)]);

        public Task<int?> CrearAsync(string email, string nombreMostrar, CancellationToken ct)
        {
            Creado = (email, nombreMostrar);
            return Task.FromResult<int?>(10);
        }

        public Task<int> ActualizarAsync(int id, string nombreAnterior, bool activoAnterior, string nombre, bool activo, int usuarioId,
            DateTime fechaUtc, CancellationToken ct)
        {
            Actualizado = (id, nombreAnterior, activoAnterior, nombre, activo, usuarioId);
            return Task.FromResult(filas);
        }

        public Task<CambioDepartamentos> AsignarDepartamentosAsync(int usuarioId, IReadOnlySet<int> anteriores, IReadOnlySet<int> nuevos,
            CancellationToken ct)
        {
            Asignado = (anteriores, nuevos);
            return Task.FromResult(new CambioDepartamentos(conflicto, [], []));
        }
    }

    private sealed class CacheFalsa : IUsuarioProvisionamiento
    {
        public List<string> Invalidados { get; } = [];

        public Task<UsuarioResuelto> ResolverAsync(Guid? entraObjectId, string email, string nombreMostrar, CancellationToken ct) =>
            throw new NotSupportedException();

        public void Invalidar(string email, Guid? entraObjectId) => Invalidados.Add($"{email}|{entraObjectId}");
    }

    private sealed class AdminActual : IUsuarioActual
    {
        public int? UsuarioId => AdminId;
        public string? Email => "admin@prueba.local";
        public string? NombreMostrar => "ADMIN";
        public IReadOnlyCollection<string> Roles => [RolesApp.Admin];
        public bool EstaAutenticado => true;
        public bool TieneRol(string rol) => Roles.Contains(rol);
    }
}
