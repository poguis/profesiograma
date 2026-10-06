using App.Application.Proyectos.Crear;

namespace App.Application.Proyectos.Personal;

// ------------------------------------------------------------------ GET /api/proyectos/{id}/edicion

public sealed record EmpleadoEdicionDto(int Id, string CodigoEkon, string NombreCompleto);

public sealed record LimitesEdicionDto(int MaxPrincipales, int MaxBacks, int BackMaxDiasDescanso);

/// <summary>Qué se puede editar de una persona. Históricos: todo false / null.</summary>
/// <param name="FechaInicio">Solo vigentes que aún no empiezan (inicio ≥ corte).</param>
/// <param name="FechaFinMinima">Corte − 1 para vigentes (se exige si la fecha fin cambia).</param>
/// <param name="Eliminable">Vigentes que aún no empiezan: se eliminan omitiéndolas del cuerpo.</param>
public sealed record PermisosEdicionDto(bool FechaInicio, DateOnly? FechaFinMinima, bool Jornada, bool Eliminable);

/// <summary>Persona guardada. Clase = HISTORICO | VIGENTE. Rol = PRINCIPAL | BACK.</summary>
public sealed record PersonaEdicionDto(
    int Id, string Rol, short Numero, EmpleadoEdicionDto Empleado, string Clase,
    string? Jornada, DateOnly FechaInicio, DateOnly FechaFin, string TipoRegistro, byte DiasDescanso,
    int? PrincipalRelacionadoId, string? Cargo, string? Observacion, PermisosEdicionDto Permisos);

/// <summary>
/// Datos del formulario de edición. PuedeEditar = false (con Motivo) si no es ACTIVO o ya terminó (D1).
/// VersionProyecto = token de concurrencia (TAREA-19x).
/// </summary>
public sealed record EdicionPersonalDto(
    int Id, string Codigo, string Estado, DateOnly FechaInicio, DateOnly FechaFin, DateOnly Corte,
    bool PuedeEditar, string? Motivo, IReadOnlyList<PersonaEdicionDto> Personal, LimitesEdicionDto Limites, int VersionProyecto);

// ------------------------------------------------------------------ vista previa y resultado

/// <summary>Clase = HISTORICO | VIGENTE | NUEVO; Accion = SIN_CAMBIO | MODIFICADO | ELIMINADO | NUEVO.</summary>
public sealed record PersonaCambioDto(
    string Clave, int? Id, string Rol, short Numero, EmpleadoEdicionDto Empleado, string Clase, string Accion);

/// <summary>200 de POST /api/proyectos/{id}/personal/previsualizar (no guarda).</summary>
/// <param name="Cruces">Origen INTERNO | HISTORICO | EXTERNO.</param>
/// <param name="Resumen">Resumen de cruces (persona / rol / proyecto / mes / días), como en la creación.</param>
/// <param name="VersionProyecto">Token de concurrencia (TAREA-19x) que el cliente envía al registrar.</param>
public sealed record PrevisualizacionPersonalDto(
    DateOnly Corte,
    IReadOnlyList<PersonaCambioDto> Personal,
    IReadOnlyList<TramoDto> Tramos,
    IReadOnlyList<CruceDto> Cruces,
    IReadOnlyList<ResumenCruceDto> Resumen,
    IReadOnlyList<string> Advertencias,
    int VersionProyecto);

/// <summary>200 de POST /api/proyectos/{id}/personal.</summary>
public sealed record PersonalActualizadoDto(int Id, int Version);

public enum EstadoEdicion
{
    Invalido,
    NoEncontrado,
    /// <summary>El proyecto cambió desde la lectura (Id vigentes, estado, fechas o concurrencia).</summary>
    Cambiado,
    /// <summary>Hay cruces: el registro responde 409 con extensiones.</summary>
    ConCruces,
    Previsualizado,
    Realizado,
}

public sealed record ResultadoEdicionPersonal(
    EstadoEdicion Estado,
    IReadOnlyDictionary<string, string[]>? Errores = null,
    PrevisualizacionPersonalDto? Previsualizacion = null,
    PersonalActualizadoDto? Realizado = null)
{
    public const string MensajeCambiado = "El proyecto cambió; vuelve a cargarlo.";
    public const string MensajeCruces = "El proyecto tiene cruces de asignación.";

    /// <summary>C9 (TAREA-18, pendiente 25): registro sin cambios → 400 en la clave "general".</summary>
    public const string ClaveSinCambios = "general";
    public const string MensajeSinCambios = "No hay cambios para registrar.";
    /// <summary>C9: advertencia de la vista previa cuando no hay cambios (200).</summary>
    public const string AdvertenciaSinCambios = "No hay cambios.";

    public static IReadOnlyDictionary<string, string[]> ErroresSinCambios() =>
        new Dictionary<string, string[]> { [ClaveSinCambios] = [MensajeSinCambios] };

    public static ResultadoEdicionPersonal Invalido(IReadOnlyDictionary<string, string[]> e) => new(EstadoEdicion.Invalido, e);
    public static ResultadoEdicionPersonal NoEncontrado() => new(EstadoEdicion.NoEncontrado);
    public static ResultadoEdicionPersonal Cambiado() => new(EstadoEdicion.Cambiado);
    public static ResultadoEdicionPersonal ConCruces(PrevisualizacionPersonalDto p) => new(EstadoEdicion.ConCruces, Previsualizacion: p);
    public static ResultadoEdicionPersonal Previsualizado(PrevisualizacionPersonalDto p) => new(EstadoEdicion.Previsualizado, Previsualizacion: p);
    public static ResultadoEdicionPersonal Hecho(PersonalActualizadoDto r) => new(EstadoEdicion.Realizado, Realizado: r);
}
