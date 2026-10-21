using App.Application.Comun;
using App.Application.Novedades;
using App.Application.Seguridad;
using Microsoft.AspNetCore.Http.HttpResults;

namespace App.Api.Endpoints;

/// <summary>Novedades (TAREA-23-1): listado, opciones, detalle y creación. Editar y anular: 23-2.</summary>
public static class NovedadEndpoints
{
    public static IEndpointRouteBuilder MapNovedadEndpoints(this IEndpointRouteBuilder app)
    {
        var novedades = app.MapGroup("/api/novedades")
            .WithTags("Novedades")
            .RequireAuthorization(Politicas.Gestor);

        // Filtros: aplicacion, tipo, origen, texto, desde, hasta (solapamiento), incluirAnuladas, pagina, tamano (≤ 100).
        novedades.MapGet("/", async Task<Results<Ok<PaginaResultado<NovedadResumenDto>>, ValidationProblem>> (
            [AsParameters] NovedadListadoSolicitud solicitud, NovedadConsultaServicio servicio, CancellationToken ct) =>
        {
            var resultado = await servicio.ListarAsync(solicitud, ct);
            return resultado.EsValido
                ? TypedResults.Ok(resultado.Valor!)
                : TypedResults.ValidationProblem(resultado.Errores!, title: "Parámetros de consulta no válidos.");
        });

        novedades.MapGet("/opciones-formulario", async (OpcionesFormularioNovedad servicio, CancellationToken ct) =>
            TypedResults.Ok(await servicio.ObtenerAsync(ct)));

        // Inexistente o no visible → 404 (nunca 403).
        novedades.MapGet("/{id:int}", async Task<Results<Ok<NovedadDetalleDto>, NotFound>> (
            int id, NovedadConsultaServicio servicio, CancellationToken ct) =>
            await servicio.ObtenerDetalleAsync(id, ct) is { } detalle ? TypedResults.Ok(detalle) : TypedResults.NotFound());

        // 201 + Location; 400 por campo; 403 GENERAL sin permiso; 409 duplicado (N7) o alta concurrente; 503 empleados,
        // registro ocupado o base caída (manejadores comunes).
        novedades.MapPost("/", async Task<Results<Created<NovedadCreadaDto>, ValidationProblem, ProblemHttpResult>> (
            CrearNovedadSolicitud solicitud, NovedadEscrituraServicio servicio, CancellationToken ct) =>
        {
            var resultado = await servicio.CrearAsync(solicitud, ct);
            return resultado.Estado switch
            {
                EstadoCrearNovedad.Creado => TypedResults.Created($"/api/novedades/{resultado.Creado!.Id}", resultado.Creado),
                EstadoCrearNovedad.Invalido => TypedResults.ValidationProblem(resultado.Errores!, title: "Los datos de la novedad no son válidos."),
                EstadoCrearNovedad.Prohibido => TypedResults.Problem(
                    title: ResultadoCrearNovedad.MensajeProhibido, statusCode: StatusCodes.Status403Forbidden),
                EstadoCrearNovedad.Duplicado => TypedResults.Problem(
                    title: ResultadoCrearNovedad.MensajeDuplicado, statusCode: StatusCodes.Status409Conflict,
                    // R6: el código de la existente solo si quien registra la ve.
                    extensions: resultado.CodigoDuplicado is { } codigo ? new Dictionary<string, object?> { ["codigo"] = codigo } : null),
                _ => TypedResults.Problem(title: ResultadoCrearNovedad.MensajeCambiado, statusCode: StatusCodes.Status409Conflict),
            };
        });

        return app;
    }
}
