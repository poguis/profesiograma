namespace App.Domain.Proyectos.Cronograma;

/// <summary>Persona dentro del proyecto: Rol = Principal o Back + su número (enlaza con ProyectoPersonal).</summary>
public readonly record struct PersonaProyecto(RolCronograma Rol, short Numero);

/// <summary>Tramo continuo de días con el mismo rol, tipo y bloque (vista previa).</summary>
public sealed record Tramo(
    RolCronograma Rol,
    TipoAsignacionCronograma Tipo,
    short Bloque,
    PersonaProyecto Persona,
    int EmpleadoId,
    DateOnly Inicio,
    DateOnly Fin)
{
    public int Dias => Fin.DayNumber - Inicio.DayNumber + 1;
}

/// <summary>Un día asignado (equivale a una fila de ProyectoAsignacionDia).</summary>
public sealed record DiaAsignado(
    int EmpleadoId,
    DateOnly Fecha,
    RolCronograma Rol,
    TipoAsignacionCronograma Tipo,
    short Bloque,
    PersonaProyecto Persona);

/// <summary>Misma persona y fecha con más de un registro de rol distinto de DESCANSO (FASE_5 §6).</summary>
/// <param name="Involucrados">Personas del proyecto que generan los registros en conflicto, en orden de generación.</param>
public sealed record CruceInterno(int EmpleadoId, DateOnly Fecha, IReadOnlyList<PersonaProyecto> Involucrados);

/// <summary>Resultado del motor de cronograma.</summary>
/// <param name="Tramos">Principales → backs (tramo y descanso posterior) → descansos automáticos (D2).</param>
/// <param name="DiasBase">Principales + backs (con descanso posterior), sin descansos automáticos y sin deduplicar (D1).</param>
/// <param name="DiasFinales">DiasBase + descansos automáticos, deduplicados por (EmpleadoId, Fecha, Rol) conservando el primero.</param>
/// <param name="CrucesInternos">Calculados sobre DiasBase, ignorando DESCANSO.</param>
public sealed record ResultadoCronograma(
    IReadOnlyList<Tramo> Tramos,
    IReadOnlyList<DiaAsignado> DiasBase,
    IReadOnlyList<DiaAsignado> DiasFinales,
    IReadOnlyList<CruceInterno> CrucesInternos)
{
    public bool TieneCrucesInternos => CrucesInternos.Count > 0;
}
