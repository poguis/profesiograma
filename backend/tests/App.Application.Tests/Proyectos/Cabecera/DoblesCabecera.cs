using App.Application.Erp;
using App.Application.Proyectos.Cabecera;
using App.Application.Proyectos.Estados;
using App.Domain.Proyectos.Cronograma;
using App.Domain.Proyectos.Estados;

namespace App.Application.Tests.Proyectos.Cabecera;

/// <summary>
/// "Proyecto C" de la TAREA-18 (Id 11), CAMPO, DEV-ERP-001, 01/11–30/11/2026 (aún no empieza; hoy = 06/10/2026):
/// P1 (Id 201) DEV007 TIPO_2 01/11–30/11, principal inicial; K1 (Id 202) DEV001 JORNADA 10/11–12/11 + 2 días, relacionado
/// con P1. Actividad DEV.01 v1 CREACION 01/11–30/11. Horario 1, almuerzo 13:00–14:00. Días armados con MotorCronograma.Generar.
/// </summary>
internal static class DoblesCabecera
{
    public const int ProyectoId = 11;
    public static readonly DateOnly Inicio = new(2026, 11, 1), Fin = new(2026, 11, 30), Hoy = new(2026, 10, 6);

    /// <summary>06/10/2026 10:00 en Ecuador.</summary>
    public static readonly DateTimeOffset Ahora = new(2026, 10, 6, 15, 0, 0, TimeSpan.Zero);

    public static readonly PersonaCorte P1 = new(201, RolCronograma.Principal, 1, 7, Inicio, Fin, null);
    public static readonly PersonaCorte K1 = new(202, RolCronograma.Back, 1, 1, new DateOnly(2026, 11, 10), new DateOnly(2026, 11, 12), 201);

    public static PersonalCambio Persona(PersonaCorte p) => p.Rol == RolCronograma.Principal
        ? new PersonalCambio(p, $"DEV{p.EmpleadoId:000}", $"EMPLEADO PRUEBA {p.EmpleadoId:00}", "TIPO_2", 11, 4, "JORNADA")
        : new PersonalCambio(p, $"DEV{p.EmpleadoId:000}", $"EMPLEADO PRUEBA {p.EmpleadoId:00}", null, null, 2, "JORNADA");

    public static ActividadGuardada Actividad(int id, int version, string codigo, DateOnly inicio, DateOnly fin, string tipo = "CREACION") =>
        new(id, version, codigo, codigo == "DEV.01" ? "ACTIVIDAD DE PRUEBA" : "ACTIVIDAD DE PRUEBA 2", "PRUEBA", tipo, inicio, fin);

    /// <summary>Proyecto con todos sus días; el repositorio falso entrega solo los posteriores a la fecha pedida.</summary>
    public static DatosCabecera Proyecto(
        string estado = "ACTIVO", DateOnly? inicio = null, DateOnly? fin = null, string grupo = "CAMPO",
        IReadOnlyList<PersonaCorte>? personal = null, IReadOnlyList<ActividadGuardada>? actividades = null, byte rowVer = 1,
        IReadOnlySet<int>? iniciales = null)
    {
        var i = inicio ?? Inicio;
        var f = fin ?? Fin;
        var personas = personal ?? [P1, K1];
        var generado = MotorCronograma.Generar(new SolicitudCronograma(i, f,
            personas.Where(p => p.Rol == RolCronograma.Principal).Select(p => new PrincipalEntrada(p.Numero, p.EmpleadoId, p.FechaInicio, p.FechaFin, 11, 4)).ToList(),
            personas.Where(p => p.Rol == RolCronograma.Back).Select(p => new BackEntrada(p.Numero, p.EmpleadoId, p.FechaInicio, p.FechaFin,
                TipoRegistroBack.Jornada, 2, (short?)personas.FirstOrDefault(x => x.Id == p.PrincipalRelacionadoId)?.Numero)).ToList()));
        var ids = personas.ToDictionary(p => new PersonaProyecto(p.Rol, p.Numero), p => p.Id);

        return new DatosCabecera(
            new CabeceraGuardada(ProyectoId, "PRY-C", estado, i, f, grupo, grupo == "CAMPO", 9001, grupo == "CAMPO" ? "DEV-ERP-001" : null,
                1, "07:00 - 18:00 (PRUEBA)", new TimeOnly(7, 0), new TimeOnly(18, 0), new TimeOnly(13, 0), new TimeOnly(14, 0), [rowVer]),
            personas.Select(Persona).ToList(),
            iniciales ?? personas.Where(p => p.Id == 201).Select(p => p.Id).ToHashSet(),
            generado.DiasFinales.Select(d => new DiaCorte(ids[d.Persona], d.Fecha, d.Rol)).ToList(),
            actividades ?? [Actividad(1, 1, "DEV.01", i, f)]);
    }
}

