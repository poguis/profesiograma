using App.Application.Erp;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Configuracion;

/// <summary>
/// ErpNoDisponibleException → 503 ProblemDetails "Servicio ERP no disponible" (sin detalles internos). TAREA-26d: si la
/// operación es la de empleados, "Servicio de empleados no disponible".
/// </summary>
public sealed class ErpNoDisponibleExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    /// <summary>Operación de ErpNoDisponibleException de la API de empleados (FuenteEmpleadosErpHttp, CacheEmpleadosErp).</summary>
    public const string OperacionEmpleados = "empleados";

    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excepcion, CancellationToken ct)
    {
        if (excepcion is not ErpNoDisponibleException erp)
        {
            return false;
        }

        var empleados = string.Equals(erp.Operacion, OperacionEmpleados, StringComparison.Ordinal);

        contexto.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            Exception = excepcion,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = empleados ? "Servicio de empleados no disponible" : "Servicio ERP no disponible",
                Detail = empleados
                    ? "No se pudo consultar los empleados en el ERP. Intente nuevamente en unos minutos."
                    : "No se pudo consultar el ERP. Intente nuevamente en unos minutos.",
            },
        });
    }
}
