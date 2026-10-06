namespace App.Application.Proyectos.Estados;

/// <summary>Empleado en la vista previa del cambio de estado (sin cédula ni correo).</summary>
public sealed record EmpleadoCambioDto(int Id, string CodigoEkon, string NombreCompleto);

/// <summary>Días que se eliminan de una persona. Rol = PRINCIPAL | BACK | DESCANSO.</summary>
public sealed record DiasEliminadosDto(EmpleadoCambioDto Empleado, string Rol, int Cantidad, DateOnly Desde, DateOnly Hasta);

/// <summary>Persona que se elimina (empieza después de la fecha del movimiento). Rol = PRINCIPAL | BACK.</summary>
public sealed record PersonaEliminadaDto(EmpleadoCambioDto Empleado, string Rol, short Numero, DateOnly FechaInicio, DateOnly FechaFin);

public sealed record PersonaRecortadaDto(
    EmpleadoCambioDto Empleado, string Rol, short Numero, DateOnly FechaInicio, DateOnly FechaFinAnterior, DateOnly FechaFinNueva);

/// <summary>Actividad afectada (H3). Accion = ELIMINADA | RECORTADA; FechaFinNueva null si se elimina.</summary>
public sealed record ActividadAfectadaDto(
    string ActividadCodigo, int Version, DateOnly FechaInicio, DateOnly FechaFinAnterior, DateOnly? FechaFinNueva, string Accion);

/// <summary>
/// 200 de POST /api/proyectos/{id}/cambio-estado/previsualizar (no guarda nada). VersionProyecto = token de concurrencia
/// (TAREA-19x) que el cliente envía al aplicar.
/// </summary>
public sealed record CambioEstadoPrevisualizacionDto(
    string Movimiento,
    string EstadoActual,
    string EstadoNuevo,
    DateOnly Fecha,
    DateOnly FechaFinActual,
    DateOnly FechaFinNueva,
    IReadOnlyList<DiasEliminadosDto> DiasEliminados,
    IReadOnlyList<PersonaEliminadaDto> PersonalEliminado,
    IReadOnlyList<PersonaRecortadaDto> PersonalRecortado,
    IReadOnlyList<ActividadAfectadaDto> ActividadesAfectadas,
    IReadOnlyList<string> Advertencias,
    int VersionProyecto);

/// <summary>200 de POST /api/proyectos/{id}/cambio-estado.</summary>
public sealed record CambioEstadoRealizadoDto(int Id, string Estado, int Version);

public enum EstadoCambio
{
    Invalido,
    NoEncontrado,
    Conflicto,
    /// <summary>Token de concurrencia viejo (TAREA-19x): 409 "El proyecto cambió; vuelve a cargarlo.".</summary>
    Cambiado,
    Previsualizado,
    Realizado,
}

public sealed record ResultadoCambioEstado(
    EstadoCambio Estado,
    IReadOnlyDictionary<string, string[]>? Errores = null,
    CambioEstadoPrevisualizacionDto? Previsualizacion = null,
    CambioEstadoRealizadoDto? Realizado = null)
{
    public const string MensajeConflicto = "El proyecto cambió de estado; vuelve a cargarlo.";

    public static ResultadoCambioEstado Invalido(IReadOnlyDictionary<string, string[]> errores) => new(EstadoCambio.Invalido, errores);
    public static ResultadoCambioEstado NoEncontrado() => new(EstadoCambio.NoEncontrado);
    public static ResultadoCambioEstado Conflicto() => new(EstadoCambio.Conflicto);
    public static ResultadoCambioEstado Cambiado() => new(EstadoCambio.Cambiado);
    public static ResultadoCambioEstado Previsualizado(CambioEstadoPrevisualizacionDto p) => new(EstadoCambio.Previsualizado, Previsualizacion: p);
    public static ResultadoCambioEstado Hecho(CambioEstadoRealizadoDto r) => new(EstadoCambio.Realizado, Realizado: r);
}
