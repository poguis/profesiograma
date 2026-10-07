using App.Application.Comun;

namespace App.Application.Empleados;

/// <summary>
/// Empleado del buscador (TAREA-26d: desde la API, no de la base). Clave = CodigoEkon. Sin cédula, correo ni otros datos
/// sensibles.
/// </summary>
/// <param name="Cargo">Puesto del empleado en el ERP.</param>
public sealed record EmpleadoBusquedaDto(string CodigoEkon, string NombreCompleto, string? Cargo, string? Departamento, string? Unidad);

/// <summary>
/// Respuesta del buscador: los campos de <see cref="PaginaResultado{T}"/> más AvisoErp (P4: lista anterior porque el ERP
/// no respondió; null si la lista es actual).
/// </summary>
public sealed record ResultadoBusquedaEmpleadosDto(
    IReadOnlyList<EmpleadoBusquedaDto> Items, int Pagina, int Tamano, int Total, string? AvisoErp);

/// <summary>Parámetros crudos del query string (se validan en EmpleadoConsultaServicio).</summary>
public sealed record EmpleadoBusquedaSolicitud(string? Texto, string? SoloMisDepartamentos, string? Pagina, string? Tamano);

/// <summary>Filtro validado.</summary>
/// <param name="Departamentos">Nombres de departamento/unidad permitidos; null = todos.</param>
public sealed record EmpleadoFiltro(string? Texto, IReadOnlyList<string>? Departamentos, int Pagina, int Tamano);
