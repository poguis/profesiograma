using App.Application.Empleados;
using App.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Consultas;

/// <summary>
/// Consultas del buscador de empleados. TAREA-26d: los empleados vienen de la API (IFuenteEmpleadosErp, caché en
/// memoria) y se filtran en memoria (BusquedaEmpleados); los departamentos del usuario, de la base. La regla de
/// departamentos la decide Application.
/// </summary>
internal sealed class EmpleadoConsultas(ProfesiogramaDbContext db, IFuenteEmpleadosErp fuente) : IEmpleadoConsultas
{
    public async Task<ResultadoBusquedaEmpleadosDto> BuscarAsync(EmpleadoFiltro filtro, CancellationToken ct) =>
        BusquedaEmpleados.Buscar(await fuente.ObtenerActivosAsync(ct), filtro);

    public async Task<IReadOnlyList<string>> ObtenerDepartamentosDeUsuarioAsync(int usuarioId, CancellationToken ct) =>
        await ConsultaDepartamentosDeUsuario(db, usuarioId).ToListAsync(ct);

    // ------------------------------------------------------------------ consultas (internal: pruebas de traducción con ToQueryString)

    internal static IQueryable<string> ConsultaDepartamentosDeUsuario(ProfesiogramaDbContext db, int usuarioId) =>
        db.UsuarioDepartamentos.AsNoTracking()
            .Where(ud => ud.UsuarioId == usuarioId && ud.Activo && ud.Departamento.Activo)
            .Select(ud => ud.Departamento.Nombre)
            .Distinct();
}
