using System.Globalization;
using System.Text;
using App.Application.Proyectos;
using App.Domain.Catalogos;
using App.Domain.Proyectos.Estados;

namespace App.Application.Cronograma.Reporte;

/// <summary>
/// Filas del reporte Excel de un proyecto (TAREA-22-2; original: ControlMensualProyectos, botón «REPORTE»). Lógica pura.
/// Una fila por persona + día con asignación en el proyecto (de cualquier rol, Z4); una novedad sin asignación no da fila.
/// Por fila: novedad (PERSONA &gt; GENERAL &gt; PROYECTO de este proyecto, la de Id mayor, Z5/D9) → LIBRE por novedad;
/// si no, trabajo → LABORABLE; si solo descanso → LIBRE por descanso. En los días libres las 4 horas van en 00:00:00.
/// El día de un turno nocturno es el de la entrada (pendiente 50).
/// </summary>
public static class ArmadoReporte
{
    public const string Laborable = "LABORABLE";
    public const string Libre = "LIBRE";
    public const string Descanso = "DESCANSO";

    /// <summary>Z7: máximo de dígitos que se exporta como número (15: la precisión de Excel).</summary>
    public const int MaxDigitosNumero = 15;

    private static readonly StringComparer OrdenNombre = StringComparer.Create(CultureInfo.GetCultureInfo("es-EC"), ignoreCase: true);

