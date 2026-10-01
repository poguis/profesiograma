using App.Application.Catalogos;
using App.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Consultas;

internal sealed class CatalogoConsultas(ProfesiogramaDbContext db) : ICatalogoConsultas
{
    public async Task<CatalogosDto> ObtenerTodosAsync(CancellationToken ct)
    {
        // Consultas secuenciales: DbContext no admite operaciones concurrentes.
        var estados = await Estados(db).ToListAsync(ct);
        var movimientos = await TiposMovimiento(db).ToListAsync(ct);
        var grupos = await Grupos(db).ToListAsync(ct);
        var jornadas = await Jornadas(db).ToListAsync(ct);
        var roles = await Roles(db).ToListAsync(ct);
        var aplicaciones = await TiposAplicacion(db).ToListAsync(ct);
        var origenes = await Origenes(db).ToListAsync(ct);
        var tiposNovedad = await TiposNovedad(db).ToListAsync(ct);
        var departamentos = await Departamentos(db).ToListAsync(ct);

        return new CatalogosDto(estados, movimientos, grupos, jornadas, roles, aplicaciones, origenes, tiposNovedad, departamentos);
    }

    public async Task<IReadOnlyList<ParametroDto>> ObtenerParametrosAsync(CancellationToken ct)
        => await Parametros(db).ToListAsync(ct);

    // ------------------------------------------------------------------ consultas (internal: pruebas de traducción con ToQueryString)

    internal static IQueryable<EstadoProyectoDto> Estados(ProfesiogramaDbContext db) =>
        db.EstadosProyecto.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Orden)
            .Select(x => new EstadoProyectoDto(x.Id, x.Codigo, x.Nombre, x.EsVigente, x.Orden));

    internal static IQueryable<ItemCatalogoDto> TiposMovimiento(ProfesiogramaDbContext db) =>
        db.TiposMovimiento.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Orden)
            .Select(x => new ItemCatalogoDto(x.Id, x.Codigo, x.Nombre, x.Orden));

    internal static IQueryable<GrupoProyectoDto> Grupos(ProfesiogramaDbContext db) =>
        db.GruposProyecto.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Orden)
            .Select(x => new GrupoProyectoDto(x.Id, x.Codigo, x.Nombre, x.RequiereProyectoErp, x.RequiereDimension, x.Orden));

    internal static IQueryable<JornadaDto> Jornadas(ProfesiogramaDbContext db) =>
        db.Jornadas.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Orden)
            .Select(x => new JornadaDto(x.Id, x.Codigo, x.Nombre, x.DiasTrabajo, x.DiasDescanso, x.Orden));

    internal static IQueryable<RolAsignacionDto> Roles(ProfesiogramaDbContext db) =>
        db.RolesAsignacion.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Orden)
            .Select(x => new RolAsignacionDto(x.Id, x.Codigo, x.Nombre, x.EsDescanso, x.Orden));

    internal static IQueryable<ItemCatalogoDto> TiposAplicacion(ProfesiogramaDbContext db) =>
        db.TiposAplicacionNovedad.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Orden)
            .Select(x => new ItemCatalogoDto(x.Id, x.Codigo, x.Nombre, x.Orden));

    internal static IQueryable<OrigenNovedadDto> Origenes(ProfesiogramaDbContext db) =>
        db.OrigenesNovedad.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Orden)
            .Select(x => new OrigenNovedadDto(x.Id, x.Codigo, x.Nombre, x.EsEditable, x.Orden));

    internal static IQueryable<TipoNovedadDto> TiposNovedad(ProfesiogramaDbContext db) =>
        db.TiposNovedad.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Orden)
            .Select(x => new TipoNovedadDto(x.Id, x.Codigo, x.Nombre, x.SiglaCronograma, x.ColorHex,
                x.AplicaPersona, x.AplicaProyecto, x.AplicaGeneral, x.SeleccionableManual, x.Orden));

    internal static IQueryable<DepartamentoDto> Departamentos(ProfesiogramaDbContext db) =>
        db.Departamentos.AsNoTracking().Where(x => x.Activo).OrderBy(x => x.Orden)
            .Select(x => new DepartamentoDto(x.Id, x.Nombre, x.NombreCorto, x.Tipo, x.Orden));

    internal static IQueryable<ParametroDto> Parametros(ProfesiogramaDbContext db) =>
        db.Parametros.AsNoTracking().OrderBy(x => x.Clave)
            .Select(x => new ParametroDto(x.Clave, x.Valor, x.TipoDato, x.Descripcion));
}
