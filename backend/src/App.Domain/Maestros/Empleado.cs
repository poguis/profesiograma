using App.Domain.Comun;

namespace App.Domain.Maestros;

/// <summary>
/// Personas ASIGNADAS a algún proyecto (TAREA-26d, opción C; antes: caché completa de la API). La API EvolutionEmployee
/// es la única fuente de los empleados: la fila se crea o se refresca con la "alta puntual" al asignar a la persona
/// (crear proyecto, actualizar personal, reactivar) o, en la Fase 3, al migrar sus asignaciones de SharePoint.
/// Solo datos laborales: NO guardar salario, BPR, fecha de nacimiento, teléfono, correo personal ni dirección (C31).
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
    /// <summary>Estado en la API en la última alta puntual ("A" = activo). Si sigue activo HOY lo dice la API, no esta columna.</summary>
    public string? EstadoErp { get; set; }
    public bool EsOrigenLegado { get; set; }
    /// <summary>Fecha (UTC) de la última copia de los datos desde la API (alta puntual). Nombre heredado del modelo de la Fase 2.</summary>
    public DateTime? FechaSincronizacion { get; set; }
}
