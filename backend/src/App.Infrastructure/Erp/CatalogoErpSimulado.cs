using App.Application.Erp;

namespace App.Infrastructure.Erp;

/// <summary>
/// Catálogos del ERP con datos fijos de prueba (SOLO Development). Coherentes con los datos de prueba de la base
/// (compañía 9001, PRY-DEV-0001..0003). Incluye un proyecto inactivo y un horario inactivo para probar los filtros,
/// que se aplican con las mismas reglas que el modo Http (ReglasErp).
/// </summary>
internal sealed class CatalogoErpSimulado : CatalogoErpBase
{
    public const int CompaniaPrueba = 9001;

    // name / tradeName del ERP → Nombre = tradeName ?? name = "COMPAÑÍA DE PRUEBA S.A." (igual que la tabla Compania).
    private static readonly (int Id, string Nombre, string? NombreComercial, string? Ruc)[] Companias =
    [
        (CompaniaPrueba, "PRUEBA", "COMPAÑÍA DE PRUEBA S.A.", "0999999999001"),
    ];

    private static readonly (int CompaniaId, string Id, string Nombre, string Estado)[] Proyectos =
    [
        (CompaniaPrueba, "DEV-ERP-001", "PROYECTO ERP DE PRUEBA", "Activo"),
        (CompaniaPrueba, "DEV-ERP-002", "PROYECTO ERP INACTIVO DE PRUEBA", "Inactivo"),
    ];

    private static readonly (int CompaniaId, string ProyectoId, ActividadErp Actividad)[] Actividades =
    [
        (CompaniaPrueba, "DEV-ERP-001", new ActividadErp("DEV.01", "ACTIVIDAD DE PRUEBA", "PRUEBA")),
    ];

    private static readonly (int CompaniaId, DimensionErp Dimension)[] Dimensiones =
    [
        (CompaniaPrueba, new DimensionErp("DEV-DIM-01", "PLANTA DE PRUEBA")),
        (CompaniaPrueba, new DimensionErp("DEV-DIM-02", "OFICINAS DE PRUEBA")),
    ];

    // Formato del ERP: horas "HHmm", status "A"/"I", tipoHorario código. El 1 coincide con PRY-DEV-0001 (tipo null).
    private static readonly (int Codigo, string Descripcion, string Entrada, string Salida, string Horas, string HorasTrab, string? Tipo, string Status)[] Horarios =
    [
        (1, "07:00 - 18:00 (PRUEBA)", "0700", "1800", "1100", "1000", null, "A"),
        (2, "08:00 - 17:00 (PRUEBA)", "0800", "1700", "0900", "0800", "M", "A"),
        (3, "06:00 - 14:00 (INACTIVO)", "0600", "1400", "0800", "0700", "M", "I"),
    ];

    public override Task<IReadOnlyList<CompaniaErp>> ListarCompaniasAsync(CancellationToken ct) =>
        Resultado(Companias.Select(c => new CompaniaErp(c.Id, ReglasErp.NombreCompania(c.Nombre, c.NombreComercial), c.Nombre, c.Ruc)));

    public override Task<IReadOnlyList<ProyectoErp>> ListarProyectosAsync(int companiaId, CancellationToken ct) =>
        Resultado(Proyectos
            .Where(p => p.CompaniaId == companiaId && ReglasErp.EsProyectoActivo(p.Estado))
            .Select(p => new ProyectoErp(p.Id, p.Nombre, p.Estado)));

    public override Task<IReadOnlyList<DimensionErp>> ListarDimensionesAsync(int companiaId, CancellationToken ct) =>
        Resultado(Dimensiones.Where(d => d.CompaniaId == companiaId).Select(d => d.Dimension));

    public override Task<IReadOnlyList<ActividadErp>> ListarActividadesAsync(int companiaId, string proyectoErpId, CancellationToken ct) =>
        Resultado(Actividades
            .Where(a => a.CompaniaId == companiaId && string.Equals(a.ProyectoId, proyectoErpId.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(a => a.Actividad));

    public override Task<IReadOnlyList<HorarioErp>> ListarHorariosAsync(CancellationToken ct) =>
        Resultado(Horarios
            .Where(h => ReglasErp.EsHorarioActivo(h.Status))
            .Select(h => new HorarioErp(h.Codigo, h.Descripcion, ReglasErp.AHora(h.Entrada), ReglasErp.AHora(h.Salida),
                ReglasErp.AMinutos(h.Horas), ReglasErp.AMinutos(h.HorasTrab), h.Tipo)));

    private static Task<IReadOnlyList<T>> Resultado<T>(IEnumerable<T> items) => Task.FromResult<IReadOnlyList<T>>(items.ToList());
}
