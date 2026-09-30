using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace App.Infrastructure.Persistencia.Convenciones;

/// <summary>
/// Garantiza DateTimeKind.Utc al leer y convierte a UTC al escribir.
/// Regla: toda marca de tiempo en BD es UTC; la UI convierte a Ecuador (UTC-5).
/// </summary>
internal sealed class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public static readonly UtcDateTimeConverter Instancia = new();

    public UtcDateTimeConverter()
        : base(
            v => v.Kind == DateTimeKind.Local ? v.ToUniversalTime() : DateTime.SpecifyKind(v, DateTimeKind.Utc),
            v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}
