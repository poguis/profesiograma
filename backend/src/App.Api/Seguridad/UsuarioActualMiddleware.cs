using App.Application.Seguridad;
using Microsoft.AspNetCore.Authorization;

namespace App.Api.Seguridad;

/// <summary>
/// Después de autenticar: resuelve/crea el registro dbo.Usuario (por oid y correo)
/// y llena IUsuarioActual para la auditoría y las reglas de negocio.
/// </summary>
public sealed class UsuarioActualMiddleware(RequestDelegate next)
{
    public const string MensajeSinRol = "Sin rol asignado";
    public const string DetalleSinRol = "Pida a TI que le asigne el rol Gestor o Admin en Entra ID.";

    public async Task InvokeAsync(HttpContext contexto, UsuarioActual usuarioActual, IUsuarioProvisionamiento provisionamiento)
    {
        // Endpoints anónimos no consultan dbo.Usuario; no deben escribir datos (auditoría usaría SISTEMA).
        if (contexto.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await next(contexto);
            return;
        }

        var principal = contexto.User;
        if (principal.Identity?.IsAuthenticated == true)
        {
            // Fase 7b (P1): sin rol de la app no hay acceso, y se responde ANTES del alta para no crear la fila.
            if (!principal.ObtenerRoles().Any(r => r.Equals(RolesApp.Admin, StringComparison.OrdinalIgnoreCase)
                                                   || r.Equals(RolesApp.Gestor, StringComparison.OrdinalIgnoreCase)))
            {
                await TypedResults.Problem(
                    title: MensajeSinRol,
                    detail: DetalleSinRol,
                    statusCode: StatusCodes.Status403Forbidden).ExecuteAsync(contexto);
                return;
            }

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
