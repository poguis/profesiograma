using App.Application.Empleados;
using App.Application.Erp;
using App.Application.Proyectos.Crear;
using App.Application.Seguridad;

namespace App.Application.Tests.Proyectos.Crear;

/// <summary>Dobles de prueba para "Crear proyecto" (datos ficticios coherentes con el modo Simulado).</summary>
internal static class Dobles
{
    public const int Compania = 9001;
    public const int DepartamentoSig = 7;

    /// <summary>Solicitud válida: CAMPO, diciembre 2026, P1 = emp 6 TIPO_2, P2 = emp 7 TIPO_3, back emp 8.</summary>
    public static CrearProyectoSolicitud SolicitudCampo() => new(
        CompaniaId: Compania, Grupo: "CAMPO", ProyectoErpId: "DEV-ERP-001", ActividadId: "DEV.01", DimensionUegpId: null,
        FechaInicio: new DateOnly(2026, 12, 1), FechaFin: new DateOnly(2026, 12, 31),
        HorarioCodigo: 1, SalidaAlmuerzo: "13:00", RegresoAlmuerzo: "14:00", DepartamentoId: null,
        Principales:
        [
            new PrincipalSolicitud(6, "TIPO_2", new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), null),
            new PrincipalSolicitud(7, "TIPO_3", new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31), "CARGO MANUAL"),
        ],
        Backs: [new BackSolicitud(8, "JORNADA", 2, new DateOnly(2026, 12, 12), new DateOnly(2026, 12, 15), 1, "Cubre al principal 1", null)]);

    public static CrearProyectoSolicitud SolicitudPlanta() => SolicitudCampo() with
    {
        Grupo = "PLANTA", ProyectoErpId = null, ActividadId = null, DimensionUegpId = "DEV-DIM-01",
    };
}

internal sealed class UsuarioFalso(int? usuarioId = 3) : IUsuarioActual
{
    public int? UsuarioId { get; } = usuarioId;
    public string? Email => "gestor.dev@profesiograma.local";
    public string? NombreMostrar => "GESTOR";
    public IReadOnlyCollection<string> Roles => [RolesApp.Gestor];
    public bool EstaAutenticado => UsuarioId.HasValue;
    public bool TieneRol(string rol) => Roles.Contains(rol);
}

internal sealed class ErpFalso : ICatalogoErp
{
    private static readonly CompaniaErp Compania = new(Dobles.Compania, "COMPAÑÍA DE PRUEBA S.A.", "PRUEBA", "0999999999001");
    private static readonly ProyectoErp Proyecto = new("DEV-ERP-001", "PROYECTO ERP DE PRUEBA", "Activo");
    private static readonly ActividadErp Actividad = new("DEV.01", "ACTIVIDAD DE PRUEBA", "PRUEBA");
    private static readonly DimensionErp[] Dimensiones = [new("DEV-DIM-01", "PLANTA DE PRUEBA"), new("DEV-DIM-02", "OFICINAS DE PRUEBA")];
    private static readonly HorarioErp Horario = new(1, "07:00 - 18:00 (PRUEBA)", new TimeOnly(7, 0), new TimeOnly(18, 0), 660, 600, null);

    public Task<IReadOnlyList<CompaniaErp>> ListarCompaniasAsync(CancellationToken ct) => L(Compania);
    public Task<CompaniaErp?> ObtenerCompaniaAsync(int id, CancellationToken ct) => R(id == Compania.Id ? Compania : null);
    public Task<IReadOnlyList<ProyectoErp>> ListarProyectosAsync(int c, CancellationToken ct) => L(Proyecto);
    public Task<ProyectoErp?> ObtenerProyectoAsync(int c, string id, CancellationToken ct) => R(c == Compania.Id && id == Proyecto.Id ? Proyecto : null);
    public Task<IReadOnlyList<DimensionErp>> ListarDimensionesAsync(int c, CancellationToken ct) => L(Dimensiones);
    public Task<DimensionErp?> ObtenerDimensionAsync(int c, string id, CancellationToken ct) => R(Dimensiones.FirstOrDefault(d => c == Compania.Id && d.UegpId == id));
    public Task<IReadOnlyList<ActividadErp>> ListarActividadesAsync(int c, string p, CancellationToken ct) => L(Actividad);
    public Task<ActividadErp?> ObtenerActividadAsync(int c, string p, string id, CancellationToken ct) => R(p == Proyecto.Id && id == Actividad.Id ? Actividad : null);
    public Task<IReadOnlyList<HorarioErp>> ListarHorariosAsync(CancellationToken ct) => L(Horario);
    public Task<HorarioErp?> ObtenerHorarioAsync(int codigo, CancellationToken ct) => R(codigo == Horario.Codigo ? Horario : null);

