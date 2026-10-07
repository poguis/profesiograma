namespace App.Application.Proyectos.Crear;

/// <summary>Persona dentro del proyecto: Rol = PRINCIPAL | BACK + número.</summary>
public sealed record PersonaDto(string Rol, short Numero);

/// <summary>Tramo del cronograma. Rol = PRINCIPAL | BACK | DESCANSO; Tipo = AUTO | MANUAL.</summary>
public sealed record TramoDto(
    string Rol, string Tipo, short Bloque, PersonaDto Persona,
    int EmpleadoId, string CodigoEkon, string NombreEmpleado,
    DateOnly Inicio, DateOnly Fin, int Dias);

public sealed record DiaDto(DateOnly Fecha, string Rol, string Tipo, short Bloque);

/// <summary>Días finales de una persona (con descansos automáticos), ordenados por fecha.</summary>
public sealed record DiasPersonaDto(PersonaDto Persona, int EmpleadoId, string CodigoEkon, string NombreEmpleado, IReadOnlyList<DiaDto> Dias);

/// <summary>
/// Cruce de asignación. Origen = INTERNO (mismo proyecto) | EXTERNO (otro proyecto vigente).
/// Del proyecto existente solo se exponen código, nombre y estado.
/// </summary>
public sealed record CruceDto(
    string Origen, int EmpleadoId, string CodigoEkon, string NombreEmpleado, DateOnly Fecha, string Rol,
    string Proyecto, string? ProyectoCodigo, string? EstadoProyecto);

/// <summary>Resumen como la app original: persona / rol / proyecto / mes ("diciembre 2026") con días "5, 6, 7".</summary>
public sealed record ResumenCruceDto(string NombreEmpleado, string Rol, string Proyecto, string Mes, string Dias);

/// <param name="Advertencias">TAREA-19y: avisos que no bloquean (p. ej. proyecto sin principal).</param>
public sealed record PrevisualizacionDto(
    IReadOnlyList<TramoDto> Tramos,
    IReadOnlyList<DiasPersonaDto> DiasPorPersona,
    IReadOnlyList<CruceDto> Cruces,
    IReadOnlyList<ResumenCruceDto> Resumen,
    IReadOnlyList<string> Advertencias);

public sealed record ProyectoCreadoDto(int Id, string Codigo);

public enum EstadoCrearProyecto
{
    Invalido,
    Conflicto,
    Previsualizado,
    Creado,
    /// <summary>TAREA-26d: otro registro guardó al mismo empleado al mismo tiempo (UQ_Empleado_CodigoEkon) → 409.</summary>
    Cambiado,
}

public sealed record ResultadoCrearProyecto(
    EstadoCrearProyecto Estado,
    IReadOnlyDictionary<string, string[]>? Errores = null,
    PrevisualizacionDto? Previsualizacion = null,
    ProyectoCreadoDto? Creado = null)
{
    public static ResultadoCrearProyecto Invalido(IReadOnlyDictionary<string, string[]> errores) => new(EstadoCrearProyecto.Invalido, errores);
    public static ResultadoCrearProyecto Conflicto(PrevisualizacionDto p) => new(EstadoCrearProyecto.Conflicto, Previsualizacion: p);
    public static ResultadoCrearProyecto Previsualizado(PrevisualizacionDto p) => new(EstadoCrearProyecto.Previsualizado, Previsualizacion: p);
    public static ResultadoCrearProyecto Registrado(ProyectoCreadoDto c) => new(EstadoCrearProyecto.Creado, Creado: c);
    public static ResultadoCrearProyecto Cambiado() => new(EstadoCrearProyecto.Cambiado);
    public const string MensajeCambiado = "Otro registro se guardó al mismo tiempo; vuelva a intentarlo.";
}
