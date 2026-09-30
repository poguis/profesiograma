using System.Globalization;

namespace App.Application.Comun;

/// <summary>
/// Lectura y validación de parámetros de consulta recibidos como texto (query string).
/// Los errores se acumulan por campo con mensajes en español (formato de ValidationProblem).
/// </summary>
public static class LectorParametros
{
    public const string FormatoFecha = "yyyy-MM-dd";
    public const int PaginaPorDefecto = 1;

    public static int LeerPagina(string? valor, IDictionary<string, string[]> errores)
        => LeerEntero(valor, "pagina", PaginaPorDefecto, 1, int.MaxValue,
            "La página debe ser un número entero mayor o igual a 1.", errores);

    public static int LeerTamano(string? valor, int porDefecto, int maximo, IDictionary<string, string[]> errores)
        => LeerEntero(valor, "tamano", porDefecto, 1, maximo,
            $"El tamaño de página debe ser un número entero entre 1 y {maximo}.", errores);

    /// <summary>"yyyy-MM-dd" → DateOnly; vacío → null.</summary>
    public static DateOnly? LeerFecha(string? valor, string campo, IDictionary<string, string[]> errores)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        if (DateOnly.TryParseExact(valor.Trim(), FormatoFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
        {
            return fecha;
        }

        errores[campo] = [$"La fecha '{campo}' debe tener el formato {FormatoFecha}."];
        return null;
    }

    /// <summary>"true" / "false" (sin distinguir mayúsculas); vacío → valor por defecto.</summary>
    public static bool LeerBooleano(string? valor, string campo, bool porDefecto, IDictionary<string, string[]> errores)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return porDefecto;
        }

        if (bool.TryParse(valor.Trim(), out var resultado))
        {
            return resultado;
        }

        errores[campo] = [$"El valor de '{campo}' debe ser true o false."];
        return porDefecto;
    }

    /// <summary>Texto recortado; vacío o solo espacios → null.</summary>
    public static string? Normalizar(string? valor)
        => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static int LeerEntero(string? valor, string campo, int porDefecto, int minimo, int maximo,
        string mensaje, IDictionary<string, string[]> errores)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return porDefecto;
        }

        if (int.TryParse(valor.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var numero)
            && numero >= minimo && numero <= maximo)
        {
            return numero;
        }

        errores[campo] = [mensaje];
        return porDefecto;
    }
}