    private static Task<IReadOnlyList<T>> L<T>(params T[] items) => Task.FromResult<IReadOnlyList<T>>(items);
    private static Task<T?> R<T>(T? valor) where T : class => Task.FromResult(valor);
}

internal sealed class DatosFalsos : IDatosReferenciaProyecto
{
    public List<DepartamentoRef> DepartamentosUsuario { get; set; } = [new(Dobles.DepartamentoSig, "UNIDAD SISTEMA INTEGRADO DE GESTION")];
    public LimitesProyecto Limites { get; set; } = new(20, 20, 20);

    private static readonly GrupoRef[] Grupos =
    [
        new(1, "CAMPO", true, false),
        new(2, "PLANTA", false, true),
        new(3, "OFICINAS ADMINISTRATIVAS", false, true),
        new(9, "SIN_ORIGEN", false, false),
    ];

    private static readonly JornadaRef[] Jornadas =
        [new(1, "TIPO_1", 22, 8), new(2, "TIPO_2", 11, 4), new(3, "TIPO_3", 5, 2), new(4, "ESPECIAL", 3, 0)];

    /// <summary>
    /// Filas de Empleado 1..9 (DEV001–DEV009). TAREA-26d: activos en la API (EmpleadosErpFalsos) solo 1..8; el 9 existe
    /// pero está inactivo (no está en la API).
    /// </summary>
    internal static readonly Dictionary<int, EmpleadoAsignable> Empleados = Enumerable.Range(1, 9)
        .ToDictionary(i => i, i => new EmpleadoAsignable(i, $"DEV{i:000}", $"EMPLEADO PRUEBA {i:00}", $"PUESTO {i}"));

