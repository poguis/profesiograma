using App.Domain.Proyectos.Cronograma;
using App.Domain.Proyectos.Estados;

namespace App.Application.Proyectos.Personal;

// ------------------------------------------------------------------ solicitud (TAREA-17)

/// <summary>Cuerpo de POST /api/proyectos/{id}/personal[/previsualizar]: estado deseado del personal vigente y nuevo.</summary>
public sealed record ActualizarPersonalSolicitud(
    IReadOnlyList<PrincipalEdicionSolicitud>? Principales,
    IReadOnlyList<BackEdicionSolicitud>? Backs);

/// <summary>Principal. Id presente = vigente existente; ausente = nuevo. Cargo null en un vigente = se conserva.</summary>
public sealed record PrincipalEdicionSolicitud(
    string? Clave, int? Id, int? EmpleadoId, string? Jornada, DateOnly? FechaInicio, DateOnly? FechaFin, string? Cargo);

/// <summary>
/// Back. PrincipalClave = clave de un principal del cuerpo (vigente o nuevo); PrincipalId = Id de un principal
/// HISTÓRICO del proyecto (D3). Son excluyentes.
/// </summary>
public sealed record BackEdicionSolicitud(
    string? Clave, int? Id, int? EmpleadoId, string? TipoRegistro, DateOnly? FechaInicio, DateOnly? FechaFin,
    int? DiasDescanso, string? PrincipalClave, int? PrincipalId, string? Observacion);

// ------------------------------------------------------------------ lectura

/// <summary>Persona guardada del proyecto (sin cédula ni correo).</summary>
public sealed record PersonaGuardada(
    int Id, RolCronograma Rol, short Numero, int EmpleadoId, string CodigoEkon, string NombreCompleto,
    DateOnly FechaInicio, DateOnly FechaFin, string? JornadaCodigo, byte? DiasTrabajo, byte DiasDescanso,
    string TipoRegistro, int? PrincipalRelacionadoId, string? Cargo, string? Observacion, bool EsPrincipalInicial)
{
    /// <summary>
    /// Back sin días DESCANSO guardados después de su FechaFin: su descanso posterior se borró (recorte de una
    /// suspensión). Lo calcula Infrastructure; false en los principales.
    /// </summary>
    public bool SinDescansoPosterior { get; init; }

    /// <summary>
    /// R2 del motor: principal con fin &lt; corte; back con fin + descanso &lt; corte.
    /// H12 (TAREA-17b): un back con fin &lt; corte cuyo descanso posterior se borró también es histórico
    /// (ForzarHistorica): no se regenera el descanso que la suspensión eliminó.
    /// </summary>
    public bool EsHistorica(DateOnly corte) =>
        Rol == RolCronograma.Principal
            ? FechaFin < corte
            : FechaFin.AddDays(DiasDescanso) < corte || (SinDescansoPosterior && FechaFin < corte);
}

/// <summary>Día guardado anterior al corte (base del motor).</summary>
public sealed record DiaGuardado(int PersonalId, DiaAsignado Dia);

/// <summary>Proyecto leído para editar el personal. DiasBase = solo días con Fecha &lt; corte.</summary>
public sealed record DatosEdicion(
    int Id, string Codigo, string EstadoCodigo, DateOnly FechaInicio, DateOnly FechaFin,
    IReadOnlyList<PersonaGuardada> Personal, IReadOnlyList<DiaGuardado> DiasBase, IReadOnlyList<ActividadCorte> Actividades);

// ------------------------------------------------------------------ escritura

/// <summary>
/// Relación de un back con su principal: Id de uno guardado (vigente o histórico) o clave de uno nuevo.
/// Ambos null = sin relación.
/// </summary>
public sealed record RelacionPrincipal(int? Id, string? ClaveNueva);

/// <summary>Valores finales de una persona vigente.</summary>
public sealed record PersonaVigenteActualizada(
    int Id, byte? JornadaId, byte? DiasTrabajo, byte DiasDescanso, DateOnly FechaInicio, DateOnly FechaFin,
    string TipoRegistro, string? Cargo, string? Observacion, RelacionPrincipal Relacion);

/// <summary>Persona nueva con su número (máx + 1 por rol).</summary>
public sealed record PersonaNueva(
    string Clave, RolCronograma Rol, short Numero, int EmpleadoId, byte? JornadaId, byte? DiasTrabajo, byte DiasDescanso,
    DateOnly FechaInicio, DateOnly FechaFin, string TipoRegistro, string? Cargo, string? Observacion, RelacionPrincipal Relacion);

/// <summary>Día a insertar: de una persona guardada (PersonalId) o de una nueva (ClaveNueva).</summary>
public sealed record DiaParaInsertar(int? PersonalId, string? ClaveNueva, DiaAsignado Dia);

/// <summary>Fila nueva de ProyectoActividad (R8 de la reactivación): misma actividad, de R a la nueva fecha fin.</summary>
public sealed record ActividadNueva(
    int Version, string Codigo, string? Descripcion, string? Tipo, DateOnly FechaInicio, DateOnly FechaFin);

/// <summary>
/// Parte propia de la REACTIVACION (TAREA-17b): principal inicial nuevo (R6), proyecto ACTIVO con la nueva fecha fin
/// (R4, H5) y la actividad nueva (R8, null si no había actividad vigente en la fecha de suspensión).
/// </summary>
public sealed record ReactivacionAplicar(string ClavePrincipalInicial, DateOnly FechaFinProyecto, ActividadNueva? Actividad);

/// <summary>
/// Todo lo que se escribe en la transacción (recalculado dentro del applock).
/// FechaInicioProyecto / FechaFinProyecto son las fechas de la etapa (en la reactivación: R y la nueva fecha fin).
/// Reactivacion = null en ACTUALIZACION_PERSONAL.
/// </summary>
public sealed record CambioPersonal(
    int ProyectoId,
    DateOnly Corte,
    int Version,
    string TipoMovimiento,
    DateOnly FechaInicioProyecto,
    DateOnly FechaFinProyecto,
    string? ActividadCodigo,
    string SnapshotPersonal,
    IReadOnlyList<PersonaVigenteActualizada> Vigentes,
    IReadOnlyList<PersonaNueva> Nuevas,
    IReadOnlyList<int> Eliminadas,
    IReadOnlyList<DiaParaInsertar> Dias,
    ReactivacionAplicar? Reactivacion = null);

public interface IEdicionPersonalRepositorio
{
    /// <summary>Proyecto no eliminado y visible (null = Admin). null si no existe o no es visible. Días: solo &lt; corte.</summary>
    Task<DatosEdicion?> ObtenerAsync(int proyectoId, int? propietarioUsuarioId, DateOnly corte, CancellationToken ct);

    /// <summary>Última versión de etapa (0 si no hay). Dentro de la transacción.</summary>
    Task<int> ObtenerUltimaVersionEtapaAsync(int proyectoId, CancellationToken ct);

    /// <summary>
    /// Escribe el cambio (debe ejecutarse dentro de ITransaccionAsignaciones): borra los días ≥ corte, actualiza vigentes,
    /// inserta nuevas, reasigna relaciones, elimina omitidas, inserta días y la etapa.
    /// </summary>
    /// <exception cref="Estados.ConflictoConcurrenciaException">Duplicado o conflicto de concurrencia al guardar.</exception>
    Task AplicarAsync(CambioPersonal cambio, CancellationToken ct);
}
