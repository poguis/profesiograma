using System.Globalization;

namespace App.Domain.Proyectos;

/// <summary>
/// RN01 (FASE_5 §3, P6): "PRY-" + fecha de negocio (yyyyMMdd) + "-" + últimos 6 caracteres del Uid en formato "N",
/// en minúsculas (igual que los códigos existentes). La unicidad la verifica la capa Application.
/// </summary>
public static class GeneradorCodigoProyecto
{
    public const string Prefijo = "PRY-";

    public static string Generar(DateOnly hoyNegocio, Guid uid)
    {
        var sufijo = uid.ToString("N", CultureInfo.InvariantCulture)[^6..].ToLowerInvariant();
        return $"{Prefijo}{hoyNegocio.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-{sufijo}";
    }
}
