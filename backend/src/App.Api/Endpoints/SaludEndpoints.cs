using App.Application.Diagnostics;

namespace App.Api.Endpoints;

/// <summary>
/// TAREA-7d-1 (D6): salud anónima para IIS y la prueba de humo, en todos los entornos. Sin detalles (ni servidor, ni base,
/// ni excepciones). /api/health/db sigue siendo solo de Development.
/// </summary>
public static class SaludEndpoints
{
    public static IEndpointRouteBuilder MapSaludEndpoints(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/salud").AllowAnonymous();

        // El proceso responde; no toca la base.
        grupo.MapGet("/vivo", () => Results.Ok(new { estado = "vivo" }));

        // La base responde a un SELECT 1 (503 sin cuerpo detallado si no).
        grupo.MapGet("/listo", async (IDatabaseDiagnostics diagnostico, CancellationToken ct) =>
            await diagnostico.EstaDisponibleAsync(ct)
                ? Results.Ok(new { estado = "listo" })
                : Results.StatusCode(StatusCodes.Status503ServiceUnavailable));

        return app;
    }
}
