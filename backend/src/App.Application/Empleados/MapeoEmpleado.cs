namespace App.Application.Empleados;

/// <summary>Valores que la alta puntual escribe en una fila de Empleado (ya normalizados y recortados a su columna).</summary>
public sealed record DatosEmpleado(
    string NombreCompleto,
    string? Apellidos,
    string? Nombres,
    string? CorreoEmpresa,
    string? CodEmpresa,
    string? Empresa,
    string? CodPuesto,
    string? Puesto,
    string? CodDepartamento,
    string? Departamento,
    string? CodUnidad,
    string? Unidad,
    string? CodArea,
    string? Area,
    string? CodSeccion,
    string? Seccion,
    string? FamiliaPuesto);

/// <summary>
/// Normalización y mapeo API → Empleado (TAREA-26d; reglas tomadas de la 26b): espacios extremos fuera, vacío → null,
/// textos recortados a su columna. Un registro de la API es VÁLIDO si está activo ("A"), tiene código (≤ 20), nombre y
/// códigos cod* de ≤ 10 caracteres; los no válidos no entran a la lista (para la app, "no existe o no está activo").
/// </summary>
public static class MapeoEmpleado
{
    public const string EstadoActivo = "A";
    public const int LargoCodigoEkon = 20;
    public const int LargoCodigo = 10;

    public static bool EsValido(EmpleadoErp e)
    {
        ArgumentNullException.ThrowIfNull(e);
        var codigo = Normalizar(e.CodigoEkon);
        return codigo is { Length: <= LargoCodigoEkon }
               && Normalizar(e.NombreCompleto) is not null
               && string.Equals(Normalizar(e.Estado), EstadoActivo, StringComparison.OrdinalIgnoreCase)
               && new[] { e.CodEmpresa, e.CodPuesto, e.CodDepartamento, e.CodUnidad, e.CodArea, e.CodSeccion }
                   .All(c => Normalizar(c) is not { Length: > LargoCodigo });
    }

    /// <summary>Registro con los textos normalizados (código sin espacios). Se aplica a los válidos.</summary>
    public static EmpleadoErp Normalizado(EmpleadoErp e)
    {
        ArgumentNullException.ThrowIfNull(e);
        return e with { CodigoEkon = Normalizar(e.CodigoEkon), NombreCompleto = Normalizar(e.NombreCompleto), Puesto = Normalizar(e.Puesto) };
    }

    public static DatosEmpleado Datos(EmpleadoErp r)
    {
        ArgumentNullException.ThrowIfNull(r);
        return new DatosEmpleado(
            Recortar(Normalizar(r.NombreCompleto) ?? string.Empty, 200),
            Texto(r.Apellidos, 120),
            Texto(r.Nombres, 120),
            Texto(r.CorreoEmpresa, 256),
            Normalizar(r.CodEmpresa),
            Texto(r.Empresa, 200),
            Normalizar(r.CodPuesto),
            Texto(r.Puesto, 200),
            Normalizar(r.CodDepartamento),
            Texto(r.Departamento, 200),
            Normalizar(r.CodUnidad),
            Texto(r.Unidad, 200),
            Normalizar(r.CodArea),
            Texto(r.Area, 200),
            Normalizar(r.CodSeccion),
            Texto(r.Seccion, 200),
            Texto(r.FamiliaPuesto, 100));
    }

    /// <summary>Cargo de CargoInfor: mayúsculas y sin espacios extremos (UQ_CargoInfor_Cargo), máximo 200.</summary>
    public static string? NormalizarCargo(string? puesto) =>
        Normalizar(puesto) is { } cargo ? Recortar(cargo.ToUpperInvariant(), 200) : null;

    public static string? Normalizar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string? Texto(string? valor, int maximo) => Normalizar(valor) is { } texto ? Recortar(texto, maximo) : null;

    private static string Recortar(string valor, int maximo) => valor.Length <= maximo ? valor : valor[..maximo].TrimEnd();
}
