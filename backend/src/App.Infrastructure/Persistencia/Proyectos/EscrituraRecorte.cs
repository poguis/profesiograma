using App.Domain.Proyectos;
using App.Domain.Proyectos.Estados;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Persistencia.Proyectos;

/// <summary>
/// Escritura común de un PlanRecorte (E3): cambio de estado (TAREA-14) y edición de cabecera al acortar la fecha fin
/// (TAREA-18, C4). Extraída de CambioEstadoRepositorio sin cambiar el orden (FK compuesta y autorreferenciada, Restrict):
///  1) ExecuteDelete de los días (Fecha &gt; F, o del personal que se elimina);
///  2) referencias a principales eliminados en null y recorte de FechaFin del personal (el llamador guarda: SaveChanges 1);
///  3) eliminación del personal y de las actividades, recorte de actividades (el llamador guarda con el resto: SaveChanges 2).
/// </summary>
internal static class EscrituraRecorte
{
    /// <summary>Pasos 1 y 2. Devuelve el personal con seguimiento para el paso 3.</summary>
    public static async Task<List<ProyectoPersonal>> AplicarAntesDeGuardarAsync(
        ProfesiogramaDbContext db, int proyectoId, PlanRecorte plan, CancellationToken ct)
    {
        var personalEliminado = plan.PersonalEliminado.ToHashSet();

        // 1) Días: antes que el personal (FK compuesta). Las filas desaparecen: no necesitan auditoría.
        await CambioEstadoRepositorio.ConsultaDiasAEliminar(db, proyectoId, plan.Fecha, plan.PersonalEliminado).ExecuteDeleteAsync(ct);

        var personal = await CambioEstadoRepositorio.ConsultaPersonalSeguimiento(db, proyectoId).ToListAsync(ct);

        // 2) Referencias a principales que se eliminan (FK autorreferenciada Restrict) y recorte de fechas.
        foreach (var p in personal.Where(p => p.PrincipalRelacionadoId is int principal && personalEliminado.Contains(principal)))
        {
            p.PrincipalRelacionadoId = null;
        }

        var recortes = plan.PersonalRecortado.ToDictionary(r => r.Id, r => r.FinNuevo);
        foreach (var p in personal.Where(p => recortes.ContainsKey(p.Id)))
        {
            p.FechaFin = recortes[p.Id]; // DiasDescanso no cambia (E3)
        }

        return personal;
    }

    /// <summary>Paso 3: personal y actividades eliminadas, actividades recortadas (H3). No guarda.</summary>
    public static void AplicarPersonalYActividades(
        ProfesiogramaDbContext db, PlanRecorte plan, IReadOnlyCollection<ProyectoPersonal> personal, IReadOnlyCollection<ProyectoActividad> actividades)
    {
        var personalEliminado = plan.PersonalEliminado.ToHashSet();
        db.ProyectoPersonal.RemoveRange(personal.Where(p => personalEliminado.Contains(p.Id)));

        var actividadesEliminadas = plan.ActividadesEliminadas.ToHashSet();
        db.ProyectoActividades.RemoveRange(actividades.Where(a => actividadesEliminadas.Contains(a.Id)));
        var actividadesRecortadas = plan.ActividadesRecortadas.ToDictionary(r => r.Id, r => r.FinNuevo);
        foreach (var a in actividades.Where(a => actividadesRecortadas.ContainsKey(a.Id)))
        {
            a.FechaFin = actividadesRecortadas[a.Id];
        }
    }
}
