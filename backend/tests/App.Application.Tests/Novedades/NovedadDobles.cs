using App.Application.Comun;
using App.Application.Empleados;
using App.Application.Novedades;
using App.Application.Seguridad;
using App.Application.Tests.Proyectos.Crear;
using App.Domain.Catalogos;

namespace App.Application.Tests.Novedades;

/// <summary>Usuario de prueba: admin (Id 1) o gestor (Id 3 por defecto). Datos ficticios.</summary>
internal sealed class UsuarioNovedad(bool admin, int? usuarioId = null) : IUsuarioActual
{
    public static UsuarioNovedad Admin() => new(true, 1);
    public static UsuarioNovedad Gestor(int id = 3) => new(false, id);

    public int? UsuarioId { get; } = usuarioId;
    public string? Email => admin ? "admin.dev@profesiograma.local" : "gestor.dev@profesiograma.local";
    public string? NombreMostrar => admin ? "ADMIN DESARROLLO" : "GESTOR DESARROLLO";
    public IReadOnlyCollection<string> Roles => [admin ? RolesApp.Admin : RolesApp.Gestor];
    public bool EstaAutenticado => UsuarioId.HasValue;
    public bool TieneRol(string rol) => Roles.Contains(rol);
}

/// <summary>
/// Consultas falsas: los 7 tipos de las semillas (mismas banderas) y proyectos elegibles por Id → propietario
/// (10 del usuario 3, 20 de otro usuario; ninguno más).
/// </summary>
internal sealed class NovedadConsultasFalsas : INovedadConsultas
{
    public static readonly IReadOnlyList<TipoNovedadRef> Tipos =
    [
        new(1, "CALAMIDAD_DOMESTICA", "CALAMIDAD DOMESTICA", "CD", "#8E44AD", true, false, false, true),
        new(2, "DESCANSO", "DESCANSO", "D", "#607D8B", true, false, false, false),
        new(3, "FERIADO", "FERIADO", "F", "#D13438", false, true, true, true),
        new(4, "PATERNIDAD", "PATERNIDAD", "PT", "#27AE60", true, false, false, true),
        new(5, "PERMISO", "PERMISO", "P", "#FFB900", true, false, false, true),
        new(6, "VACACIONES", "VACACIONES", "V", "#0277BD", true, false, false, true),
        new(7, "PERMISO_MEDICO", "PERMISO MEDICO", "PM", "#FFB900", true, false, false, false),
    ];

    public Dictionary<int, int> ProyectosElegibles { get; } = new() { [10] = 3, [20] = 99 };
    public NovedadDetalleDatos? Detalle { get; set; }
    public (int Id, int? Visible)? DetallePedido { get; private set; }
    public NovedadFiltro? FiltroRecibido { get; private set; }

    public Task<PaginaResultado<NovedadResumenDto>> ListarAsync(NovedadFiltro filtro, CancellationToken ct)
    {
        FiltroRecibido = filtro;
        return Task.FromResult(new PaginaResultado<NovedadResumenDto>([], filtro.Pagina, filtro.Tamano, 0));
    }

    public Task<NovedadDetalleDatos?> ObtenerDetalleAsync(int id, int? visibleParaUsuarioId, CancellationToken ct)
    {
        DetallePedido = (id, visibleParaUsuarioId);
        return Task.FromResult(Detalle?.Id == id ? Detalle : null);
    }

    public Task<IReadOnlyList<TipoNovedadRef>> ObtenerTiposAsync(CancellationToken ct) => Task.FromResult(Tipos);

    public Task<bool> ProyectoElegibleAsync(int proyectoId, int? propietarioUsuarioId, CancellationToken ct) =>
        Task.FromResult(ProyectosElegibles.TryGetValue(proyectoId, out var propietario)
                        && (propietarioUsuarioId is null || propietarioUsuarioId == propietario));
}

/// <summary>Repositorio falso: duplicado configurable y códigos existentes por secuencia (true = ya existe).</summary>
internal sealed class NovedadRepositorioFalso(params bool[] codigosExistentes) : INovedadRepositorio
{
    public DuplicadoNovedad? Duplicado { get; set; }
    public (CriterioDuplicado Criterio, int? Visible)? DuplicadoPedido { get; private set; }
    public List<string> CodigosConsultados { get; } = [];
    public NuevaNovedad? Agregada { get; private set; }

    public Task<DuplicadoNovedad?> BuscarDuplicadoAsync(CriterioDuplicado criterio, int? visibleParaUsuarioId, CancellationToken ct)
    {
        DuplicadoPedido = (criterio, visibleParaUsuarioId);
        return Task.FromResult(Duplicado);
    }

