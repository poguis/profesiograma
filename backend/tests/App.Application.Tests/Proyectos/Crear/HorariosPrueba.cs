using App.Application.Erp;

namespace App.Application.Tests.Proyectos.Crear;

/// <summary>
/// TAREA-26c: horarios con los códigos, horas y descripciones observados en la B1 (datos de catálogo, no personales).
/// Los minutos trabajados que la B1 no detalló son ilustrativos; la jornada coincide con (salida − entrada + 24h) mod 24h.
/// </summary>
internal static class HorariosPrueba
{
    /// <summary>73: 17:00–04:00 (cruza la medianoche).</summary>
    public static readonly HorarioErp Nocturno73 = new(73, "HORARIO 73 (B1)", new TimeOnly(17, 0), new TimeOnly(4, 0), 660, 600, "M");

    /// <summary>118: 22:00–06:00 sin almuerzo (jornada = trabajados).</summary>
    public static readonly HorarioErp Nocturno118 = new(118, "HORARIO 118 (B1)", new TimeOnly(22, 0), new TimeOnly(6, 0), 480, 480, "M");

    /// <summary>110: 15:30–00:00 (la salida 00:00 es el fin del día).</summary>
    public static readonly HorarioErp Tarde110 = new(110, "HORARIO 110 (B1)", new TimeOnly(15, 30), TimeOnly.MinValue, 510, 450, "M");

    /// <summary>1: LIBRE (no laboral por lista y por datos: entrada = salida, 0 trabajados).</summary>
    public static readonly HorarioErp Libre1 = new(1, "LIBRE", TimeOnly.MinValue, TimeOnly.MinValue, 0, 0, "M");

    /// <summary>113: VACACIONES con horas normales (no laboral solo por lista).</summary>
    public static readonly HorarioErp Vacaciones113 = new(113, "VACACIONES", new TimeOnly(8, 0), new TimeOnly(17, 0), 540, 480, "M");

    /// <summary>160: PATERNIDAD PROYECTOS (no laboral solo por lista).</summary>
    public static readonly HorarioErp Paternidad160 = new(160, "PATERNIDAD PROYECTOS", new TimeOnly(8, 0), new TimeOnly(17, 0), 540, 480, "M");

    /// <summary>Ficticio: no está en la lista, pero entrada = salida y 0 trabajados (no laboral por datos).</summary>
    public static readonly HorarioErp PorDatos9005 = new(9005, "DESCANSO (PRUEBA)", TimeOnly.MinValue, TimeOnly.MinValue, 0, 0, "M");

    /// <summary>Horarios que los dobles del ERP agregan a los suyos (sin el 1, que en los dobles es 07:00–18:00).</summary>
    public static readonly HorarioErp[] Extra = [Nocturno73, Nocturno118, Tarde110, Vacaciones113, Paternidad160, PorDatos9005];
}
