using App.Domain.Catalogos;
using App.Domain.Comun;

namespace App.Domain.Proyectos;

public class ProyectoEtapa : EntidadAuditable
{
    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public Proyecto Proyecto { get; set; } = null!;
    public Guid EtapaUid { get; set; }
    public int Version { get; set; }
    public byte TipoMovimientoId { get; set; }
    public TipoMovimiento TipoMovimiento { get; set; } = null!;
    public byte EstadoProyectoId { get; set; }
    public EstadoProyecto EstadoProyecto { get; set; } = null!;
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public DateOnly? FechaCorte { get; set; }
    public string? ActividadCodigo { get; set; }
    /// <summary>JSON de auditoría (no se consulta).</summary>
    public string? SnapshotPersonal { get; set; }
    public int? LegacyId { get; set; }
}
