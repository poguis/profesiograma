using App.Application.Erp;
using App.Application.Seguridad;
using Microsoft.AspNetCore.Http.HttpResults;

namespace App.Api.Endpoints;

/// <summary>Catálogos del ERP para crear proyectos. Error del ERP → 503 (ErpNoDisponibleExceptionHandler).</summary>
public static class ErpEndpoints
{
    public static IEndpointRouteBuilder MapErpEndpoints(this IEndpointRouteBuilder app)
    {
        var erp = app.MapGroup("/api/erp")
            .WithTags("ERP")
            .RequireAuthorization(Politicas.Gestor);

        erp.MapGet("/companias", async (CatalogoErpConsultaServicio servicio, CancellationToken ct) =>
            TypedResults.Ok(await servicio.ListarCompaniasAsync(ct)));

        erp.MapGet("/companias/{companiaId:int}/proyectos", async Task<Results<Ok<IReadOnlyList<ProyectoErp>>, ProblemHttpResult>> (
            int companiaId, CatalogoErpConsultaServicio servicio, CancellationToken ct) =>
            Responder(await servicio.ListarProyectosAsync(companiaId, ct)));

        erp.MapGet("/companias/{companiaId:int}/dimensiones", async Task<Results<Ok<IReadOnlyList<DimensionErp>>, ProblemHttpResult>> (
            int companiaId, CatalogoErpConsultaServicio servicio, CancellationToken ct) =>
            Responder(await servicio.ListarDimensionesAsync(companiaId, ct)));

        erp.MapGet("/companias/{companiaId:int}/proyectos/{proyectoErpId}/actividades",
            async Task<Results<Ok<IReadOnlyList<ActividadErp>>, ProblemHttpResult>> (
                int companiaId, string proyectoErpId, CatalogoErpConsultaServicio servicio, CancellationToken ct) =>
                Responder(await servicio.ListarActividadesAsync(companiaId, proyectoErpId, ct)));

        erp.MapGet("/horarios", async (CatalogoErpConsultaServicio servicio, CancellationToken ct) =>
            TypedResults.Ok(await servicio.ListarHorariosAsync(ct)));

        return app;
    }

    private static Results<Ok<T>, ProblemHttpResult> Responder<T>(ResultadoErp<T> resultado) =>
        resultado.NoEncontrado is null
            ? TypedResults.Ok(resultado.Valor!)
            : TypedResults.Problem(title: resultado.NoEncontrado, statusCode: StatusCodes.Status404NotFound);
}
