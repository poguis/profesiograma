using System.Security.Claims;

namespace App.Api.Seguridad;

/// <summary>Nombres de claims de Entra ID (tokens v2).</summary>
public static class ClaimNames
{
    public const string ObjectId = "oid";
    public const string ObjectIdLargo = "http://schemas.microsoft.com/identity/claims/objectidentifier";
    public const string PreferredUsername = "preferred_username";
    public const string Name = "name";
    public const string Roles = "roles";
}

public static class ClaimsPrincipalExtensions
{
    public static Guid? ObtenerObjectId(this ClaimsPrincipal usuario)
    {
        var valor = usuario.FindFirst(ClaimNames.ObjectId)?.Value ?? usuario.FindFirst(ClaimNames.ObjectIdLargo)?.Value;
        return Guid.TryParse(valor, out var oid) ? oid : null;
    }

    public static string? ObtenerEmail(this ClaimsPrincipal usuario)
        => usuario.FindFirst(ClaimNames.PreferredUsername)?.Value
           ?? usuario.FindFirst(ClaimTypes.Email)?.Value
           ?? usuario.FindFirst("email")?.Value
           ?? usuario.FindFirst(ClaimTypes.Upn)?.Value
           ?? usuario.FindFirst("upn")?.Value;

    public static string? ObtenerNombre(this ClaimsPrincipal usuario)
        => usuario.FindFirst(ClaimNames.Name)?.Value ?? usuario.Identity?.Name;

    public static IReadOnlyCollection<string> ObtenerRoles(this ClaimsPrincipal usuario)
        => usuario.FindAll(ClaimNames.Roles)
            .Concat(usuario.FindAll(ClaimTypes.Role))
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
