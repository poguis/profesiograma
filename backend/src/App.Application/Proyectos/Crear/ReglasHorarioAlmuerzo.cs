using System.Globalization;
using App.Application.Comun;
using App.Application.Erp;
using static App.Application.Proyectos.Crear.ReglasPersonal;

namespace App.Application.Proyectos.Crear;

/// <summary>
/// Reglas de horario (ERP) y almuerzo (RN09) comunes a "Crear proyecto" y "Editar cabecera" (TAREA-18, C5).
/// Extraídas de CrearProyectoValidador sin cambiar los mensajes.
/// </summary>
internal static class ReglasHorarioAlmuerzo
{
    public const string MensajeSalidaObligatoria = "La hora de salida a almuerzo es obligatoria.";
    public const string MensajeRegresoObligatorio = "La hora de regreso de almuerzo es obligatoria.";

    private static readonly string[] FormatosHora = ["HH:mm", "HH:mm:ss"];

    /// <summary>Horario del catálogo ERP (solo status "A"). null y error en "horarioCodigo" si falta o no existe.</summary>
    public static async Task<HorarioErp?> ValidarHorarioAsync(int? codigo, ICatalogoErp erp, Dictionary<string, List<string>> e,
        CancellationToken ct)
    {
        if (codigo is not int horarioCodigo)
        {
            Agregar(e, "horarioCodigo", "El horario es obligatorio.");
            return null;
        }

        var horario = await erp.ObtenerHorarioAsync(horarioCodigo, ct);
        if (horario is null)
        {
            Agregar(e, "horarioCodigo", "El horario no existe o no está activo.");
        }

        return horario;
    }

    /// <summary>Hora "HH:mm" (o "HH:mm:ss"). Vacía: error con el mensaje de obligatoria; ilegible: "Use el formato HH:mm.".</summary>
    public static TimeOnly? LeerHora(string? valor, string clave, string mensajeObligatorio, Dictionary<string, List<string>> e)
    {
        var texto = LectorParametros.Normalizar(valor);
        if (texto is null)
        {
            Agregar(e, clave, mensajeObligatorio);
            return null;
        }

        if (TimeOnly.TryParseExact(texto, FormatosHora, CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora))
        {
            return hora;
        }

        Agregar(e, clave, "Use el formato HH:mm.");
        return null;
    }

    /// <summary>RN09: rangos de ReglasAlmuerzo (solo de las horas indicadas en validarSalida / validarRegreso) y regreso &gt; salida.</summary>
    public static void ValidarAlmuerzo(TimeOnly? salida, TimeOnly? regreso, bool validarSalida, bool validarRegreso,
        Dictionary<string, List<string>> e)
    {
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
            Agregar(e, "regresoAlmuerzo", "El regreso de almuerzo debe ser posterior a la salida.");
        }
    }
}
