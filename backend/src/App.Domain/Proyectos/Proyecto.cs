using App.Domain.Catalogos;
using App.Domain.Comun;
using App.Domain.Maestros;
using App.Domain.Seguridad;

namespace App.Domain.Proyectos;

public class Proyecto : EntidadAuditable
{
    public int Id { get; set; }
    public Guid Uid { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string NombreVisual { get; set; } = string.Empty;

    public byte GrupoProyectoId { get; set; }
    public GrupoProyecto GrupoProyecto { get; set; } = null!;
    public int CompaniaId { get; set; }
    public Compania Compania { get; set; } = null!;

    public string? ProyectoErpId { get; set; }
    public string? ProyectoErpNombre { get; set; }
    public string? ProyectoErpEstado { get; set; }
    public string? DimensionUegpId { get; set; }
    public string? DimensionDescripcion { get; set; }

    public DateOnly FechaInicio { get; set; }
    public DateOnly FechaFin { get; set; }

    public byte EstadoProyectoId { get; set; }
    public EstadoProyecto EstadoProyecto { get; set; } = null!;

    public int? HorarioCodigo { get; set; }
    public string? HorarioDescripcion { get; set; }
    public TimeOnly? HoraEntrada { get; set; }
    public TimeOnly? HoraSalida { get; set; }
    public short? HorasJornadaMin { get; set; }
    public short? HorasTrabajadasMin { get; set; }
    public string? TipoHorario { get; set; }
    public TimeOnly? SalidaAlmuerzo { get; set; }
    public TimeOnly? RegresoAlmuerzo { get; set; }

    public int? DepartamentoId { get; set; }
    public Departamento? Departamento { get; set; }
    public int PropietarioUsuarioId { get; set; }
    public Usuario Propietario { get; set; } = null!;

    public bool Eliminado { get; set; }
    public int? LegacyId { get; set; }
    public byte[] RowVer { get; set; } = [];

    public ICollection<ProyectoPersonal> Personal { get; set; } = [];
    public ICollection<ProyectoEtapa> Etapas { get; set; } = [];
    public ICollection<ProyectoActividad> Actividades { get; set; } = [];
}