    public Task<GrupoRef?> ObtenerGrupoAsync(string codigo, CancellationToken ct) =>
        Task.FromResult(Grupos.FirstOrDefault(g => string.Equals(g.Codigo, codigo, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyDictionary<string, JornadaRef>> ObtenerJornadasAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<string, JornadaRef>>(Jornadas.ToDictionary(j => j.Codigo, StringComparer.OrdinalIgnoreCase));

    public Task<LimitesProyecto> ObtenerLimitesAsync(CancellationToken ct) => Task.FromResult(Limites);

    public Task<IReadOnlyDictionary<int, EmpleadoAsignable>> ObtenerEmpleadosPorIdsAsync(IReadOnlyCollection<int> ids, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<int, EmpleadoAsignable>>(ids.Where(Empleados.ContainsKey).ToDictionary(i => i, i => Empleados[i]));

    public Task<IReadOnlyDictionary<string, int>> ObtenerIdsPorCodigosAsync(IReadOnlyCollection<string> codigos, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<string, int>>(Empleados.Values
            .Where(e => codigos.Contains(e.CodigoEkon, StringComparer.OrdinalIgnoreCase))
            .ToDictionary(e => e.CodigoEkon, e => e.Id, StringComparer.OrdinalIgnoreCase));

    public Task<IReadOnlyList<DepartamentoRef>> ObtenerDepartamentosDeUsuarioAsync(int usuarioId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DepartamentoRef>>(DepartamentosUsuario);
}

/// <summary>
/// TAREA-26d: API de empleados falsa. Activos = DEV001–DEV008 con el mismo nombre y puesto que sus filas (DatosFalsos)
/// más los "extra" indicados (personas sin fila). Cuenta las lecturas en caché y las frescas.
/// </summary>
internal sealed class EmpleadosErpFalsos(params EmpleadoErp[] extra) : IFuenteEmpleadosErp
{
    public int Lecturas { get; private set; }
    public int LecturasFrescas { get; private set; }
    public Exception? Error { get; set; }

    public static EmpleadoErp Activo(string codigo, string nombre, string? puesto) =>
        new(codigo, nombre, null, null, null, null, null, null, puesto, null, null, null, null, null, null, null, null, null, "A");

    private ListaEmpleadosErp Lista() =>
        new(DatosFalsos.Empleados.Values.Where(e => e.Id <= 8).Select(e => Activo(e.CodigoEkon, e.NombreCompleto, e.Puesto))
            .Concat(extra).ToList(), DateTimeOffset.UnixEpoch);

    public Task<ListaEmpleadosErp> ObtenerActivosAsync(CancellationToken ct)
    {
        Lecturas++;
        return Error is null ? Task.FromResult(Lista()) : Task.FromException<ListaEmpleadosErp>(Error);
    }

    public Task<ListaEmpleadosErp> ObtenerActivosFrescosAsync(CancellationToken ct)
    {
        LecturasFrescas++;
        return Error is null ? Task.FromResult(Lista()) : Task.FromException<ListaEmpleadosErp>(Error);
    }
}

/// <summary>Cruces externos: respuesta por llamada (la 1.ª es fuera de la transacción, la 2.ª dentro).</summary>
internal sealed class CrucesExternosFalsos(params IReadOnlyList<AsignacionExistente>[] respuestas) : IConsultaCrucesExternos
{
    public int Llamadas { get; private set; }

    public Task<IReadOnlyList<AsignacionExistente>> BuscarAsync(
        IReadOnlyCollection<int> empleadoIds, DateOnly desde, DateOnly hasta, int? excluirProyectoId, CancellationToken ct)
    {
        var respuesta = respuestas.Length == 0 ? [] : respuestas[Math.Min(Llamadas, respuestas.Length - 1)];
        Llamadas++;
        return Task.FromResult<IReadOnlyList<AsignacionExistente>>(
            respuesta.Where(a => empleadoIds.Contains(a.EmpleadoId) && a.Fecha >= desde && a.Fecha <= hasta).ToList());
    }
}

/// <summary>Repositorio: ExisteCodigoAsync responde según la secuencia indicada (true = el código ya existe).</summary>
internal sealed class RepositorioFalso(params bool[] codigosExistentes) : IProyectoRepositorio
{
    public List<string> CodigosConsultados { get; } = [];
    public (NuevoProyecto Proyecto, string Codigo, Guid Uid)? Agregado { get; private set; }

    public Task<bool> ExisteCodigoAsync(string codigo, CancellationToken ct)
    {
        var existe = CodigosConsultados.Count < codigosExistentes.Length && codigosExistentes[CodigosConsultados.Count];
        CodigosConsultados.Add(codigo);
        return Task.FromResult(existe);
    }

    public Task<int> AgregarAsync(NuevoProyecto proyecto, string codigo, Guid uid, CancellationToken ct)
    {
        Agregado = (proyecto, codigo, uid);
        return Task.FromResult(99);
    }
}

internal sealed class TransaccionFalsa : ITransaccionAsignaciones
{
    public int Iniciadas { get; private set; }
    public int Confirmadas { get; private set; }
    public int Revertidas { get; private set; }

    public async Task<T> EjecutarAsync<T>(Func<CancellationToken, Task<T>> accion, Func<T, bool> confirmar, CancellationToken ct)
    {
        Iniciadas++;
        try
        {
            var resultado = await accion(ct);
            if (confirmar(resultado)) Confirmadas++; else Revertidas++;
            return resultado;
        }
        catch
        {
            Revertidas++;
            throw;
        }
    }
}

/// <summary>Reloj fijo (UTC).</summary>
internal sealed class RelojFijo(DateTimeOffset utc) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => utc;
}
