using App.Application.Proyectos.Crear;
using App.Application.Proyectos.Personal;
using App.Domain.Proyectos.Estados;
using static App.Application.Proyectos.Crear.ReglasPersonal;

namespace App.Application.Proyectos.Reactivacion;

/// <summary>
/// Validación de la reactivación (TAREA-17b: R2, R5, R6). Lógica pura sobre los datos ya leídos.
/// Las reglas por persona son las de la TAREA-17 (núcleo de EdicionPersonalValidador, mismos mensajes) con:
/// corte = R, todo el personal guardado histórico, personas solo nuevas (sin id), inicio ≥ R y RN08 sobre
/// (inicio del proyecto, nueva fecha fin).
/// </summary>
public sealed class ReactivacionValidador
{
    private readonly EdicionPersonalValidador nucleo = new();

    public ResultadoValidacionEdicion Validar(
        ReactivarProyectoSolicitud s,
        DatosEdicion proyecto,
        IReadOnlyDictionary<string, JornadaRef> jornadas,
        LimitesProyecto limites,
        IReadOnlyDictionary<int, EmpleadoRef> empleadosActivos)
    {
        ArgumentNullException.ThrowIfNull(s);
        ArgumentNullException.ThrowIfNull(proyecto);
        var e = new Dictionary<string, List<string>>();

        if (MotivoNoReactivable(proyecto) is { } motivo)
        {
            Agregar(e, "proyecto", motivo);
            return Invalido(e);
        }

        // R2: FechaFin actual < R ≤ fechaFin (R futura permitida; R pasada: advertencia R3 en el servicio).
        if (s.Fecha is not DateOnly r)
        {
            Agregar(e, "fecha", "La fecha de reactivación es obligatoria.");
        }
        else if (r <= proyecto.FechaFin)
        {
            Agregar(e, "fecha", $"La fecha de reactivación debe ser posterior a la fecha fin actual del proyecto ({Formato(proyecto.FechaFin)}).");
        }

        if (s.FechaFin is not DateOnly fin)
        {
            Agregar(e, "fechaFin", "La fecha fin es obligatoria.");
        }
        else if (s.Fecha is DateOnly fecha && fin < fecha)
        {
            Agregar(e, "fechaFin", $"La fecha fin no puede ser anterior a la fecha de reactivación ({Formato(fecha)}).");
        }

        if (e.Count > 0)
        {
            return Invalido(e); // sin R y fecha fin válidas no se pueden validar las personas
        }

        var reactivacion = s.Fecha!.Value;
        var principales = s.Principales ?? [];

        // R5: al menos un principal. R6: el primero empieza exactamente en R (antes de R lo informa el núcleo).
        if (principales.Count == 0)
        {
            Agregar(e, "principales", "Se requiere al menos 1 principal(es).");
        }
        else if (principales[0].Id is null && principales[0].FechaInicio is DateOnly inicio && inicio > reactivacion)
        {
            Agregar(e, "principales[0].fechaInicio", $"El primer principal debe empezar en la fecha de reactivación ({Formato(reactivacion)}).");
        }

        var personal = nucleo.ValidarPersonal(
            new ActualizarPersonalSolicitud(principales, s.Backs ?? []), proyecto,
            new ReglasValidacionPersonal(reactivacion, (proyecto.FechaInicio, s.FechaFin!.Value), Reactivacion: true),
            jornadas, limites, empleadosActivos);

        foreach (var (clave, mensajes) in personal.Errores)
        {
            foreach (var mensaje in mensajes)
            {
                Agregar(e, clave, mensaje);
            }
        }

        return e.Count > 0 ? Invalido(e) : personal;
    }

    /// <summary>Motivo por el que no se puede reactivar (null = se puede): solo proyectos SUSPENDIDO.</summary>
    public static string? MotivoNoReactivable(DatosEdicion proyecto) =>
        string.Equals(proyecto.EstadoCodigo, CodigosEstadoProyecto.Suspendido, StringComparison.OrdinalIgnoreCase)
            ? null
            : $"Solo se puede reactivar un proyecto SUSPENDIDO (estado actual: {proyecto.EstadoCodigo}).";

    private static ResultadoValidacionEdicion Invalido(Dictionary<string, List<string>> e) => new(null, Congelar(e), Cambiado: false);
}
