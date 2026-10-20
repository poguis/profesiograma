using App.Application.Empleados;
using App.Domain.Maestros;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Persistencia.Empleados;

/// <summary>«Completar cédulas» (TAREA-22-1). Se usa dentro de ITransaccionAsignaciones (mismo DbContext con alcance).</summary>
internal sealed class CompletarCedulasRepositorio(ProfesiogramaDbContext db) : ICompletarCedulasRepositorio
{
    public async Task<IReadOnlyList<Empleado>> ObtenerSinCedulaAsync(CancellationToken ct) =>
        await ConsultaSinCedula(db).ToListAsync(ct);

    public Task GuardarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    /// <summary>Filas sin cédula, CON seguimiento (internal: prueba de traducción con ToQueryString).</summary>
    internal static IQueryable<Empleado> ConsultaSinCedula(ProfesiogramaDbContext db) =>
        db.Empleados.Where(e => e.Cedula == null);
}
