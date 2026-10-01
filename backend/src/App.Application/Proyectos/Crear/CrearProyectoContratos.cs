using App.Application.Erp;
using App.Domain.Proyectos.Cronograma;

namespace App.Application.Proyectos.Crear;

// ------------------------------------------------------------------ datos de referencia (lectura, Infrastructure)

public sealed record GrupoRef(byte Id, string Codigo, bool RequiereProyectoErp, bool RequiereDimension);

public sealed record JornadaRef(byte Id, string Codigo, byte DiasTrabajo, byte DiasDescanso);

/// <summary>Empleado activo (sin datos sensibles).</summary>
public sealed record EmpleadoRef(int Id, string CodigoEkon, string NombreCompleto, string? Puesto);

public sealed record DepartamentoRef(int Id, string Nombre);

/// <summary>Límites desde Parametro: PROYECTO_MAX_PRINCIPALES, PROYECTO_MAX_BACKS, BACK_MAX_DIAS_DESCANSO.</summary>
public sealed record LimitesProyecto(int MaxPrincipales, int MaxBacks, int MaxDiasDescansoBack);

public interface IDatosReferenciaProyecto
{
    /// <summary>Grupo activo por código; null si no existe.</summary>
    Task<GrupoRef?> ObtenerGrupoAsync(string codigo, CancellationToken ct);

    /// <summary>Jornadas activas por código (sin distinguir mayúsculas).</summary>
    Task<IReadOnlyDictionary<string, JornadaRef>> ObtenerJornadasAsync(CancellationToken ct);

    Task<LimitesProyecto> ObtenerLimitesAsync(CancellationToken ct);

    /// <summary>Empleados activos (EstadoErp = "A") entre los Id indicados.</summary>
    Task<IReadOnlyDictionary<int, EmpleadoRef>> ObtenerEmpleadosActivosAsync(IReadOnlyCollection<int> ids, CancellationToken ct);

    /// <summary>Departamentos activos asignados al usuario (UsuarioDepartamento).</summary>
    Task<IReadOnlyList<DepartamentoRef>> ObtenerDepartamentosDeUsuarioAsync(int usuarioId, CancellationToken ct);
}

// ------------------------------------------------------------------ cruces externos

/// <summary>Día rol ≠ DESCANSO de otro proyecto vigente. Solo se exponen código, nombre y estado del proyecto.</summary>
public sealed record AsignacionExistente(
    int EmpleadoId, DateOnly Fecha, byte RolAsignacionId,
    int ProyectoId, string ProyectoCodigo, string ProyectoNombre, string EstadoProyecto);

public interface IConsultaCrucesExternos
{
    /// <summary>
    /// Días con rol ≠ DESCANSO en ProyectoAsignacionDia de OTROS proyectos no eliminados con estado vigente
    /// (EstadoProyecto.EsVigente), para esos empleados y el rango [desde, hasta].
    /// </summary>
    Task<IReadOnlyList<AsignacionExistente>> BuscarAsync(
        IReadOnlyCollection<int> empleadoIds, DateOnly desde, DateOnly hasta, int? excluirProyectoId, CancellationToken ct);
}

// ------------------------------------------------------------------ escritura

/// <summary>Personal validado. Principales: Jornada no null. Backs: TipoRegistro no null.</summary>
public sealed record PersonalValidado(
    PersonaProyecto Persona,
    EmpleadoRef Empleado,
    DateOnly Inicio,
    DateOnly Fin,
    JornadaRef? Jornada,
    TipoRegistroBack? TipoRegistro,
    byte DiasDescanso,
    short? PrincipalRelacionado,
    string? Cargo,
    string? Observacion);

/// <summary>Solicitud validada con los datos del ERP obtenidos por Id.</summary>
public sealed record ProyectoValidado(
    CompaniaErp Compania,
    GrupoRef Grupo,
    ProyectoErp? ProyectoErp,
    ActividadErp? Actividad,
    DimensionErp? Dimension,
    DateOnly FechaInicio,
    DateOnly FechaFin,
    HorarioErp Horario,
    TimeOnly SalidaAlmuerzo,
    TimeOnly RegresoAlmuerzo,
    int? DepartamentoId,
    int PropietarioUsuarioId,
    IReadOnlyList<PersonalValidado> Principales,
    IReadOnlyList<PersonalValidado> Backs);

/// <summary>Todo lo que se guarda en la transacción de registro.</summary>
/// <param name="Dias">DiasFinales del motor (con descansos automáticos, deduplicados).</param>
/// <param name="SnapshotPersonal">JSON de la etapa v1 (sin cédula ni correo).</param>
public sealed record NuevoProyecto(ProyectoValidado Datos, string NombreVisual, IReadOnlyList<DiaAsignado> Dias, string SnapshotPersonal);

public interface IProyectoRepositorio
{
    Task<bool> ExisteCodigoAsync(string codigo, CancellationToken ct);

    /// <summary>Guarda cabecera, compañía (upsert), personal, días, etapa v1 y actividad v1. Devuelve el Id.</summary>
    Task<int> AgregarAsync(NuevoProyecto proyecto, string codigo, Guid uid, CancellationToken ct);
}

/// <summary>
/// Transacción con bloqueo de aplicación exclusivo 'profesiograma:asignaciones' (sp_getapplock).
/// TODA escritura de asignaciones (crear y, en el futuro, editar o fecha de corte) debe usarla.
/// </summary>
public interface ITransaccionAsignaciones
{
    /// <summary>Ejecuta la acción; confirma si <paramref name="confirmar"/> devuelve true, si no hace rollback.</summary>
    /// <exception cref="RegistroOcupadoException">No se obtuvo el bloqueo a tiempo.</exception>
    Task<T> EjecutarAsync<T>(Func<CancellationToken, Task<T>> accion, Func<T, bool> confirmar, CancellationToken ct);
}

/// <summary>Otro registro de asignaciones está en curso y no se obtuvo el bloqueo a tiempo (la API responde 503).</summary>
public sealed class RegistroOcupadoException(string detalle) : Exception(detalle);
