using App.Application.Cronograma.Reporte;
using ClosedXML.Excel;

namespace App.Infrastructure.Reportes;

/// <summary>
/// Libro del reporte Excel por proyecto (TAREA-22-2, Z8), armado EN CÓDIGO con la estructura de la plantilla del original
/// (docs/PLANTILLA_REPORTE_PROYECTOS.xlsx): hoja 'PROYECTO ' (con espacio final), Tabla1 con TableStyleMedium2 y
/// autofiltro, los 14 encabezados exactos y los anchos de la plantilla. No se carga ni se distribuye la plantilla: así no
/// viajan sus metadatos personales ni los nombres definidos rotos (LABORABLE/LIBRE → #REF!).
/// Formatos por COLUMNA (no por celda): texto "@" en C, E y K; [h]:mm:ss (numFmtId 46) en F–I; General en el resto.
/// Todo en memoria (sin archivos temporales).
/// </summary>
internal sealed class EscritorReporteExcel : IEscritorReporteExcel
{
    public const string NombreHoja = "PROYECTO ";
    public const string NombreTabla = "Tabla1";

    /// <summary>Encabezados exactos de la plantilla ("ACTIVIDAD " y "CARGO INFOR " con espacio final).</summary>
    public static readonly IReadOnlyList<string> Encabezados =
    [
        "EMPRESA", "EKON", "CEDULA", "NOMBRE", "FECHA", "INGRESO DE JORNADA", "INGRESO ALMUERZO", "SALIDA ALMUERZO",
        "SALIDA DE JORNADA", "PROYECTO", "ACTIVIDAD ", "ESTADO", "OBSERVACION", "CARGO INFOR ",
    ];

    /// <summary>Anchos de la plantilla tal como están en su sheet1.xml (&lt;col width&gt;, 6 decimales).</summary>
    public static readonly IReadOnlyList<double> Anchos =
    [
        10.855469, 13.855469, 13.710938, 45.710938, 17.140625, 30.285156, 22.285156, 20.710938, 22.285156,
        16.140625, 17.425781, 17.425781, 17.425781, 22.285156,
    ];

    /// <summary>
    /// Margen interno que ClosedXML suma al ancho al guardar (fuente por defecto, Calibri 11; medido: 11,566094 − 10,855469).
    /// Se resta para que el &lt;col width&gt; del archivo sea el de la plantilla (lo verifica EscritorReporteExcelTests).
    /// </summary>
    public const double MargenAncho = 0.710625;

    /// <summary>Formato de horas de la plantilla: numFmtId 46 = [h]:mm:ss.</summary>
    public const int FormatoHoras = 46;

    private static readonly int[] ColumnasTexto = [3, 5, 11];      // CEDULA, FECHA, ACTIVIDAD
    private static readonly int[] ColumnasHora = [6, 7, 8, 9];     // ingreso, almuerzo (2) y salida

    public byte[] Escribir(IReadOnlyList<FilaReporte> filas)
    {
        ArgumentNullException.ThrowIfNull(filas);
        using var libro = new XLWorkbook();
        // Sin metadatos personales (la plantilla traía autor y correo).
        libro.Properties.Author = string.Empty;
        libro.Properties.LastModifiedBy = string.Empty;
        libro.Properties.Company = string.Empty;

        var hoja = libro.Worksheets.Add(NombreHoja);
        for (var c = 1; c <= Encabezados.Count; c++)
        {
            var columna = hoja.Column(c);
            columna.Width = Anchos[c - 1] - MargenAncho;
            if (ColumnasTexto.Contains(c))
            {
                columna.Style.NumberFormat.Format = "@";
            }
            else if (ColumnasHora.Contains(c))
            {
                columna.Style.NumberFormat.NumberFormatId = FormatoHoras;
            }

            hoja.Cell(1, c).Value = Encabezados[c - 1];
        }

        var fila = 2;
        foreach (var f in filas)
        {
            hoja.Cell(fila, 1).Value = NumeroOTexto(f.Empresa);
            hoja.Cell(fila, 2).Value = NumeroOTexto(f.Ekon);
            hoja.Cell(fila, 3).Value = Texto(f.Cedula);
            hoja.Cell(fila, 4).Value = f.Nombre;
            hoja.Cell(fila, 5).Value = f.Fecha;
            hoja.Cell(fila, 6).Value = f.IngresoJornada;
            hoja.Cell(fila, 7).Value = f.IngresoAlmuerzo;
            hoja.Cell(fila, 8).Value = f.SalidaAlmuerzo;
            hoja.Cell(fila, 9).Value = f.SalidaJornada;
            hoja.Cell(fila, 10).Value = NumeroOTexto(f.Proyecto);
            hoja.Cell(fila, 11).Value = Texto(f.Actividad);
            hoja.Cell(fila, 12).Value = f.Estado;
            hoja.Cell(fila, 13).Value = f.Observacion;
            hoja.Cell(fila, 14).Value = Texto(f.CargoInfor);
            fila++;
        }

        // Sin filas: A1:N2 con la fila de datos vacía, como la plantilla (una tabla de Excel necesita al menos una fila).
        var tabla = hoja.Range(1, 1, Math.Max(2, filas.Count + 1), Encabezados.Count).CreateTable(NombreTabla);
        tabla.Theme = XLTableTheme.TableStyleMedium2;
        tabla.ShowAutoFilter = true;

        using var salida = new MemoryStream();
        libro.SaveAs(salida);
        return salida.ToArray();
    }

    /// <summary>Z7: número solo si ArmadoReporte.EsNumeroExportable; si no, texto (vacío → celda en blanco).</summary>
    private static XLCellValue NumeroOTexto(string? texto) =>
        ArmadoReporte.EsNumeroExportable(texto, out var numero) ? (double)numero : Texto(texto);

    private static XLCellValue Texto(string? texto) => string.IsNullOrEmpty(texto) ? Blank.Value : texto;
}
