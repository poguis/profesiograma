using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace App.Api.Configuracion;

/// <summary>
/// TAREA-26d-1 (defecto V3): BadHttpRequestException (cuerpo que no se puede leer: tipo JSON incorrecto, JSON mal
/// formado, cuerpo vacío o sin Content-Type JSON) → 400/415 ValidationProblem con el mensaje en español en su campo
/// ("actividadId", "principales[0].fechaInicio"…). Antes llegaba al manejador por defecto como 500.
/// Los campos desconocidos se ignoran (comportamiento por defecto de System.Text.Json): no son error.
/// </summary>
public sealed class SolicitudIlegibleExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public const string Titulo = "La solicitud no tiene el formato esperado.";
    public const string ClaveCuerpo = "cuerpo";
    public const string ClaveSolicitud = "solicitud";

    public async ValueTask<bool> TryHandleAsync(HttpContext contexto, Exception excepcion, CancellationToken ct)
    {
        if (excepcion is not BadHttpRequestException solicitud)
        {
            return false;
        }

        var estado = solicitud.StatusCode is >= 400 and < 500 ? solicitud.StatusCode : StatusCodes.Status400BadRequest;
        var (clave, mensaje) = Error(solicitud, estado);

        contexto.Response.StatusCode = estado;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = contexto,
            Exception = excepcion,
            ProblemDetails = new ValidationProblemDetails(new Dictionary<string, string[]> { [clave] = [mensaje] })
            {
                Status = estado,
                Title = Titulo,
            },
        });
    }

    /// <summary>Campo y mensaje. El campo sale de la ruta JSON ("$.principales[0].fechaInicio" → "principales[0].fechaInicio").</summary>
    private static (string Clave, string Mensaje) Error(BadHttpRequestException solicitud, int estado)
    {
        if (solicitud.InnerException is JsonException json)
        {
            var raiz = TipoRaiz(json.Message);
            if (raiz is null)
            {
                var linea = json.LineNumber is long l ? $" (línea {l + 1})" : "";
                return (ClaveCuerpo, $"El cuerpo no es un JSON válido{linea}: revise comas, comillas y llaves.");
            }

            var ruta = json.Path;
            var clave = string.IsNullOrEmpty(ruta) || ruta == "$" ? ClaveCuerpo
                : ruta.StartsWith("$.", StringComparison.Ordinal) ? ruta[2..]
                : ruta.TrimStart('$');
            return (clave, MensajeTipo(TipoDelCampo(raiz, clave)));
        }

        return estado == StatusCodes.Status415UnsupportedMediaType
            ? (ClaveCuerpo, "Envíe el cuerpo como JSON (Content-Type: application/json).")
            : (ClaveSolicitud, "No se pudo leer la solicitud: cuerpo vacío o un parámetro con formato incorrecto.");
    }

    /// <summary>
    /// Nombre del tipo de "The JSON value could not be converted to {tipo}." (error de tipo); null si es otro error
    /// (JSON mal formado). Con records, System.Text.Json nombra el tipo RAÍZ del cuerpo, no el del campo. El texto en
    /// inglés de System.Text.Json no se devuelve al cliente.
    /// </summary>
    private static string? TipoRaiz(string mensaje)
    {
        const string marca = "could not be converted to ";
        var i = mensaje.IndexOf(marca, StringComparison.Ordinal);
        if (i < 0)
        {
            return null;
        }

        var inicio = i + marca.Length;
        var fin = mensaje.IndexOf(". Path:", inicio, StringComparison.Ordinal);
        return fin < 0 ? mensaje[inicio..] : mensaje[inicio..fin];
    }

    /// <summary>
    /// Tipo del campo que falló. System.Text.Json nombra en el mensaje, según el caso, el tipo del valor (p. ej.
    /// "System.Nullable`1[System.Int32]", "…IReadOnlyList`1[…]") o el record que contiene el campo (el raíz u otro
    /// anidado): se prueba el nombre como tipo del valor y, si es un record, se recorre la ruta (o su parte final) desde él.
    /// </summary>
    private static Type? TipoDelCampo(string nombre, string clave)
    {
        if (nombre.Contains("List`1", StringComparison.Ordinal) || nombre.EndsWith("[]", StringComparison.Ordinal))
        {
            return typeof(object[]);
        }

        var simple = nombre.StartsWith("System.Nullable`1[", StringComparison.Ordinal) ? nombre["System.Nullable`1[".Length..^1] : nombre;
        var tipo = Type.GetType(simple)
            ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(simple)).FirstOrDefault(t => t is not null);
        if (tipo is null || tipo.Namespace?.StartsWith("System", StringComparison.Ordinal) == true || clave == ClaveCuerpo)
        {
            return tipo;
        }

        var segmentos = clave.Replace("[", ".[", StringComparison.Ordinal).Split('.', StringSplitOptions.RemoveEmptyEntries);
        for (var desde = 0; desde < segmentos.Length; desde++)
        {
            if (Recorrer(tipo, segmentos[desde..]) is { } campo)
            {
                return campo;
            }
        }

        return null;
    }

    private static Type? Recorrer(Type tipo, string[] segmentos)
    {
        Type? actual = tipo;
        foreach (var segmento in segmentos)
        {
            actual = segmento.StartsWith('[')
                ? Elemento(actual!)
                : actual!.GetProperty(segmento, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)?.PropertyType;
            if (actual is null)
            {
                return null;
            }
        }

        return actual;
    }

    private static Type? Elemento(Type tipo) =>
        tipo.IsArray ? tipo.GetElementType()
        : tipo.IsGenericType && tipo.GetGenericArguments() is [var unico] && typeof(System.Collections.IEnumerable).IsAssignableFrom(tipo) ? unico
        : null;

    private static string MensajeTipo(Type? tipo)
    {
        if (tipo is null)
        {
            return "El valor no tiene el tipo esperado.";
        }

        if (tipo != typeof(string) && Elemento(tipo) is not null)
        {
            return "Debe ser una lista ([ … ]).";
        }

        var simple = Nullable.GetUnderlyingType(tipo) ?? tipo;
        return simple switch
        {
            _ when simple == typeof(string) => "Debe ser texto entre comillas (p. ej. \"20\").",
            _ when simple == typeof(int) || simple == typeof(short) || simple == typeof(long) || simple == typeof(byte)
                => "Debe ser un número entero sin comillas.",
            _ when simple == typeof(DateOnly) => "Debe ser una fecha con formato aaaa-mm-dd entre comillas.",
            _ when simple == typeof(bool) => "Debe ser true o false.",
            _ when simple.IsClass => "Debe ser un objeto ({ … }).",
            _ => "El valor no tiene el tipo esperado.",
        };
    }
}
