using System.Globalization;
using App.Application.Proyectos;
using App.Application.Proyectos.Crear;
using App.Domain.Catalogos;
using App.Domain.Proyectos.Estados;

namespace App.Application.Cronograma;

/// <summary>
/// Armado del cronograma persona × día (TAREA-20). Lógica pura: recibe las filas de las consultas y aplica la precedencia
/// del original (RN14, ControlMensualProyectos l. 746–764) con D1, D2, D3, D4 y D9 (docs/fases/FASE_5_Cronograma.md):
/// PERSONA &gt; GENERAL &gt; PROYECTO (de un proyecto con día de la persona ESE día) &gt; CHOQUE &gt; PROYECTO &gt; DESCANSO &gt; LIBRE.
/// Los días LIBRE no se devuelven (D7).
/// </summary>
public static class ArmadoCronograma
{
    public const string EstadoNovedad = "NOVEDAD";
    public const string EstadoChoque = "CHOQUE";
    public const string EstadoProyecto = "PROYECTO";
    public const string EstadoOtroProyecto = "OTRO_PROYECTO";
    public const string EstadoDescanso = "DESCANSO";

    private static readonly StringComparer OrdenNombre = StringComparer.Create(CultureInfo.GetCultureInfo("es-EC"), ignoreCase: true);

    /// <summary>Horario del proyecto; cruza la medianoche si la salida es anterior a la entrada (el día es el de entrada).</summary>
    public static HorarioCronogramaDto Horario(int? codigo, string? descripcion, TimeOnly? entrada, TimeOnly? salida) =>
        new(codigo, descripcion, entrada, salida, ReglasAlmuerzo.EsNocturno(entrada, salida));

    public static CronogramaDto Armar(
        DateOnly desde, DateOnly hasta, int maxDias,
        IReadOnlyList<DiaCronograma> dias,
        IReadOnlyList<DiaOtrosProyectos> otros,
        IReadOnlyList<NovedadDiaCronograma> novedades,
        IReadOnlyList<ActividadCronograma> actividades,
        IReadOnlyList<ProyectoCronogramaDto> proyectos)
    {
        ArgumentNullException.ThrowIfNull(dias);
        ArgumentNullException.ThrowIfNull(otros);
        ArgumentNullException.ThrowIfNull(novedades);
        ArgumentNullException.ThrowIfNull(actividades);
        ArgumentNullException.ThrowIfNull(proyectos);

        var otrosPorDia = otros.ToDictionary(o => (o.EmpleadoId, o.Fecha), o => o.Cantidad);
        var generales = novedades.Where(n => n.TipoAplicacionId == CatalogoIds.TipoAplicacionNovedad.General)
            .ToLookup(n => n.Fecha);
        var porPersona = novedades.Where(n => n.TipoAplicacionId == CatalogoIds.TipoAplicacionNovedad.Persona)
            .ToLookup(n => (n.EmpleadoId, n.Fecha));
        var porProyecto = novedades.Where(n => n.TipoAplicacionId == CatalogoIds.TipoAplicacionNovedad.Proyecto)
            .ToLookup(n => (n.ProyectoId, n.Fecha));
        var actividadesPorProyecto = actividades
            .GroupBy(a => a.ProyectoId)
            .ToDictionary(g => g.Key, g => g.Select(a => new ActividadCorte(a.Id, a.Version, a.Codigo, a.FechaInicio, a.FechaFin)).ToList());

        // D4: solo las personas con al menos un día asignado en el rango.
        var personas = dias
            .GroupBy(d => d.EmpleadoId)
            .Select(g => ArmarPersona(g.ToList(), desde, hasta, otrosPorDia, generales, porPersona, porProyecto, actividadesPorProyecto))
            .OrderBy(p => p.Nombre, OrdenNombre)
            .ThenBy(p => p.CodigoEkon, StringComparer.Ordinal)
            .ToList();

        var leyenda = personas
            .SelectMany(p => p.Dias)
            .Select(c => c.Novedad)
            .OfType<NovedadCeldaDto>()
            .DistinctBy(n => n.Tipo)
            .OrderBy(n => n.Tipo, StringComparer.Ordinal)
            .Select(n => new LeyendaNovedadDto(n.Tipo, n.Sigla, n.Color))
            .ToList();

        return new CronogramaDto(desde, hasta, maxDias, proyectos.OrderBy(p => p.Nombre, OrdenNombre).ToList(), personas, leyenda);
    }

