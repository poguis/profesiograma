using App.Application.Comun;
using App.Application.Novedades;
using App.Domain.Novedades;
using App.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Consultas;

/// <summary>
/// Lectura de novedades (TAREA-23-1). La visibilidad la define PermisosNovedad.VisiblePara (J2) y se aplica aquí en SQL.
/// Sin N+1: el listado trae los días con un COUNT por fila; el detalle, sus días en una segunda consulta.
/// Índices: IX_Novedad_Rango (Anulada = 0), UQ_NovedadDia (NovedadId, Fecha).
/// </summary>
internal sealed class NovedadConsultas(ProfesiogramaDbContext db) : INovedadConsultas
{
    public async Task<PaginaResultado<NovedadResumenDto>> ListarAsync(NovedadFiltro filtro, CancellationToken ct)
    {
        var consulta = ConsultaListado(db, filtro);
        var total = await consulta.CountAsync(ct);

        var saltar = (long)(filtro.Pagina - 1) * filtro.Tamano;
        if (total == 0 || saltar >= total)
        {
            return new PaginaResultado<NovedadResumenDto>([], filtro.Pagina, filtro.Tamano, total);
        }

        var filas = await PaginaListado(consulta, (int)saltar, filtro.Tamano).ToListAsync(ct);
        var items = filas.Select(f => new NovedadResumenDto(
                f.Id, f.Codigo, AplicacionNovedad.Codigo(f.AplicacionId),
                new TipoNovedadResumenDto(f.TipoCodigo, f.TipoNombre, f.TipoSigla, f.TipoColor),
                f.Origen, Sujeto(f), f.FechaInicio, f.FechaFin, f.TotalDias, f.Anulada, f.FechaCreacion))
            .ToList();

        return new PaginaResultado<NovedadResumenDto>(items, filtro.Pagina, filtro.Tamano, total);
    }

    public async Task<NovedadDetalleDatos?> ObtenerDetalleAsync(int id, int? visibleParaUsuarioId, CancellationToken ct)
    {
        // Consultas secuenciales: DbContext no admite operaciones concurrentes.
        var f = await ConsultaDetalle(db, id, visibleParaUsuarioId).FirstOrDefaultAsync(ct);
        if (f is null)
        {
            return null;
        }

        var dias = await ConsultaDias(db, id).ToListAsync(ct);
        return new NovedadDetalleDatos(
            f.Id, f.Codigo, f.AplicacionId,
            new TipoNovedadResumenDto(f.TipoCodigo, f.TipoNombre, f.TipoSigla, f.TipoColor),
            f.Origen, f.OrigenEditable, Sujeto(f), f.PropietarioProyectoId,
            f.FechaInicio, f.FechaFin, dias, f.Observacion,
            f.Anulada, f.FechaAnulacion, f.MotivoAnulacion,
            f.RegistradoPorNombre, f.RegistradoPorCorreo, f.CreadoPorId, f.FechaCreacion, f.RowVer);
    }

    public async Task<IReadOnlyList<TipoNovedadRef>> ObtenerTiposAsync(CancellationToken ct) =>
        await ConsultaTipos(db).ToListAsync(ct);

    public Task<bool> ProyectoElegibleAsync(int proyectoId, int? propietarioUsuarioId, CancellationToken ct) =>
        ConsultaProyectoElegible(db, proyectoId, propietarioUsuarioId).AnyAsync(ct);

    private static SujetoNovedadDto? Sujeto(FilaNovedad f) =>
        f.CodigoEkon is not null ? new SujetoNovedadDto(new EmpleadoSujetoDto(f.CodigoEkon, f.EmpleadoNombre!), null)
        : f.ProyectoId is int p ? new SujetoNovedadDto(null, new ProyectoSujetoDto(p, f.ProyectoCodigo!, f.ProyectoNombre!))
        : null;

    // ------------------------------------------------------------------ consultas (internal: pruebas de traducción con ToQueryString)

    /// <summary>Novedades visibles: todas (Admin, null) o las de PermisosNovedad.VisiblePara(usuario).</summary>
    internal static IQueryable<Novedad> Visibles(ProfesiogramaDbContext db, int? visibleParaUsuarioId)
    {
        var novedades = db.Novedades.AsNoTracking();
        return visibleParaUsuarioId is int usuarioId ? novedades.Where(PermisosNovedad.VisiblePara(usuarioId)) : novedades;
    }

    internal static IQueryable<Novedad> ConsultaListado(ProfesiogramaDbContext db, NovedadFiltro filtro)
    {
        var consulta = Visibles(db, filtro.VisibleParaUsuarioId);

        if (!filtro.IncluirAnuladas)
        {
            consulta = consulta.Where(n => !n.Anulada);
        }

        if (filtro.AplicacionId is byte aplicacionId)
        {
            consulta = consulta.Where(n => n.TipoAplicacionNovedadId == aplicacionId);
        }

        if (filtro.TipoCodigo is not null)
        {
            consulta = consulta.Where(n => n.TipoNovedad.Codigo == filtro.TipoCodigo);
        }

        if (filtro.OrigenCodigo is not null)
        {
            consulta = consulta.Where(n => n.OrigenNovedad.Codigo == filtro.OrigenCodigo);
        }

        if (filtro.Texto is not null)
        {
            var t = filtro.Texto;
            consulta = consulta.Where(n => n.Codigo.Contains(t)
                || (n.Empleado != null && (n.Empleado.CodigoEkon.Contains(t) || n.Empleado.NombreCompleto.Contains(t)))
                || (n.Proyecto != null && (n.Proyecto.Codigo.Contains(t) || n.Proyecto.NombreVisual.Contains(t))));
        }

        // Solapamiento con el rango de la novedad: FechaInicio <= hasta AND FechaFin >= desde.
        if (filtro.Hasta is DateOnly hasta)
        {
            consulta = consulta.Where(n => n.FechaInicio <= hasta);
        }

        if (filtro.Desde is DateOnly desde)
        {
            consulta = consulta.Where(n => n.FechaFin >= desde);
        }

        return consulta;
    }

