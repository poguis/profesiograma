using App.Application.Empleados;
using App.Application.Seguridad;
using Microsoft.AspNetCore.Http.HttpResults;

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

        return app;
    }
}
