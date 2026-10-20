namespace App.Application.Empleados;

/// <summary>
/// Cédulas de la API por código EKON (TAREA-22-1, D15). DATO SENSIBLE: viaja aparte de <see cref="EmpleadoErp"/> (que es
/// un record y su ToString la imprimiría) y solo existe en la lectura FRESCA (registro y «completar cédulas»); la caché
/// de empleados nunca la guarda. Solo se escribe en Empleado.Cedula: nunca en un DTO, ProblemDetails, log ni mensaje de
/// excepción. Sin enumeración pública: solo se consulta por código.
/// </summary>
public sealed class CedulasEmpleados
{
    public static readonly CedulasEmpleados Vacio = new(new Dictionary<string, string>());

    private readonly Dictionary<string, string> porCodigo;

    /// <summary>Pares (código, cédula) con los textos normalizados; los vacíos se omiten y, ante un código repetido, queda el primero.</summary>
    public CedulasEmpleados(IEnumerable<(string? Codigo, string? Cedula)> pares)
    {
        ArgumentNullException.ThrowIfNull(pares);
        porCodigo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (codigo, cedula) in pares)
        {
            if (MapeoEmpleado.Normalizar(codigo) is { } c && MapeoEmpleado.NormalizarCedula(cedula) is { } valor)
            {
                porCodigo.TryAdd(c, valor);
            }
        }
    }

    private CedulasEmpleados(Dictionary<string, string> porCodigo) => this.porCodigo = porCodigo;

    public int Cantidad => porCodigo.Count;

    /// <summary>Cédula normalizada (sin espacios extremos, máximo 20) del código; null si la API no la trae.</summary>
    public string? De(string? codigo) =>
        MapeoEmpleado.Normalizar(codigo) is { } c && porCodigo.TryGetValue(c, out var cedula) ? cedula : null;

    /// <summary>Solo las de esos códigos (el catálogo de un registro no guarda las ~1633 cédulas).</summary>
    public CedulasEmpleados Solo(IEnumerable<string> codigos)
    {
        ArgumentNullException.ThrowIfNull(codigos);
        var seleccion = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var codigo in codigos)
        {
            if (MapeoEmpleado.Normalizar(codigo) is { } c && De(c) is { } cedula)
            {
                seleccion.TryAdd(c, cedula);
            }
        }

        return new CedulasEmpleados(seleccion);
    }

    /// <summary>Nunca los valores: solo la cantidad.</summary>
    public override string ToString() => $"CedulasEmpleados({Cantidad})";
}
