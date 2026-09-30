using App.Application.Seguridad;

namespace App.Api.Endpoints;

public static class UsuarioEndpoints
{
    public static IEndpointRouteBuilder MapUsuarioEndpoints(this IEndpointRouteBuilder app)
    {
        // Cualquier usuario autenticado: útil para probar DevAuth y, luego, Entra ID.
        app.MapGet("/api/usuarios/me", (IUsuarioActual usuario) =>
                TypedResults.Ok(new UsuarioActualDto(usuario.UsuarioId!.Value, usuario.Email!, usuario.NombreMostrar, usuario.Roles)))
            .WithTags("Usuarios")
            .RequireAuthorization();

        return app;
    }
}
