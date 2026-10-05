using App.Application.Proyectos.Personal;
using App.Application.Proyectos.Reactivacion;
using App.Domain.Proyectos.Cronograma;
using App.Domain.Proyectos.Estados;

namespace App.Application.Tests.Proyectos.Reactivacion;

/// <summary>
/// "Proyecto B" de la TAREA-17b (Id 10), creado 28/09–30/11/2026 y SUSPENDIDO el 09/10/2026 (F):
/// P1 (Id 101) DEV007 TIPO_2 28/09–09/10, principal inicial (recortado a F);
/// K1 (Id 102) DEV008 JORNADA 05/10–09/10 + 3 días de descanso, relacionado con P1. La suspensión borró su descanso
/// del 10 al 12/10 (H12: SinDescansoPosterior). Actividad DEV.01 v1 28/09–09/10 (recortada a F, H3).
/// Hoy (reloj) = 05/10/2026; R = 11/10/2026 cae dentro de fin + descanso del back (pendiente 23).
/// </summary>
internal static class DoblesReactivacion
{
    public const int ProyectoId = 10;
    public static readonly DateOnly Inicio = new(2026, 9, 28), FinOriginal = new(2026, 11, 30), F = new(2026, 10, 9);
    public static readonly DateOnly R = new(2026, 10, 11), FinNueva = new(2026, 11, 30);

    /// <summary>05/10/2026 10:00 en Ecuador.</summary>
    public static readonly DateTimeOffset Ahora = new(2026, 10, 5, 15, 0, 0, TimeSpan.Zero);

    public static readonly PersonaGuardada P1 = new(101, RolCronograma.Principal, 1, 7, "DEV007", "EMPLEADO PRUEBA 07",
        Inicio, F, "TIPO_2", 11, 4, "JORNADA", null, "PUESTO 7", null, true);

    public static readonly PersonaGuardada K1 = new(102, RolCronograma.Back, 1, 8, "DEV008", "EMPLEADO PRUEBA 08",
        new DateOnly(2026, 10, 5), F, null, null, 3, "JORNADA", 101, "PUESTO 8", null, false)
    { SinDescansoPosterior = true };

    /// <summary>Proyecto con su personal y la base (días ≤ F, como los dejó la suspensión).</summary>
    public static DatosEdicion Proyecto(string estado = "SUSPENDIDO", DateOnly? fin = null, IReadOnlyList<PersonaGuardada>? personal = null,
        bool conActividad = true)
    {
        var generado = MotorCronograma.Generar(new SolicitudCronograma(Inicio, FinOriginal,
            [new PrincipalEntrada(1, 7, Inicio, F, 11, 4)],
            [new BackEntrada(1, 8, K1.FechaInicio, K1.FechaFin, TipoRegistroBack.Jornada, 3, 1)]));
        var ids = new Dictionary<PersonaProyecto, int>
        {
            [new(RolCronograma.Principal, 1)] = 101,
            [new(RolCronograma.Back, 1)] = 102,
        };

        return new DatosEdicion(ProyectoId, "PRY-B", estado, Inicio, fin ?? F, personal ?? [P1, K1],
            generado.DiasFinales.Where(d => d.Fecha <= F).Select(d => new DiaGuardado(ids[d.Persona], d)).ToList(),
            conActividad ? [new ActividadCorte(1, 1, "DEV.01", Inicio, F)] : []);
    }

    public static PrincipalEdicionSolicitud Principal(string clave = "p1", int empleado = 7, DateOnly? inicio = null, DateOnly? fin = null,
        string jornada = "TIPO_2", int? id = null) =>
        new(clave, id, empleado, jornada, inicio ?? R, fin ?? FinNueva, null);

    public static BackEdicionSolicitud Back(string clave, int empleado, DateOnly inicio, DateOnly fin, string? principalClave = null,
        int? principalId = null, int diasDescanso = 0) =>
        new(clave, null, empleado, "JORNADA", inicio, fin, diasDescanso, principalClave, principalId, null);

    public static ReactivarProyectoSolicitud Cuerpo(DateOnly? fecha = null, DateOnly? fechaFin = null,
        PrincipalEdicionSolicitud[]? principales = null, BackEdicionSolicitud[]? backs = null) =>
        new(fecha ?? R, fechaFin ?? FinNueva, principales ?? [Principal()], backs ?? []);
}

/// <summary>Actividad vigente en la fecha de suspensión (R8). null = el proyecto no tiene actividad.</summary>
internal sealed class RepositorioReactivacionFalso(ActividadParaReactivar? actividad) : IReactivacionRepositorio
{
    public List<DateOnly> Fechas { get; } = [];

    public Task<ActividadParaReactivar?> ObtenerActividadVigenteAsync(int proyectoId, DateOnly fecha, CancellationToken ct)
    {
        Fechas.Add(fecha);
        return Task.FromResult(actividad);
    }
}
