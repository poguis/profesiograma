using App.Application.Seguridad;
using Microsoft.AspNetCore.Authorization;

namespace App.Api.Seguridad;

/// <summary>
/// Después de autenticar: resuelve/crea el registro dbo.Usuario (por oid y correo)
/// y llena IUsuarioActual para la auditoría y las reglas de negocio.
/// </summary>
public sealed class UsuarioActualMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext contexto, UsuarioActual usuarioActual, IUsuarioProvisionamiento provisionamiento)
    {
        var principal = contexto.User;
        var autenticado = principal.Identity?.IsAuthenticated == true;

        // Endpoint anónimo (p. ej. /api/health/db) sin usuario autenticado: no se consulta dbo.Usuario.
        // Si el usuario sí está autenticado, se aprovisiona normalmente.
        if (!autenticado && contexto.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await next(contexto);
            return;
        }

        if (autenticado)
        {
            var email = principal.ObtenerEmail();
            if (string.IsNullOrWhiteSpace(email))
            {
                await TypedResults.Problem(
                    title: "Token sin correo",
                    detail: "El token no contiene preferred_username/email.",
                    statusCode: StatusCodes.Status403Forbidden).ExecuteAsync(contexto);
                return;
            }

            var nombre = principal.ObtenerNombre();
            var resuelto = await provisionamiento.ResolverAsync(principal.ObtenerObjectId(), email, nombre ?? email, contexto.RequestAborted);
            if (!resuelto.Habilitado)
            {
                await TypedResults.Problem(
                    title: "Usuario no habilitado",
                    detail: resuelto.Motivo,
                    statusCode: StatusCodes.Status403Forbidden).ExecuteAsync(contexto);
                return;
            }

            usuarioActual.Establecer(resuelto.UsuarioId, email.Trim().ToLowerInvariant(), nombre, principal.ObtenerRoles());
        }

        await next(contexto);
    }
}
