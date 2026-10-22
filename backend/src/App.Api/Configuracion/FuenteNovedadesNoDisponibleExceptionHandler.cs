using App.Application.Novedades.Sincronizacion;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Configuracion;

/// <summary>
/// TAREA-23-3: FuenteNovedadesNoDisponibleException → 503 ProblemDetails «Fuente de novedades externas no configurada» /
/// «…no disponible». El detalle no lleva rutas ni datos personales.
/// </summary>
public sealed class FuenteNovedadesNoDisponibleExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excepcion, CancellationToken ct)
    {
        if (excepcion is not FuenteNovedadesNoDisponibleException fuente)
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
                Title = fuente.Titulo,
                Detail = fuente.Detalle,
            },
        });
    }
}
