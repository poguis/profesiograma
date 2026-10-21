using System.Globalization;

namespace App.Domain.Novedades;

/// <summary>
/// R7 (TAREA-23): "NOV-" + fecha de negocio (yyyyMMdd) + "-" + últimos 6 caracteres del Uid en formato "N", en
/// minúsculas (igual que el original: Right(Substitute(GUID(), "-", ""), 6)). La unicidad la verifica Application.
/// </summary>
public static class GeneradorCodigoNovedad
{
    public const string Prefijo = "NOV-";

    public static string Generar(DateOnly hoyNegocio, Guid uid)
    {
        var sufijo = uid.ToString("N", CultureInfo.InvariantCulture)[^6..].ToLowerInvariant();
        return $"{Prefijo}{hoyNegocio.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-{sufijo}";
    }
}
