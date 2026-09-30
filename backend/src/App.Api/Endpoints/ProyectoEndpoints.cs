using App.Application.Comun;
using App.Application.Proyectos;
using App.Application.Seguridad;
using Microsoft.AspNetCore.Http.HttpResults;

namespace App.Api.Endpoints;

public static class ProyectoEndpoints
{
    public static IEndpointRouteBuilder MapProyectoEndpoints(this IEndpointRouteBuilder app)
    {
        var proyectos = app.MapGroup("/api/proyectos")
            .WithTags("Proyectos")
            .RequireAuthorization(Politicas.Gestor);

        // Filtros opcionales: estado, grupo, texto, desde, hasta (yyyy-MM-dd), pagina, tamano.
        proyectos.MapGet("/", async Task<Results<Ok<PaginaResultado<ProyectoResumenDto>>, ValidationProblem>> (
            [AsParameters] ProyectoListadoSolicitud solicitud, ProyectoConsultaServicio servicio, CancellationToken ct) =>
        {
            var resultado = await servicio.ListarAsync(solicitud, ct);
            return resultado.EsValido
                ? TypedResults.Ok(resultado.Valor!)
                : TypedResults.ValidationProblem(resultado.Errores!, title: "Parámetros de consulta no válidos.");
        });

        // Inexistente o no visible → 404 (nunca 403).
        proyectos.MapGet("/{id:int}", async Task<Results<Ok<ProyectoDetalleDto>, NotFound>> (
            int id, ProyectoConsultaServicio servicio, CancellationToken ct) =>
        {
            var detalle = await servicio.ObtenerDetalleAsync(id, ct);
            return detalle is null ? TypedResults.NotFound() : TypedResults.Ok(detalle);
        });

        return app;
    }
}
