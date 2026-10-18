using App.Application.Empleados;

namespace App.Application.Erp;

/// <summary>
/// TAREA-26c (pendiente 41): único punto de decisión de qué horarios del ERP se pueden elegir. Lo usan la lista
/// (GET /api/erp/horarios) y la validación de crear y de editar la cabecera.
/// No laboral = descripción en Parametro HORARIOS_NO_LABORALES (sin distinguir mayúsculas ni tildes, espacios colapsados,
/// igual que la comparación con el ERP) o entrada = salida con 0 minutos trabajados (o nulos).
/// </summary>
public static class ReglasHorario
{
    public const string MensajeNoSeleccionable = "El horario no es laborable o no está activo.";

    public static bool EsSeleccionable(HorarioErp horario, IReadOnlyCollection<string> noLaborales)
    {
        ArgumentNullException.ThrowIfNull(horario);
        ArgumentNullException.ThrowIfNull(noLaborales);
        var porLista = noLaborales.Any(n => ComparacionEmpleadosErp.Iguales(n, horario.Descripcion));
        var porDatos = horario.HoraEntrada == horario.HoraSalida && (horario.MinutosTrabajados ?? 0) == 0;
        return !porLista && !porDatos;
    }
}
