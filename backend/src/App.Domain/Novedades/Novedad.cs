using App.Domain.Catalogos;
using App.Domain.Comun;
using App.Domain.Maestros;
using App.Domain.Proyectos;

namespace App.Domain.Novedades;

public class Novedad : EntidadAuditable
{
    public int Id { get; set; }
    public Guid Uid { get; set; }
    public string Codigo { get; set; } = string.Empty;

    public byte TipoAplicacionNovedadId { get; set; }
    public TipoAplicacionNovedad TipoAplicacionNovedad { get; set; } = null!;
    public byte TipoNovedadId { get; set; }
    public TipoNovedad TipoNovedad { get; set; } = null!;
    public byte OrigenNovedadId { get; set; }
    public OrigenNovedad OrigenNovedad { get; set; } = null!;

    /// <summary>Solo PERSONA.</summary>
    public int? EmpleadoId { get; set; }
    public Empleado? Empleado { get; set; }
    /// <summary>Solo PROYECTO.</summary>
    public int? ProyectoId { get; set; }
    public Proyecto? Proyecto { get; set; }

    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public string? Observacion { get; set; }
    public string? OrigenArea { get; set; }
    public string? RegistradoPorNombre { get; set; }
    public string? RegistradoPorCorreo { get; set; }
    public string? DetalleOrigenJson { get; set; }

    public bool Anulada { get; set; }
    public DateTime? FechaAnulacion { get; set; }
    public int? AnuladaPorId { get; set; }
    public string? MotivoAnulacion { get; set; }

    public int? LegacyId { get; set; }
    public byte[] RowVer { get; set; } = [];

    public ICollection<NovedadDia> Dias { get; set; } = [];
}
