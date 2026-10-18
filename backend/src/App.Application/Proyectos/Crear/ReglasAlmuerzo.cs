using System.Globalization;

namespace App.Application.Proyectos.Crear;

/// <summary>
/// RN09: rangos del almuerzo (coinciden con CK_Proyecto_Almuerzo: regreso &gt; salida). Fuente ÚNICA para
/// CrearProyectoValidador y para las opciones del formulario (GET /api/proyectos/opciones-formulario).
/// Los parámetros ALMUERZO_SALIDA_OPCIONES / ALMUERZO_REGRESO_OPCIONES no se usan (pendiente en 00_ESTADO_ACTUAL §7).
/// TAREA-26c: los rangos fijos son del turno diurno; en el turno nocturno (salida &lt; entrada, incluida la salida 00:00)
/// el almuerzo va dentro del turno en tiempo circular. El frontend replica estas reglas en almuerzoTurno.ts.
/// </summary>
public static class ReglasAlmuerzo
{
    public static readonly TimeOnly SalidaMinima = new(11, 0), SalidaMaxima = new(14, 0);
    public static readonly TimeOnly RegresoMinimo = new(12, 0), RegresoMaximo = new(15, 0);
    private const int MinutosDia = 24 * 60;

    /// <summary>Opciones de salida cada hora ("HH:mm"): 11:00, 12:00, 13:00, 14:00.</summary>
    public static IReadOnlyList<string> OpcionesSalida { get; } = GenerarOpciones(SalidaMinima, SalidaMaxima);

    /// <summary>Opciones de regreso cada hora ("HH:mm"): 12:00, 13:00, 14:00, 15:00.</summary>
    public static IReadOnlyList<string> OpcionesRegreso { get; } = GenerarOpciones(RegresoMinimo, RegresoMaximo);

    public static bool SalidaEnRango(TimeOnly salida) => salida >= SalidaMinima && salida <= SalidaMaxima;

    public static bool RegresoEnRango(TimeOnly regreso) => regreso >= RegresoMinimo && regreso <= RegresoMaximo;

    /// <summary>Turno nocturno: salida anterior a la entrada (la salida 00:00 es el fin del día).</summary>
    public static bool EsNocturno(TimeOnly? entrada, TimeOnly? salida) =>
        entrada is TimeOnly en && salida is TimeOnly sa && sa < en;

    /// <summary>Almuerzo opcional: el ERP no descuenta tiempo (minutos de jornada = minutos trabajados).</summary>
    public static bool EsOpcional(short? minutosJornada, short? minutosTrabajados) =>
        minutosJornada is short j && j == minutosTrabajados;

    /// <summary>Minutos desde la entrada en tiempo circular: (t − entrada + 24h) mod 24h.</summary>
    public static int Posicion(TimeOnly hora, TimeOnly entrada) =>
        ((int)(hora - entrada).TotalMinutes + MinutosDia) % MinutosDia;

    /// <summary>Salida a almuerzo dentro del turno nocturno: desde la entrada y antes de la salida del turno.</summary>
    public static bool SalidaEnTurno(TimeOnly salida, TimeOnly entrada, TimeOnly fin) => Posicion(salida, entrada) < Posicion(fin, entrada);

    /// <summary>Regreso de almuerzo dentro del turno nocturno: después de la entrada y hasta la salida del turno.</summary>
    public static bool RegresoEnTurno(TimeOnly regreso, TimeOnly entrada, TimeOnly fin) =>
        Posicion(regreso, entrada) is var p && p > 0 && p <= Posicion(fin, entrada);

    public static string Formato(TimeOnly hora) => hora.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Horas en punto dentro de [desde, hasta] (los rangos de RN09 empiezan y terminan en punto).</summary>
    private static List<string> GenerarOpciones(TimeOnly desde, TimeOnly hasta) =>
        Enumerable.Range(desde.Hour, hasta.Hour - desde.Hour + 1)
            .Select(h => Formato(new TimeOnly(h, 0)))
            .ToList();
}