    /// <summary>
    /// Z7 [SUPUESTO mientras falte el Office Script]: un texto se exporta como NÚMERO solo si tiene únicamente dígitos, no
    /// empieza en "0" y tiene 15 dígitos o menos. Si no, texto (así "0123" conserva su cero).
    /// </summary>
    public static bool EsNumeroExportable(string? texto, out long numero)
    {
        numero = 0;
        return texto is { Length: > 0 and <= MaxDigitosNumero }
               && texto[0] != '0'
               && texto.All(char.IsAsciiDigit)
               && long.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out numero);
    }

    /// <summary>FECHA: texto d/M/yyyy sin ceros a la izquierda (como el original: Text(fecha, "d/m/yyyy")), p. ej. 2/10/2026.</summary>
    public static string Fecha(DateOnly fecha) => fecha.ToString("d/M/yyyy", CultureInfo.InvariantCulture);

    /// <summary>Z14: Reporte_{código}_{yyyyMMdd}_{yyyyMMdd}.xlsx con las fechas pedidas; caracteres no válidos del código → "_".</summary>
    public static string NombreArchivo(string codigoProyecto, DateOnly desde, DateOnly hasta)
    {
        var codigo = new StringBuilder();
        foreach (var c in codigoProyecto)
        {
            codigo.Append(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' ? c : '_');
        }

        var f = (DateOnly d) => d.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        return $"Reporte_{codigo}_{f(desde)}_{f(hasta)}.xlsx";
    }

    /// <summary>Filas ordenadas (Z15) y resumen (Z9).</summary>
    /// <param name="dias">Días del proyecto en el rango (consulta 1 con SoloProyectoId).</param>
    /// <param name="otros">Consulta 1b con SalvoProyectoId: días de trabajo de esas personas en otros proyectos.</param>
    /// <param name="actividades">Actividades del proyecto (todas sus versiones).</param>
    public static (IReadOnlyList<FilaReporte> Filas, ResumenReporteDto Resumen) Armar(
        ProyectoCronogramaDto proyecto,
        IReadOnlyList<DiaCronograma> dias,
        IReadOnlyList<NovedadDiaCronograma> novedades,
        IReadOnlyList<ActividadCronograma> actividades,
        IReadOnlyList<EmpleadoReporte> empleados,
        IReadOnlyList<DiaOtrosProyectos> otros)
    {
        ArgumentNullException.ThrowIfNull(proyecto);
        ArgumentNullException.ThrowIfNull(dias);
        ArgumentNullException.ThrowIfNull(novedades);
        ArgumentNullException.ThrowIfNull(actividades);
        ArgumentNullException.ThrowIfNull(empleados);
        ArgumentNullException.ThrowIfNull(otros);

        var deLaPersona = novedades.Where(n => n.TipoAplicacionId == CatalogoIds.TipoAplicacionNovedad.Persona).ToLookup(n => (n.EmpleadoId, n.Fecha));
        var generales = novedades.Where(n => n.TipoAplicacionId == CatalogoIds.TipoAplicacionNovedad.General).ToLookup(n => n.Fecha);
        var delProyecto = novedades
            .Where(n => n.TipoAplicacionId == CatalogoIds.TipoAplicacionNovedad.Proyecto && n.ProyectoId == proyecto.Id)
            .ToLookup(n => n.Fecha);
        var cortes = actividades.Where(a => a.ProyectoId == proyecto.Id)
            .Select(a => new ActividadCorte(a.Id, a.Version, a.Codigo, a.FechaInicio, a.FechaFin)).ToList();
        var ultimaActividad = cortes.MaxBy(a => a.Version)?.Codigo;
        var personas = empleados.ToDictionary(e => e.Id);
        var conOtroProyecto = otros.Where(o => o.Cantidad > 0).Select(o => (o.EmpleadoId, o.Fecha)).ToHashSet();
        var empresa = proyecto.CompaniaId.ToString(CultureInfo.InvariantCulture);

        var armadas = dias
            .Where(d => d.ProyectoId == proyecto.Id)
            .GroupBy(d => (d.EmpleadoId, d.Fecha))
            .Select(g =>
            {
                var (empleadoId, fecha) = g.Key;
                var persona = personas.GetValueOrDefault(empleadoId)
                              ?? new EmpleadoReporte(empleadoId, g.First().CodigoEkon, g.First().NombreCompleto, null, null);
                var novedad = deLaPersona[(empleadoId, fecha)].MaxBy(n => n.NovedadId)
                              ?? generales[fecha].MaxBy(n => n.NovedadId)
                              ?? delProyecto[fecha].MaxBy(n => n.NovedadId);
                var trabaja = g.Any(d => d.RolAsignacionId != CatalogoIds.RolAsignacion.Descanso);
                var actividad = ActividadVigente.Elegir(cortes, fecha)?.Codigo ?? ultimaActividad;
                var (estado, observacion) = novedad is not null
                    ? (novedad.EstadoReporte, novedad.ObservacionReporte)
                    : trabaja ? (Laborable, proyecto.Grupo) : (Libre, Descanso);
                var laborable = novedad is null && trabaja;

                var fila = new FilaReporte(
                    empresa,
                    persona.CodigoEkon,
                    persona.Cedula,
                    persona.NombreCompleto,
                    Fecha(fecha),
                    Hora(laborable, proyecto.Horario.Entrada),
                    Hora(laborable, proyecto.Almuerzo.Regreso), // INGRESO ALMUERZO ← regreso, como lo mapeaba el original
                    Hora(laborable, proyecto.Almuerzo.Salida),
                    Hora(laborable, proyecto.Horario.Salida),   // nocturno: tal cual, menor que el ingreso (supuesto 54)
                    proyecto.ProyectoErpId,                       // PLANTA/OFICINAS sin ProjectId: vacío (supuesto 53)
                    actividad,
                    estado,
                    observacion,
                    persona.CodigoInfor);
                return (Fila: fila, EmpleadoId: empleadoId, FechaDia: fecha, ConOtro: conOtroProyecto.Contains((empleadoId, fecha)));
            })
            .OrderBy(x => x.Fila.Empresa.Length).ThenBy(x => x.Fila.Empresa, StringComparer.Ordinal) // numérica (CompaniaId)
            .ThenBy(x => x.Fila.Proyecto is null).ThenBy(x => x.Fila.Proyecto, StringComparer.Ordinal)
            .ThenBy(x => x.Fila.Nombre, OrdenNombre)
            .ThenBy(x => x.FechaDia)
            .ThenBy(x => x.Fila.Ekon, StringComparer.Ordinal)
            .ToList();

        var filas = armadas.Select(x => x.Fila).ToList();
        var enReporte = armadas.Select(x => x.EmpleadoId).Distinct().Select(id => personas.GetValueOrDefault(id)).ToList();
        var resumen = new ResumenReporteDto(
            filas.Count,
            enReporte.Count,
            enReporte.Count(p => p?.Cedula is null),
            enReporte.Count(p => p?.CodigoInfor is null),
            filas.Count(f => f.Actividad is null),
            armadas.Count(x => x.ConOtro));
        return (filas, resumen);
    }

    /// <summary>Hora del proyecto en un día laborable; 00:00:00 en los días libres o si falta (almuerzo nulo, D16).</summary>
    private static TimeSpan Hora(bool laborable, TimeOnly? hora) => laborable && hora is { } h ? h.ToTimeSpan() : TimeSpan.Zero;
}
