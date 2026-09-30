using App.Domain.Catalogos;
using App.Domain.Comun;

namespace App.Domain.Proyectos;

public class ProyectoActividad : EntidadAuditable
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public Proyecto Proyecto { get; set; } = null!;
    public Guid MovimientoUid { get; set; }
    public int Version { get; set; }
    public byte TipoMovimientoId { get; set; }
    public TipoMovimiento TipoMovimiento { get; set; } = null!;
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public string ActividadCodigo { get; set; } = string.Empty;
    public string? ActividadDescripcion { get; set; }
    public string? ActividadTipo { get; set; }
    public int? LegacyId { get; set; }
}
