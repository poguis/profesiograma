using App.Domain.Catalogos;
using App.Domain.Comun;

namespace App.Domain.Proyectos;

public class ProyectoAsignacionDia : EntidadAuditable
{
    public const string TipoAuto = "AUTO";
    public const string TipoManual = "MANUAL";

    public long Id { get; set; }
    public int ProyectoId { get; set; }
    public int ProyectoPersonalId { get; set; }
    public ProyectoPersonal ProyectoPersonal { get; set; } = null!;
    /// <summary>Redundante, controlado por FK compuesta a ProyectoPersonal (J5).</summary>
    public int EmpleadoId { get; set; }
    public DateOnly Fecha { get; set; }
    /// <summary>PRINCIPAL | BACK | DESCANSO</summary>
    public byte RolAsignacionId { get; set; }
    public RolAsignacion RolAsignacion { get; set; } = null!;
    public string TipoAsignacion { get; set; } = TipoAuto;
    public short Bloque { get; set; }
    public int? LegacyId { get; set; }
}
