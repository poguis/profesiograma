using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using App.Application.Comun;
using App.Domain.Novedades;

namespace App.Application.Novedades.Sincronizacion;

/// <summary>
/// Fila de PERMISO MEDICO ya validada. Correo = correo de la persona: SOLO para resolver al empleado (nunca se guarda ni
/// se registra). DetalleOrigenJson = lista blanca N13, serializada de forma determinista (para comparar sin escribir).
/// </summary>
public sealed record PermisoMedicoMapeado(
    Guid Uid,
    int? LegacyId,
    string Codigo,
    bool Activa,
    string? Ekon,
    string? Correo,
    IReadOnlyList<DateOnly> Dias,
    string? OrigenArea,
    string? RegistradoPorNombre,
    string? RegistradoPorCorreo,
    DateTime? FechaRegistroUtc,
    string DetalleOrigenJson)
{
    public DateOnly FechaInicio => Dias[0];
    public DateOnly FechaFin => Dias[^1];

    /// <summary>El correo de la persona no se imprime nunca.</summary>
    public override string ToString() => $"PermisoMedicoMapeado({Codigo}, {Uid})";
}

/// <summary>Fila omitida: categoría y referencia (código NOV- o «fila ID n»; nunca datos personales).</summary>
public sealed record OmisionSincronizacion(string Categoria, string Referencia);

public static class CategoriaOmision
{
    public const string FilaIlegible = "filaIlegible", UidInvalido = "uidInvalido", JsonInvalido = "jsonInvalido",
        OtroOrigen = "otroOrigen", TipoNoSoportado = "tipoNoSoportado", AplicacionNoSoportada = "aplicacionNoSoportada",
        FechasInvalidas = "fechasInvalidas", SinEmpleado = "sinEmpleado", ConflictoCodigo = "conflictoCodigo",
        ConflictoLegacyId = "conflictoLegacyId", NoActivaSinRegistro = "noActivaSinRegistro";
}

/// <summary>
/// DETALLE_JSON de NOVEDADES ASISTENCIA → PermisoMedicoMapeado u omisión (TAREA-23-3, lógica pura). Reutilizable en la
/// Fase 3 (carga masiva). Reglas: Uid legible; OrigenPermiso = aplicacionPermisosMedicos; TipoNovedad = PERMISO MEDICO;
/// TipoAplicacion = PERSONA; días de Dias[].Fecha (sin repetidos) o, si no vienen, el rango de Fechas; rango ≤ 366 días;
/// empleado por Fechas.Ekon → Persona.Ekon → correo (la resolución contra la base y la API la hace el servicio).
/// No usa las columnas FECHA_INICIO/FECHA_FIN (traen Z) ni lee Persona.Nombre.
/// </summary>
public static partial class MapeoPermisoMedico
{
    public const string OrigenPermisosMedicos = "aplicacionPermisosMedicos";
    public const string EstadoActiva = "ACTIVA";
    public const int LargoCodigo = 30, LargoOrigenArea = 30, LargoNombre = 200, LargoCorreo = 256;

    private const string FormatoFecha = "yyyy-MM-dd";
    private const string FormatoRegistro = "yyyy-MM-dd HH:mm:ss";

    /// <summary>Resultado: Mapeado o Omision (uno de los dos).</summary>
    public sealed record Resultado(PermisoMedicoMapeado? Mapeado, OmisionSincronizacion? Omision);

    public static Resultado Mapear(FilaNovedadExterna fila)
    {
        ArgumentNullException.ThrowIfNull(fila);
        var codigoFuente = Texto(fila.Codigo, LargoCodigo);
        var referencia = codigoFuente ?? (fila.LegacyId is int id ? $"fila ID {id}" : "fila sin ID");
        Resultado Omitir(string categoria) => new(null, new OmisionSincronizacion(categoria, referencia));

        if (!Guid.TryParse(fila.Uid?.Trim(), out var uid) || uid == Guid.Empty)
        {
            return Omitir(CategoriaOmision.UidInvalido);
        }

        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(fila.DetalleJson ?? string.Empty);
        }
        catch (JsonException)
        {
            return Omitir(CategoriaOmision.JsonInvalido);
        }

