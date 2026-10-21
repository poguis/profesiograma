using App.Application.Cronograma;
using App.Application.Cronograma.Reporte;
using App.Application.Proyectos;
using App.Domain.Catalogos;

namespace App.Application.Tests.Cronograma;

/// <summary>Datos ficticios del cronograma (TAREA-20): códigos EKON 9000xx y proyectos 1–3.</summary>
internal static class DoblesCronograma
{
    public static readonly DateOnly D1 = new(2026, 10, 1);

    public static DateOnly D(int dia) => new(2026, 10, dia);

    public static DiaCronograma Dia(int empleado, int proyecto, int dia, byte rol = CatalogoIds.RolAsignacion.Principal, string? cargo = "CARGO") =>
        new(proyecto, empleado, $"9000{empleado:00}", $"PERSONA {empleado:00}", D(dia), rol,
            rol switch { CatalogoIds.RolAsignacion.Principal => "PRINCIPAL", CatalogoIds.RolAsignacion.Back => "BACK", _ => "DESCANSO" }, cargo);

    public static NovedadDiaCronograma Persona(int id, int empleado, int dia, string tipo = "VACACIONES") =>
        Novedad(id, CatalogoIds.TipoAplicacionNovedad.Persona, "PERSONA", empleado, null, dia, tipo);

    public static NovedadDiaCronograma General(int id, int dia) =>
        Novedad(id, CatalogoIds.TipoAplicacionNovedad.General, "GENERAL", null, null, dia, "FERIADO");

    public static NovedadDiaCronograma DeProyecto(int id, int proyecto, int dia) =>
        Novedad(id, CatalogoIds.TipoAplicacionNovedad.Proyecto, "PROYECTO", null, proyecto, dia, "FERIADO");

    private static NovedadDiaCronograma Novedad(int id, byte aplicacionId, string aplicacion, int? empleado, int? proyecto, int dia, string tipo) =>
        new(id, D(dia), aplicacionId, aplicacion, empleado, proyecto, tipo, tipo[..1], "#000000", "LIBRE", tipo, "PROFESIOGRAMA");

    public static ProyectoCronogramaDto Proyecto(int id) =>
        new(id, $"PRY-{id}", $"PROYECTO {id}", "ACTIVO", "CAMPO", 9001, $"ERP-{id}", null,
            ArmadoCronograma.Horario(1, "07:00 - 18:00", new TimeOnly(7, 0), new TimeOnly(18, 0)), new AlmuerzoDto(new TimeOnly(13, 0), new TimeOnly(14, 0)));

    public static CronogramaDto Armar(IReadOnlyList<DiaCronograma> dias, IReadOnlyList<NovedadDiaCronograma>? novedades = null,
        IReadOnlyList<DiaOtrosProyectos>? otros = null, IReadOnlyList<ActividadCronograma>? actividades = null, int hasta = 31) =>
        ArmadoCronograma.Armar(D1, D(hasta), 90, dias, otros ?? [], novedades ?? [], actividades ?? [],
            dias.Select(d => d.ProyectoId).Distinct().Select(Proyecto).ToList());

    public static CeldaCronogramaDto Celda(CronogramaDto c, int empleado, int dia) =>
        c.Personas.Single(p => p.CodigoEkon == $"9000{empleado:00}").Dias.Single(x => x.Fecha == D(dia));
}

/// <summary>Consultas falsas: devuelven los datos cargados (filtrados por rango, proyecto y propietario) y registran los filtros.</summary>
internal sealed class CronogramaConsultasFalsas : ICronogramaConsultas
{
    public int? MaxDias { get; set; } = 90;
    public List<DiaCronograma> Dias { get; } = [];
    public Dictionary<int, int> PropietarioDe { get; } = [];
    public List<DiaOtrosProyectos> Otros { get; } = [];
    public List<NovedadDiaCronograma> Novedades { get; } = [];
    public Dictionary<int, RangoProyecto> Rangos { get; } = [];

    public FiltroCronograma? UltimoFiltro { get; private set; }
    public FiltroOtrosProyectos? UltimoFiltroOtros { get; private set; }
    public int LecturasOtros { get; private set; }

    public Task<int?> ObtenerMaxDiasAsync(CancellationToken ct) => Task.FromResult(MaxDias);

    public Task<IReadOnlyList<DiaCronograma>> ObtenerDiasAsync(FiltroCronograma filtro, CancellationToken ct)
    {
        UltimoFiltro = filtro;
        return Task.FromResult<IReadOnlyList<DiaCronograma>>(Dias
            .Where(d => d.Fecha >= filtro.Desde && d.Fecha <= filtro.Hasta)
            .Where(d => filtro.SoloProyectoId is null || d.ProyectoId == filtro.SoloProyectoId)
            .Where(d => filtro.PropietarioUsuarioId is null || PropietarioDe.GetValueOrDefault(d.ProyectoId) == filtro.PropietarioUsuarioId)
            .ToList());
    }

    public Task<IReadOnlyList<DiaOtrosProyectos>> ObtenerOtrosProyectosAsync(FiltroOtrosProyectos filtro, CancellationToken ct)
    {
        LecturasOtros++;
        UltimoFiltroOtros = filtro;
        return Task.FromResult<IReadOnlyList<DiaOtrosProyectos>>(Otros);
    }

    public Task<IReadOnlyList<NovedadDiaCronograma>> ObtenerNovedadesAsync(
        IReadOnlyCollection<int> empleadoIds, IReadOnlyCollection<int> proyectoIds, DateOnly desde, DateOnly hasta, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<NovedadDiaCronograma>>(Novedades);

    public Task<IReadOnlyList<ActividadCronograma>> ObtenerActividadesAsync(
        IReadOnlyCollection<int> proyectoIds, DateOnly desde, DateOnly hasta, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ActividadCronograma>>([]);

    public Task<IReadOnlyList<ProyectoCronogramaDto>> ObtenerProyectosAsync(IReadOnlyCollection<int> proyectoIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ProyectoCronogramaDto>>(proyectoIds.Select(DoblesCronograma.Proyecto).ToList());

    public Task<RangoProyecto?> ObtenerRangoProyectoAsync(int proyectoId, int? propietarioUsuarioId, CancellationToken ct) =>
        Task.FromResult(Rangos.TryGetValue(proyectoId, out var r)
                        && (propietarioUsuarioId is null || PropietarioDe.GetValueOrDefault(proyectoId) == propietarioUsuarioId)
            ? r
            : null);

    // TAREA-22-2 (reporte): datos de las personas (cédulas FICTICIAS).
    public List<EmpleadoReporte> EmpleadosReporte { get; } = [];

    public Task<IReadOnlyList<EmpleadoReporte>> ObtenerEmpleadosReporteAsync(IReadOnlyCollection<int> empleadoIds, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<EmpleadoReporte>>(EmpleadosReporte.Where(e => empleadoIds.Contains(e.Id)).ToList());
}
