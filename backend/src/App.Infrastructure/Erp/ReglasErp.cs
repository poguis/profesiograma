using System.Globalization;

namespace App.Infrastructure.Erp;

/// <summary>Reglas de lectura de datos del ERP, compartidas por los modos Http y Simulado.</summary>
internal static class ReglasErp
{
    /// <summary>Proyectos seleccionables: status "Activo" (FASE_5 §4).</summary>
    public const string EstadoProyectoActivo = "Activo";

    /// <summary>Horarios seleccionables: status "A" (FASE_5 §4; el ERP devuelve "A" o "I").</summary>
    public const string EstadoHorarioActivo = "A";

    public static bool EsProyectoActivo(string? status) =>
        string.Equals(status?.Trim(), EstadoProyectoActivo, StringComparison.OrdinalIgnoreCase);

    public static bool EsHorarioActivo(string? status) =>
        string.Equals(status?.Trim(), EstadoHorarioActivo, StringComparison.OrdinalIgnoreCase);

    /// <summary>Razón social = tradeName ?? name (igual que la app original al guardar).</summary>
    public static string NombreCompania(string? nombre, string? nombreComercial) =>
        (string.IsNullOrWhiteSpace(nombreComercial) ? nombre : nombreComercial)?.Trim() ?? string.Empty;

    /// <summary>"0700" (formato real del ERP: HHmm) o "07:00" → TimeOnly. Vacío o inválido → null.</summary>
    public static TimeOnly? AHora(string? valor) =>
        LeerHorasMinutos(valor) is (int h, int m) && h < 24 ? new TimeOnly(h, m) : null;

    /// <summary>"1100" (HHmm) o "11:00" → 660 minutos. Vacío o inválido → null.</summary>
    public static short? AMinutos(string? valor) =>
        LeerHorasMinutos(valor) is (int h, int m) && h * 60 + m <= short.MaxValue ? (short)(h * 60 + m) : null;

    private static (int Horas, int Minutos)? LeerHorasMinutos(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var texto = valor.Trim();
        string horas, minutos;
        if (texto.Length == 4 && texto.All(char.IsAsciiDigit))
        {
            (horas, minutos) = (texto[..2], texto[2..]);
        }
        else if (texto.Split(':') is [var h, var m, ..] && h.Length is 1 or 2 && m.Length == 2)
        {
            (horas, minutos) = (h, m);
        }
        else
        {
            return null;
        }

        return int.TryParse(horas, NumberStyles.None, CultureInfo.InvariantCulture, out var hh)
               && int.TryParse(minutos, NumberStyles.None, CultureInfo.InvariantCulture, out var mm)
               && mm < 60
            ? (hh, mm)
            : null;
    }
}
