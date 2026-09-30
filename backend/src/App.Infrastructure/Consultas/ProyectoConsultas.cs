using App.Application.Comun;
using App.Application.Proyectos;
using App.Domain.Proyectos;
using App.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Consultas;

internal sealed class ProyectoConsultas(ProfesiogramaDbContext db) : IProyectoConsultas
{
    private const string SeparadorBacks = " | "; // mismo separador que STRING_AGG en vwProyectoResumen

    public async Task<PaginaResultado<ProyectoResumenDto>> ListarAsync(ProyectoFiltro filtro, CancellationToken ct)
    {
        // Vista de solo lectura consultada con SqlQuery (tipo no mapeado: no se rastrea ni entra al modelo EF).
        // EF envuelve este SELECT como subconsulta, así que filtros, conteo y paginación se ejecutan en SQL.
        var consulta = db.Database.SqlQuery<FilaVwProyectoResumen>($"""
            SELECT Id, Codigo, NombreVisual, Grupo, Estado, FechaInicio, FechaFin, PropietarioUsuarioId,
                   HoraEntrada, HoraSalida, ResponsableNombre, BacksNombres
            FROM dbo.vwProyectoResumen
            """);

        if (filtro.PropietarioUsuarioId is int propietarioId)
        {
            consulta = consulta.Where(x => x.PropietarioUsuarioId == propietarioId);
        }

        if (filtro.EstadoCodigo is not null)
        {
            consulta = consulta.Where(x => x.Estado == filtro.EstadoCodigo);
        }

        if (filtro.GrupoCodigo is not null)
        {
            consulta = consulta.Where(x => x.Grupo == filtro.GrupoCodigo);
        }

        if (filtro.Texto is not null)
        {
            consulta = consulta.Where(x => x.Codigo.Contains(filtro.Texto) || x.NombreVisual.Contains(filtro.Texto));
        }

        // Solapamiento con el periodo del proyecto: FechaInicio <= hasta AND FechaFin >= desde.
        if (filtro.Hasta is DateOnly hasta)
        {
            consulta = consulta.Where(x => x.FechaInicio <= hasta);
        }

        if (filtro.Desde is DateOnly desde)
        {
            consulta = consulta.Where(x => x.FechaFin >= desde);
        }

        var total = await consulta.CountAsync(ct);

        var saltar = (long)(filtro.Pagina - 1) * filtro.Tamano;
        if (total == 0 || saltar >= total)
        {
            return new PaginaResultado<ProyectoResumenDto>([], filtro.Pagina, filtro.Tamano, total);
        }

        var filas = await consulta
            .OrderByDescending(x => x.FechaInicio)
            .ThenBy(x => x.Codigo)
            .Skip((int)saltar)
            .Take(filtro.Tamano)
            .ToListAsync(ct);

        var items = filas.Select(x => new ProyectoResumenDto(
                x.Id, x.Codigo, x.NombreVisual, x.Grupo, x.Estado, x.FechaInicio, x.FechaFin,
                x.ResponsableNombre, SepararBacks(x.BacksNombres), x.HoraEntrada, x.HoraSalida))
            .ToList();

        return new PaginaResultado<ProyectoResumenDto>(items, filtro.Pagina, filtro.Tamano, total);
    }

