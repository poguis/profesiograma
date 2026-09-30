namespace App.Application.Erp;

/// <summary>
/// El ERP no respondió correctamente (red, timeout, 5xx o respuesta inválida).
/// La API la traduce a 503 ProblemDetails "Servicio ERP no disponible".
/// </summary>
public sealed class ErpNoDisponibleException(string operacion, string detalle, Exception? interna = null)
    : Exception($"ERP no disponible ({operacion}): {detalle}", interna)
{
    public string Operacion { get; } = operacion;
}
