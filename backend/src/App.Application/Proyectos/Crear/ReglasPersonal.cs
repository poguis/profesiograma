using System.Globalization;
using App.Application.Comun;
using App.Domain.Proyectos.Cronograma;

namespace App.Application.Proyectos.Crear;

/// <summary>
/// Reglas de personal comunes a "Crear proyecto" (CrearProyectoValidador) y "Actualizar personal"
/// (EdicionPersonalValidador). Extraídas sin cambiar los mensajes de la creación (TAREA-17, D9).
/// </summary>
internal static class ReglasPersonal
{
    /// <summary>RN08: fechas obligatorias, fin ≥ inicio y dentro del rango del proyecto.</summary>
    public static (DateOnly Inicio, DateOnly Fin)? ValidarFechasPersona(DateOnly? inicio, DateOnly? fin, string clave,
        (DateOnly Inicio, DateOnly Fin)? proyecto, Dictionary<string, List<string>> e)
    {
        if (inicio is null)
        {
            Agregar(e, $"{clave}.fechaInicio", "La fecha de inicio es obligatoria.");
        }

        if (fin is null)
        {
            Agregar(e, $"{clave}.fechaFin", "La fecha fin es obligatoria.");
        }

        if (inicio is not DateOnly i || fin is not DateOnly f)
        {
            return null;
        }

        if (f < i)
        {
            Agregar(e, $"{clave}.fechaFin", "La fecha fin debe ser mayor o igual a la fecha de inicio.");
            return null;
        }

        if (proyecto is (DateOnly pi, DateOnly pf) && (i < pi || f > pf))
        {
            var mensaje = $"Las fechas deben estar dentro del rango del proyecto ({Formato(pi)} – {Formato(pf)}).";
            Agregar(e, i < pi ? $"{clave}.fechaInicio" : $"{clave}.fechaFin", mensaje);
            return null;
        }

        return (i, f);
    }

    public static JornadaRef? ValidarJornada(string? codigo, string clave, IReadOnlyDictionary<string, JornadaRef> jornadas,
        Dictionary<string, List<string>> e)
    {
        var codigoJornada = LectorParametros.Normalizar(codigo);
        if (codigoJornada is null)
        {
            Agregar(e, $"{clave}.jornada", "La jornada es obligatoria.");
            return null;
        }

        if (!jornadas.TryGetValue(codigoJornada, out var jornada))
        {
            Agregar(e, $"{clave}.jornada", $"La jornada '{codigoJornada}' no existe.");
            return null;
        }

        return jornada;
    }

    public static TipoRegistroBack? ValidarTipoRegistro(string? valor, string clave, Dictionary<string, List<string>> e)
    {
        TipoRegistroBack? tipo = LectorParametros.Normalizar(valor)?.ToUpperInvariant() switch
        {
            "JORNADA" => TipoRegistroBack.Jornada,
            "DESCANSO" => TipoRegistroBack.Descanso,
            _ => null,
        };
        if (tipo is null)
        {
            Agregar(e, $"{clave}.tipoRegistro", "El tipo de registro debe ser JORNADA o DESCANSO.");
        }

        return tipo;
    }

    /// <summary>Días de descanso del back entre 0 y BACK_MAX_DIAS_DESCANSO (null = 0).</summary>
    public static int ValidarDiasDescanso(int? valor, string clave, int maximo, Dictionary<string, List<string>> e)
    {
        var dias = valor ?? 0;
        if (dias < 0 || dias > maximo)
        {
            Agregar(e, $"{clave}.diasDescanso", $"Los días de descanso deben estar entre 0 y {maximo}.");
        }

        return dias;
    }

    public static string? ValidarLargo(string? valor, string clave, int maximo, string nombre, Dictionary<string, List<string>> e)
    {
        var texto = LectorParametros.Normalizar(valor);
        if (texto is not null && texto.Length > maximo)
        {
            Agregar(e, clave, $"{nombre} admite como máximo {maximo} caracteres.");
        }

        return texto;
    }

    public static string Formato(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static void Agregar(Dictionary<string, List<string>> e, string clave, string mensaje)
    {
        if (!e.TryGetValue(clave, out var lista))
        {
            e[clave] = lista = [];
        }

        lista.Add(mensaje);
    }

    public static IReadOnlyDictionary<string, string[]> Congelar(Dictionary<string, List<string>> e) =>
        e.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray());
}
