using App.Application.Proyectos.Crear;
using App.Domain.Maestros;

namespace App.Application.Empleados;

// Acción Admin «completar cédulas» (TAREA-22-1, D15 / Z11): llena Empleado.Cedula de las filas guardadas que la tienen en
// NULL (la alta puntual solo la escribe al asignar). Ver docs/fases/FASE_4_Empleados_API.md §6.1.

/// <summary>Solo conteos: nunca cédulas ni códigos.</summary>
public sealed record ResultadoCompletarCedulasDto(int Revisadas, int Completadas, int SinCedulaEnApi, int NoEncontradasEnApi);

public interface ICompletarCedulasRepositorio
{
    /// <summary>Filas de Empleado con Cedula NULL, CON seguimiento (la auditoría llena ModificadoPor). Dentro de la transacción.</summary>
    Task<IReadOnlyList<Empleado>> ObtenerSinCedulaAsync(CancellationToken ct);

    Task GuardarAsync(CancellationToken ct);
}

public static class CompletarCedulas
{
    /// <summary>
    /// Lógica pura: completa SOLO las filas sin cédula que la API trae con una cédula no vacía. Nunca sobrescribe una
    /// cédula guardada (idempotente). No encontrada = el código no está entre los activos de la API.
    /// </summary>
    public static ResultadoCompletarCedulasDto Aplicar(IEnumerable<Empleado> sinCedula, ListaEmpleadosErp api)
    {
        ArgumentNullException.ThrowIfNull(sinCedula);
        ArgumentNullException.ThrowIfNull(api);
        int revisadas = 0, completadas = 0, sinCedulaEnApi = 0, noEncontradas = 0;
        foreach (var e in sinCedula.Where(e => e.Cedula is null))
        {
            revisadas++;
            if (!api.PorCodigo.ContainsKey(e.CodigoEkon))
            {
                noEncontradas++;
            }
            else if (api.Cedulas.De(e.CodigoEkon) is { } cedula)
            {
                e.Cedula = cedula;
                completadas++;
            }
            else
            {
                sinCedulaEnApi++;
            }
        }

        return new ResultadoCompletarCedulasDto(revisadas, completadas, sinCedulaEnApi, noEncontradas);
    }
}

/// <summary>POST /api/empleados/completar-cedulas (Politica.Admin).</summary>
public sealed class CompletarCedulasServicio(
    IFuenteEmpleadosErp fuente, ITransaccionAsignaciones transaccion, ICompletarCedulasRepositorio repositorio)
{
    /// <exception cref="Erp.ErpNoDisponibleException">La API de empleados no respondió (503, sin abrir la transacción).</exception>
    public async Task<ResultadoCompletarCedulasDto> CompletarAsync(CancellationToken ct)
    {
        // P5: lectura FRESCA (la única con cédulas), fuera de la transacción: si el ERP falla, nada se abre ni se escribe.
        var api = await fuente.ObtenerActivosFrescosAsync(ct);

        // Dentro del applock de asignaciones: todas las escrituras de Empleado pasan por ahí. Así no se cruza con una alta
        // puntual simultánea, que podría crear o refrescar la misma fila.
        return await transaccion.EjecutarAsync(async ctTx =>
        {
            var resultado = CompletarCedulas.Aplicar(await repositorio.ObtenerSinCedulaAsync(ctTx), api);
            if (resultado.Completadas > 0)
            {
                await repositorio.GuardarAsync(ctTx);
            }

            return resultado;
        }, _ => true, ct);
    }
}
