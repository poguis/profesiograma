namespace App.Application.Seguridad;

/// <summary>
/// Usuario de la petición actual. En jobs o procesos sin HTTP, UsuarioId es null
/// y la auditoría usa el usuario SISTEMA (Id = 1).
/// </summary>
public interface IUsuarioActual
{
    int? UsuarioId { get; }
    string? Email { get; }
    string? NombreMostrar { get; }
    IReadOnlyCollection<string> Roles { get; }
    bool EstaAutenticado { get; }
    bool TieneRol(string rol);
}

public sealed record UsuarioActualDto(int Id, string Email, string? NombreMostrar, IReadOnlyCollection<string> Roles);
