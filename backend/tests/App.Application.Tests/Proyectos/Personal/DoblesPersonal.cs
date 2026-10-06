using App.Application.Proyectos.Crear;
using App.Application.Proyectos.Estados;
using App.Application.Proyectos.Personal;
using App.Application.Proyectos.Reactivacion;
using App.Domain.Proyectos.Cronograma;
using App.Domain.Proyectos.Estados;

namespace App.Application.Tests.Proyectos.Personal;

/// <summary>
/// "Proyecto A" del plan de la TAREA-17 (Id 9), CAMPO 21/09–29/11/2026, corte 02/10/2026:
/// P1 (Id 91) DEV006 TIPO_3 21/09–29/11 (vigente que ya empezó); P2 (Id 92) DEV007 TIPO_2 21/09–30/09 (histórico);
/// K1 (Id 93) DEV007 JORNADA 20/10–23/10 + 2 días, relacionado con P1 (vigente que aún no empieza).
/// La base (días &lt; corte) se arma con MotorCronograma.Generar, como al crearlo.
/// </summary>
internal static class DoblesPersonal
{
    public const int ProyectoId = 9;
    public static readonly DateOnly Inicio = new(2026, 9, 21), Fin = new(2026, 11, 29), Corte = new(2026, 10, 2);

    /// <summary>02/10/2026 10:00 en Ecuador.</summary>
    public static readonly DateTimeOffset Ahora = new(2026, 10, 2, 15, 0, 0, TimeSpan.Zero);

    public static readonly PersonaGuardada P1 = new(91, RolCronograma.Principal, 1, 6, "DEV006", "EMPLEADO PRUEBA 06",
        Inicio, Fin, "TIPO_3", 5, 2, "JORNADA", null, "PUESTO 6", null, true);
    public static readonly PersonaGuardada P2 = new(92, RolCronograma.Principal, 2, 7, "DEV007", "EMPLEADO PRUEBA 07",
        Inicio, new DateOnly(2026, 9, 30), "TIPO_2", 11, 4, "JORNADA", null, "PUESTO 7", null, false);
    public static readonly PersonaGuardada K1 = new(93, RolCronograma.Back, 1, 7, "DEV007", "EMPLEADO PRUEBA 07",
        new DateOnly(2026, 10, 20), new DateOnly(2026, 10, 23), null, null, 2, "JORNADA", 91, "PUESTO 7", "Back de prueba", false);

    public static DatosEdicion Proyecto(string estado = "ACTIVO", DateOnly? fin = null, DateOnly? corte = null)
    {
        var generado = MotorCronograma.Generar(new SolicitudCronograma(Inicio, Fin,
            [new PrincipalEntrada(1, 6, Inicio, Fin, 5, 2), new PrincipalEntrada(2, 7, Inicio, new DateOnly(2026, 9, 30), 11, 4)],
            [new BackEntrada(1, 7, K1.FechaInicio, K1.FechaFin, TipoRegistroBack.Jornada, 2, 1)]));
        var ids = new Dictionary<PersonaProyecto, int>
        {
            [new(RolCronograma.Principal, 1)] = 91,
            [new(RolCronograma.Principal, 2)] = 92,
            [new(RolCronograma.Back, 1)] = 93,
        };

        var c = corte ?? Corte;
        return new DatosEdicion(ProyectoId, "PRY-A", estado, Inicio, fin ?? Fin, [P1, P2, K1],
            generado.DiasFinales.Where(d => d.Fecha < c).Select(d => new DiaGuardado(ids[d.Persona], d)).ToList(),
            [new ActividadCorte(1, 1, "DEV.01", Inicio, Fin)]);
    }

    // Cuerpos de solicitud
    public static PrincipalEdicionSolicitud SolP1(string jornada = "TIPO_3", DateOnly? fin = null, int? empleado = 6, DateOnly? inicio = null) =>
        new("p1", 91, empleado, jornada, inicio ?? Inicio, fin ?? Fin, null);

    public static BackEdicionSolicitud SolK1(DateOnly? inicio = null) =>
        new("k1", 93, 7, "JORNADA", inicio ?? K1.FechaInicio, K1.FechaFin, 2, "p1", null, "Back de prueba");

