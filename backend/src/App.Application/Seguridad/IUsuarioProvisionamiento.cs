namespace App.Application.Seguridad;

/// <summary>
/// Resuelve (o crea en el primer ingreso) el registro Usuario a partir de las claims del token.
/// </summary>
public interface IUsuarioProvisionamiento
{
    Task<UsuarioResuelto> ResolverAsync(Guid? entraObjectId, string email, string nombreMostrar, CancellationToken ct);
}

/// <param name="UsuarioId">Id en dbo.Usuario.</param>
/// <param name="Habilitado">false si está inactivo o hay conflicto de identidad.</param>
/// <param name="Motivo">Detalle cuando no está habilitado.</param>
public sealed record UsuarioResuelto(int UsuarioId, bool Habilitado, string? Motivo = null);
