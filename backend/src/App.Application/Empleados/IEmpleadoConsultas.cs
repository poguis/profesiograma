using App.Application.Comun;

namespace App.Application.Empleados;

/// <summary>Consultas de solo lectura sobre la caché local de empleados. No aplica reglas de negocio.</summary>
public interface IEmpleadoConsultas
{
    /// <summary>Empleados activos (EstadoErp = "A"), filtrados y paginados en SQL.</summary>
    Task<PaginaResultado<EmpleadoBusquedaDto>> BuscarAsync(EmpleadoFiltro filtro, CancellationToken ct);

    /// <summary>Nombres de los departamentos activos asignados al usuario en UsuarioDepartamento.</summary>
    Task<IReadOnlyList<string>> ObtenerDepartamentosDeUsuarioAsync(int usuarioId, CancellationToken ct);
}
