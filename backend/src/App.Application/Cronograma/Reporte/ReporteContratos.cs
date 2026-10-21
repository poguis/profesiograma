namespace App.Application.Cronograma.Reporte;

// Reporte Excel por proyecto (TAREA-22-2, docs/fases/FASE_5_Cronograma.md §8). Columnas de la plantilla del original
// (docs/PLANTILLA_REPORTE_PROYECTOS.xlsx, hoja 'PROYECTO ', Tabla1).

/// <summary>GET /api/proyectos/{id}/reporte (y /resumen). Fechas "yyyy-MM-dd", obligatorias; sin tope de días (Z2).</summary>
public sealed record ReporteProyectoSolicitud(string? Desde, string? Hasta);

/// <summary>
/// Una fila del reporte (14 columnas, ya resueltas). EMPRESA, EKON y PROYECTO van como texto: el escritor los exporta como
/// número solo si cumplen <see cref="ArmadoReporte.EsNumeroExportable"/> (Z7). Cedula: tal como está en Empleado.Cedula.
/// </summary>
public sealed record FilaReporte(
    string Empresa,
    string Ekon,
    string? Cedula,
    string Nombre,
    string Fecha,
    TimeSpan IngresoJornada,
    TimeSpan IngresoAlmuerzo,
    TimeSpan SalidaAlmuerzo,
    TimeSpan SalidaJornada,
    string? Proyecto,
    string? Actividad,
    string Estado,
    string Observacion,
    string? CargoInfor)
{
    /// <summary>Sin la cédula (dato sensible): un ToString accidental en un log no la expone.</summary>
    public override string ToString() => $"{nameof(FilaReporte)}({Ekon}, {Fecha}, {Estado})";
}

/// <summary>Solo conteos (sin nombres ni cédulas). DiasConOtroProyecto: días-persona del reporte en que la persona también trabaja en otro proyecto (informativo).</summary>
public sealed record ResumenReporteDto(
    int Filas, int Personas, int PersonasSinCedula, int PersonasSinCodigoInfor, int FilasSinActividad, int DiasConOtroProyecto);

/// <summary>Reporte armado: filas ordenadas, resumen y nombre del archivo (con las fechas pedidas).</summary>
public sealed record ReporteProyecto(int ProyectoId, string NombreArchivo, DateOnly Desde, DateOnly Hasta,
    IReadOnlyList<FilaReporte> Filas, ResumenReporteDto Resumen);

/// <summary>Datos de la persona para el reporte (consulta nueva de la TAREA-22-2; la única que lee la cédula, además de la 22-1).</summary>
/// <param name="CodigoInfor">D14: CargoInfor.CodigoInfor (activo) cuyo Cargo = Empleado.Puesto; null si no hay.</param>
public sealed record EmpleadoReporte(int Id, string CodigoEkon, string NombreCompleto, string? Cedula, string? CodigoInfor)
{
    /// <summary>Sin la cédula.</summary>
    public override string ToString() => $"{nameof(EmpleadoReporte)}({Id}, {CodigoEkon})";
}

/// <summary>Arma el libro .xlsx (Infrastructure, ClosedXML). Devuelve el archivo completo, armado en memoria.</summary>
public interface IEscritorReporteExcel
{
    byte[] Escribir(IReadOnlyList<FilaReporte> filas);
}
