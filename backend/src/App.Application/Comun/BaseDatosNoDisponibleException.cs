namespace App.Application.Comun;

/// <summary>Momento en que la base de datos dejó de responder (TAREA-22c).</summary>
public enum FaseFalloBaseDatos
{
    /// <summary>Al abrir la conexión: la petición no llegó a escribir nada.</summary>
    AlAbrir,

    /// <summary>Durante un comando (con la conexión ya abierta).</summary>
    EnComando,

    /// <summary>Al confirmar la transacción: no se sabe si el cambio se guardó.</summary>
    EnConfirmacion,
}

/// <summary>
/// La base de datos no responde (red, servidor o timeout de conexión). La lanza el interceptor de EF de Infrastructure
/// con la excepción original como interna; la API la traduce a 503 "Base de datos no disponible". El mensaje no lleva
/// el del proveedor (puede nombrar el servidor).
/// </summary>
public sealed class BaseDatosNoDisponibleException(FaseFalloBaseDatos fase, int numeroError, double duracionMs, Exception interna)
    : Exception($"Base de datos no disponible ({fase}, error {numeroError}).", interna)
{
    public FaseFalloBaseDatos Fase { get; } = fase;

    /// <summary>Número del error del proveedor (SqlException.Number).</summary>
    public int NumeroError { get; } = numeroError;

    public double DuracionMs { get; } = duracionMs;
}
