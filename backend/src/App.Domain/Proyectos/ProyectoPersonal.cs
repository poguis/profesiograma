using App.Domain.Catalogos;
using App.Domain.Comun;
using App.Domain.Maestros;

namespace App.Domain.Proyectos;

public class ProyectoPersonal : EntidadAuditable
{
    public const string TipoRegistroJornada = "JORNADA";
    public const string TipoRegistroDescanso = "DESCANSO";

    public int Id { get; set; }
    public int ProyectoId { get; set; }
    public Proyecto Proyecto { get; set; } = null!;
    /// <summary>1 PRINCIPAL | 2 BACK</summary>
    public byte RolAsignacionId { get; set; }
    public RolAsignacion RolAsignacion { get; set; } = null!;
    public short Numero { get; set; }
    public int EmpleadoId { get; set; }
    public Empleado Empleado { get; set; } = null!;
    public string? CargoAsignado { get; set; }
    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }
    public byte? JornadaId { get; set; }
    public Jornada? Jornada { get; set; }
    public byte? DiasTrabajo { get; set; }
    public byte DiasDescanso { get; set; }
    public bool EsPrincipalInicial { get; set; }
    public string TipoRegistro { get; set; } = TipoRegistroJornada;
    public int? PrincipalRelacionadoId { get; set; }
    public string? Observacion { get; set; }
    public int? LegacyId { get; set; }

    public ICollection<ProyectoAsignacionDia> AsignacionesDia { get; set; } = [];
}
