using App.Application.Empleados;
using App.Infrastructure.Persistencia.DatosPrueba;

namespace App.Infrastructure.Erp;

/// <summary>
/// Empleados del modo Simulado (SOLO Development, TAREA-26d P12): DEV001–DEV008 (idénticos a las filas del sembrador) y
/// SIM001–SIM200 ficticios repartidos entre los 7 departamentos de la tabla, sin datos sensibles. Resuelve el pendiente
/// 39 en Simulado (empleados suficientes para pruebas sin cruces).
/// </summary>
internal sealed class EmpleadosErpSimulado : IDescargaEmpleadosErp
{
    private static readonly IReadOnlyList<EmpleadoErp> Lista = EmpleadosPrueba.ListaSimulada();

    public Task<IReadOnlyList<EmpleadoErp>> DescargarAsync(CancellationToken ct) => Task.FromResult(Lista);
}
