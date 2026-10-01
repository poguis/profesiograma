using System.Globalization;

namespace App.Application.Proyectos.Crear;

/// <summary>
/// RN09: rangos del almuerzo (coinciden con CK_Proyecto_Almuerzo: regreso &gt; salida). Fuente ÚNICA para
/// CrearProyectoValidador y para las opciones del formulario (GET /api/proyectos/opciones-formulario).
/// Los parámetros ALMUERZO_SALIDA_OPCIONES / ALMUERZO_REGRESO_OPCIONES no se usan (pendiente en 00_ESTADO_ACTUAL §7).
/// </summary>
public static class ReglasAlmuerzo
{
    public static readonly TimeOnly SalidaMinima = new(11, 0), SalidaMaxima = new(14, 0);
    public static readonly TimeOnly RegresoMinimo = new(12, 0), RegresoMaximo = new(15, 0);

    /// <summary>Opciones de salida cada hora ("HH:mm"): 11:00, 12:00, 13:00, 14:00.</summary>
    public static IReadOnlyList<string> OpcionesSalida { get; } = GenerarOpciones(SalidaMinima, SalidaMaxima);

    /// <summary>Opciones de regreso cada hora ("HH:mm"): 12:00, 13:00, 14:00, 15:00.</summary>
    public static IReadOnlyList<string> OpcionesRegreso { get; } = GenerarOpciones(RegresoMinimo, RegresoMaximo);

    public static bool SalidaEnRango(TimeOnly salida) => salida >= SalidaMinima && salida <= SalidaMaxima;

    public static bool RegresoEnRango(TimeOnly regreso) => regreso >= RegresoMinimo && regreso <= RegresoMaximo;

    public static string Formato(TimeOnly hora) => hora.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Horas en punto dentro de [desde, hasta] (los rangos de RN09 empiezan y terminan en punto).</summary>
    private static List<string> GenerarOpciones(TimeOnly desde, TimeOnly hasta) =>
        Enumerable.Range(desde.Hour, hasta.Hour - desde.Hour + 1)
            .Select(h => Formato(new TimeOnly(h, 0)))
            .ToList();
}
