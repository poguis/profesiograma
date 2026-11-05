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
    public const string Scope = "scp";
    public const string Email = "email";
}

/// <summary>Scope delegado que el SPA pide para la API (Fase 7b): lo exigen todas las políticas.</summary>
public static class ScopesApi
{
    public const string AccederComoUsuario = "access_as_user";
}

public static class ClaimsPrincipalExtensions
{
    public static Guid? ObtenerObjectId(this ClaimsPrincipal usuario)
    {
        var valor = usuario.FindFirst(ClaimNames.ObjectId)?.Value ?? usuario.FindFirst(ClaimNames.ObjectIdLargo)?.Value;
        return Guid.TryParse(valor, out var oid) ? oid : null;
    }

    /// <summary>TAREA-7b-2: UPN = preferred_username (respaldo: upn). En SEDEMI suele ser distinto del correo.</summary>
    public static string? ObtenerUpn(this ClaimsPrincipal usuario)
        => Valor(usuario, ClaimNames.PreferredUsername) ?? Valor(usuario, ClaimTypes.Upn) ?? Valor(usuario, "upn");

    /// <summary>Correo = claim opcional email (respaldo: el UPN si el token no lo trae).</summary>
    public static string? ObtenerCorreo(this ClaimsPrincipal usuario)
        => Valor(usuario, ClaimNames.Email) ?? Valor(usuario, ClaimTypes.Email) ?? usuario.ObtenerUpn();

    private static string? Valor(ClaimsPrincipal usuario, string tipo)
        => usuario.FindFirst(tipo)?.Value is { } v && !string.IsNullOrWhiteSpace(v) ? v : null;

    public static string? ObtenerNombre(this ClaimsPrincipal usuario)
        => usuario.FindFirst(ClaimNames.Name)?.Value ?? usuario.Identity?.Name;

    public static IReadOnlyCollection<string> ObtenerRoles(this ClaimsPrincipal usuario)
        => usuario.FindAll(ClaimNames.Roles)
            .Concat(usuario.FindAll(ClaimTypes.Role))
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
