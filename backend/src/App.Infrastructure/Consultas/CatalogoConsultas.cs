using App.Application.Catalogos;
using App.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Consultas;

internal sealed class CatalogoConsultas(ProfesiogramaDbContext db) : ICatalogoConsultas
{
    public async Task<CatalogosDto> ObtenerTodosAsync(CancellationToken ct)
    {
        // Consultas secuenciales: DbContext no admite operaciones concurrentes.
        var estados = await db.EstadosProyecto.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Orden)
            .Select(x => new EstadoProyectoDto(x.Id, x.Codigo, x.Nombre, x.EsVigente, x.Orden)).ToListAsync(ct);

        var movimientos = await db.TiposMovimiento.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Orden)
            .Select(x => new ItemCatalogoDto(x.Id, x.Codigo, x.Nombre, x.Orden)).ToListAsync(ct);

        var grupos = await db.GruposProyecto.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Orden)
            .Select(x => new GrupoProyectoDto(x.Id, x.Codigo, x.Nombre, x.RequiereProyectoErp, x.RequiereDimension, x.Orden))
            .ToListAsync(ct);

        var jornadas = await db.Jornadas.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Orden)
            .Select(x => new JornadaDto(x.Id, x.Codigo, x.Nombre, x.DiasTrabajo, x.DiasDescanso, x.Orden)).ToListAsync(ct);

        var roles = await db.RolesAsignacion.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Orden)
            .Select(x => new RolAsignacionDto(x.Id, x.Codigo, x.Nombre, x.EsDescanso, x.Orden)).ToListAsync(ct);

        var aplicaciones = await db.TiposAplicacionNovedad.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Orden)
            .Select(x => new ItemCatalogoDto(x.Id, x.Codigo, x.Nombre, x.Orden)).ToListAsync(ct);

        var origenes = await db.OrigenesNovedad.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Orden)
            .Select(x => new OrigenNovedadDto(x.Id, x.Codigo, x.Nombre, x.EsEditable, x.Orden)).ToListAsync(ct);

        var tiposNovedad = await db.TiposNovedad.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Orden)
            .Select(x => new TipoNovedadDto(x.Id, x.Codigo, x.Nombre, x.SiglaCronograma, x.ColorHex,
                x.AplicaPersona, x.AplicaProyecto, x.AplicaGeneral, x.SeleccionableManual, x.Orden))
            .ToListAsync(ct);

        var departamentos = await db.Departamentos.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Orden)
            .Select(x => new DepartamentoDto(x.Id, x.Nombre, x.NombreCorto, x.Tipo, x.Orden)).ToListAsync(ct);

        return new CatalogosDto(estados, movimientos, grupos, jornadas, roles, aplicaciones, origenes, tiposNovedad, departamentos);
    }

    public async Task<IReadOnlyList<ParametroDto>> ObtenerParametrosAsync(CancellationToken ct)
        => await db.Parametros.AsNoTracking().OrderBy(x => x.Clave)
            .Select(x => new ParametroDto(x.Clave, x.Valor, x.TipoDato, x.Descripcion))
            .ToListAsync(ct);
}
