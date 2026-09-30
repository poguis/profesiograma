namespace App.Application.Empleados;

/// <summary>Resultado de la búsqueda de empleados. Sin cédula, correo ni otros datos sensibles.</summary>
/// <param name="Cargo">Puesto del empleado en el ERP.</param>
public sealed record EmpleadoBusquedaDto(int Id, string CodigoEkon, string NombreCompleto, string? Cargo, string? Departamento);

/// <summary>Parámetros crudos del query string (se validan en EmpleadoConsultaServicio).</summary>
public sealed record EmpleadoBusquedaSolicitud(string? Texto, string? SoloMisDepartamentos, string? Pagina, string? Tamano);

/// <summary>Filtro validado que recibe Infrastructure.</summary>
/// <param name="Departamentos">Nombres de departamento/unidad permitidos; null = todos.</param>
public sealed record EmpleadoFiltro(string? Texto, IReadOnlyList<string>? Departamentos, int Pagina, int Tamano);
