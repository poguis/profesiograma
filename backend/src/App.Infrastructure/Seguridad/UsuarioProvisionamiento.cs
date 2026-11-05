using App.Application.Comun;
using App.Application.Seguridad;
using App.Domain.Seguridad;
using App.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace App.Infrastructure.Seguridad;

/// <summary>
/// Resuelve el Usuario del token. TAREA-7b-2: busca por oid → UPN → correo, decide con ReglasProvisionamiento (Application)
/// y aquí solo se lee y se guarda. El correo NO es un identificador confiable (C29): prevalece el oid.
/// Logs (Q3): solo el Id y el oid abreviado; nunca el correo ni el UPN.
/// </summary>
internal sealed class UsuarioProvisionamiento(
    ProfesiogramaDbContext db,
    IMemoryCache cache,
    ILogger<UsuarioProvisionamiento> logger) : IUsuarioProvisionamiento
{
    private static readonly TimeSpan DuracionCache = TimeSpan.FromMinutes(10);

    public async Task<UsuarioResuelto> ResolverAsync(Guid? entraObjectId, string? upn, string correo, string nombreMostrar, CancellationToken ct)
    {
        var token = new IdentidadToken(entraObjectId, Normalizar(upn), Normalizar(correo)!);
        var claveCache = ClaveCache(entraObjectId?.ToString() ?? token.Correo);

        if (cache.TryGetValue(claveCache, out UsuarioResuelto? enCache) && enCache is not null)
        {
            return enCache;
        }

        UsuarioResuelto resultado;
        try
        {
            resultado = await ResolverEnBaseAsync(token, nombreMostrar, ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is not BaseDatosNoDisponibleException) // TAREA-22c: base caída → 503
        {
            // Dos peticiones simultáneas del mismo usuario nuevo: la segunda choca con un índice único (correo, oid o UPN).
            logger.LogWarning(ex, "Conflicto al provisionar (oid {Oid}); se reintenta la lectura.", Abreviar(entraObjectId));
            db.ChangeTracker.Clear();
            resultado = await ResolverEnBaseAsync(token, nombreMostrar, ct);
        }

        cache.Set(claveCache, resultado, DuracionCache);
        return resultado;
    }

    public void Invalidar(string email, Guid? entraObjectId)
    {
        cache.Remove(ClaveCache(email.Trim().ToLowerInvariant()));
        if (entraObjectId is { } oid)
        {
            cache.Remove(ClaveCache(oid.ToString()));
        }
    }

    internal static string ClaveCache(string oidOEmail) => $"usuario:{oidOEmail}";

    internal static IQueryable<Usuario> ConsultaPorOid(ProfesiogramaDbContext db, Guid oid) => db.Usuarios.Where(u => u.EntraObjectId == oid);

    internal static IQueryable<Usuario> ConsultaPorUpn(ProfesiogramaDbContext db, string upn) => db.Usuarios.Where(u => u.Upn == upn);

    internal static IQueryable<Usuario> ConsultaPorCorreo(ProfesiogramaDbContext db, string correo) => db.Usuarios.Where(u => u.Email == correo);

    private async Task<UsuarioResuelto> ResolverEnBaseAsync(IdentidadToken token, string nombre, CancellationToken ct)
    {
        var porOid = token.EntraObjectId is { } oid ? await ConsultaPorOid(db, oid).FirstOrDefaultAsync(ct) : null;
        var porUpn = token.Upn is not null && (porOid is null || porOid.Upn is null)
            ? await ConsultaPorUpn(db, token.Upn).FirstOrDefaultAsync(ct)
            : null;
        var porCorreo = porOid is null && porUpn is null ? await ConsultaPorCorreo(db, token.Correo).FirstOrDefaultAsync(ct) : null;

        var decision = ReglasProvisionamiento.Decidir(token, Identidad(porOid), Identidad(porUpn), Identidad(porCorreo));
        switch (decision.Tipo)
        {
            case TipoDecisionProvisionamiento.Conflicto:
                logger.LogWarning("Conflicto de identidad al ingresar: usuario {Id}, oid {Oid}.", decision.Usuario!.Id, Abreviar(token.EntraObjectId));
                return new UsuarioResuelto(decision.Usuario.Id, false, decision.Motivo);

            case TipoDecisionProvisionamiento.Crear:
                var nuevo = new Usuario
                {
                    EntraObjectId = token.EntraObjectId,
                    Upn = token.Upn,
                    Email = token.Correo,
                    NombreMostrar = string.IsNullOrWhiteSpace(nombre) ? token.Correo : nombre.Trim()
                };
                db.Usuarios.Add(nuevo);
                await db.SaveChangesAsync(ct);
                logger.LogInformation("Usuario provisionado: Id {Id}.", nuevo.Id);
                return new UsuarioResuelto(nuevo.Id, true);

            default:
                var usuario = new[] { porOid, porUpn, porCorreo }.First(u => u?.Id == decision.Usuario!.Id)!;
                if (decision.CompletarOid || decision.CompletarUpn)
                {
                    // Usuario precargado (Admin o Fase 3) que ingresa por primera vez, o fila anterior a la 7b-2 sin UPN.
                    if (decision.CompletarOid) usuario.EntraObjectId = token.EntraObjectId;
                    if (decision.CompletarUpn) usuario.Upn = token.Upn;
                    await db.SaveChangesAsync(ct);
                }

                return usuario.Activo
                    ? new UsuarioResuelto(usuario.Id, true)
                    : new UsuarioResuelto(usuario.Id, false, "El usuario está inactivo en la aplicación.");
        }
    }

    private static UsuarioIdentidad? Identidad(Usuario? u) => u is null ? null : new(u.Id, u.EntraObjectId, u.Upn, u.Activo);

    private static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim().ToLowerInvariant();

    private static string Abreviar(Guid? oid) => oid?.ToString()[..8] ?? "-";
}
