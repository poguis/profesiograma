using System.Globalization;
using App.Application.Cronograma;
using App.Application.Cronograma.Reporte;
using App.Application.Proyectos;
using App.Domain.Catalogos;
using App.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace App.Infrastructure.Consultas;

/// <summary>
/// Consultas del cronograma (TAREA-20, docs/fases/FASE_5_Cronograma.md §5). Cada método es UNA consulta (sin N+1);
/// las consultas se exponen como internal static IQueryable para las pruebas de traducción con ToQueryString.
/// Índices: IX_ProyectoAsignacionDia_Cronograma (Fecha), IX_NovedadDia_Fecha, IX_ProyectoActividad_Vigencia.
/// </summary>
internal sealed class CronogramaConsultas(ProfesiogramaDbContext db) : ICronogramaConsultas
{
    internal const string ClaveMaxDias = "CRONOGRAMA_MAX_DIAS";

    public async Task<int?> ObtenerMaxDiasAsync(CancellationToken ct) =>
        int.TryParse(await ConsultaMaxDias(db).FirstOrDefaultAsync(ct), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            ? n
            : null;

    public async Task<IReadOnlyList<DiaCronograma>> ObtenerDiasAsync(FiltroCronograma filtro, CancellationToken ct) =>
        (await ConsultaDias(db, filtro).ToListAsync(ct))
            .Select(d => new DiaCronograma(d.ProyectoId, d.EmpleadoId, d.CodigoEkon, d.NombreCompleto, d.Fecha, d.RolAsignacionId, d.Rol, d.Cargo))
            .ToList();

    public async Task<IReadOnlyList<DiaOtrosProyectos>> ObtenerOtrosProyectosAsync(FiltroOtrosProyectos filtro, CancellationToken ct) =>
        filtro.EmpleadoIds.Count == 0
            ? []
            : (await ConsultaOtrosProyectos(db, filtro).ToListAsync(ct)).Select(o => new DiaOtrosProyectos(o.EmpleadoId, o.Fecha, o.Cantidad)).ToList();

    public async Task<IReadOnlyList<NovedadDiaCronograma>> ObtenerNovedadesAsync(
        IReadOnlyCollection<int> empleadoIds, IReadOnlyCollection<int> proyectoIds, DateOnly desde, DateOnly hasta, CancellationToken ct) =>
        (await ConsultaNovedades(db, empleadoIds, proyectoIds, desde, hasta).ToListAsync(ct))
            .Select(n => new NovedadDiaCronograma(n.NovedadId, n.Fecha, n.TipoAplicacionId, n.Aplicacion, n.EmpleadoId, n.ProyectoId,
                n.Tipo, n.Sigla, n.Color, n.EstadoReporte, n.ObservacionReporte, n.Origen))
            .ToList();

    public async Task<IReadOnlyList<ActividadCronograma>> ObtenerActividadesAsync(
        IReadOnlyCollection<int> proyectoIds, DateOnly desde, DateOnly hasta, CancellationToken ct) =>
        (await ConsultaActividades(db, proyectoIds, desde, hasta).ToListAsync(ct))
            .Select(a => new ActividadCronograma(a.ProyectoId, a.Id, a.Version, a.Codigo, a.FechaInicio, a.FechaFin))
            .ToList();

    public async Task<IReadOnlyList<ProyectoCronogramaDto>> ObtenerProyectosAsync(IReadOnlyCollection<int> proyectoIds, CancellationToken ct) =>
        (await ConsultaProyectos(db, proyectoIds).ToListAsync(ct))
            .Select(p => new ProyectoCronogramaDto(p.Id, p.Codigo, p.NombreVisual, p.Estado, p.Grupo, p.CompaniaId, p.ProyectoErpId,
                p.DimensionUegpId,
                ArmadoCronograma.Horario(p.HorarioCodigo, p.HorarioDescripcion, p.HoraEntrada, p.HoraSalida),
                new AlmuerzoDto(p.SalidaAlmuerzo, p.RegresoAlmuerzo)))
            .ToList();

    public Task<RangoProyecto?> ObtenerRangoProyectoAsync(int proyectoId, int? propietarioUsuarioId, CancellationToken ct) =>
        ConsultaRangoProyecto(db, proyectoId, propietarioUsuarioId).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<EmpleadoReporte>> ObtenerEmpleadosReporteAsync(IReadOnlyCollection<int> empleadoIds, CancellationToken ct) =>
        empleadoIds.Count == 0
            ? []
            : (await ConsultaEmpleadosReporte(db, empleadoIds).ToListAsync(ct))
                .Select(e => new EmpleadoReporte(e.Id, e.CodigoEkon, e.NombreCompleto, e.Cedula,
                    string.IsNullOrWhiteSpace(e.CodigoInfor) ? null : e.CodigoInfor.Trim()))
                .ToList();

    // ------------------------------------------------------------------ consultas

    internal static IQueryable<string> ConsultaMaxDias(ProfesiogramaDbContext db) =>
        db.Parametros.AsNoTracking().Where(p => p.Clave == ClaveMaxDias).Select(p => p.Valor);

    /// <summary>
    /// (1) Días del rango en proyectos no eliminados y visibles (R1). ProyectoId / DepartamentoId: las personas con días
    /// en ese proyecto o departamento, con TODOS sus días visibles (como el filtro del original).
    /// </summary>
    internal static IQueryable<FilaDia> ConsultaDias(ProfesiogramaDbContext db, FiltroCronograma f)
    {
        var dias =
            from d in db.ProyectoAsignacionesDia.AsNoTracking()
            join p in db.Proyectos on d.ProyectoId equals p.Id
            where d.Fecha >= f.Desde && d.Fecha <= f.Hasta && !p.Eliminado
                  && (f.PropietarioUsuarioId == null || p.PropietarioUsuarioId == f.PropietarioUsuarioId)
            select new { d, p };

        if (f.SoloProyectoId is int solo)
        {
            dias = dias.Where(x => x.d.ProyectoId == solo);
        }

        if (f.ProyectoId is int proyectoId)
        {
            var personas = dias.Where(x => x.d.ProyectoId == proyectoId).Select(x => x.d.EmpleadoId);
            dias = dias.Where(x => personas.Contains(x.d.EmpleadoId));
        }

        if (f.DepartamentoId is int departamentoId)
        {
            var personas = dias.Where(x => x.p.DepartamentoId == departamentoId).Select(x => x.d.EmpleadoId);
            dias = dias.Where(x => personas.Contains(x.d.EmpleadoId));
        }

        if (f.CodigoEkon is { } codigo)
        {
            dias = dias.Where(x => x.d.ProyectoPersonal.Empleado.CodigoEkon == codigo);
        }

        return dias.Select(x => new FilaDia
        {
            ProyectoId = x.d.ProyectoId,
            EmpleadoId = x.d.EmpleadoId,
            CodigoEkon = x.d.ProyectoPersonal.Empleado.CodigoEkon,
            NombreCompleto = x.d.ProyectoPersonal.Empleado.NombreCompleto,
            Fecha = x.d.Fecha,
            RolAsignacionId = x.d.RolAsignacionId,
            Rol = x.d.RolAsignacion.Codigo,
            Cargo = x.d.ProyectoPersonal.CargoAsignado,
        });
    }

    /// <summary>
    /// (1b) D2: días de trabajo (rol ≠ DESCANSO) por persona y fecha en proyectos no eliminados que NO vienen en la
    /// respuesta: de otro propietario (global, no Admin) o distintos del proyecto pedido (por proyecto). Cualquier estado
    /// (D3). Solo cantidades: sin Id, código ni nombre del proyecto.
    /// </summary>
    internal static IQueryable<FilaOtros> ConsultaOtrosProyectos(ProfesiogramaDbContext db, FiltroOtrosProyectos f) =>
        from d in db.ProyectoAsignacionesDia.AsNoTracking()
        join p in db.Proyectos on d.ProyectoId equals p.Id
        where f.EmpleadoIds.Contains(d.EmpleadoId)
              && d.Fecha >= f.Desde && d.Fecha <= f.Hasta
              && d.RolAsignacionId != CatalogoIds.RolAsignacion.Descanso
              && !p.Eliminado
              && (f.VisiblesDePropietario == null || p.PropietarioUsuarioId != f.VisiblesDePropietario)
              && (f.SalvoProyectoId == null || p.Id != f.SalvoProyectoId)
        group d by new { d.EmpleadoId, d.Fecha } into g
        select new FilaOtros { EmpleadoId = g.Key.EmpleadoId, Fecha = g.Key.Fecha, Cantidad = g.Count() };

    /// <summary>
    /// (2) Novedades no anuladas del rango (cualquier origen, D11): GENERAL, PERSONA de esas personas y PROYECTO de esos
    /// proyectos. LINQ sobre las tablas, sin mapear vwNovedadDiaVigente (D13).
    /// </summary>
    internal static IQueryable<FilaNovedad> ConsultaNovedades(
        ProfesiogramaDbContext db, IReadOnlyCollection<int> empleadoIds, IReadOnlyCollection<int> proyectoIds, DateOnly desde, DateOnly hasta) =>
        from nd in db.NovedadDias.AsNoTracking()
        let n = nd.Novedad
        where nd.Fecha >= desde && nd.Fecha <= hasta && !n.Anulada
              && (n.TipoAplicacionNovedadId == CatalogoIds.TipoAplicacionNovedad.General
                  || (n.TipoAplicacionNovedadId == CatalogoIds.TipoAplicacionNovedad.Persona && empleadoIds.Contains(n.EmpleadoId!.Value))
                  || (n.TipoAplicacionNovedadId == CatalogoIds.TipoAplicacionNovedad.Proyecto && proyectoIds.Contains(n.ProyectoId!.Value)))
        select new FilaNovedad
        {
            NovedadId = n.Id,
            Fecha = nd.Fecha,
            TipoAplicacionId = n.TipoAplicacionNovedadId,
            Aplicacion = n.TipoAplicacionNovedad.Codigo,
            EmpleadoId = n.EmpleadoId,
            ProyectoId = n.ProyectoId,
            Tipo = n.TipoNovedad.Codigo,
            Sigla = n.TipoNovedad.SiglaCronograma,
            Color = n.TipoNovedad.ColorHex,
            EstadoReporte = n.TipoNovedad.EstadoReporte,
            ObservacionReporte = n.TipoNovedad.ObservacionReporte,
            Origen = n.OrigenNovedad.Codigo,
        };

    /// <summary>(3) Actividades de esos proyectos que se solapan con el rango (la vigente por día la elige ActividadVigente).</summary>
    internal static IQueryable<FilaActividad> ConsultaActividades(
        ProfesiogramaDbContext db, IReadOnlyCollection<int> proyectoIds, DateOnly desde, DateOnly hasta) =>
        db.ProyectoActividades.AsNoTracking()
            .Where(a => proyectoIds.Contains(a.ProyectoId) && a.FechaInicio <= hasta && a.FechaFin >= desde)
            .Select(a => new FilaActividad
            {
                ProyectoId = a.ProyectoId, Id = a.Id, Version = a.Version, Codigo = a.ActividadCodigo,
                FechaInicio = a.FechaInicio, FechaFin = a.FechaFin,
            });

    /// <summary>(4) Cabeceras de esos proyectos (ya filtrados por visibilidad en la consulta 1).</summary>
    internal static IQueryable<FilaProyecto> ConsultaProyectos(ProfesiogramaDbContext db, IReadOnlyCollection<int> proyectoIds) =>
        db.Proyectos.AsNoTracking()
            .Where(p => proyectoIds.Contains(p.Id))
            .Select(p => new FilaProyecto
            {
                Id = p.Id,
                Codigo = p.Codigo,
                NombreVisual = p.NombreVisual,
                Estado = p.EstadoProyecto.Codigo,
                Grupo = p.GrupoProyecto.Codigo,
                CompaniaId = p.CompaniaId,
                ProyectoErpId = p.ProyectoErpId,
                DimensionUegpId = p.DimensionUegpId,
                HorarioCodigo = p.HorarioCodigo,
                HorarioDescripcion = p.HorarioDescripcion,
                HoraEntrada = p.HoraEntrada,
                HoraSalida = p.HoraSalida,
                SalidaAlmuerzo = p.SalidaAlmuerzo,
                RegresoAlmuerzo = p.RegresoAlmuerzo,
            });

    /// <summary>(5) Proyecto no eliminado y visible (R1) con su rango, para el endpoint por proyecto.</summary>
    internal static IQueryable<RangoProyecto> ConsultaRangoProyecto(ProfesiogramaDbContext db, int proyectoId, int? propietarioUsuarioId) =>
        db.Proyectos.AsNoTracking()
            .Where(p => p.Id == proyectoId && !p.Eliminado && (propietarioUsuarioId == null || p.PropietarioUsuarioId == propietarioUsuarioId))
            .Select(p => new RangoProyecto(p.Id, p.FechaInicio, p.FechaFin));

    /// <summary>
    /// (6) TAREA-22-2, reporte Excel: datos de esas personas con su cédula y el código Infor de D14 (CargoInfor ACTIVO cuyo
    /// Cargo = Empleado.Puesto). La comparación usa la collation de la base (Modern_Spanish_CI_AS, sin UPPER: el predicado
    /// queda sobre la columna, que tiene el índice único UQ_CargoInfor_Cargo). Es la única consulta del cronograma con [Cedula].
    /// </summary>
    internal static IQueryable<FilaEmpleadoReporte> ConsultaEmpleadosReporte(ProfesiogramaDbContext db, IReadOnlyCollection<int> empleadoIds) =>
        from e in db.Empleados.AsNoTracking()
        where empleadoIds.Contains(e.Id)
        join c in db.CargosInfor.AsNoTracking().Where(c => c.Activo) on e.Puesto equals c.Cargo into cargos
        from c in cargos.DefaultIfEmpty()
        select new FilaEmpleadoReporte
        {
            Id = e.Id,
            CodigoEkon = e.CodigoEkon,
            NombreCompleto = e.NombreCompleto,
            Cedula = e.Cedula,
            CodigoInfor = c == null ? null : c.CodigoInfor,
        };

    // ------------------------------------------------------------------ filas (clases con init: traducibles por EF)

    /// <summary>Sin ToString propio: es una clase (no un record), no imprime sus valores.</summary>
    internal sealed class FilaEmpleadoReporte
    {
        public int Id { get; init; }
        public string CodigoEkon { get; init; } = string.Empty;
        public string NombreCompleto { get; init; } = string.Empty;
        public string? Cedula { get; init; }
        public string? CodigoInfor { get; init; }
    }

    internal sealed class FilaDia
    {
        public int ProyectoId { get; init; }
        public int EmpleadoId { get; init; }
        public string CodigoEkon { get; init; } = string.Empty;
        public string NombreCompleto { get; init; } = string.Empty;
        public DateOnly Fecha { get; init; }
        public byte RolAsignacionId { get; init; }
        public string Rol { get; init; } = string.Empty;
        public string? Cargo { get; init; }
    }

    internal sealed class FilaOtros
    {
        public int EmpleadoId { get; init; }
        public DateOnly Fecha { get; init; }
        public int Cantidad { get; init; }
    }

    internal sealed class FilaNovedad
    {
        public int NovedadId { get; init; }
        public DateOnly Fecha { get; init; }
        public byte TipoAplicacionId { get; init; }
        public string Aplicacion { get; init; } = string.Empty;
        public int? EmpleadoId { get; init; }
        public int? ProyectoId { get; init; }
        public string Tipo { get; init; } = string.Empty;
        public string Sigla { get; init; } = string.Empty;
        public string Color { get; init; } = string.Empty;
        public string EstadoReporte { get; init; } = string.Empty;
        public string ObservacionReporte { get; init; } = string.Empty;
        public string Origen { get; init; } = string.Empty;
    }

    internal sealed class FilaActividad
    {
        public int ProyectoId { get; init; }
        public int Id { get; init; }
        public int Version { get; init; }
        public string Codigo { get; init; } = string.Empty;
        public DateOnly FechaInicio { get; init; }
        public DateOnly FechaFin { get; init; }
    }

    internal sealed class FilaProyecto
    {
        public int Id { get; init; }
        public string Codigo { get; init; } = string.Empty;
        public string NombreVisual { get; init; } = string.Empty;
        public string Estado { get; init; } = string.Empty;
        public string Grupo { get; init; } = string.Empty;
        public int CompaniaId { get; init; }
        public string? ProyectoErpId { get; init; }
        public string? DimensionUegpId { get; init; }
        public int? HorarioCodigo { get; init; }
        public string? HorarioDescripcion { get; init; }
        public TimeOnly? HoraEntrada { get; init; }
        public TimeOnly? HoraSalida { get; init; }
        public TimeOnly? SalidaAlmuerzo { get; init; }
        public TimeOnly? RegresoAlmuerzo { get; init; }
    }
}
