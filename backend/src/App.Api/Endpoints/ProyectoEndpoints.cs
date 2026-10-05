using App.Application.Comun;
using App.Application.Proyectos;
using App.Application.Proyectos.Crear;
using App.Application.Proyectos.Estados;
using App.Application.Proyectos.Personal;
using App.Application.Proyectos.Reactivacion;
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

        // Cambio de estado (TAREA-14): SUSPENSION y CIERRE. No visible o inexistente → 404.
        proyectos.MapPost("/{id:int}/cambio-estado/previsualizar",
            async Task<Results<Ok<CambioEstadoPrevisualizacionDto>, ValidationProblem, NotFound>> (
                int id, CambioEstadoSolicitud solicitud, CambioEstadoServicio servicio, CancellationToken ct) =>
            {
                var resultado = await servicio.PrevisualizarAsync(id, solicitud, ct);
                return resultado.Estado switch
                {
                    EstadoCambio.Previsualizado => TypedResults.Ok(resultado.Previsualizacion!),
                    EstadoCambio.Invalido => CambioEstadoInvalido(resultado),
                    _ => TypedResults.NotFound(),
                };
            });

        // Una transacción con applock. 200 { id, estado, version }; 400; 404; 409 si el estado cambió; 503 ocupado.
        proyectos.MapPost("/{id:int}/cambio-estado",
            async Task<Results<Ok<CambioEstadoRealizadoDto>, ValidationProblem, NotFound, ProblemHttpResult>> (
                int id, CambioEstadoSolicitud solicitud, CambioEstadoServicio servicio, CancellationToken ct) =>
            {
                var resultado = await servicio.AplicarAsync(id, solicitud, ct);
                return resultado.Estado switch
                {
                    EstadoCambio.Realizado => TypedResults.Ok(resultado.Realizado!),
                    EstadoCambio.Invalido => CambioEstadoInvalido(resultado),
                    EstadoCambio.Conflicto => TypedResults.Problem(
                        title: ResultadoCambioEstado.MensajeConflicto, statusCode: StatusCodes.Status409Conflict),
                    _ => TypedResults.NotFound(),
                };
            });

        // Actualización de personal (TAREA-17): datos del formulario, vista previa y registro.
        proyectos.MapGet("/{id:int}/edicion", async Task<Results<Ok<EdicionPersonalDto>, NotFound>> (
            int id, EdicionPersonalServicio servicio, CancellationToken ct) =>
            await servicio.ObtenerEdicionAsync(id, ct) is { } edicion ? TypedResults.Ok(edicion) : TypedResults.NotFound());

        proyectos.MapPost("/{id:int}/personal/previsualizar",
            async Task<Results<Ok<PrevisualizacionPersonalDto>, ValidationProblem, NotFound, ProblemHttpResult>> (
                int id, ActualizarPersonalSolicitud solicitud, EdicionPersonalServicio servicio, CancellationToken ct) =>
            {
                var resultado = await servicio.PrevisualizarAsync(id, solicitud, ct);
                return resultado.Estado switch
                {
                    EstadoEdicion.Previsualizado => TypedResults.Ok(resultado.Previsualizacion!),
                    EstadoEdicion.Invalido => PersonalInvalido(resultado),
                    EstadoEdicion.Cambiado => ProyectoCambiado(),
                    _ => TypedResults.NotFound(),
                };
            });

        // Una transacción con applock. 200 { id, version }; 400; 404; 409 (cruces con extensiones, o proyecto cambiado); 503.
        proyectos.MapPost("/{id:int}/personal",
            async Task<Results<Ok<PersonalActualizadoDto>, ValidationProblem, NotFound, ProblemHttpResult>> (
                int id, ActualizarPersonalSolicitud solicitud, EdicionPersonalServicio servicio, CancellationToken ct) =>
            {
                var resultado = await servicio.RegistrarAsync(id, solicitud, ct);
                return resultado.Estado switch
                {
                    EstadoEdicion.Realizado => TypedResults.Ok(resultado.Realizado!),
                    EstadoEdicion.Invalido => PersonalInvalido(resultado),
                    EstadoEdicion.Cambiado => ProyectoCambiado(),
                    EstadoEdicion.ConCruces => TypedResults.Problem(
                        title: ResultadoEdicionPersonal.MensajeCruces,
                        detail: "No se guardó el personal. Revise los cruces y ajuste el personal o las fechas.",
                        statusCode: StatusCodes.Status409Conflict,
                        extensions: new Dictionary<string, object?>
                        {
                            ["cruces"] = resultado.Previsualizacion!.Cruces,
                            ["resumen"] = resultado.Previsualizacion.Resumen,
                        }),
                    _ => TypedResults.NotFound(),
                };
            });

        // ---------------------------------------------------------- reactivación (TAREA-17b)

        proyectos.MapGet("/{id:int}/reactivacion", async Task<Results<Ok<ReactivacionDto>, NotFound>> (
            int id, ReactivacionServicio servicio, CancellationToken ct) =>
            await servicio.ObtenerAsync(id, ct) is { } reactivacion ? TypedResults.Ok(reactivacion) : TypedResults.NotFound());

        // Vista previa: cronograma + cruces + actividad que se creará. No guarda nada (200 aunque haya cruces).
        proyectos.MapPost("/{id:int}/reactivacion/previsualizar",
            async Task<Results<Ok<PrevisualizacionReactivacionDto>, ValidationProblem, NotFound, ProblemHttpResult>> (
                int id, ReactivarProyectoSolicitud solicitud, ReactivacionServicio servicio, CancellationToken ct) =>
            {
                var resultado = await servicio.PrevisualizarAsync(id, solicitud, ct);
                return resultado.Estado switch
                {
                    EstadoEdicion.Previsualizado => TypedResults.Ok(resultado.Previsualizacion!),
                    EstadoEdicion.Invalido => ReactivacionInvalida(resultado),
                    EstadoEdicion.Cambiado => ProyectoCambiado(),
                    _ => TypedResults.NotFound(),
                };
            });

        // Una transacción con applock. 200 { id, estado, version }; 400; 404; 409 (cruces con extensiones, o proyecto cambiado); 503.
        proyectos.MapPost("/{id:int}/reactivacion",
            async Task<Results<Ok<ProyectoReactivadoDto>, ValidationProblem, NotFound, ProblemHttpResult>> (
                int id, ReactivarProyectoSolicitud solicitud, ReactivacionServicio servicio, CancellationToken ct) =>
            {
                var resultado = await servicio.RegistrarAsync(id, solicitud, ct);
                return resultado.Estado switch
                {
                    EstadoEdicion.Realizado => TypedResults.Ok(resultado.Realizado!),
                    EstadoEdicion.Invalido => ReactivacionInvalida(resultado),
                    EstadoEdicion.Cambiado => ProyectoCambiado(),
                    EstadoEdicion.ConCruces => TypedResults.Problem(
                        title: ResultadoEdicionPersonal.MensajeCruces,
                        detail: "No se reactivó el proyecto. Revise los cruces y ajuste el personal o las fechas.",
                        statusCode: StatusCodes.Status409Conflict,
                        extensions: new Dictionary<string, object?>
                        {
                            ["cruces"] = resultado.Previsualizacion!.Cruces,
                            ["resumen"] = resultado.Previsualizacion.Resumen,
                        }),
                    _ => TypedResults.NotFound(),
                };
            });

        return app;
    }

    private static ValidationProblem ReactivacionInvalida(ResultadoReactivacion resultado) =>
        TypedResults.ValidationProblem(resultado.Errores!, title: "Los datos de la reactivación no son válidos.");

    private static ValidationProblem PersonalInvalido(ResultadoEdicionPersonal resultado) =>
        TypedResults.ValidationProblem(resultado.Errores!, title: "Los datos del personal no son válidos.");

    private static ProblemHttpResult ProyectoCambiado() =>
        TypedResults.Problem(title: ResultadoEdicionPersonal.MensajeCambiado, statusCode: StatusCodes.Status409Conflict);

    private static ValidationProblem CambioEstadoInvalido(ResultadoCambioEstado resultado) =>
        TypedResults.ValidationProblem(resultado.Errores!, title: "Los datos del cambio de estado no son válidos.");

    private static ValidationProblem ValidacionFallida(ResultadoCrearProyecto resultado) =>
        TypedResults.ValidationProblem(resultado.Errores!, title: "Los datos del proyecto no son válidos.");
}
