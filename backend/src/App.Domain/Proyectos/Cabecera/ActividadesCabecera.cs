using App.Domain.Proyectos.Estados;

namespace App.Domain.Proyectos.Cabecera;

// Reglas puras de las actividades al editar la cabecera (TAREA-18). Ver docs/fases/FASE_5_Edicion_Cabecera.md.
// Trabajan sobre ActividadCorte (Id, Version, Codigo, FechaInicio, FechaFin): el llamador aplica los resultados en orden
// (inicio → fecha fin → cambio de actividad) sobre la lista ya modificada.

/// <summary>Resultado de mover el inicio del proyecto (P1).</summary>
/// <param name="Actividades">Lista resultante (las que empezaban en el inicio anterior pasan al nuevo).</param>
/// <param name="TerminanAntes">Hay actividades que terminan antes del nuevo inicio (400 en el llamador).</param>
public sealed record ResultadoMovimientoInicio(IReadOnlyList<ActividadCorte> Actividades, bool TerminanAntes);

/// <summary>
/// P1 (TAREA-18): al mover la fecha de inicio del proyecto (atrasarla o adelantarla), las actividades con
/// FechaInicio = inicio anterior pasan al nuevo inicio. Si alguna actividad termina antes del nuevo inicio, no es válido.
/// </summary>
public static class MovimientoInicioProyecto
{
    public static ResultadoMovimientoInicio Calcular(IReadOnlyList<ActividadCorte> actividades, DateOnly inicioAnterior, DateOnly inicioNuevo)
    {
        ArgumentNullException.ThrowIfNull(actividades);
        var resultado = actividades
            .Select(a => a.FechaInicio == inicioAnterior ? a with { FechaInicio = inicioNuevo } : a)
            .ToList();
        return new ResultadoMovimientoInicio(resultado, resultado.Any(a => a.FechaFin < inicioNuevo));
    }
}

/// <summary>
/// C3 / H14 (TAREA-18): al ampliar la fecha fin del proyecto, las actividades que terminaban en la fecha fin anterior se
/// extienden a la nueva. El resto no cambia.
/// </summary>
public static class AmpliacionProyecto
{
    public static IReadOnlyList<ActividadCorte> Calcular(IReadOnlyList<ActividadCorte> actividades, DateOnly finAnterior, DateOnly finNuevo)
    {
        ArgumentNullException.ThrowIfNull(actividades);
        if (finNuevo < finAnterior)
        {
            throw new ArgumentOutOfRangeException(nameof(finNuevo), finNuevo, "La ampliación necesita una fecha fin posterior a la anterior.");
        }

        return actividades.Select(a => a.FechaFin == finAnterior ? a with { FechaFin = finNuevo } : a).ToList();
    }
}

/// <summary>Actividad nueva del cambio de actividad (TipoMovimiento CAMBIO_ACTIVIDAD).</summary>
public sealed record ActividadNuevaCabecera(int Version, string Codigo, DateOnly FechaInicio, DateOnly FechaFin);

/// <summary>Resultado del cambio de actividad. MismaActividad = la vigente en "desde" ya es esa actividad (400).</summary>
/// <param name="Actividades">Lista resultante sin la nueva (recortadas y sin las eliminadas).</param>
public sealed record ResultadoCambioActividad(
    bool MismaActividad,
    IReadOnlyList<ActividadCorte> Actividades,
    IReadOnlyList<int> Eliminadas,
    IReadOnlyList<CambioFechaFin> Recortadas,
    ActividadNuevaCabecera? Nueva);

/// <summary>
/// C6 (TAREA-18), como Registrar del original (l. 2297–2334): se eliminan las actividades que empiezan en "desde" o
/// después; las que empiezan antes y llegan a "desde" se recortan a desde − 1 (el original solo recorta la última; sin
/// solapes es lo mismo); la nueva va de "desde" a la fecha fin del proyecto con versión = máx + 1 sobre todas las
/// existentes. Si la actividad vigente en "desde" (la de mayor versión) ya es la nueva, no hay cambio.
/// </summary>
public static class CambioActividad
{
    public static ResultadoCambioActividad Calcular(
        IReadOnlyList<ActividadCorte> actividades, string codigo, DateOnly desde, DateOnly finProyecto, int versionMaxima)
    {
        ArgumentNullException.ThrowIfNull(actividades);
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        if (desde > finProyecto)
        {
            throw new ArgumentOutOfRangeException(nameof(desde), desde, "La actividad debe empezar dentro del rango del proyecto.");
        }

        var vigente = actividades
            .Where(a => a.FechaInicio <= desde && a.FechaFin >= desde)
            .OrderByDescending(a => a.Version)
            .FirstOrDefault();
        if (vigente is not null && string.Equals(vigente.Codigo, codigo, StringComparison.OrdinalIgnoreCase))
        {
            return new ResultadoCambioActividad(true, actividades, [], [], null);
        }

        var eliminadas = actividades.Where(a => a.FechaInicio >= desde).Select(a => a.Id).ToList();
        var recortadas = actividades
            .Where(a => a.FechaInicio < desde && a.FechaFin >= desde)
            .Select(a => new CambioFechaFin(a.Id, a.FechaFin, desde.AddDays(-1)))
            .ToList();
        var nuevosFines = recortadas.ToDictionary(r => r.Id, r => r.FinNuevo);

        var resultado = actividades
            .Where(a => a.FechaInicio < desde)
            .Select(a => nuevosFines.TryGetValue(a.Id, out var fin) ? a with { FechaFin = fin } : a)
            .ToList();

        return new ResultadoCambioActividad(false, resultado, eliminadas, recortadas,
            new ActividadNuevaCabecera(versionMaxima + 1, codigo, desde, finProyecto));
    }
}
