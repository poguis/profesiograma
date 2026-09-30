using App.Application.Catalogos;
using App.Application.Seguridad;

namespace App.Api.Endpoints;

public static class CatalogoEndpoints
{
    public static IEndpointRouteBuilder MapCatalogoEndpoints(this IEndpointRouteBuilder app)
    {
        var catalogos = app.MapGroup("/api/catalogos")
            .WithTags("Catálogos")
            .RequireAuthorization(Politicas.Gestor);

        catalogos.MapGet("/", async (ICatalogoConsultas consultas, CancellationToken ct) =>
            TypedResults.Ok(await consultas.ObtenerTodosAsync(ct)));

        var admin = app.MapGroup("/api/admin")
            .WithTags("Administración")
            .RequireAuthorization(Politicas.Admin);

        admin.MapGet("/parametros", async (ICatalogoConsultas consultas, CancellationToken ct) =>
            TypedResults.Ok(await consultas.ObtenerParametrosAsync(ct)));

        return app;
    }
}
