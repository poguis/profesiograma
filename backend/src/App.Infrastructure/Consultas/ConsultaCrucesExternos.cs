using App.Application.Proyectos.Crear;
using App.Domain.Catalogos;
using App.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Consultas;

/// <summary>
/// Cruces con otros proyectos (RN07, FASE_5 §6): días rol ≠ DESCANSO de proyectos no eliminados con estado vigente.
/// Usa IX_ProyectoAsignacionDia_Cruces (EmpleadoId, Fecha) INCLUDE (ProyectoId, RolAsignacionId).
/// Dentro de ITransaccionAsignaciones usa la misma conexión y transacción (DbContext con alcance de petición).
/// </summary>
internal sealed class ConsultaCrucesExternos(ProfesiogramaDbContext db) : IConsultaCrucesExternos
{
    public async Task<IReadOnlyList<AsignacionExistente>> BuscarAsync(
        IReadOnlyCollection<int> empleadoIds, DateOnly desde, DateOnly hasta, int? excluirProyectoId, CancellationToken ct)
    {
        if (empleadoIds.Count == 0)
        {
            return [];
        }

        return await Consulta(db, empleadoIds, desde, hasta, excluirProyectoId).ToListAsync(ct);
    }

    /// <summary>Consulta LINQ (expuesta para verificar el SQL generado con ToQueryString en las pruebas).</summary>
    internal static IQueryable<AsignacionExistente> Consulta(
        ProfesiogramaDbContext db, IReadOnlyCollection<int> empleadoIds, DateOnly desde, DateOnly hasta, int? excluirProyectoId) =>
        from d in db.ProyectoAsignacionesDia.AsNoTracking()
        join p in db.Proyectos on d.ProyectoId equals p.Id
        where empleadoIds.Contains(d.EmpleadoId)
              && d.Fecha >= desde && d.Fecha <= hasta
              && d.RolAsignacionId != CatalogoIds.RolAsignacion.Descanso
              && !p.Eliminado
              && p.EstadoProyecto.EsVigente
              && (excluirProyectoId == null || p.Id != excluirProyectoId)
        select new AsignacionExistente(
            d.EmpleadoId, d.Fecha, d.RolAsignacionId, p.Id, p.Codigo, p.NombreVisual, p.EstadoProyecto.Codigo);
}
