namespace App.Domain.Proyectos.Cronograma;

/// <summary>Cruces internos del proyecto (RN07, FASE_5 §6).</summary>
public static class CruceDetector
{
    /// <summary>
    /// Misma persona y fecha con más de un registro de rol distinto de DESCANSO.
    /// Se aplica sobre los días base (sin descansos automáticos y sin deduplicar), igual que la app original.
    /// Resultado ordenado por fecha y empleado.
    /// </summary>
    public static IReadOnlyList<CruceInterno> DetectarInternos(IEnumerable<DiaAsignado> diasBase)
    {
        ArgumentNullException.ThrowIfNull(diasBase);

        return diasBase
            .Where(d => d.Rol != RolCronograma.Descanso)
            .GroupBy(d => (d.EmpleadoId, d.Fecha))
            .Where(g => g.Count() > 1)
            .Select(g => new CruceInterno(g.Key.EmpleadoId, g.Key.Fecha, g.Select(d => d.Persona).ToList()))
            .OrderBy(c => c.Fecha)
            .ThenBy(c => c.EmpleadoId)
            .ToList();
    }
}
