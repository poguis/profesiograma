namespace App.Application.Proyectos;

/// <summary>
/// Token de concurrencia desde el cliente (TAREA-19x, pendiente 32): última versión de etapa del proyecto (0 si no hay
/// etapas). Sirve porque toda escritura de un proyecto crea una etapa (00_ESTADO_ACTUAL §7.1, regla 3).
/// Los GET de formulario y las vistas previas lo devuelven (leído ANTES que los datos: si alguien escribe en medio, el
/// token queda más viejo que los datos y el registro da 409). Los registros lo exigen:
///  1) ausente → 400 <see cref="Clave"/> (antes de leer la base);
///  2) distinto de la versión actual → 409 "El proyecto cambió; vuelve a cargarlo." (antes de cualquier 400, "No hay
///     cambios" o cruces);
///  3) se vuelve a comparar dentro del applock, antes de calcular la versión nueva (comprobación definitiva).
/// </summary>
public static class VersionProyecto
{
    public const string Clave = "versionProyecto";
    public const string MensajeFalta = "Falta la versión del proyecto; vuelve a cargarlo.";

    public static IReadOnlyDictionary<string, string[]> ErroresFalta() =>
        new Dictionary<string, string[]> { [Clave] = [MensajeFalta] };
}
