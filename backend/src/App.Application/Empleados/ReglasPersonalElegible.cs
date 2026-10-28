namespace App.Application.Empleados;

/// <summary>
/// TAREA-62 (pendiente 62): único punto de decisión de qué empleados se pueden elegir como personal NUEVO (buscador,
/// crear proyecto, actualizar personal, reactivar y novedades PERSONA). Criterio = Parametro EMPLEADO_FAMILIAS_ASIGNABLES
/// (LIST separada por comas) contra `familiaPuesto` del ERP (= el «Grupo» del original; confirmado con el sondeo de la
/// TAREA-62: ADMINISTRATIVO y OPERATIVO). Sin mayúsculas ni tildes y con espacios colapsados, como la comparación con el
/// ERP. Lista vacía = sin filtro. Las personas ya guardadas no pasan por aquí (la regla solo aplica a altas nuevas).
/// </summary>
public static class ReglasPersonalElegible
{
    public const string ClaveParametro = "EMPLEADO_FAMILIAS_ASIGNABLES";

    public static string MensajeNoAsignable(string codigoEkon) => $"El empleado {codigoEkon} no pertenece a un grupo asignable.";

    /// <summary>Lista vacía → todos. Con filtro, un empleado sin familia no es asignable.</summary>
    public static bool EsAsignable(EmpleadoErp empleado, IReadOnlyCollection<string> familias)
    {
        ArgumentNullException.ThrowIfNull(empleado);
        ArgumentNullException.ThrowIfNull(familias);
        return familias.Count == 0
               || (MapeoEmpleado.Normalizar(empleado.FamiliaPuesto) is { } familia
                   && familias.Any(f => ComparacionEmpleadosErp.Iguales(f, familia)));
    }
}
