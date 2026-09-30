using App.Application.Seguridad;
using App.Domain.Comun;
using App.Domain.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace App.Infrastructure.Persistencia.Auditoria;

/// <summary>
/// Asigna CreadoPorId/FechaCreacion (Added) y ModificadoPorId/FechaModificacion (Modified) en UTC.
/// - Sin usuario autenticado (jobs, semillas, consola) se usa SISTEMA (Id = 1).
/// - En Added, si CreadoPorId/FechaCreacion ya traen valor, se respetan
///   (necesario para conservar Author/Created originales en la carga de Fase 3).
/// - En Modified, nunca se permite alterar CreadoPorId/FechaCreacion.
/// </summary>
public sealed class AuditoriaInterceptor(IUsuarioActual usuarioActual, TimeProvider reloj) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Aplicar(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Aplicar(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Aplicar(DbContext? contexto)
    {
        if (contexto is null)
        {
            return;
        }

        var usuarioId = usuarioActual.UsuarioId ?? Usuario.IdSistema;
        var ahora = TruncarASegundos(reloj.GetUtcNow().UtcDateTime); // DATETIME2(0)

        foreach (var entrada in contexto.ChangeTracker.Entries<IAuditable>())
        {
            switch (entrada.State)
            {
                case EntityState.Added:
                    if (entrada.Entity.CreadoPorId == 0)
                    {
                        entrada.Entity.CreadoPorId = usuarioId;
                    }
                    if (entrada.Entity.FechaCreacion == default)
                    {
                        entrada.Entity.FechaCreacion = ahora;
                    }
                    break;

                case EntityState.Modified:
                    entrada.Entity.ModificadoPorId = usuarioId;
                    entrada.Entity.FechaModificacion = ahora;
                    entrada.Property(nameof(IAuditable.CreadoPorId)).IsModified = false;
                    entrada.Property(nameof(IAuditable.FechaCreacion)).IsModified = false;
                    break;
            }
        }
    }

    private static DateTime TruncarASegundos(DateTime valor)
        => new(valor.Ticks - valor.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
}
