using App.Application.Seguridad;
using App.Domain.Seguridad;
using App.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Seguridad;

/// <summary>
/// Busca el Usuario por EntraObjectId (claim oid); si no existe, por Email; si tampoco, lo crea.
/// El correo NO es un identificador confiable (C29): prevalece el oid.
/// </summary>
internal sealed class UsuarioProvisionamiento(
    ProfesiogramaDbContext db,
    IMemoryCache cache,
    ILogger<UsuarioProvisionamiento> logger) : IUsuarioProvisionamiento
{
    private static readonly TimeSpan DuracionCache = TimeSpan.FromMinutes(10);

    public async Task<UsuarioResuelto> ResolverAsync(Guid? entraObjectId, string email, string nombreMostrar, CancellationToken ct)
    {
        var emailNormalizado = email.Trim().ToLowerInvariant();
        var claveCache = $"usuario:{entraObjectId?.ToString() ?? emailNormalizado}";

        if (cache.TryGetValue(claveCache, out UsuarioResuelto? enCache) && enCache is not null)
        {
            return enCache;
        }

        UsuarioResuelto resultado;
        try
        {
            resultado = await ResolverEnBaseAsync(entraObjectId, emailNormalizado, nombreMostrar, ct);
        }
        catch (DbUpdateException ex)
        {
            // Dos peticiones simultáneas del mismo usuario nuevo: la segunda choca con UQ_Usuario_Email.
            logger.LogWarning(ex, "Conflicto al provisionar {Email}; se reintenta la lectura.", emailNormalizado);
            db.ChangeTracker.Clear();
            resultado = await ResolverEnBaseAsync(entraObjectId, emailNormalizado, nombreMostrar, ct);
        }

        cache.Set(claveCache, resultado, DuracionCache);
        return resultado;
    }

    private async Task<UsuarioResuelto> ResolverEnBaseAsync(Guid? oid, string email, string nombre, CancellationToken ct)
    {
        Usuario? usuario = null;

        if (oid is not null)
        {
            usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.EntraObjectId == oid, ct);
        }

        if (usuario is null)
        {
            usuario = await db.Usuarios.FirstOrDefaultAsync(u => u.Email == email, ct);

            if (usuario is not null && usuario.EntraObjectId is not null && oid is not null && usuario.EntraObjectId != oid)
            {
                logger.LogWarning("Conflicto de identidad: {Email} registrado con otro oid.", email);
                return new UsuarioResuelto(usuario.Id, false, "El correo está asociado a otra identidad de Entra ID.");
            }
        }

        if (usuario is null)
        {
            usuario = new Usuario
            {
                EntraObjectId = oid,
                Email = email,
                NombreMostrar = string.IsNullOrWhiteSpace(nombre) ? email : nombre.Trim()
            };
            db.Usuarios.Add(usuario);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Usuario provisionado: {Email} (Id {Id}).", email, usuario.Id);
        }
        else if (usuario.EntraObjectId is null && oid is not null)
        {
            // Usuario precargado (migración / permisos) que ingresa por primera vez.
            usuario.EntraObjectId = oid;
            await db.SaveChangesAsync(ct);
        }

        return usuario.Activo
            ? new UsuarioResuelto(usuario.Id, true)
            : new UsuarioResuelto(usuario.Id, false, "El usuario está inactivo en la aplicación.");
    }
}
