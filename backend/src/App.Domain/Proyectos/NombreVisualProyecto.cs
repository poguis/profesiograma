namespace App.Domain.Proyectos;

/// <summary>
/// Nombre visual del proyecto (FASE_5 §3): nombre del proyecto ERP; si no hay, "PLANTA - " + descripción de la
/// dimensión (igual que la app original, también para OFICINAS ADMINISTRATIVAS).
/// </summary>
public static class NombreVisualProyecto
{
    public const string PrefijoDimension = "PLANTA - ";

    public static string Calcular(string? nombreProyectoErp, string? descripcionDimension)
    {
        if (!string.IsNullOrWhiteSpace(nombreProyectoErp))
        {
            return nombreProyectoErp.Trim();
        }

        if (!string.IsNullOrWhiteSpace(descripcionDimension))
        {
            return PrefijoDimension + descripcionDimension.Trim();
        }

        throw new ArgumentException("Se requiere el nombre del proyecto ERP o la descripción de la dimensión.");
    }
}