    internal static IQueryable<FilaNovedad> PaginaListado(IQueryable<Novedad> consulta, int saltar, int tamano) =>
        Proyectar(consulta.OrderByDescending(n => n.FechaInicio).ThenByDescending(n => n.Id).Skip(saltar).Take(tamano));

    internal static IQueryable<FilaNovedad> ConsultaDetalle(ProfesiogramaDbContext db, int id, int? visibleParaUsuarioId) =>
        Proyectar(Visibles(db, visibleParaUsuarioId).Where(n => n.Id == id));

    internal static IQueryable<DateOnly> ConsultaDias(ProfesiogramaDbContext db, int novedadId) =>
        db.NovedadDias.AsNoTracking().Where(d => d.NovedadId == novedadId).OrderBy(d => d.Fecha).Select(d => d.Fecha);

    internal static IQueryable<TipoNovedadRef> ConsultaTipos(ProfesiogramaDbContext db) =>
        db.TiposNovedad.AsNoTracking().Where(t => t.Activo).OrderBy(t => t.Orden)
            .Select(t => new TipoNovedadRef(t.Id, t.Codigo, t.Nombre, t.SiglaCronograma, t.ColorHex,
                t.AplicaPersona, t.AplicaProyecto, t.AplicaGeneral, t.SeleccionableManual));

    /// <summary>Proyecto no eliminado, en estado vigente (ACTIVO o SUSPENDIDO) y visible (R1: propietario; null = Admin).</summary>
    internal static IQueryable<int> ConsultaProyectoElegible(ProfesiogramaDbContext db, int proyectoId, int? propietarioUsuarioId)
    {
        var proyectos = db.Proyectos.AsNoTracking().Where(p => p.Id == proyectoId && !p.Eliminado && p.EstadoProyecto.EsVigente);
        if (propietarioUsuarioId is int propietario)
        {
            proyectos = proyectos.Where(p => p.PropietarioUsuarioId == propietario);
        }

        return proyectos.Select(p => p.Id);
    }

    private static IQueryable<FilaNovedad> Proyectar(IQueryable<Novedad> consulta) =>
        consulta.Select(n => new FilaNovedad
        {
            Id = n.Id,
            Codigo = n.Codigo,
            AplicacionId = n.TipoAplicacionNovedadId,
            TipoCodigo = n.TipoNovedad.Codigo,
            TipoNombre = n.TipoNovedad.Nombre,
            TipoSigla = n.TipoNovedad.SiglaCronograma,
            TipoColor = n.TipoNovedad.ColorHex,
            Origen = n.OrigenNovedad.Codigo,
            OrigenEditable = n.OrigenNovedad.EsEditable,
            CodigoEkon = n.Empleado != null ? n.Empleado.CodigoEkon : null,
            EmpleadoNombre = n.Empleado != null ? n.Empleado.NombreCompleto : null,
            ProyectoId = n.ProyectoId,
            ProyectoCodigo = n.Proyecto != null ? n.Proyecto.Codigo : null,
            ProyectoNombre = n.Proyecto != null ? n.Proyecto.NombreVisual : null,
            PropietarioProyectoId = n.Proyecto != null ? n.Proyecto.PropietarioUsuarioId : null,
            FechaInicio = n.FechaInicio,
            FechaFin = n.FechaFin,
            TotalDias = n.Dias.Count,
            Observacion = n.Observacion,
            Anulada = n.Anulada,
            FechaAnulacion = n.FechaAnulacion,
            MotivoAnulacion = n.MotivoAnulacion,
            RegistradoPorNombre = n.RegistradoPorNombre,
            RegistradoPorCorreo = n.RegistradoPorCorreo,
            CreadoPorId = n.CreadoPorId,
            FechaCreacion = n.FechaCreacion,
            RowVer = n.RowVer,
        });

    /// <summary>Fila plana (sin cédula; la cédula nunca sale de Empleado).</summary>
    internal sealed class FilaNovedad
    {
        public int Id { get; init; }
        public string Codigo { get; init; } = string.Empty;
        public byte AplicacionId { get; init; }
        public string TipoCodigo { get; init; } = string.Empty;
        public string TipoNombre { get; init; } = string.Empty;
        public string TipoSigla { get; init; } = string.Empty;
        public string TipoColor { get; init; } = string.Empty;
        public string Origen { get; init; } = string.Empty;
        public bool OrigenEditable { get; init; }
        public string? CodigoEkon { get; init; }
        public string? EmpleadoNombre { get; init; }
        public int? ProyectoId { get; init; }
        public string? ProyectoCodigo { get; init; }
        public string? ProyectoNombre { get; init; }
        public int? PropietarioProyectoId { get; init; }
        public DateOnly FechaInicio { get; init; }
        public DateOnly FechaFin { get; init; }
        public int TotalDias { get; init; }
        public string? Observacion { get; init; }
        public bool Anulada { get; init; }
        public DateTime? FechaAnulacion { get; init; }
        public string? MotivoAnulacion { get; init; }
        public string? RegistradoPorNombre { get; init; }
        public string? RegistradoPorCorreo { get; init; }
        public int CreadoPorId { get; init; }
        public DateTime FechaCreacion { get; init; }
        public byte[] RowVer { get; init; } = [];
    }
}
