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

        return app;
    }
}
