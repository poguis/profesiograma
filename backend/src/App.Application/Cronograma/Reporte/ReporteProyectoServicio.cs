using App.Application.Comun;
using App.Application.Seguridad;

namespace App.Application.Cronograma.Reporte;

/// <summary>
/// Reporte Excel por proyecto (TAREA-22-2, Z1): GET /api/proyectos/{id}/reporte y …/reporte/resumen. Valida las fechas
/// (obligatorias, sin tope: Z2), aplica la visibilidad R1 y reutiliza las consultas del cronograma (1, 1b, 2, 3, 4 y 5)
/// más la de los datos de la persona (cédula y código Infor).
/// </summary>
public sealed class ReporteProyectoServicio(ICronogramaConsultas consultas, IUsuarioActual usuario)
{
    /// <summary>Null si el proyecto no existe, está eliminado o no es visible (404, nunca 403).</summary>
    public async Task<ResultadoConsulta<ReporteProyecto>?> ArmarAsync(int proyectoId, ReporteProyectoSolicitud s, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(s);
        if (!TryObtenerVisibilidad(out var propietario)
            || await consultas.ObtenerRangoProyectoAsync(proyectoId, propietario, ct) is not { } proyecto)
        {
            return null;
        }

        var errores = new Dictionary<string, string[]>();
        var desde = LeerObligatoria(s.Desde, "desde", errores);
        var hasta = LeerObligatoria(s.Hasta, "hasta", errores);
        if (errores.Count == 0 && hasta < desde)
        {
            errores["hasta"] = ["La fecha 'hasta' no puede ser anterior a la fecha 'desde'."];
        }

        if (errores.Count > 0)
        {
            return ResultadoConsulta<ReporteProyecto>.Invalido(errores);
        }

        var cabecera = (await consultas.ObtenerProyectosAsync([proyectoId], ct)).Single();
        var nombre = ArmadoReporte.NombreArchivo(cabecera.Codigo, desde!.Value, hasta!.Value);

        // Sin tope (Z2): el rango pedido se intersecta con el del proyecto antes de consultar (mismo resultado, menos lectura).
        var inicio = desde.Value > proyecto.FechaInicio ? desde.Value : proyecto.FechaInicio;
        var fin = hasta.Value < proyecto.FechaFin ? hasta.Value : proyecto.FechaFin;
        var dias = inicio > fin
            ? []
            : await consultas.ObtenerDiasAsync(new FiltroCronograma(inicio, fin, propietario, SoloProyectoId: proyectoId), ct);
        if (dias.Count == 0)
        {
            var vacio = ArmadoReporte.Armar(cabecera, [], [], [], [], []);
            return ResultadoConsulta<ReporteProyecto>.Ok(new ReporteProyecto(proyectoId, nombre, desde.Value, hasta.Value, vacio.Filas, vacio.Resumen));
        }

        var empleados = dias.Select(d => d.EmpleadoId).Distinct().ToList();
        var otros = await consultas.ObtenerOtrosProyectosAsync(
            new FiltroOtrosProyectos(empleados, inicio, fin, VisiblesDePropietario: null, SalvoProyectoId: proyectoId), ct);
        var novedades = await consultas.ObtenerNovedadesAsync(empleados, [proyectoId], inicio, fin, ct);
        // Z6: todas las versiones del proyecto (la vigente ese día o, si no hay, la de mayor versión).
        var actividades = await consultas.ObtenerActividadesAsync([proyectoId], proyecto.FechaInicio, proyecto.FechaFin, ct);
        var personas = await consultas.ObtenerEmpleadosReporteAsync(empleados, ct);

        var (filas, resumen) = ArmadoReporte.Armar(cabecera, dias, novedades, actividades, personas, otros);
        return ResultadoConsulta<ReporteProyecto>.Ok(new ReporteProyecto(proyectoId, nombre, desde.Value, hasta.Value, filas, resumen));
    }

    /// <summary>Solo los conteos (Z9). Null = 404.</summary>
    public async Task<ResultadoConsulta<ResumenReporteDto>?> ResumirAsync(int proyectoId, ReporteProyectoSolicitud s, CancellationToken ct) =>
        await ArmarAsync(proyectoId, s, ct) is not { } r
            ? null
            : r.EsValido ? ResultadoConsulta<ResumenReporteDto>.Ok(r.Valor!.Resumen) : ResultadoConsulta<ResumenReporteDto>.Invalido(r.Errores!);

    private static DateOnly? LeerObligatoria(string? valor, string campo, Dictionary<string, string[]> errores)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            errores[campo] = [$"La fecha '{campo}' es obligatoria."];
            return null;
        }

        return LectorParametros.LeerFecha(valor, campo, errores);
    }

    /// <summary>R1 (D10): Admin ve todos (null); cualquier otro rol solo sus proyectos. False sin usuario resuelto.</summary>
    private bool TryObtenerVisibilidad(out int? propietarioUsuarioId)
    {
        if (usuario.TieneRol(RolesApp.Admin))
        {
            propietarioUsuarioId = null;
            return true;
        }

        propietarioUsuarioId = usuario.UsuarioId;
        return propietarioUsuarioId is not null;
    }
}
