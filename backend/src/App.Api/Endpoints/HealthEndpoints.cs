using App.Application.Diagnostics;
using Microsoft.Data.SqlClient;

namespace App.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/health");

        // Diagnóstico de conexión y versión. Solo disponible en Development hasta tener autenticación (Fase 5).
        group.MapGet("/db", async (IDatabaseDiagnostics diagnostics, IHostEnvironment env, CancellationToken ct) =>
        {
            if (!env.IsDevelopment())
                return Results.NotFound();

            try
            {
                var info = await diagnostics.GetInfoAsync(ct);
                return Results.Ok(info);
            }
            catch (SqlException ex)
            {
                return Results.Problem(
                    title: "No se pudo conectar a SQL Server",
                    detail: $"Error {ex.Number}: {ex.Message}",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        return app;
    }
}