        using (documento)
        {
            var raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object)
            {
                return Omitir(CategoriaOmision.JsonInvalido);
            }

            if (!string.Equals(Cadena(raiz, "OrigenPermiso"), OrigenPermisosMedicos, StringComparison.OrdinalIgnoreCase))
            {
                return Omitir(CategoriaOmision.OtroOrigen);
            }

            if (!EsPermisoMedico(Cadena(raiz, "TipoNovedad")))
            {
                return Omitir(CategoriaOmision.TipoNoSoportado);
            }

            if (!string.Equals(Cadena(raiz, "TipoAplicacion"), AplicacionNovedad.Persona, StringComparison.OrdinalIgnoreCase))
            {
                return Omitir(CategoriaOmision.AplicacionNoSoportada);
            }

            var fechas = Objeto(raiz, "Fechas");
            var persona = Objeto(raiz, "Persona");
            var registro = Objeto(raiz, "Registro");

            if (Dias(raiz, fechas) is not { } dias)
            {
                return Omitir(CategoriaOmision.FechasInvalidas);
            }

            var ekon = NormalizarEkon(Cadena(fechas, "Ekon")) ?? NormalizarEkon(Cadena(persona, "Ekon"));
            var correo = NormalizarCorreo(Cadena(persona, "Correo")) ?? NormalizarCorreo(fila.CorreoPersona);
            if (ekon is null && correo is null)
            {
                return Omitir(CategoriaOmision.SinEmpleado);
            }

            var fechaRegistro = FechaRegistro(Cadena(registro, "FechaRegistro"));
            var codigo = codigoFuente ?? GeneradorCodigoNovedad.Generar(
                fechaRegistro is { } fr ? DateOnly.FromDateTime(fr.Local) : dias[0], uid);

