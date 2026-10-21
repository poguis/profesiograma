using System.Diagnostics;
using App.Application.Cronograma.Reporte;
using App.Infrastructure.Reportes;
using ClosedXML.Excel;

namespace App.Infrastructure.Tests.Reportes;

/// <summary>
/// TAREA-22-2: el libro se genera EN MEMORIA y se vuelve a leer con ClosedXML (sin disco, sin red). Datos FICTICIOS
/// (cédulas "0…", códigos 9000xx). La plantilla del original solo se lee (estructura), nunca se modifica.
/// </summary>
public class EscritorReporteExcelTests(ITestOutputHelper salida)
{
    private static readonly TimeSpan H7 = new(7, 0, 0), H13 = new(13, 0, 0), H14 = new(14, 0, 0), H18 = new(18, 0, 0);

    private static FilaReporte Fila(string ekon = "900001", string? cedula = "0912345678", string fecha = "2/10/2026", string nombre = "PERSONA 01") =>
        new("9001", ekon, cedula, nombre, fecha, H7, H14, H13, H18, "120260053", "20", "LABORABLE", "CAMPO", "P0021");

    private static XLWorkbook Leer(byte[] archivo) => new(new MemoryStream(archivo));

    [Fact]
    public void Hoja_Tabla_Encabezados_TiposYFormatos()
    {
        var filas = new[] { Fila(), Fila("DEV001", "0000000002", "10/10/2026", "PERSONA 02"), Fila("900003", null, "11/10/2026", "PERSONA 03") };

        using var libro = Leer(new EscritorReporteExcel().Escribir(filas));

        var hoja = libro.Worksheet(EscritorReporteExcel.NombreHoja);
        Assert.Equal("PROYECTO ", hoja.Name);
        var tabla = Assert.Single(hoja.Tables);
        Assert.Equal(("Tabla1", "A1:N4", true), (tabla.Name, tabla.RangeAddress.ToString(), tabla.ShowAutoFilter));
        Assert.Equal(XLTableTheme.TableStyleMedium2.Name, tabla.Theme.Name);
        Assert.Equal(EscritorReporteExcel.Anchos, AnchosEnElArchivo(new EscritorReporteExcel().Escribir(filas))); // <col width> del XML
        Assert.Equal(EscritorReporteExcel.Encabezados, tabla.Fields.Select(f => f.Name));
        Assert.Contains("ACTIVIDAD ", tabla.Fields.Select(f => f.Name));
        Assert.Contains("CARGO INFOR ", tabla.Fields.Select(f => f.Name));

        // Textos que parecen número o fecha quedan como TEXTO; EKON numérico solo si cumple Z7.
        Assert.Equal((XLDataType.Text, "0912345678"), (hoja.Cell("C2").DataType, hoja.Cell("C2").GetText()));
        Assert.Equal((XLDataType.Text, "2/10/2026"), (hoja.Cell("E2").DataType, hoja.Cell("E2").GetText()));
        Assert.Equal((XLDataType.Number, 900001d), (hoja.Cell("B2").DataType, hoja.Cell("B2").GetDouble()));
        Assert.Equal((XLDataType.Text, "DEV001"), (hoja.Cell("B3").DataType, hoja.Cell("B3").GetText()));
        Assert.Equal((XLDataType.Number, 9001d), (hoja.Cell("A2").DataType, hoja.Cell("A2").GetDouble()));
        Assert.Equal(XLDataType.Number, hoja.Cell("J2").DataType);
        Assert.Equal(XLDataType.Text, hoja.Cell("K2").DataType); // ACTIVIDAD "20" como texto (columna @)
        Assert.True(hoja.Cell("C4").IsEmpty());                   // sin cédula → vacía

        // Horas: TimeSpan con [h]:mm:ss (numFmtId 46).
        Assert.Equal((XLDataType.TimeSpan, H7), (hoja.Cell("F2").DataType, hoja.Cell("F2").GetTimeSpan()));
        Assert.Equal(H14, hoja.Cell("G2").GetTimeSpan()); // INGRESO ALMUERZO ← regreso
        Assert.All(new[] { "F2", "G2", "H2", "I2" }, c => Assert.Equal(EscritorReporteExcel.FormatoHoras, hoja.Cell(c).Style.NumberFormat.NumberFormatId));
        Assert.All(new[] { "C2", "E2", "K2" }, c => Assert.Equal("@", Formato(hoja.Cell(c))));

        // Orden: el del armado (no se reordena al escribir).
        Assert.Equal(["PERSONA 01", "PERSONA 02", "PERSONA 03"], new[] { 2, 3, 4 }.Select(r => hoja.Cell(r, 4).GetText()));
        Assert.True(string.IsNullOrEmpty(libro.Properties.Author));
    }

