namespace App.Application.Comun;

/// <summary>Fecha de negocio (Ecuador, UTC-5).</summary>
public static class FechaNegocio
{
    // [PENDIENTE] Leer de Parametro.ZONA_HORARIA (hoy es el mismo valor que usa el sembrador de datos de prueba).
    public const string ZonaNegocio = "SA Pacific Standard Time";

    private static readonly Lazy<TimeZoneInfo> Zona = new(() => TimeZoneInfo.FindSystemTimeZoneById(ZonaNegocio));

    /// <summary>Fecha de hoy en Ecuador según el reloj indicado.</summary>
    public static DateOnly Hoy(TimeProvider reloj)
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(reloj.GetUtcNow(), Zona.Value).DateTime);
}
