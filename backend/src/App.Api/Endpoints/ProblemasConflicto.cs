using Microsoft.AspNetCore.Http.HttpResults;

namespace App.Api.Endpoints;

/// <summary>
/// TAREA-27 (pendiente 67): 409 con la extensión estable <c>tipoConflicto</c>, para que el frontend decida por un valor fijo
/// y no por el texto del mensaje. Hoy lo usan solo las novedades (pendiente 76: alta de usuarios).
/// </summary>
public static class ProblemasConflicto
{
    public const string Extension = "tipoConflicto";

    /// <summary>Valores de <see cref="Extension"/> (camelCase, como el resto del JSON).</summary>
    public static class Tipos
    {
        public const string Duplicado = "duplicado";
        public const string Cambio = "cambio";
        public const string Anulada = "anulada";
        public const string RegistroConcurrente = "registroConcurrente";
    }

    /// <summary>409 con el title, <c>tipoConflicto</c> y las extensiones adicionales (p. ej. <c>codigo</c>).</summary>
    public static ProblemHttpResult Conflicto(string titulo, string tipoConflicto, IReadOnlyDictionary<string, object?>? extensiones = null)
    {
        var todas = new Dictionary<string, object?> { [Extension] = tipoConflicto };
        foreach (var (clave, valor) in extensiones ?? new Dictionary<string, object?>())
        {
            todas[clave] = valor;
        }

        return TypedResults.Problem(title: titulo, statusCode: StatusCodes.Status409Conflict, extensions: todas);
    }
}
