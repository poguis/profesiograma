using App.Application.Empleados;
using App.Application.Seguridad;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Endpoints;

public static class EmpleadoEndpoints
{
    public static IEndpointRouteBuilder MapEmpleadoEndpoints(this IEndpointRouteBuilder app)
    {
        // Filtros opcionales: texto, soloMisDepartamentos (true por defecto), pagina, tamano. Sin datos sensibles.
        // TAREA-26d: desde la API de empleados (caché en memoria); avisoErp si se usa la lista anterior; 503 si no hay lista.
        app.MapGet("/api/empleados", async Task<Results<Ok<ResultadoBusquedaEmpleadosDto>, ValidationProblem>> (
                [AsParameters] EmpleadoBusquedaSolicitud solicitud, EmpleadoConsultaServicio servicio, CancellationToken ct) =>
            {
                var resultado = await servicio.BuscarAsync(solicitud, ct);
                return resultado.EsValido
                    ? TypedResults.Ok(resultado.Valor!)
                    : TypedResults.ValidationProblem(resultado.Errores!, title: "Parámetros de consulta no válidos.");
            })
            .WithTags("Empleados")
            .RequireAuthorization(Politicas.Gestor);

        // TAREA-22-1 (D15): llena Empleado.Cedula de las filas guardadas sin cédula, desde la lectura fresca de la API.
        // Sin cuerpo. Respuesta y log: SOLO conteos (nunca cédulas ni códigos). ERP caído → 503 sin escribir.
        app.MapPost("/api/empleados/completar-cedulas", async Task<Ok<ResultadoCompletarCedulasDto>> (
                CompletarCedulasServicio servicio, IUsuarioActual usuario, ILoggerFactory registro, CancellationToken ct) =>
            {
                var r = await servicio.CompletarAsync(ct);
                registro.CreateLogger(nameof(EmpleadoEndpoints)).LogInformation(
                    "Completar cédulas (usuario {UsuarioId}): revisadas {Revisadas}, completadas {Completadas}, sin cédula en la API {SinCedulaEnApi}, no encontradas en la API {NoEncontradasEnApi}.",
                    usuario.UsuarioId, r.Revisadas, r.Completadas, r.SinCedulaEnApi, r.NoEncontradasEnApi);
                return TypedResults.Ok(r);
            })
            .WithTags("Empleados")
            .RequireAuthorization(Politicas.Admin);

        // TAREA-62: criterio de personal elegible (Parametro EMPLEADO_FAMILIAS_ASIGNABLES). Solo familias y conteos.
        var admin = app.MapGroup("/api/admin/empleados").WithTags("Empleados").RequireAuthorization(Politicas.Admin);

        // Familias de la lista de la API en caché (sin lista utilizable → 503 por el manejador común del ERP).
        admin.MapGet("/familias", async ([FromServices] CriterioPersonalServicio servicio, CancellationToken ct) =>
            TypedResults.Ok(await servicio.ObtenerAsync(ct)));

        // 200 { valorActual }; 400 en familias/valorAnterior; 409 si otro lo cambió (compare-and-set). Efecto inmediato.
        admin.MapPut("/familias-asignables", async Task<Results<Ok<CriterioGuardadoDto>, ValidationProblem, ProblemHttpResult>> (
            GuardarCriterioSolicitud solicitud, [FromServices] CriterioPersonalServicio servicio, IUsuarioActual usuario, ILoggerFactory registro,
            CancellationToken ct) =>
        {
            var r = await servicio.GuardarAsync(solicitud, ct);
            switch (r.Estado)
            {
                case EstadoGuardarCriterio.Guardado:
                    registro.CreateLogger(nameof(EmpleadoEndpoints)).LogInformation(
                        "Criterio de personal elegible (usuario {UsuarioId}): «{Anterior}» → «{Nuevo}».",
                        usuario.UsuarioId, solicitud.ValorAnterior, r.Guardado!.ValorActual);
                    return TypedResults.Ok(r.Guardado);
                case EstadoGuardarCriterio.Invalido:
                    return TypedResults.ValidationProblem(r.Errores!, title: "El criterio de personal no es válido.");
                default:
                    return TypedResults.Problem(title: ResultadoGuardarCriterio.MensajeConflicto, statusCode: StatusCodes.Status409Conflict);
            }
        });

        return app;
    }
}