/// <summary>Repositorio: cada ObtenerAsync devuelve la siguiente respuesta (la última se repite) con los días &gt; fechaDias.</summary>
internal sealed class RepositorioCabeceraFalso(params DatosCabecera?[] respuestas) : ICabeceraRepositorio
{
    public List<(int Id, int? Propietario, DateOnly FechaDias)> Lecturas { get; } = [];
    public int UltimaVersion { get; set; } = 1;
    /// <summary>TAREA-19x: versiones que devuelven las próximas lecturas de la versión (después, UltimaVersion).</summary>
    public Queue<int> Versiones { get; } = new();

    /// <summary>TAREA-19x: orden de las lecturas ("version" / "datos").</summary>
    public List<string> Orden { get; } = [];
    public bool LanzarConflicto { get; set; }
    public CambioCabeceraAplicar? Aplicado { get; private set; }

    public Task<DatosCabecera?> ObtenerAsync(int proyectoId, int? propietarioUsuarioId, DateOnly fechaDias, CancellationToken ct)
    {
        Lecturas.Add((proyectoId, propietarioUsuarioId, fechaDias));
        Orden.Add("datos");
        var d = respuestas.Length == 0 ? null : respuestas[Math.Min(Lecturas.Count - 1, respuestas.Length - 1)];
        return Task.FromResult(d is null ? null : d with { DiasPosteriores = d.DiasPosteriores.Where(x => x.Fecha > fechaDias).ToList() });
    }

    public Task<int> ObtenerUltimaVersionEtapaAsync(int proyectoId, CancellationToken ct)
    {
        Orden.Add("version");
        return Task.FromResult(Versiones.Count > 0 ? Versiones.Dequeue() : UltimaVersion);
    }

    public Task AplicarAsync(CambioCabeceraAplicar cambio, CancellationToken ct)
    {
        if (LanzarConflicto)
        {
            throw new ConflictoConcurrenciaException("RowVer");
        }

        Aplicado = cambio;
        return Task.CompletedTask;
    }
}

/// <summary>ERP para la cabecera: horarios 1 y 2 (el 3 está inactivo: no se devuelve); actividades DEV.01 y DEV.02 de DEV-ERP-001.</summary>
internal sealed class ErpCabeceraFalso : ICatalogoErp
{
    private static readonly HorarioErp[] Horarios =
    [
        new(1, "07:00 - 18:00 (PRUEBA)", new TimeOnly(7, 0), new TimeOnly(18, 0), 660, 600, null),
        new(2, "08:00 - 17:00 (PRUEBA)", new TimeOnly(8, 0), new TimeOnly(17, 0), 540, 480, "D"),
    ];

    private static readonly ActividadErp[] Actividades =
    [
        new("DEV.01", "ACTIVIDAD DE PRUEBA", "PRUEBA"),
        new("DEV.02", "ACTIVIDAD DE PRUEBA 2", "PRUEBA"),
    ];

    public Task<HorarioErp?> ObtenerHorarioAsync(int codigo, CancellationToken ct) =>
        Task.FromResult(Horarios.FirstOrDefault(h => h.Codigo == codigo));

    public Task<ActividadErp?> ObtenerActividadAsync(int companiaId, string proyectoErpId, string actividadId, CancellationToken ct) =>
        Task.FromResult(companiaId == 9001 && proyectoErpId == "DEV-ERP-001" ? Actividades.FirstOrDefault(a => a.Id == actividadId) : null);

    public Task<IReadOnlyList<ActividadErp>> ListarActividadesAsync(int companiaId, string proyectoErpId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ActividadErp>>(Actividades);

    public Task<IReadOnlyList<HorarioErp>> ListarHorariosAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<HorarioErp>>(Horarios);

    public Task<IReadOnlyList<CompaniaErp>> ListarCompaniasAsync(CancellationToken ct) => throw new NotSupportedException();
    public Task<CompaniaErp?> ObtenerCompaniaAsync(int companiaId, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<ProyectoErp>> ListarProyectosAsync(int companiaId, CancellationToken ct) => throw new NotSupportedException();
    public Task<ProyectoErp?> ObtenerProyectoAsync(int companiaId, string proyectoErpId, CancellationToken ct) => throw new NotSupportedException();
    public Task<IReadOnlyList<DimensionErp>> ListarDimensionesAsync(int companiaId, CancellationToken ct) => throw new NotSupportedException();
    public Task<DimensionErp?> ObtenerDimensionAsync(int companiaId, string uegpId, CancellationToken ct) => throw new NotSupportedException();
}

/// <summary>TAREA-19x: solicitudes de registro con el token de concurrencia (versión del repositorio falso o explícita).</summary>
internal static class ConVersionCabecera
{
    public static EditarCabeceraSolicitud ConVersion(this EditarCabeceraSolicitud s, RepositorioCabeceraFalso repo) => s with { VersionProyecto = repo.UltimaVersion };
    public static EditarCabeceraSolicitud ConVersion(this EditarCabeceraSolicitud s, int version) => s with { VersionProyecto = version };
}
