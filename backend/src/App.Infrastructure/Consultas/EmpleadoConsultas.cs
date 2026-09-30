using App.Application.Comun;
using App.Application.Empleados;
using App.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Consultas;

/// <summary>Consultas de solo lectura sobre la caché local de empleados. La regla de departamentos la decide Application.</summary>
internal sealed class EmpleadoConsultas(ProfesiogramaDbContext db) : IEmpleadoConsultas
{
    /// <summary>EstadoErp de un empleado activo (API EvolutionEmployee: estado = "A").</summary>
    private const string EstadoActivo = "A";

    public async Task<PaginaResultado<EmpleadoBusquedaDto>> BuscarAsync(EmpleadoFiltro filtro, CancellationToken ct)
    {
        var consulta = db.Empleados.AsNoTracking().Where(e => e.EstadoErp == EstadoActivo);

        if (filtro.Texto is not null)
        {
            consulta = consulta.Where(e => e.CodigoEkon.Contains(filtro.Texto) || e.NombreCompleto.Contains(filtro.Texto));
        }

        // Coincidencia POR NOMBRE con el departamento o la unidad del empleado (collation CI: sin distinguir mayúsculas).
        if (filtro.Departamentos is { } departamentos)
        {
            consulta = consulta.Where(e =>
                (e.Departamento != null && departamentos.Contains(e.Departamento)) ||
                (e.Unidad != null && departamentos.Contains(e.Unidad)));
        }

        var total = await consulta.CountAsync(ct);
        var saltar = (long)(filtro.Pagina - 1) * filtro.Tamano;
        if (total == 0 || saltar >= total)
        {
            return new PaginaResultado<EmpleadoBusquedaDto>([], filtro.Pagina, filtro.Tamano, total);
        }

        var items = await consulta
            .OrderBy(e => e.NombreCompleto).ThenBy(e => e.CodigoEkon)
            .Skip((int)saltar)
            .Take(filtro.Tamano)
            .Select(e => new EmpleadoBusquedaDto(e.Id, e.CodigoEkon, e.NombreCompleto, e.Puesto, e.Departamento))
            .ToListAsync(ct);

        return new PaginaResultado<EmpleadoBusquedaDto>(items, filtro.Pagina, filtro.Tamano, total);
    }

    public async Task<IReadOnlyList<string>> ObtenerDepartamentosDeUsuarioAsync(int usuarioId, CancellationToken ct) =>
        await db.UsuarioDepartamentos.AsNoTracking()
            .Where(ud => ud.UsuarioId == usuarioId && ud.Activo && ud.Departamento.Activo)
            .Select(ud => ud.Departamento.Nombre)
            .Distinct()
            .ToListAsync(ct);
}
