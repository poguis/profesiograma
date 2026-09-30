using App.Domain.Seguridad;

namespace App.Infrastructure.Persistencia.Convenciones;

/// <summary>
/// Valores fijos para HasData (deben ser deterministas; DateTime.UtcNow generaría
/// una migración distinta en cada "migrations add").
/// </summary>
internal static class Semilla
{
    public static readonly DateTime Fecha = new(2026, 9, 29, 0, 0, 0, DateTimeKind.Utc);
    public const int Sistema = Usuario.IdSistema;
}
