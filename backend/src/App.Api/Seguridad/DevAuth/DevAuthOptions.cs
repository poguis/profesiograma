using Microsoft.AspNetCore.Authentication;

namespace App.Api.Seguridad.DevAuth;

public static class DevAuthDefaults
{
    public const string Esquema = "DevAuth";

    /// <summary>Encabezado para elegir el usuario simulado: admin | gestor | anonimo.</summary>
    public const string Encabezado = "X-Dev-User";
}

public sealed class DevAuthOptions : AuthenticationSchemeOptions
{
    public string UsuarioPorDefecto { get; set; } = "admin";
    public List<DevAuthUsuario> Usuarios { get; set; } = [];
}

public sealed class DevAuthUsuario
{
    public string Clave { get; set; } = string.Empty;
    public Guid ObjectId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string[] Roles { get; set; } = [];
}
