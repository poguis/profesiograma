using System.Globalization;
using App.Application.Comun;
using App.Application.Erp;
using static App.Application.Proyectos.Crear.ReglasPersonal;

namespace App.Application.Proyectos.Crear;

/// <summary>
/// Reglas de horario (ERP) y almuerzo (RN09) comunes a "Crear proyecto" y "Editar cabecera" (TAREA-18, C5).
/// Extraídas de CrearProyectoValidador sin cambiar los mensajes. TAREA-26c: horarios no laborales, turno nocturno y
/// almuerzo opcional.
/// </summary>
internal static class ReglasHorarioAlmuerzo
{
    public const string MensajeSalidaObligatoria = "La hora de salida a almuerzo es obligatoria.";
    public const string MensajeRegresoObligatorio = "La hora de regreso de almuerzo es obligatoria.";
    public const string MensajeRegresoPosterior = "El regreso de almuerzo debe ser posterior a la salida.";
    public const string MensajeCruzaMedianoche = "El almuerzo no puede cruzar la medianoche.";

    public static string MensajeFueraDelTurno(TimeOnly entrada, TimeOnly salida) =>
        $"El almuerzo debe estar dentro del turno ({ReglasAlmuerzo.Formato(entrada)}–{ReglasAlmuerzo.Formato(salida)}).";

    private static readonly string[] FormatosHora = ["HH:mm", "HH:mm:ss"];

    /// <summary>
    /// Horario del catálogo ERP (solo status "A") y seleccionable (TAREA-26c: ReglasHorario). null y error en
    /// "horarioCodigo" si falta, no existe o no es laborable.
    /// </summary>
    public static async Task<HorarioErp?> ValidarHorarioAsync(int? codigo, ICatalogoErp erp, IDatosReferenciaProyecto datos,
        Dictionary<string, List<string>> e, CancellationToken ct)
    {
        if (codigo is not int horarioCodigo)
        {
            Agregar(e, "horarioCodigo", "El horario es obligatorio.");
            return null;
        }

        // El parámetro se lee de la base en cada validación: un cambio aplica aunque la lista del ERP esté en caché.
        var horario = await erp.ObtenerHorarioAsync(horarioCodigo, ct);
        if (horario is null || !ReglasHorario.EsSeleccionable(horario, await datos.ObtenerHorariosNoLaboralesAsync(ct)))
        {
            Agregar(e, "horarioCodigo", ReglasHorario.MensajeNoSeleccionable);
            return null;
        }

        return horario;
    }

    /// <summary>Hora "HH:mm" (o "HH:mm:ss"). Vacía: null sin error (lo decide ValidarPresencia); ilegible: "Use el formato HH:mm.".</summary>
    public static TimeOnly? LeerHora(string? valor, string clave, Dictionary<string, List<string>> e)
    {
        var texto = LectorParametros.Normalizar(valor);
        if (texto is null)
        {
            return null;
        }

        if (TimeOnly.TryParseExact(texto, FormatosHora, CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora))
        {
            return hora;
        }

        Agregar(e, clave, "Use el formato HH:mm.");
        return null;
    }

    /// <summary>
    /// Horas obligatorias. Sin almuerzo solo si es opcional (ReglasAlmuerzo.EsOpcional) y faltan las dos; con una sola
    /// hora, la otra es obligatoria. No repite el error de un campo ilegible.
    /// </summary>
    public static void ValidarPresencia(TimeOnly? salida, TimeOnly? regreso, bool opcional, Dictionary<string, List<string>> e)
    {
        if (opcional && salida is null && regreso is null)
        {
            return;
        }

        if (salida is null && !e.ContainsKey("salidaAlmuerzo"))
        {
            Agregar(e, "salidaAlmuerzo", MensajeSalidaObligatoria);
        }

        if (regreso is null && !e.ContainsKey("regresoAlmuerzo"))
        {
            Agregar(e, "regresoAlmuerzo", MensajeRegresoObligatorio);
        }
    }

    /// <summary>
    /// RN09 (solo las horas indicadas en validarSalida / validarRegreso). Turno diurno: rangos de ReglasAlmuerzo y
    /// regreso &gt; salida. Turno nocturno (TAREA-26c): dentro del turno en tiempo circular, regreso posterior en ese eje
    /// y sin cruzar la medianoche (CK_Proyecto_Almuerzo).
    /// </summary>
    public static void ValidarAlmuerzo(TimeOnly? salida, TimeOnly? regreso, bool validarSalida, bool validarRegreso,
        TimeOnly? entradaTurno, TimeOnly? salidaTurno, Dictionary<string, List<string>> e)
    {
        if (ReglasAlmuerzo.EsNocturno(entradaTurno, salidaTurno))
        {
            ValidarAlmuerzoNocturno(salida, regreso, validarSalida, validarRegreso, entradaTurno!.Value, salidaTurno!.Value, e);
            return;
        }

        if (validarSalida && salida is TimeOnly sal && !ReglasAlmuerzo.SalidaEnRango(sal))
        {
            Agregar(e, "salidaAlmuerzo",
                $"La salida a almuerzo debe estar entre {ReglasAlmuerzo.Formato(ReglasAlmuerzo.SalidaMinima)} y {ReglasAlmuerzo.Formato(ReglasAlmuerzo.SalidaMaxima)}.");
        }

        if (validarRegreso && regreso is TimeOnly reg && !ReglasAlmuerzo.RegresoEnRango(reg))
        {
            Agregar(e, "regresoAlmuerzo",
                $"El regreso de almuerzo debe estar entre {ReglasAlmuerzo.Formato(ReglasAlmuerzo.RegresoMinimo)} y {ReglasAlmuerzo.Formato(ReglasAlmuerzo.RegresoMaximo)}.");
        }

        if (salida is TimeOnly s1 && regreso is TimeOnly r1 && r1 <= s1)
        {
            Agregar(e, "regresoAlmuerzo", MensajeRegresoPosterior);
        }
    }

    private static void ValidarAlmuerzoNocturno(TimeOnly? salida, TimeOnly? regreso, bool validarSalida, bool validarRegreso,
        TimeOnly entrada, TimeOnly fin, Dictionary<string, List<string>> e)
    {
        var salidaFuera = salida is TimeOnly sal && !ReglasAlmuerzo.SalidaEnTurno(sal, entrada, fin);
        var regresoFuera = regreso is TimeOnly reg && !ReglasAlmuerzo.RegresoEnTurno(reg, entrada, fin);
        if (validarSalida && salidaFuera)
        {
            Agregar(e, "salidaAlmuerzo", MensajeFueraDelTurno(entrada, fin));
        }

        if (validarRegreso && regresoFuera)
        {
            Agregar(e, "regresoAlmuerzo", MensajeFueraDelTurno(entrada, fin));
        }

        if (salida is not TimeOnly s1 || regreso is not TimeOnly r1 || salidaFuera || regresoFuera)
        {
            return;
        }

        if (ReglasAlmuerzo.Posicion(r1, entrada) <= ReglasAlmuerzo.Posicion(s1, entrada))
        {
            Agregar(e, "regresoAlmuerzo", MensajeRegresoPosterior);
        }
        else if (r1 <= s1)
        {
            Agregar(e, "regresoAlmuerzo", MensajeCruzaMedianoche);
        }
    }
}
