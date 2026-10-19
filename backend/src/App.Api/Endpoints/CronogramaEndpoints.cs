using App.Application.Comun;
using App.Application.Cronograma;
using App.Application.Seguridad;
using Microsoft.AspNetCore.Http.HttpResults;

namespace App.Api.Endpoints;

/// <summary>
/// Cronograma (TAREA-20, docs/fases/FASE_5_Cronograma.md). Solo lectura. Los parámetros llegan como texto y los valida el
/// servicio: un valor ilegible responde 400 en su campo (nunca un error de enlace).
/// </summary>
public static class CronogramaEndpoints
{
    public static IEndpointRouteBuilder MapCronogramaEndpoints(this IEndpointRouteBuilder app)
    {
        // desde, hasta (yyyy-MM-dd), proyectoId, departamentoId y codigoEkon, todos opcionales.
        app.MapGet("/api/cronograma", async Task<Results<Ok<CronogramaDto>, ValidationProblem>> (
                [AsParameters] CronogramaSolicitud solicitud, CronogramaConsultaServicio servicio, CancellationToken ct) =>
                Responder(await servicio.ConsultarAsync(solicitud, ct)))
            .WithTags("Cronograma")
            .RequireAuthorization(Politicas.Gestor);

        // Solo los días de ese proyecto. Inexistente, eliminado o no visible → 404 (nunca 403).
        app.MapGet("/api/proyectos/{id:int}/cronograma", async Task<Results<Ok<CronogramaDto>, ValidationProblem, NotFound>> (
                int id, [AsParameters] ProyectoCronogramaSolicitud solicitud, CronogramaConsultaServicio servicio, CancellationToken ct) =>
            {
                var resultado = await servicio.ConsultarProyectoAsync(id, solicitud, ct);
                if (resultado is null)
                {
                    return TypedResults.NotFound();
                }

                return resultado.EsValido
                    ? TypedResults.Ok(resultado.Valor!)
                    : TypedResults.ValidationProblem(resultado.Errores!, title: TituloInvalido);
            })
            .WithTags("Cronograma")
            .RequireAuthorization(Politicas.Gestor);

        return app;
    }

    private static Results<Ok<CronogramaDto>, ValidationProblem> Responder(ResultadoConsulta<CronogramaDto> resultado) =>
        resultado.EsValido
            ? TypedResults.Ok(resultado.Valor!)
            : TypedResults.ValidationProblem(resultado.Errores!, title: TituloInvalido);

    private const string TituloInvalido = "Parámetros de consulta no válidos.";
}