            return new Resultado(new PermisoMedicoMapeado(
                uid,
                fila.LegacyId,
                codigo,
                string.Equals(fila.Estado?.Trim(), EstadoActiva, StringComparison.OrdinalIgnoreCase),
                ekon,
                correo,
                dias,
                Texto(Cadena(raiz, "Origen"), LargoOrigenArea),
                Texto(Cadena(registro, "UsuarioRegistro"), LargoNombre),
                Texto(Cadena(registro, "CorreoRegistro"), LargoCorreo),
                fechaRegistro?.Utc,
                DetalleOrigen(raiz, fechas, registro)), null);
        }
    }

    /// <summary>«PERMISO MEDICO» o «PERMISO_MEDICO» (nombre o código del TipoNovedad 7), sin distinguir mayúsculas ni tildes.</summary>
    public static bool EsPermisoMedico(string? tipo)
    {
        if (tipo is null)
        {
            return false;
        }

        var sinTildes = new string(tipo.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark).ToArray());
        return string.Equals(sinTildes.Trim().Replace('_', ' '), "PERMISO MEDICO", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>EKON sin espacios; con puntos de miles («2.448», C3) se quitan [SUPUESTO, pendiente 64].</summary>
    public static string? NormalizarEkon(string? ekon)
    {
        var valor = ekon?.Trim();
        if (string.IsNullOrEmpty(valor))
        {
            return null;
        }

        return MilesConPuntos().IsMatch(valor) ? valor.Replace(".", string.Empty, StringComparison.Ordinal) : valor;
    }

    /// <summary>Correo en minúsculas si tiene formato válido (pendiente 45); si no, null.</summary>
    public static string? NormalizarCorreo(string? correo)
    {
        var valor = correo?.Trim().ToLowerInvariant();
        return !string.IsNullOrEmpty(valor) && FormatoCorreo().IsMatch(valor) ? valor : null;
    }

    /// <summary>Registro.FechaRegistro en hora de Ecuador [SUPUESTO] → UTC truncada a segundos; ilegible → null.</summary>
    public static (DateTime Local, DateTime Utc)? FechaRegistro(string? texto)
    {
        if (!DateTime.TryParseExact(texto?.Trim(), FormatoRegistro, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return null;
        }

        var utc = FechaNegocio.AUtc(local);
        return (local, new DateTime(utc.Ticks - utc.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc));
    }

    /// <summary>Días: Dias[].Fecha (sin repetidos) o el rango de Fechas; null si alguna fecha es ilegible o el rango supera 366 días.</summary>
    private static List<DateOnly>? Dias(JsonElement raiz, JsonElement? fechas)
    {
        var lista = new SortedSet<DateOnly>();
        if (raiz.TryGetProperty("Dias", out var dias) && dias.ValueKind == JsonValueKind.Array && dias.GetArrayLength() > 0)
        {
            foreach (var dia in dias.EnumerateArray())
            {
                if (dia.ValueKind != JsonValueKind.Object || Fecha(Cadena(dia, "Fecha")) is not { } fecha)
                {
                    return null;
                }

                lista.Add(fecha);
            }
        }
        else
        {
            if (Fecha(Cadena(fechas, "FechaInicio")) is not { } inicio || Fecha(Cadena(fechas, "FechaFin")) is not { } fin
                || fin < inicio || DiasNovedad.Contar(inicio, fin) > DiasNovedad.MaxDias)
            {
                return null;
            }

            lista.UnionWith(DiasNovedad.Generar(inicio, fin));
        }

        return DiasNovedad.Contar(lista.Min, lista.Max) > DiasNovedad.MaxDias ? null : [.. lista];
    }

    /// <summary>N13: lista blanca en orden fijo, sin Persona.* (determinista: misma entrada → mismo texto).</summary>
    private static string DetalleOrigen(JsonElement raiz, JsonElement? fechas, JsonElement? registro)
    {
        using var flujo = new MemoryStream();
        using (var w = new Utf8JsonWriter(flujo))
        {
            w.WriteStartObject();
            Copiar(w, "NovedadUID", raiz);
            Copiar(w, "TipoAplicacion", raiz);
            Copiar(w, "TipoNovedad", raiz);
            Copiar(w, "OrigenPermiso", raiz);
            Copiar(w, "Origen", raiz);
            w.WriteStartObject("Fechas");
            Copiar(w, "FechaInicio", fechas);
            Copiar(w, "FechaFin", fechas);
            Copiar(w, "Ekon", fechas);
            w.WriteEndObject();
            w.WriteStartArray("Dias");
            if (raiz.TryGetProperty("Dias", out var dias) && dias.ValueKind == JsonValueKind.Array)
            {
                foreach (var dia in dias.EnumerateArray())
                {
                    w.WriteStartObject();
                    Copiar(w, "Fecha", dia);
                    w.WriteEndObject();
                }
            }

            w.WriteEndArray();
            w.WriteStartObject("Registro");
            Copiar(w, "FechaRegistro", registro);
            w.WriteEndObject();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(flujo.ToArray());
    }

    private static void Copiar(Utf8JsonWriter w, string nombre, JsonElement? origen)
    {
        if (Cadena(origen, nombre) is { } valor)
        {
            w.WriteString(nombre, valor);
        }
        else
        {
            w.WriteNull(nombre);
        }
    }

    /// <summary>Valor de una propiedad como texto (un número se toma como su texto); ausente o null → null.</summary>
    private static string? Cadena(JsonElement? objeto, string nombre)
    {
        if (objeto is not { ValueKind: JsonValueKind.Object } o || !o.TryGetProperty(nombre, out var valor))
        {
            return null;
        }

        return valor.ValueKind switch
        {
            JsonValueKind.String => valor.GetString(),
            JsonValueKind.Number => valor.GetRawText(),
            _ => null,
        };
    }

    private static JsonElement? Objeto(JsonElement raiz, string nombre) =>
        raiz.TryGetProperty(nombre, out var valor) && valor.ValueKind == JsonValueKind.Object ? valor : null;

    private static DateOnly? Fecha(string? texto) =>
        DateOnly.TryParseExact(texto?.Trim(), FormatoFecha, CultureInfo.InvariantCulture, DateTimeStyles.None, out var f) ? f : null;

    private static string? Texto(string? valor, int maximo)
    {
        var limpio = valor?.Trim();
        return string.IsNullOrEmpty(limpio) ? null : limpio.Length <= maximo ? limpio : limpio[..maximo];
    }

    [GeneratedRegex(@"^\d{1,3}(\.\d{3})+$")]
    private static partial Regex MilesConPuntos();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex FormatoCorreo();
}
