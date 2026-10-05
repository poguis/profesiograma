using System.Globalization;
using App.Domain.Proyectos.Cronograma;

namespace App.Application.Proyectos.Crear;

/// <summary>
/// Cruces internos (motor) y externos (otros proyectos vigentes) + resumen como la app original
/// (persona / rol / proyecto / mes, días "5, 6, 7"). Solo presentación: las reglas están en el motor y en la consulta.
/// </summary>
public static class CalculadorCruces
{
    public const string OrigenInterno = "INTERNO";
    public const string OrigenExterno = "EXTERNO";
    /// <summary>Edición: día regenerado que repite persona y fecha con un día ya guardado del mismo proyecto (TAREA-17).</summary>
    public const string OrigenHistorico = "HISTORICO";
    public const string MismoProyecto = "MISMO PROYECTO";

    /// <summary>Cultura fija para el mes ("diciembre 2026"); nunca la del servidor.</summary>
    private static readonly CultureInfo CulturaEcuador = CultureInfo.GetCultureInfo("es-EC");

    public static IReadOnlyList<CruceDto> Internos(ResultadoCronograma cronograma, IReadOnlyDictionary<int, EmpleadoRef> empleados) =>
        Internos(cronograma.CrucesInternos, empleados);

    /// <summary>Un cruce por persona involucrada (rol de cada una), como en la creación.</summary>
    public static IReadOnlyList<CruceDto> Internos(IEnumerable<CruceInterno> cruces, IReadOnlyDictionary<int, EmpleadoRef> empleados) =>
        cruces
            .SelectMany(c => c.Involucrados.Select(persona => Crear(
                OrigenInterno, empleados[c.EmpleadoId], c.Fecha, persona.Rol, MismoProyecto, null, null)))
            .ToList();

    /// <summary>HISTORICO_PROPIO (edición): un cruce por día regenerado, con el rol del día regenerado.</summary>
    public static IReadOnlyList<CruceDto> Historicos(IEnumerable<CruceHistorico> cruces, IReadOnlyDictionary<int, EmpleadoRef> empleados) =>
        cruces
            .Select(c => Crear(OrigenHistorico, empleados[c.EmpleadoId], c.Fecha, c.Regenerada.Rol, MismoProyecto, null, null))
            .ToList();

    /// <summary>
    /// Cruza los días ≠ DESCANSO del proyecto nuevo con los días existentes (ya filtrados por la consulta:
    /// otros proyectos vigentes, rol ≠ DESCANSO). Un cruce por día nuevo y proyecto existente.
    /// </summary>
    public static IReadOnlyList<CruceDto> Externos(
        IEnumerable<DiaAsignado> diasNuevos, IReadOnlyList<AsignacionExistente> existentes, IReadOnlyDictionary<int, EmpleadoRef> empleados)
    {
        var porClave = existentes.ToLookup(a => (a.EmpleadoId, a.Fecha));

        return diasNuevos
            .Where(d => d.Rol != RolCronograma.Descanso)
            .SelectMany(d => porClave[(d.EmpleadoId, d.Fecha)]
                .DistinctBy(a => a.ProyectoId)
                .Select(a => Crear(OrigenExterno, empleados[d.EmpleadoId], d.Fecha, d.Rol, a.ProyectoNombre, a.ProyectoCodigo, a.EstadoProyecto)))
            .OrderBy(c => c.Fecha).ThenBy(c => c.NombreEmpleado)
            .ToList();
    }

    /// <summary>Agrupa por persona / rol / proyecto / mes con la lista de días del mes ("5, 6, 7").</summary>
    public static IReadOnlyList<ResumenCruceDto> Resumen(IEnumerable<CruceDto> cruces) =>
        cruces
            .GroupBy(c => (c.NombreEmpleado, c.Rol, Proyecto: EtiquetaProyecto(c), c.Fecha.Year, c.Fecha.Month))
            .OrderBy(g => g.Key.NombreEmpleado, StringComparer.Ordinal).ThenBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .ThenBy(g => g.Key.Rol, StringComparer.Ordinal).ThenBy(g => g.Key.Proyecto, StringComparer.Ordinal)
            .Select(g => new ResumenCruceDto(
                g.Key.NombreEmpleado,
                g.Key.Rol,
                g.Key.Proyecto,
                new DateOnly(g.Key.Year, g.Key.Month, 1).ToString("MMMM yyyy", CulturaEcuador),
                string.Join(", ", g.Select(c => c.Fecha.Day).Distinct().Order())))
            .ToList();

    public static string NombreRol(RolCronograma rol) => rol switch
    {
        RolCronograma.Principal => "PRINCIPAL",
        RolCronograma.Back => "BACK",
        RolCronograma.Descanso => "DESCANSO",
        _ => rol.ToString().ToUpperInvariant(),
    };

    private static string EtiquetaProyecto(CruceDto c) =>
        c.ProyectoCodigo is null ? c.Proyecto : $"{c.ProyectoCodigo} · {c.Proyecto}";

    private static CruceDto Crear(string origen, EmpleadoRef empleado, DateOnly fecha, RolCronograma rol,
        string proyecto, string? codigo, string? estado) =>
        new(origen, empleado.Id, empleado.CodigoEkon, empleado.NombreCompleto, fecha, NombreRol(rol), proyecto, codigo, estado);
}
