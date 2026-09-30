using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace App.Api.Seguridad.DevAuth;

/// <summary>
/// Autenticación SIMULADA para desarrollo local, sin App Registrations.
/// Emite las mismas claims que un token de Entra ID (oid, preferred_username, name, roles).
/// Solo se registra en el entorno Development (ver AutenticacionExtensions).
/// </summary>
public sealed class DevAuthHandler(
    IOptionsMonitor<DevAuthOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<DevAuthOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var clave = Request.Headers[DevAuthDefaults.Encabezado].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(clave))
        {
            clave = Options.UsuarioPorDefecto;
        }

        if (string.IsNullOrWhiteSpace(clave) || clave.Equals("anonimo", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var usuario = Options.Usuarios.FirstOrDefault(u => u.Clave.Equals(clave, StringComparison.OrdinalIgnoreCase));
        if (usuario is null)
        {
            return Task.FromResult(AuthenticateResult.Fail($"Usuario DevAuth '{clave}' no está configurado en appsettings.Development.json."));
        }

        var claims = new List<Claim>
        {
            new(ClaimNames.ObjectId, usuario.ObjectId.ToString()),
            new(ClaimNames.PreferredUsername, usuario.Email),
            new(ClaimNames.Name, usuario.Nombre),
            new("auth_mode", DevAuthDefaults.Esquema)
        };
        claims.AddRange(usuario.Roles.Select(r => new Claim(ClaimNames.Roles, r)));

        var identidad = new ClaimsIdentity(claims, Scheme.Name, ClaimNames.Name, ClaimNames.Roles);
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identidad), Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
