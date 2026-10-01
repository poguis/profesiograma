using App.Domain.Proyectos.Cronograma;

namespace App.Domain.Proyectos.Estados;

/// <summary>Persona del proyecto para el recorte (PrincipalRelacionadoId = Id del principal que cubre un back).</summary>
public sealed record PersonaCorte(
    int Id, RolCronograma Rol, short Numero, int EmpleadoId, DateOnly FechaInicio, DateOnly FechaFin, int? PrincipalRelacionadoId);

/// <summary>Día asignado (ProyectoAsignacionDia) de una persona. Rol = PRINCIPAL | BACK | DESCANSO.</summary>
public sealed record DiaCorte(int PersonalId, DateOnly Fecha, RolCronograma Rol);

public sealed record ActividadCorte(int Id, int Version, string Codigo, DateOnly FechaInicio, DateOnly FechaFin);

/// <summary>Días que se eliminan de una persona, agrupados por rol (DESCANSO aparte).</summary>
public sealed record DiasEliminadosPersona(int PersonalId, RolCronograma Rol, int Cantidad, DateOnly Desde, DateOnly Hasta);

public sealed record CambioFechaFin(int Id, DateOnly FinAnterior, DateOnly FinNuevo);

/// <summary>Resultado del recorte a la fecha F (inclusiva: el día F se conserva).</summary>
public sealed record PlanRecorte(
    DateOnly Fecha,
    DateOnly FechaFinAnterior,
    IReadOnlyList<DiasEliminadosPersona> DiasEliminados,
    IReadOnlyList<int> PersonalEliminado,
    IReadOnlyList<CambioFechaFin> PersonalRecortado,
    IReadOnlyList<int> BacksSinPrincipal,
    IReadOnlyList<int> ActividadesEliminadas,
    IReadOnlyList<CambioFechaFin> ActividadesRecortadas,
    string? ActividadVigenteEnF)
{
    public int TotalDiasEliminados => DiasEliminados.Sum(d => d.Cantidad);
}

/// <summary>
/// E3: recorte de un proyecto a la fecha F (SUSPENSION y CIERRE). Lógica pura.
/// - Días: se eliminan los de Fecha &gt; F de cualquier rol, incluidos los DESCANSO AUTO y MANUAL
///   (también los descansos de backs posteriores a la fecha fin del proyecto, P3 → decisión 8.2).
/// - Personal: se elimina el que empieza después de F; se recorta a F el que termina después. DiasDescanso no cambia.
/// - Backs que cubren a un principal eliminado: quedan sin principal relacionado (la FK no permite la referencia).
/// - Actividades (H3): se eliminan las que empiezan después de F y se recortan a F las que terminan después.
/// </summary>
public static class RecorteProyecto
{
    public static PlanRecorte Calcular(
        DateOnly inicioProyecto,
        DateOnly finProyecto,
        DateOnly fecha,
        IReadOnlyList<PersonaCorte> personal,
        IReadOnlyList<DiaCorte> dias,
        IReadOnlyList<ActividadCorte> actividades)
    {
        ArgumentNullException.ThrowIfNull(personal);
        ArgumentNullException.ThrowIfNull(dias);
        ArgumentNullException.ThrowIfNull(actividades);
        if (fecha < inicioProyecto || fecha > finProyecto)
        {
            throw new ArgumentOutOfRangeException(nameof(fecha), fecha, "La fecha del movimiento debe estar dentro del rango del proyecto.");
        }

        var diasEliminados = dias
            .Where(d => d.Fecha > fecha)
            .GroupBy(d => (d.PersonalId, d.Rol))
            .Select(g => new DiasEliminadosPersona(g.Key.PersonalId, g.Key.Rol, g.Count(), g.Min(d => d.Fecha), g.Max(d => d.Fecha)))
            .OrderBy(d => d.PersonalId).ThenBy(d => d.Rol)
            .ToList();

        var personalEliminado = personal.Where(p => p.FechaInicio > fecha).Select(p => p.Id).ToHashSet();

        var personalRecortado = personal
            .Where(p => !personalEliminado.Contains(p.Id) && p.FechaFin > fecha)
            .Select(p => new CambioFechaFin(p.Id, p.FechaFin, fecha))
            .ToList();

        var backsSinPrincipal = personal
            .Where(p => p.Rol == RolCronograma.Back && !personalEliminado.Contains(p.Id)
                        && p.PrincipalRelacionadoId is int principal && personalEliminado.Contains(principal))
            .Select(p => p.Id)
            .ToList();

        var actividadesEliminadas = actividades.Where(a => a.FechaInicio > fecha).Select(a => a.Id).ToList();
        var actividadesRecortadas = actividades
            .Where(a => a.FechaInicio <= fecha && a.FechaFin > fecha)
            .Select(a => new CambioFechaFin(a.Id, a.FechaFin, fecha))
            .ToList();

        // Actividad vigente en F (después del recorte): inicio ≤ F ≤ fin; si se solapan, la de mayor versión.
        var vigente = actividades
            .Where(a => a.FechaInicio <= fecha && a.FechaFin >= fecha)
            .OrderByDescending(a => a.Version)
            .FirstOrDefault();

        return new PlanRecorte(
            fecha,
            finProyecto,
            diasEliminados,
            personal.Where(p => personalEliminado.Contains(p.Id)).Select(p => p.Id).ToList(),
            personalRecortado,
            backsSinPrincipal,
            actividadesEliminadas,
            actividadesRecortadas,
            vigente?.Codigo);
    }
}
