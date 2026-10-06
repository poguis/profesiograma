using App.Application.Proyectos.Crear;
using App.Domain.Proyectos.Estados;

namespace App.Application.Proyectos.Estados;

/// <summary>Impacto de un recorte en el formato de la vista previa del cambio de estado.</summary>
internal sealed record ImpactoRecorte(
    IReadOnlyList<DiasEliminadosDto> DiasEliminados,
    IReadOnlyList<PersonaEliminadaDto> PersonalEliminado,
    IReadOnlyList<PersonaRecortadaDto> PersonalRecortado,
    IReadOnlyList<ActividadAfectadaDto> ActividadesAfectadas,
    IReadOnlyList<string> AdvertenciasBacks);

/// <summary>
/// Presentación común de un PlanRecorte: cambio de estado (TAREA-14) y edición de cabecera al acortar la fecha fin
/// (TAREA-18, C4). Extraído de CambioEstadoServicio sin cambiar el resultado.
/// </summary>
internal static class VistaRecorte
{
    public static ImpactoRecorte Crear(IReadOnlyList<PersonalCambio> personal, IReadOnlyList<ActividadCorte> actividades, PlanRecorte plan)
    {
        var personas = personal.ToDictionary(p => p.Corte.Id);

        var advertenciasBacks = plan.BacksSinPrincipal.Select(id =>
            $"El back {personas[id].Corte.Numero} ({personas[id].NombreCompleto}) quedará sin principal relacionado.").ToList();

        var porId = actividades.ToDictionary(a => a.Id);
        var actividadesAfectadas = plan.ActividadesRecortadas
            .Select(r => new ActividadAfectadaDto(porId[r.Id].Codigo, porId[r.Id].Version, porId[r.Id].FechaInicio,
                r.FinAnterior, r.FinNuevo, "RECORTADA"))
            .Concat(plan.ActividadesEliminadas.Select(id => new ActividadAfectadaDto(porId[id].Codigo, porId[id].Version,
                porId[id].FechaInicio, porId[id].FechaFin, null, "ELIMINADA")))
            .OrderBy(a => a.Version)
            .ToList();

        return new ImpactoRecorte(
            plan.DiasEliminados.Select(d => new DiasEliminadosDto(
                Empleado(personas[d.PersonalId]), CalculadorCruces.NombreRol(d.Rol), d.Cantidad, d.Desde, d.Hasta)).ToList(),
            plan.PersonalEliminado.Select(id => personas[id]).Select(p => new PersonaEliminadaDto(
                Empleado(p), CalculadorCruces.NombreRol(p.Corte.Rol), p.Corte.Numero, p.Corte.FechaInicio, p.Corte.FechaFin)).ToList(),
            plan.PersonalRecortado.Select(r => (r, p: personas[r.Id])).Select(x => new PersonaRecortadaDto(
                Empleado(x.p), CalculadorCruces.NombreRol(x.p.Corte.Rol), x.p.Corte.Numero, x.p.Corte.FechaInicio, x.r.FinAnterior,
                x.r.FinNuevo)).ToList(),
            actividadesAfectadas,
            advertenciasBacks);
    }

    /// <summary>Snapshot del personal RESULTANTE (sin eliminados, fechas recortadas; sin recorte = tal cual). Formato de la creación.</summary>
    public static string Snapshot(IReadOnlyList<PersonalCambio> personal, PlanRecorte? plan)
    {
        var eliminados = plan?.PersonalEliminado.ToHashSet() ?? [];
        var recortes = plan?.PersonalRecortado.ToDictionary(r => r.Id, r => r.FinNuevo) ?? [];

        return SnapshotPersonal.Serializar(personal
            .Where(p => !eliminados.Contains(p.Corte.Id))
            .OrderBy(p => p.Corte.Rol).ThenBy(p => p.Corte.Numero)
            .Select(p => new ElementoSnapshot(
                p.Corte.Numero,
                CalculadorCruces.NombreRol(p.Corte.Rol),
                p.CodigoEkon,
                p.NombreCompleto,
                p.Corte.FechaInicio,
                recortes.GetValueOrDefault(p.Corte.Id, p.Corte.FechaFin),
                p.JornadaCodigo,
                p.DiasTrabajo,
                p.DiasDescanso,
                p.TipoRegistro)));
    }

    public static EmpleadoCambioDto Empleado(PersonalCambio p) => new(p.Corte.EmpleadoId, p.CodigoEkon, p.NombreCompleto);
}
