using App.Domain.Proyectos.Estados;

namespace App.Application.Proyectos;

/// <summary>
/// Regla única de "actividad vigente" (TAREA-18b, O3). Ver docs/fases/FASE_5_Edicion_Cabecera.md §7.
/// - Fecha de referencia = max(inicio, min(fecha, fin)) con las fechas del proyecto:
///   vistas (detalle, cabecera, listado): fecha = hoy en Ecuador;
///   etapas: fecha = FechaCorte de la etapa, con las fechas resultantes del proyecto.
/// - Actividad vigente = la que cubre la referencia; si hay varias, la de mayor versión; si ninguna, null (sin respaldo).
/// - Excepción: la etapa CAMBIO_ACTIVIDAD lleva la actividad nueva (O2).
/// La vista dbo.vwProyectoResumen aplica la misma regla en SQL (VistasSql.VwProyectoResumen_V2).
/// </summary>
public static class ActividadVigente
{
    public static DateOnly FechaReferencia(DateOnly inicio, DateOnly fin, DateOnly fecha)
    {
        var acotada = fecha > fin ? fin : fecha;
        return acotada < inicio ? inicio : acotada;
    }

    /// <summary>La actividad que cubre la fecha (la de mayor versión si se solapan); null si ninguna.</summary>
    public static ActividadCorte? Elegir(IEnumerable<ActividadCorte> actividades, DateOnly fecha)
    {
        ArgumentNullException.ThrowIfNull(actividades);
        return actividades
            .Where(a => a.FechaInicio <= fecha && a.FechaFin >= fecha)
            .OrderByDescending(a => a.Version)
            .FirstOrDefault();
    }

    /// <summary>Atajo: la vigente en la fecha de referencia del proyecto.</summary>
    public static ActividadCorte? Elegir(IEnumerable<ActividadCorte> actividades, DateOnly inicio, DateOnly fin, DateOnly fecha) =>
        Elegir(actividades, FechaReferencia(inicio, fin, fecha));
}
