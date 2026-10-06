using App.Domain.Proyectos.Estados;

namespace App.Application.Proyectos.Estados;

/// <summary>Cuerpo de POST /api/proyectos/{id}/cambio-estado[/previsualizar]. EstadoDestino = código (SUSPENDIDO | TERMINADO).</summary>
public sealed record CambioEstadoSolicitud(string? EstadoDestino, DateOnly? Fecha);

/// <summary>Persona del proyecto con los datos que muestran la vista previa y el snapshot (sin cédula ni correo).</summary>
public sealed record PersonalCambio(
    PersonaCorte Corte,
    string CodigoEkon,
    string NombreCompleto,
    string? JornadaCodigo,
    byte? DiasTrabajo,
    byte DiasDescanso,
    string TipoRegistro);

/// <summary>Proyecto leído para cambiar de estado. DiasPosteriores = solo los días con Fecha &gt; fecha del movimiento.</summary>
public sealed record DatosCambioEstado(
    int Id,
    string Codigo,
    string EstadoCodigo,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    IReadOnlyList<PersonalCambio> Personal,
    IReadOnlyList<DiaCorte> DiasPosteriores,
    IReadOnlyList<ActividadCorte> Actividades);

/// <summary>Lo que se escribe en la transacción (ya validado y recalculado dentro del applock). Version = máx + 1.</summary>
public sealed record CambioEstadoAplicar(
    int ProyectoId,
    int Version,
    string EstadoDestino,
    string TipoMovimiento,
    DateOnly FechaInicioProyecto,
    PlanRecorte Plan,
    string SnapshotPersonal,
    string? ActividadCodigo);

public interface ICambioEstadoRepositorio
{
    /// <summary>
    /// Proyecto no eliminado y visible (propietarioUsuarioId null = Admin, todos). null si no existe o no es visible.
    /// Carga el personal, las actividades y solo los días con Fecha &gt; fecha.
    /// </summary>
    Task<DatosCambioEstado?> ObtenerAsync(int proyectoId, int? propietarioUsuarioId, DateOnly fecha, CancellationToken ct);

    /// <summary>Última versión de etapa del proyecto (0 si no tiene). Se lee dentro de la transacción.</summary>
    Task<int> ObtenerUltimaVersionEtapaAsync(int proyectoId, CancellationToken ct);

    /// <summary>
    /// Aplica el recorte, el cambio de estado y la etapa nueva con la versión indicada. Debe ejecutarse dentro de
    /// ITransaccionAsignaciones.
    /// </summary>
    /// <exception cref="ConflictoConcurrenciaException">Otro proceso modificó el proyecto (RowVer) o la versión de etapa.</exception>
    Task AplicarAsync(CambioEstadoAplicar cambio, CancellationToken ct);
}

/// <summary>El proyecto cambió mientras se aplicaba el movimiento (la API responde 409).</summary>
public sealed class ConflictoConcurrenciaException(string mensaje, Exception? interna = null) : Exception(mensaje, interna);
