using App.Application.Proyectos.Crear;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Configuracion;

/// <summary>RegistroOcupadoException (no se obtuvo el applock de asignaciones a tiempo) → 503 ProblemDetails.</summary>
public sealed class RegistroOcupadoExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excepcion, CancellationToken ct)
    {
        if (excepcion is not RegistroOcupadoException)
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
                Title = "Registro ocupado",
                Detail = "Hay otro registro de asignaciones en curso. Intente nuevamente en unos segundos.",
            },
        });
    }
}
