using App.Application.Comun;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Configuracion;

/// <summary>
/// TAREA-22c: BaseDatosNoDisponibleException (también envuelta, p. ej. en DbUpdateException) → 503 ProblemDetails
/// "Base de datos no disponible". El texto depende del método y de la fase del fallo. Registra un aviso como máximo
/// cada 30 s (con la cuenta de los omitidos), sin el mensaje del proveedor.
/// </summary>
public sealed class BaseDatosNoDisponibleExceptionHandler(
    IProblemDetailsService problemDetails,
    ILogger<BaseDatosNoDisponibleExceptionHandler> logger) : IExceptionHandler
{
    public const string Titulo = "Base de datos no disponible";
    public const string DetalleConsulta = "No se pudo consultar la base de datos. Intente nuevamente en un minuto.";
    public const string DetalleSinCambios = "La base de datos no respondió y no se guardó ningún cambio. Intente nuevamente en un minuto.";
    public const string DetalleIncierto =
        "La base de datos dejó de responder durante el registro. Vuelva a cargar el proyecto para comprobar si el cambio se guardó antes de intentarlo de nuevo.";

    private const long IntervaloAvisoMs = 30_000;
    private long _proximoAvisoMs;
    private int _omitidos;

    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excepcion, CancellationToken ct)
    {
        if (Buscar(excepcion) is not { } bd)
        {
            return false;
        }

        Registrar(contexto, bd);

        contexto.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            Exception = excepcion,
            ProblemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = Titulo,
                Detail = Detalle(contexto.Request.Method, bd.Fase),
            },
        });
    }

    /// <summary>
    /// Escritura con fallo al abrir: no se guardó nada, porque los servicios no vuelven a la base después del commit de
    /// ITransaccionAsignaciones (devuelven su resultado). En un comando o en el commit el resultado es incierto.
    /// </summary>
    internal static string Detalle(string metodo, FaseFalloBaseDatos fase)
        => HttpMethods.IsGet(metodo) || HttpMethods.IsHead(metodo) ? DetalleConsulta
            : fase == FaseFalloBaseDatos.AlAbrir ? DetalleSinCambios
            : DetalleIncierto;

    internal static BaseDatosNoDisponibleException? Buscar(Exception? excepcion) => excepcion switch
    {
        null => null,
        BaseDatosNoDisponibleException bd => bd,
        AggregateException agregada => agregada.InnerExceptions.Select(Buscar).FirstOrDefault(e => e is not null),
        _ => Buscar(excepcion.InnerException),
    };

    private void Registrar(HttpContext contexto, BaseDatosNoDisponibleException bd)
    {
        var ahora = Environment.TickCount64;
        var proximo = Interlocked.Read(ref _proximoAvisoMs);
        if (ahora < proximo || Interlocked.CompareExchange(ref _proximoAvisoMs, ahora + IntervaloAvisoMs, proximo) != proximo)
        {
            Interlocked.Increment(ref _omitidos);
            return;
        }

        logger.LogWarning(
            "Base de datos no disponible: error SQL {Numero} ({Fase}, {DuracionMs:0} ms) en {Metodo} {Ruta}. Avisos omitidos desde el anterior: {Omitidos}.",
            bd.NumeroError, bd.Fase, bd.DuracionMs, contexto.Request.Method, contexto.Request.Path.Value, Interlocked.Exchange(ref _omitidos, 0));
    }
}