    public static BackEdicionSolicitud BackNuevo(string clave, int empleado, string desde, string hasta, string? principalClave = null,
        int? principalId = null) =>
        new(clave, null, empleado, "JORNADA", DateOnly.Parse(desde, System.Globalization.CultureInfo.InvariantCulture),
            DateOnly.Parse(hasta, System.Globalization.CultureInfo.InvariantCulture), 0, principalClave, principalId, null);
}

/// <summary>Repositorio: cada ObtenerAsync devuelve la siguiente respuesta (la última se repite).</summary>
internal sealed class RepositorioEdicionFalso(params DatosEdicion?[] respuestas) : IEdicionPersonalRepositorio
{
    public List<(int Id, int? Propietario, DateOnly Corte)> Lecturas { get; } = [];
    public int UltimaVersion { get; set; } = 1;
    /// <summary>TAREA-19x: versiones que devuelven las próximas lecturas de la versión (después, UltimaVersion).</summary>
    public Queue<int> Versiones { get; } = new();

    /// <summary>TAREA-19x: orden de las lecturas ("version" / "datos").</summary>
    public List<string> Orden { get; } = [];
    public bool LanzarConflicto { get; set; }
    public CambioPersonal? Aplicado { get; private set; }

    public Task<DatosEdicion?> ObtenerAsync(int proyectoId, int? propietarioUsuarioId, DateOnly corte, CancellationToken ct)
    {
        Lecturas.Add((proyectoId, propietarioUsuarioId, corte));
        Orden.Add("datos");
        return Task.FromResult(respuestas.Length == 0 ? null : respuestas[Math.Min(Lecturas.Count - 1, respuestas.Length - 1)]);
    }

    public Task<int> ObtenerUltimaVersionEtapaAsync(int proyectoId, CancellationToken ct)
    {
        Orden.Add("version");
        return Task.FromResult(Versiones.Count > 0 ? Versiones.Dequeue() : UltimaVersion);
    }

    public Task AplicarAsync(CambioPersonal cambio, CancellationToken ct)
    {
        if (LanzarConflicto)
        {
            throw new ConflictoConcurrenciaException("duplicado");
        }
        Aplicado = cambio;
        return Task.CompletedTask;
    }
}

/// <summary>Cruces externos: respuesta por llamada; registra el proyecto excluido.</summary>
internal sealed class CrucesEdicionFalsos(params IReadOnlyList<AsignacionExistente>[] respuestas) : IConsultaCrucesExternos
{
    public List<int?> Excluidos { get; } = [];

    public Task<IReadOnlyList<AsignacionExistente>> BuscarAsync(
        IReadOnlyCollection<int> empleadoIds, DateOnly desde, DateOnly hasta, int? excluirProyectoId, CancellationToken ct)
    {
        var respuesta = respuestas.Length == 0 ? [] : respuestas[Math.Min(Excluidos.Count, respuestas.Length - 1)];
        Excluidos.Add(excluirProyectoId);
        return Task.FromResult<IReadOnlyList<AsignacionExistente>>(
            respuesta.Where(a => empleadoIds.Contains(a.EmpleadoId) && a.Fecha >= desde && a.Fecha <= hasta).ToList());
    }
}

/// <summary>TAREA-19x: solicitudes de registro con el token de concurrencia (versión del repositorio falso o explícita).</summary>
internal static class ConVersionPersonal
{
    public static ActualizarPersonalSolicitud ConVersion(this ActualizarPersonalSolicitud s, RepositorioEdicionFalso repo) => s with { VersionProyecto = repo.UltimaVersion };
    public static ActualizarPersonalSolicitud ConVersion(this ActualizarPersonalSolicitud s, int version) => s with { VersionProyecto = version };
    public static ReactivarProyectoSolicitud ConVersion(this ReactivarProyectoSolicitud s, RepositorioEdicionFalso repo) => s with { VersionProyecto = repo.UltimaVersion };
    public static ReactivarProyectoSolicitud ConVersion(this ReactivarProyectoSolicitud s, int version) => s with { VersionProyecto = version };
}
