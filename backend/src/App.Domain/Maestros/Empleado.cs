using App.Domain.Comun;

namespace App.Domain.Maestros;

/// <summary>
/// Caché de la API EvolutionEmployee. Solo datos laborales: NO guardar salario, BPR,
/// fecha de nacimiento, teléfono, correo personal ni dirección (C31).
/// </summary>
public class Empleado : EntidadAuditable
{
    public int Id { get; set; }
    public string CodigoEkon { get; set; } = string.Empty;
    public string? Cedula { get; set; }
    public string NombreCompleto { get; set; } = string.Empty;
    public string? Apellidos { get; set; }
    public string? Nombres { get; set; }
    public string? CorreoEmpresa { get; set; }
    public string? CodEmpresa { get; set; }
    public string? Empresa { get; set; }
    public string? CodPuesto { get; set; }
    public string? Puesto { get; set; }
    public string? CodDepartamento { get; set; }
    public string? Departamento { get; set; }
    public string? CodUnidad { get; set; }
    public string? Unidad { get; set; }
    public string? CodArea { get; set; }
    public string? Area { get; set; }
    public string? CodSeccion { get; set; }
    public string? Seccion { get; set; }
    public string? FamiliaPuesto { get; set; }
    public string? EstadoErp { get; set; }
    public bool EsOrigenLegado { get; set; }
    public DateTime? FechaSincronizacion { get; set; }
}
