namespace App.Application.Seguridad;

/// <summary>
/// Resuelve (o crea en el primer ingreso) el registro Usuario a partir de las claims del token.
/// </summary>
public interface IUsuarioProvisionamiento
{
    Task<UsuarioResuelto> ResolverAsync(Guid? entraObjectId, string email, string nombreMostrar, CancellationToken ct);

    /// <summary>
    /// TAREA-25-1: quita de la caché (10 min) las entradas del usuario, por oid y por correo, para que un cambio (baja,
    /// reactivación, nombre) rija desde la SIGUIENTE solicitud. Caché en memoria del proceso (una sola instancia de la API).
    /// </summary>
    void Invalidar(string email, Guid? entraObjectId);
}

/// <param name="UsuarioId">Id en dbo.Usuario.</param>
/// <param name="Habilitado">false si está inactivo o hay conflicto de identidad.</param>
/// <param name="Motivo">Detalle cuando no está habilitado.</param>
public sealed record UsuarioResuelto(int UsuarioId, bool Habilitado, string? Motivo = null);
