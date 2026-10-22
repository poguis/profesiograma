using System.Globalization;
using System.Text;
using App.Application.Novedades.Sincronizacion;
using Microsoft.VisualBasic.FileIO;

namespace App.Infrastructure.NovedadesExternas;

/// <summary>
/// Fuente ArchivoLocal (TAREA-23-3, solo Development): el CSV exportado de la lista NOVEDADES ASISTENCIA, en la ruta de
/// user-secrets NovedadesExternas:RutaArchivo (nunca en el repo: contiene permisos médicos). Archivo ausente o ilegible
/// → 503 «no disponible» (sin la ruta en el mensaje).
/// </summary>
internal sealed class FuenteNovedadesArchivoLocal(string? ruta) : IFuenteNovedadesExternas
{
    public const string Nombre = "ArchivoLocal";

    public string Fuente => Nombre;
    public bool AdmiteEscenarios => false;

    public async Task<LecturaNovedadesExternas> LeerPermisosMedicosAsync(string? escenario, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(ruta))
        {
            throw FuenteNovedadesNoDisponibleException.NoDisponible("Falta NovedadesExternas:RutaArchivo en la configuración.");
        }

        string texto;
        try
        {
            // UTF-8 con detección de BOM (A2).
            using var lector = new StreamReader(ruta, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            texto = await lector.ReadToEndAsync(ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw FuenteNovedadesNoDisponibleException.NoDisponible("No se pudo leer el archivo de novedades externas.");
        }

        var (filas, ilegibles) = LectorCsvSharePoint.Leer(texto);
        return new LecturaNovedadesExternas(Nombre, filas, ilegibles);
    }
}

/// <summary>
/// CSV exportado de una lista de SharePoint (lógica pura). La primera línea puede ser un «ListSchema=…» (se salta). Campos
/// entre comillas, con comillas dobles escapadas y saltos de línea (TextFieldParser, P6). Solo toma las columnas que usa
/// la sincronización: ID, Título/Title, NOVEDAD_UID, ESTADO_NOVEDAD, DETALLE_JSON y CORREO_PERSONA; nunca «Creado por»
/// ni «Modificado por». Faltan columnas obligatorias → 503; una fila con otro número de columnas → ilegible.
/// </summary>
internal static class LectorCsvSharePoint
{
    private static readonly string[] Obligatorias = ["ID", "NOVEDAD_UID", "ESTADO_NOVEDAD", "DETALLE_JSON"];

    public static (IReadOnlyList<FilaNovedadExterna> Filas, int Ilegibles) Leer(string texto)
    {
        var contenido = texto.TrimStart('﻿');
        if (contenido.StartsWith("ListSchema=", StringComparison.Ordinal))
        {
            var salto = contenido.IndexOf('\n');
            contenido = salto < 0 ? string.Empty : contenido[(salto + 1)..];
        }

        using var parser = new TextFieldParser(new StringReader(contenido))
        {
            TextFieldType = FieldType.Delimited,
            HasFieldsEnclosedInQuotes = true,
            TrimWhiteSpace = false,
        };
        parser.SetDelimiters(",");

        var encabezado = parser.EndOfData ? null : parser.ReadFields();
        var columnas = (encabezado ?? []).Select((nombre, i) => (Nombre: nombre.Trim(), i))
            .GroupBy(c => c.Nombre, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().i, StringComparer.OrdinalIgnoreCase);
        var titulo = columnas.TryGetValue("Título", out var t) ? t : columnas.TryGetValue("Title", out var t2) ? t2 : -1;
        if (encabezado is null || titulo < 0 || Obligatorias.Any(c => !columnas.ContainsKey(c)))
        {
            throw FuenteNovedadesNoDisponibleException.NoDisponible("El archivo no tiene las columnas esperadas.");
        }

        var correo = columnas.TryGetValue("CORREO_PERSONA", out var c) ? c : -1;
        var filas = new List<FilaNovedadExterna>();
        var ilegibles = 0;
        while (!parser.EndOfData)
        {
            string[]? campos;
            try
            {
                campos = parser.ReadFields();
            }
            catch (MalformedLineException)
            {
                ilegibles++;
                continue;
            }

            if (campos is null || (campos.Length == 1 && string.IsNullOrWhiteSpace(campos[0])))
            {
                continue; // línea vacía
            }

            if (campos.Length != encabezado.Length)
            {
                ilegibles++;
                continue;
            }

            if (filas.Count >= FuenteNovedades.MaxFilas)
            {
                throw FuenteNovedadesNoDisponibleException.NoDisponible($"El archivo tiene más de {FuenteNovedades.MaxFilas} filas.");
            }

            filas.Add(new FilaNovedadExterna(
                int.TryParse(campos[columnas["ID"]].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null,
                campos[titulo],
                campos[columnas["NOVEDAD_UID"]],
                campos[columnas["ESTADO_NOVEDAD"]],
                campos[columnas["DETALLE_JSON"]],
                correo >= 0 ? campos[correo] : null));
        }

        return (filas, ilegibles);
    }
}
