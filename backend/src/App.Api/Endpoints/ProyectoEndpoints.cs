using App.Application.Comun;
using App.Application.Proyectos;
using App.Application.Proyectos.Crear;
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

        // Apoyo del formulario "Nuevo proyecto": departamentos del usuario, opciones de almuerzo y límites.
        proyectos.MapGet("/opciones-formulario", async (OpcionesFormularioProyectoServicio servicio, CancellationToken ct) =>
            TypedResults.Ok(await servicio.ObtenerAsync(ct)));

        // Inexistente o no visible → 404 (nunca 403). La restricción :int evita que "opciones-formulario" se tome como id.
        proyectos.MapGet("/{id:int}", async Task<Results<Ok<ProyectoDetalleDto>, NotFound>> (
            int id, ProyectoConsultaServicio servicio, CancellationToken ct) =>
        {
            var detalle = await servicio.ObtenerDetalleAsync(id, ct);
            return detalle is null ? TypedResults.NotFound() : TypedResults.Ok(detalle);
        });

        // Vista previa: cronograma + cruces. No guarda nada (200 aunque haya cruces).
        proyectos.MapPost("/previsualizar", async Task<Results<Ok<PrevisualizacionDto>, ValidationProblem>> (
            CrearProyectoSolicitud solicitud, CrearProyectoServicio servicio, CancellationToken ct) =>
        {
            var resultado = await servicio.PrevisualizarAsync(solicitud, ct);
            return resultado.Estado == EstadoCrearProyecto.Invalido
                ? ValidacionFallida(resultado)
                : TypedResults.Ok(resultado.Previsualizacion!);
        });

        // Registro (una transacción con applock). 201 + Location; 400; 409 con cruces; 503 ERP u ocupado.
        proyectos.MapPost("/", async Task<Results<Created<ProyectoCreadoDto>, ValidationProblem, ProblemHttpResult>> (
            CrearProyectoSolicitud solicitud, CrearProyectoServicio servicio, CancellationToken ct) =>
        {
            var resultado = await servicio.RegistrarAsync(solicitud, ct);
            return resultado.Estado switch
            {
                EstadoCrearProyecto.Creado => TypedResults.Created($"/api/proyectos/{resultado.Creado!.Id}", resultado.Creado),
                EstadoCrearProyecto.Invalido => ValidacionFallida(resultado),
                _ => TypedResults.Problem(
                    title: "El proyecto tiene cruces de asignación.",
                    detail: "No se registró el proyecto. Revise los cruces y ajuste el personal o las fechas.",
                    statusCode: StatusCodes.Status409Conflict,
                    extensions: new Dictionary<string, object?>
                    {
                        // Del proyecto existente solo se exponen código, nombre y estado (CruceDto).
                        ["cruces"] = resultado.Previsualizacion!.Cruces,
                        ["resumen"] = resultado.Previsualizacion.Resumen,
                    }),
            };
        });

        return app;
    }

    private static ValidationProblem ValidacionFallida(ResultadoCrearProyecto resultado) =>
        TypedResults.ValidationProblem(resultado.Errores!, title: "Los datos del proyecto no son válidos.");
}
