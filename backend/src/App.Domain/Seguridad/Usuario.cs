using App.Domain.Comun;
using App.Domain.Maestros;

namespace App.Domain.Seguridad;

/// <summary>Identidad Entra ID que usa la aplicación (no es el maestro de empleados).</summary>
public class Usuario : EntidadAuditable
{
    /// <summary>Usuario técnico para cargas, jobs y auditoría del sistema.</summary>
    public const int IdSistema = 1;

    public int Id { get; set; }
    public Guid? EntraObjectId { get; set; }
    /// <summary>Correo (precarga del Admin y avisos). En SEDEMI suele ser distinto del UPN.</summary>
    public string Email { get; set; } = string.Empty;
    /// <summary>TAREA-7b-2: UPN de Entra ID (claim preferred_username), en minúsculas; se completa en el primer ingreso.</summary>
    public string? Upn { get; set; }
    public string NombreMostrar { get; set; } = string.Empty;
    public int? EmpleadoId { get; set; }
    public Empleado? Empleado { get; set; }
    public bool EsSistema { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<UsuarioDepartamento> Departamentos { get; set; } = [];
}