    [Fact]
    public void SinFilas_TablaA1N2_ConLaFilaVacia()
    {
        using var libro = Leer(new EscritorReporteExcel().Escribir([]));

        var tabla = Assert.Single(libro.Worksheet(EscritorReporteExcel.NombreHoja).Tables);
        Assert.Equal("A1:N2", tabla.RangeAddress.ToString());
        Assert.True(tabla.DataRange.Cells().All(c => c.IsEmpty()));
    }

    [Fact]
    public void Estructura_IgualALaPlantillaDelOriginal()
    {
        using var plantilla = new XLWorkbook(RutaPlantilla());
        using var generado = Leer(new EscritorReporteExcel().Escribir([Fila()]));

        var p = Assert.Single(plantilla.Worksheets);
        var g = Assert.Single(generado.Worksheets);
        Assert.Equal(p.Name, g.Name);
        var tp = Assert.Single(p.Tables);
        var tg = Assert.Single(g.Tables);
        Assert.Equal((tp.Name, tp.RangeAddress.ToString(), tp.Theme.Name), (tg.Name, tg.RangeAddress.ToString(), tg.Theme.Name));
        Assert.Equal(tp.Fields.Select(f => f.Name), tg.Fields.Select(f => f.Name));
        for (var c = 1; c <= 14; c++)
        {
            Assert.Equal(Formato(p.Cell(2, c)), Formato(g.Cell(2, c)));
        }

        // Anchos: los <col width> del XML (la API de ClosedXML los expone sin el margen interno de la fuente).
        Assert.Equal(AnchosEnElArchivo(File.ReadAllBytes(RutaPlantilla())), AnchosEnElArchivo(new EscritorReporteExcel().Escribir([Fila()])));
    }

    /// <summary>Anchos de las columnas 1..14 tal como están en xl/worksheets/sheet1.xml (redondeados a 6 decimales).</summary>
    private static IReadOnlyList<double> AnchosEnElArchivo(byte[] archivo)
    {
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(archivo));
        var entrada = zip.Entries.Single(e => e.FullName.StartsWith("xl/worksheets/sheet", StringComparison.Ordinal));
        using var flujo = entrada.Open();
        var xml = System.Xml.Linq.XDocument.Load(flujo);
        var ns = xml.Root!.Name.Namespace;
        var anchos = new double[14];
        foreach (var col in xml.Descendants(ns + "col"))
        {
            var (min, max) = ((int)col.Attribute("min")!, (int)col.Attribute("max")!);
            for (var c = min; c <= Math.Min(max, 14); c++)
            {
                anchos[c - 1] = Math.Round((double)col.Attribute("width")!, 6);
            }
        }

        return anchos;
    }

    [Fact]
    public void Rendimiento_30000Filas_SeInformaElTiempo()
    {
        var filas = Enumerable.Range(0, 30_000)
            .Select(i => Fila($"9{i % 300:00000}", $"0{i % 300:000000000}", $"{i % 28 + 1}/10/2026", $"PERSONA {i % 300:000}")).ToList();

        var cronometro = Stopwatch.StartNew();
        var archivo = new EscritorReporteExcel().Escribir(filas);
        cronometro.Stop();

        salida.WriteLine($"30.000 filas × 14 columnas: {cronometro.ElapsedMilliseconds} ms, {archivo.Length / 1024} KB");
        Assert.True(archivo.Length > 0);
    }

    /// <summary>"@" (texto) o "General", sea por id integrado (49 / 0) o por código.</summary>
    private static string Formato(IXLCell celda) =>
        celda.Style.NumberFormat.NumberFormatId switch
        {
            49 => "@",
            0 when string.IsNullOrEmpty(celda.Style.NumberFormat.Format) => "General",
            var id when string.IsNullOrEmpty(celda.Style.NumberFormat.Format) => id.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => celda.Style.NumberFormat.Format,
        };

    /// <summary>docs/PLANTILLA_REPORTE_PROYECTOS.xlsx, subiendo desde el ensamblado de pruebas (solo lectura).</summary>
    private static string RutaPlantilla()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var ruta = Path.Combine(dir.FullName, "docs", "PLANTILLA_REPORTE_PROYECTOS.xlsx");
            if (File.Exists(ruta))
            {
                return ruta;
            }
        }

        throw new FileNotFoundException("No se encontró docs/PLANTILLA_REPORTE_PROYECTOS.xlsx subiendo desde el ensamblado de pruebas.");
    }
}
