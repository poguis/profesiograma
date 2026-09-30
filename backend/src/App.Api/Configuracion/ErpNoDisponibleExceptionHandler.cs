using App.Application.Erp;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Configuracion;

/// <summary>ErpNoDisponibleException → 503 ProblemDetails "Servicio ERP no disponible" (sin detalles internos).</summary>
public sealed class ErpNoDisponibleExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excepcion, CancellationToken ct)
    {
        if (excepcion is not ErpNoDisponibleException)
        {
            return false;
        }

        contexto.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            Exception = excepcion,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Servicio ERP no disponible",
                Detail = "No se pudo consultar el ERP. Intente nuevamente en unos minutos.",
            },
        });
    }
}