    public Task<bool> ExisteCodigoOUidAsync(string codigo, Guid uid, CancellationToken ct)
    {
        var existe = CodigosConsultados.Count < codigosExistentes.Length && codigosExistentes[CodigosConsultados.Count];
        CodigosConsultados.Add(codigo);
        return Task.FromResult(existe);
    }

    public Task<NovedadCreadaDto> AgregarAsync(NuevaNovedad nueva, CancellationToken ct)
    {
        Agregada = nueva;
        return Task.FromResult(new NovedadCreadaDto(77, nueva.Codigo, "AAAAAAAAB9E="));
    }
}

/// <summary>API de empleados con cédulas FICTICIAS solo en la lectura fresca (D15): SIM001 activo sin fila.</summary>
internal sealed class FuenteEmpleadosConCedulas : IFuenteEmpleadosErp
{
    public const string CedulaFicticia = "0000000001";
    public int LecturasFrescas { get; private set; }

    private static ListaEmpleadosErp Lista(CedulasEmpleados cedulas) =>
        new([EmpleadosErpFalsos.Activo("SIM001", "PERSONA SIMULADA 001", "AUXILIAR")], DateTimeOffset.UnixEpoch, cedulas: cedulas);

    public Task<ListaEmpleadosErp> ObtenerActivosAsync(CancellationToken ct) => Task.FromResult(Lista(CedulasEmpleados.Vacio));

    public Task<ListaEmpleadosErp> ObtenerActivosFrescosAsync(CancellationToken ct)
    {
        LecturasFrescas++;
        return Task.FromResult(Lista(new CedulasEmpleados([("SIM001", CedulaFicticia)])));
    }
}

internal static class NovedadPrueba
{
    /// <summary>22/10/2026 03:00 UTC = 21/10/2026 22:00 en Ecuador: el código lleva 20261021.</summary>
    public static readonly RelojFijo Reloj = new(new DateTimeOffset(2026, 10, 22, 3, 0, 0, TimeSpan.Zero));

    public static CrearNovedadSolicitud Persona(string codigo = "DEV001", string tipo = "VACACIONES") =>
        new("PERSONA", tipo, codigo, null, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 5), null);

    public static CrearNovedadSolicitud Proyecto(int proyectoId = 10, string tipo = "FERIADO") =>
        new("PROYECTO", tipo, null, proyectoId, new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 2), null);

    public static CrearNovedadSolicitud General(string tipo = "FERIADO") =>
        new("GENERAL", tipo, null, null, new DateOnly(2026, 12, 25), new DateOnly(2026, 12, 25), null);

    public static NovedadDetalleDatos Detalle(
        byte aplicacion = CatalogoIds.TipoAplicacionNovedad.Persona, int creadoPor = 3, int? propietario = null,
        bool origenEditable = true, bool anulada = false) =>
        new(5, "NOV-20261021-abc123", aplicacion, new TipoNovedadResumenDto("VACACIONES", "VACACIONES", "V", "#0277BD"),
            origenEditable ? "PROFESIOGRAMA" : "PERMISOS_MEDICOS", origenEditable,
            new SujetoNovedadDto(new EmpleadoSujetoDto("DEV001", "EMPLEADO PRUEBA 01"), null), propietario,
            new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 3), [new(2026, 11, 2), new(2026, 11, 3)], "Prueba",
            anulada, anulada ? new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc) : null, anulada ? "Motivo de prueba" : null,
            "REGISTRANTE FICTICIO", "registrante@profesiograma.local", creadoPor,
            new DateTime(2026, 10, 21, 15, 0, 0, DateTimeKind.Utc), [0, 0, 0, 0, 0, 0, 7, 209]);
}

/// <summary>Arma el servicio de escritura con sus dobles.</summary>
internal sealed class EscenarioNovedad
{
    public EscenarioNovedad(IUsuarioActual usuario, IFuenteEmpleadosErp? empleados = null, params bool[] codigosExistentes)
    {
        Empleados = empleados ?? new EmpleadosErpFalsos();
        Repositorio = new NovedadRepositorioFalso(codigosExistentes);
        Validador = new NovedadValidador(Consultas, new DatosFalsos(), Empleados, usuario);
        Servicio = new NovedadEscrituraServicio(Validador, Repositorio, Transaccion, usuario, NovedadPrueba.Reloj);
    }

    public NovedadConsultasFalsas Consultas { get; } = new();
    public IFuenteEmpleadosErp Empleados { get; }
    public NovedadRepositorioFalso Repositorio { get; }
    public TransaccionFalsa Transaccion { get; } = new();
    public NovedadValidador Validador { get; }
    public NovedadEscrituraServicio Servicio { get; }
}
