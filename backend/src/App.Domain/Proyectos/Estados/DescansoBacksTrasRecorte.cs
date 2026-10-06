using App.Domain.Proyectos.Cronograma;

namespace App.Domain.Proyectos.Estados;

/// <summary>Back que queda después del recorte, con su FechaFin resultante (recortada a F o la original).</summary>
public sealed record BackTrasRecorte(
    int Id, short Numero, int EmpleadoId, DateOnly FechaInicio, DateOnly FechaFin, TipoRegistroBack TipoRegistro, byte DiasDescanso);

/// <summary>Día DESCANSO MANUAL que se vuelve a insertar para un back (PersonalId = Id de ProyectoPersonal).</summary>
public sealed record DescansoAgregado(int PersonalId, DiaAsignado Dia);

/// <summary>
/// H15 (TAREA-18b): al acortar un proyecto ACTIVO (C4 de la edición de cabecera), RecorteProyecto borra los días &gt; F,
/// también el descanso posterior de los backs que quedan. El motor (R4) lo volvería a generar en la próxima
/// actualización de personal, porque el descanso posterior del back se guarda completo aunque pase de la fecha fin
/// del proyecto (P3 de la creación). Esta regla devuelve esos días para insertarlos en la misma escritura:
/// para TODOS los backs que quedan (recortados o no), sus días DESCANSO MANUAL posteriores (ReglasCronograma.TramosBack,
/// los mismos que emite el motor) con fecha &gt; F, deduplicados por (empleado, fecha).
/// SUSPENSION y CIERRE no la usan.
/// </summary>
public static class DescansoBacksTrasRecorte
{
    public static IReadOnlyList<DescansoAgregado> Calcular(DateOnly fecha, IReadOnlyList<BackTrasRecorte> backs)
    {
        ArgumentNullException.ThrowIfNull(backs);
        var vistos = new HashSet<(int, DateOnly)>();
        var resultado = new List<DescansoAgregado>();
        foreach (var b in backs)
        {
            var entrada = new BackEntrada(b.Numero, b.EmpleadoId, b.FechaInicio, b.FechaFin, b.TipoRegistro, b.DiasDescanso);
            foreach (var tramo in ReglasCronograma.TramosBack(entrada)
                         .Where(t => t.Rol == RolCronograma.Descanso && t.Tipo == TipoAsignacionCronograma.Manual && t.Fin > fecha))
            {
                for (var dia = tramo.Inicio > fecha ? tramo.Inicio : fecha.AddDays(1); dia <= tramo.Fin; dia = dia.AddDays(1))
                {
                    if (vistos.Add((b.EmpleadoId, dia)))
                    {
                        resultado.Add(new DescansoAgregado(b.Id,
                            new DiaAsignado(b.EmpleadoId, dia, RolCronograma.Descanso, TipoAsignacionCronograma.Manual, tramo.Bloque, tramo.Persona)));
                    }
                }
            }
        }

        return resultado;
    }
}