    public async Task<ProyectoDetalleDto?> ObtenerDetalleAsync(int id, int? propietarioUsuarioId, DateOnly hoy, CancellationToken ct)
    {
        var proyectos = db.Proyectos.AsNoTracking().Where(p => p.Id == id && !p.Eliminado);
        if (propietarioUsuarioId is int propietarioId)
        {
            proyectos = proyectos.Where(p => p.PropietarioUsuarioId == propietarioId);
        }

        // Consultas secuenciales: DbContext no admite operaciones concurrentes.
        var cabecera = await proyectos
            .Select(p => new
            {
                p.Id,
                p.Uid,
                p.Codigo,
                p.NombreVisual,
                Compania = new CompaniaResumenDto(p.Compania.Id, p.Compania.Nombre, p.Compania.Ruc),
                Grupo = new CodigoNombreDto(p.GrupoProyecto.Codigo, p.GrupoProyecto.Nombre),
                Estado = new CodigoNombreDto(p.EstadoProyecto.Codigo, p.EstadoProyecto.Nombre),
                p.FechaInicio,
                p.FechaFin,
                Erp = new ProyectoErpDto(p.ProyectoErpId, p.ProyectoErpNombre, p.ProyectoErpEstado,
                    p.DimensionUegpId, p.DimensionDescripcion),
                Departamento = p.Departamento != null ? p.Departamento.Nombre : null,
                Horario = new HorarioDto(p.HorarioCodigo, p.HorarioDescripcion, p.HoraEntrada, p.HoraSalida,
                    p.TipoHorario, p.HorasJornadaMin, p.HorasTrabajadasMin),
                Almuerzo = new AlmuerzoDto(p.SalidaAlmuerzo, p.RegresoAlmuerzo),
                Propietario = new PropietarioDto(p.Propietario.Id, p.Propietario.NombreMostrar, p.Propietario.Email)
            })
            .FirstOrDefaultAsync(ct);

        if (cabecera is null)
        {
            return null;
        }

        var personal = await db.ProyectoPersonal.AsNoTracking()
            .Where(pp => pp.ProyectoId == id)
            .OrderBy(pp => pp.RolAsignacionId).ThenBy(pp => pp.Numero)
            .Select(pp => new ProyectoPersonalDto(
                pp.Id, pp.RolAsignacion.Codigo, pp.Numero,
                new EmpleadoResumenDto(pp.Empleado.Id, pp.Empleado.CodigoEkon, pp.Empleado.NombreCompleto),
                pp.CargoAsignado ?? pp.Empleado.Puesto,
                pp.Jornada != null ? new CodigoNombreDto(pp.Jornada.Codigo, pp.Jornada.Nombre) : null,
                pp.DiasTrabajo, pp.DiasDescanso, pp.FechaInicio, pp.FechaFin,
                pp.EsPrincipalInicial, pp.TipoRegistro, pp.Observacion))
            .ToListAsync(ct);

        var etapas = await db.ProyectoEtapas.AsNoTracking()
            .Where(e => e.ProyectoId == id)
            .OrderBy(e => e.Version)
            .Select(e => new ProyectoEtapaDto(
                e.Version, e.TipoMovimiento.Codigo, e.EstadoProyecto.Codigo,
                e.FechaInicio, e.FechaFin, e.FechaCorte, e.ActividadCodigo, e.FechaCreacion))
            .ToListAsync(ct);

        var actividad = await OrdenarPorVigencia(db.ProyectoActividades.AsNoTracking().Where(a => a.ProyectoId == id), hoy)
            .Select(a => new ProyectoActividadDto(
                a.Version, a.TipoMovimiento.Codigo, a.ActividadCodigo, a.ActividadDescripcion,
                a.ActividadTipo, a.FechaInicio, a.FechaFin))
            .FirstOrDefaultAsync(ct);

        return new ProyectoDetalleDto(
            cabecera.Id, cabecera.Uid, cabecera.Codigo, cabecera.NombreVisual,
            cabecera.Compania, cabecera.Grupo, cabecera.Estado,
            cabecera.FechaInicio, cabecera.FechaFin,
            cabecera.Erp, cabecera.Departamento,
            cabecera.Horario, cabecera.Almuerzo, cabecera.Propietario,
            personal, etapas, actividad);
    }

    /// <summary>
    /// Actividad vigente: primero la que cubre <paramref name="hoy"/>; si ninguna, la de mayor versión.
    /// Es el mismo criterio que el OUTER APPLY "act" de dbo.vwProyectoResumen (VistasSql.VwProyectoResumen_V1).
    /// </summary>
    private static IQueryable<ProyectoActividad> OrdenarPorVigencia(IQueryable<ProyectoActividad> actividades, DateOnly hoy)
        => actividades
            .OrderBy(a => a.FechaInicio <= hoy && a.FechaFin >= hoy ? 0 : 1)
            .ThenByDescending(a => a.Version);

    private static IReadOnlyList<string> SepararBacks(string? nombres)
        => string.IsNullOrWhiteSpace(nombres)
            ? []
            : nombres.Split(SeparadorBacks, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Columnas leídas de dbo.vwProyectoResumen (tipo no mapeado para SqlQuery).</summary>
    internal sealed class FilaVwProyectoResumen
    {
        public int Id { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string NombreVisual { get; set; } = string.Empty;
        public string Grupo { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;
        public DateOnly FechaInicio { get; set; }
        public DateOnly FechaFin { get; set; }
        public int PropietarioUsuarioId { get; set; }
        public TimeOnly? HoraEntrada { get; set; }
        public TimeOnly? HoraSalida { get; set; }
        public string? ResponsableNombre { get; set; }
        public string? BacksNombres { get; set; }
    }
}
