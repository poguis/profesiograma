using App.Application.Seguridad;

namespace App.Api.Seguridad;

/// <summary>Implementación scoped de IUsuarioActual; la llena UsuarioActualMiddleware.</summary>
public sealed class UsuarioActual : IUsuarioActual
{
    public int? UsuarioId { get; private set; }
    public string? Email { get; private set; }
    public string? NombreMostrar { get; private set; }
    public IReadOnlyCollection<string> Roles { get; private set; } = [];
    public bool EstaAutenticado => UsuarioId.HasValue;

    public bool TieneRol(string rol) => Roles.Contains(rol, StringComparer.OrdinalIgnoreCase);

    internal void Establecer(int usuarioId, string email, string? nombre, IReadOnlyCollection<string> roles)
    {
        UsuarioId = usuarioId;
        Email = email;
        NombreMostrar = nombre;
        Roles = roles;
    }
}
