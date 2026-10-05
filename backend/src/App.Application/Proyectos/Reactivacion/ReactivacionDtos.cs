using App.Application.Proyectos.Crear;
using App.Application.Proyectos.Personal;

namespace App.Application.Proyectos.Reactivacion;

// ------------------------------------------------------------------ GET /api/proyectos/{id}/reactivacion

/// <summary>Empleado del principal propuesto. Activo = false: se propone con advertencia y el registro lo rechazará.</summary>
public sealed record EmpleadoPropuestoDto(int Id, string CodigoEkon, string NombreCompleto, bool Activo);

/// <summary>Principal propuesto para reactivar (R7): mismo empleado, jornada y cargo; las fechas las pone el formulario.</summary>
public sealed record PrincipalPropuestoDto(EmpleadoPropuestoDto Empleado, string? Jornada, string? Cargo);

/// <summary>Persona guardada (todas quedan históricas al reactivar; los principales sirven para relacionar backs, D3).</summary>
public sealed record PersonaReactivacionDto(
    int Id, string Rol, short Numero, EmpleadoEdicionDto Empleado, string? Jornada, DateOnly FechaInicio, DateOnly FechaFin,
    string TipoRegistro, byte DiasDescanso, bool EsPrincipalInicial);

/// <summary>
/// Datos del formulario de reactivación. PuedeReactivar = false (con Motivo) si el proyecto no está SUSPENDIDO;
/// en ese caso no hay propuesta ni advertencias. FechaMinima = FechaFinActual + 1.
/// </summary>
public sealed record ReactivacionDto(
    int Id, string Codigo, string EstadoActual, DateOnly FechaInicio, DateOnly FechaFinActual, DateOnly FechaMinima,
    bool PuedeReactivar, string? Motivo, PrincipalPropuestoDto? PrincipalPropuesto,
    IReadOnlyList<PersonaReactivacionDto> Personal, LimitesEdicionDto Limites, IReadOnlyList<string> Advertencias);

// ------------------------------------------------------------------ vista previa y resultado

/// <summary>Fila de ProyectoActividad que se creará (R8): TipoMovimiento REACTIVACION, de R a la nueva fecha fin.</summary>
public sealed record ActividadReactivacionDto(string Codigo, string? Descripcion, string? Tipo, DateOnly FechaInicio, DateOnly FechaFin);

/// <summary>
/// 200 de POST /api/proyectos/{id}/reactivacion/previsualizar (no guarda). Mismos campos que la vista previa de la
/// TAREA-17 (Corte = R) más FechaFinActual, FechaFinNueva y la actividad que se creará (null si no hay).
/// </summary>
public sealed record PrevisualizacionReactivacionDto(
    DateOnly Corte,
    DateOnly FechaFinActual,
    DateOnly FechaFinNueva,
    ActividadReactivacionDto? Actividad,
    IReadOnlyList<PersonaCambioDto> Personal,
    IReadOnlyList<TramoDto> Tramos,
    IReadOnlyList<CruceDto> Cruces,
    IReadOnlyList<ResumenCruceDto> Resumen,
    IReadOnlyList<string> Advertencias);

/// <summary>200 de POST /api/proyectos/{id}/reactivacion.</summary>
public sealed record ProyectoReactivadoDto(int Id, string Estado, int Version);

public sealed record ResultadoReactivacion(
    EstadoEdicion Estado,
    IReadOnlyDictionary<string, string[]>? Errores = null,
    PrevisualizacionReactivacionDto? Previsualizacion = null,
    ProyectoReactivadoDto? Realizado = null)
{
    public static ResultadoReactivacion Invalido(IReadOnlyDictionary<string, string[]> e) => new(EstadoEdicion.Invalido, e);
    public static ResultadoReactivacion NoEncontrado() => new(EstadoEdicion.NoEncontrado);
    public static ResultadoReactivacion Cambiado() => new(EstadoEdicion.Cambiado);
    public static ResultadoReactivacion ConCruces(PrevisualizacionReactivacionDto p) => new(EstadoEdicion.ConCruces, Previsualizacion: p);
    public static ResultadoReactivacion Previsualizado(PrevisualizacionReactivacionDto p) => new(EstadoEdicion.Previsualizado, Previsualizacion: p);
    public static ResultadoReactivacion Hecho(ProyectoReactivadoDto r) => new(EstadoEdicion.Realizado, Realizado: r);
}