    private static PersonaCronogramaDto ArmarPersona(
        List<DiaCronograma> diasPersona, DateOnly desde, DateOnly hasta,
        Dictionary<(int, DateOnly), int> otrosPorDia,
        ILookup<DateOnly, NovedadDiaCronograma> generales,
        ILookup<(int?, DateOnly), NovedadDiaCronograma> porPersona,
        ILookup<(int?, DateOnly), NovedadDiaCronograma> porProyecto,
        Dictionary<int, List<ActividadCorte>> actividadesPorProyecto)
    {
        var empleadoId = diasPersona[0].EmpleadoId;
        var asignadosPorFecha = diasPersona.ToLookup(d => d.Fecha);
        var celdas = new List<CeldaCronogramaDto>();

        for (var fecha = desde; fecha <= hasta; fecha = fecha.AddDays(1))
        {
            var asignados = asignadosPorFecha[fecha].OrderBy(d => d.ProyectoId).ThenBy(d => d.RolAsignacionId).ToList();
            var otrosDelDia = otrosPorDia.GetValueOrDefault((empleadoId, fecha));
            var celda = ArmarCelda(fecha, asignados, otrosDelDia, generales[fecha], porPersona[(empleadoId, fecha)],
                asignados.SelectMany(a => porProyecto[(a.ProyectoId, fecha)]), actividadesPorProyecto);
            if (celda is not null)
            {
                celdas.Add(celda);
            }
        }

        // Cargo asignado por proyecto: el del último día del rango en ese proyecto.
        var asignadoEn = diasPersona
            .GroupBy(d => d.ProyectoId)
            .OrderBy(g => g.Key)
            .Select(g => new AsignadoEnDto(g.Key, g.MaxBy(d => d.Fecha)!.Cargo))
            .ToList();

        return new PersonaCronogramaDto(diasPersona[0].CodigoEkon, diasPersona[0].NombreCompleto, asignadoEn, celdas);
    }

    /// <summary>Celda de un día; null = LIBRE (no se devuelve).</summary>
    internal static CeldaCronogramaDto? ArmarCelda(
        DateOnly fecha, IReadOnlyList<DiaCronograma> asignados, int otrosProyectos,
        IEnumerable<NovedadDiaCronograma> generales, IEnumerable<NovedadDiaCronograma> deLaPersona,
        IEnumerable<NovedadDiaCronograma> deSusProyectos, IReadOnlyDictionary<int, List<ActividadCorte>> actividadesPorProyecto)
    {
        var persona = deLaPersona.OrderByDescending(n => n.NovedadId).ToList();
        // D9: gana la de Id mayor en cada nivel; D1: la de proyecto solo si la persona tiene día en ese proyecto ese día.
        var novedad = persona.FirstOrDefault()
                      ?? generales.MaxBy(n => n.NovedadId)
                      ?? deSusProyectos.MaxBy(n => n.NovedadId);

        var activos = asignados.Count(a => a.RolAsignacionId != CatalogoIds.RolAsignacion.Descanso);
        var estado = (novedad, activos + otrosProyectos, activos) switch
        {
            (not null, _, _) => EstadoNovedad,
            (_, > 1, _) => EstadoChoque, // D2 y D3: con proyectos no visibles y de cualquier estado
            (_, 1, 1) => EstadoProyecto,
            (_, 1, 0) => EstadoOtroProyecto, // trabaja ese día solo en un proyecto que no viene en la respuesta
            _ when asignados.Count > 0 => EstadoDescanso,
            _ => null,
        };

        if (estado is null)
        {
            return null;
        }

        var celdasAsignacion = asignados
            .Select(a => new AsignacionCeldaDto(a.ProyectoId, a.Rol,
                actividadesPorProyecto.TryGetValue(a.ProyectoId, out var lista) ? ActividadVigente.Elegir(lista, fecha)?.Codigo : null))
            .ToList();

        return new CeldaCronogramaDto(fecha, estado, celdasAsignacion, otrosProyectos,
            novedad is null ? null : new NovedadCeldaDto(novedad.Tipo, novedad.Sigla, novedad.Color, novedad.Aplicacion, novedad.Origen,
                novedad.EstadoReporte, novedad.ObservacionReporte),
            Math.Max(persona.Count - 1, 0));
    }
}
