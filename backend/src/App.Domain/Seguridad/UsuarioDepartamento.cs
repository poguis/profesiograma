using App.Domain.Comun;

namespace App.Domain.Seguridad;

/// <summary>Usuarios autorizados por departamento (reemplaza el JSON de la fila 666).</summary>
public class UsuarioDepartamento : EntidadAuditable
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public int DepartamentoId { get; set; }
    public Departamento Departamento { get; set; } = null!;
    public bool Notificado { get; set; }
    public DateTime? FechaNotificacion { get; set; }
    public bool Activo { get; set; } = true;
}
