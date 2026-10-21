namespace App.Domain.Novedades;

/// <summary>
/// Días de una novedad (TAREA-23): TODOS los días calendario entre inicio y fin, inclusive (sin excluir fines de semana,
/// como el original) → una fila NovedadDia por fecha. FechaInicio y FechaFin de la novedad = mínimo y máximo de sus días.
/// </summary>
public static class DiasNovedad
{
    /// <summary>Tope de días de una novedad (constante, sin parámetro: N6).</summary>
    public const int MaxDias = 366;

    public static int Contar(DateOnly inicio, DateOnly fin) => fin.DayNumber - inicio.DayNumber + 1;

    /// <summary>Días del rango, en orden. Exige fin ≥ inicio y como máximo <see cref="MaxDias"/> días.</summary>
    public static IReadOnlyList<DateOnly> Generar(DateOnly inicio, DateOnly fin)
    {
        var total = Contar(inicio, fin);
        if (total < 1 || total > MaxDias)
        {
            throw new ArgumentException($"El rango debe tener entre 1 y {MaxDias} días (tiene {total}).", nameof(fin));
        }

        return Enumerable.Range(0, total).Select(inicio.AddDays).ToList();
    }

    /// <summary>
    /// TAREA-23-2: días del rango menos los excluidos, ordenados (puede quedar vacío). Los excluidos fuera del rango no
    /// cambian nada aquí: la validación los rechaza aparte (P2).
    /// </summary>
    public static IReadOnlyList<DateOnly> Resultantes(DateOnly inicio, DateOnly fin, IEnumerable<DateOnly> excluidos)
    {
        var fuera = excluidos.ToHashSet();
        return Generar(inicio, fin).Where(d => !fuera.Contains(d)).ToList();
    }

    /// <summary>Mínimo y máximo de los días (FechaInicio y FechaFin de la novedad). Exige al menos 1 día.</summary>
    public static (DateOnly Inicio, DateOnly Fin) Limites(IEnumerable<DateOnly> dias)
    {
        var lista = dias.ToList();
        if (lista.Count == 0)
        {
            throw new ArgumentException("La novedad debe tener al menos 1 día.", nameof(dias));
        }

        return (lista.Min(), lista.Max());
    }
}
