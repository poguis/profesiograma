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
    public string Email { get; set; } = string.Empty;
    public string NombreMostrar { get; set; } = string.Empty;
    public int? EmpleadoId { get; set; }
    public Empleado? Empleado { get; set; }
    public bool EsSistema { get; set; }
    public bool Activo { get; set; } = true;

    public ICollection<UsuarioDepartamento> Departamentos { get; set; } = [];
}
