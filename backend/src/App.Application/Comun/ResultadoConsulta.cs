namespace App.Application.Comun;

/// <summary>
/// Resultado de una consulta con validación de entrada: contiene el valor o los errores por campo
/// (formato compatible con ValidationProblem).
/// </summary>
public sealed class ResultadoConsulta<T>
{
    private ResultadoConsulta(T? valor, IReadOnlyDictionary<string, string[]>? errores)
    {
        Valor = valor;
        Errores = errores;
    }

    public T? Valor { get; }
    public IReadOnlyDictionary<string, string[]>? Errores { get; }
    public bool EsValido => Errores is null;

    public static ResultadoConsulta<T> Ok(T valor) => new(valor, null);

    public static ResultadoConsulta<T> Invalido(IReadOnlyDictionary<string, string[]> errores) => new(default, errores);
}
