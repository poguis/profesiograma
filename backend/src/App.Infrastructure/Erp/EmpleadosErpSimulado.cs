using App.Application.Empleados;
using App.Infrastructure.Persistencia.DatosPrueba;

namespace App.Infrastructure.Erp;

/// <summary>
/// Empleados del modo Simulado (SOLO Development, TAREA-26d P12): DEV001–DEV008 (idénticos a las filas del sembrador) y
/// SIM001–SIM200 ficticios repartidos entre los 7 departamentos de la tabla, sin datos sensibles reales (solo cédulas
/// FICTICIAS en la lectura con cédulas, TAREA-22-1). Resuelve el pendiente 39 en Simulado (empleados suficientes para
/// pruebas sin cruces).
/// </summary>
internal sealed class EmpleadosErpSimulado : IDescargaEmpleadosErp
{
    private static readonly IReadOnlyList<EmpleadoErp> Lista = EmpleadosPrueba.ListaSimulada();
    private static readonly CedulasEmpleados Cedulas = EmpleadosPrueba.CedulasSimuladas(); // FICTICIAS (TAREA-22-1)

    public Task<IReadOnlyList<EmpleadoErp>> DescargarAsync(CancellationToken ct) => Task.FromResult(Lista);

    public Task<(IReadOnlyList<EmpleadoErp> Registros, CedulasEmpleados Cedulas)> DescargarConCedulasAsync(CancellationToken ct) =>
        Task.FromResult((Lista, Cedulas));
}
