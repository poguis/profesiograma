namespace App.Application.Empleados;

/// <summary>Consultas del buscador de empleados. No aplica reglas de negocio.</summary>
public interface IEmpleadoConsultas
{
    /// <summary>
    /// Empleados activos de la API (TAREA-26d: IFuenteEmpleadosErp, caché en memoria), filtrados y paginados en memoria.
    /// </summary>
    /// <exception cref="Erp.ErpNoDisponibleException">La API no respondió y no hay lista anterior utilizable (503).</exception>
    Task<ResultadoBusquedaEmpleadosDto> BuscarAsync(EmpleadoFiltro filtro, CancellationToken ct);

    /// <summary>Nombres de los departamentos activos asignados al usuario en UsuarioDepartamento (base de datos).</summary>
    Task<IReadOnlyList<string>> ObtenerDepartamentosDeUsuarioAsync(int usuarioId, CancellationToken ct);
}
