namespace App.Infrastructure.Persistencia.Vistas;

/// <summary>
/// SQL de las vistas de lectura. V1: migración "Vistas"; VwProyectoResumen_V2: migración "ActividadVigenteVista" (TAREA-18b).
/// Si una vista cambia en el futuro, crear una constante _V2 y una migración nueva;
/// NO modificar estas constantes (cambiarían migraciones ya aplicadas).
/// </summary>
public static class VistasSql
{
    public const string VwProyectoResumen_V1 = """
        CREATE OR ALTER VIEW dbo.vwProyectoResumen
        AS
        SELECT
            p.Id,
            p.Uid,
            p.Codigo,
            p.NombreVisual,
            gp.Codigo            AS Grupo,
            p.FechaInicio,
            p.FechaFin,
            ep.Codigo            AS Estado,
            p.PropietarioUsuarioId,
            p.DepartamentoId,
            p.HoraEntrada,
            p.HoraSalida,
            p.SalidaAlmuerzo,
            p.RegresoAlmuerzo,
            resp.NombreCompleto  AS ResponsableNombre,
            bk.Nombres           AS BacksNombres,
            act.ActividadCodigo,
            act.ActividadDescripcion,
            p.FechaCreacion
        FROM dbo.Proyecto p
        JOIN dbo.EstadoProyecto ep ON ep.Id = p.EstadoProyectoId
        JOIN dbo.GrupoProyecto  gp ON gp.Id = p.GrupoProyectoId
        OUTER APPLY (
            SELECT TOP (1) e.NombreCompleto
            FROM dbo.ProyectoPersonal pp
            JOIN dbo.Empleado e ON e.Id = pp.EmpleadoId
            WHERE pp.ProyectoId = p.Id AND pp.RolAsignacionId = 1 AND pp.EsPrincipalInicial = 1
            ORDER BY pp.FechaFin DESC
        ) resp
        OUTER APPLY (
            SELECT STRING_AGG(x.NombreCompleto, N' | ') AS Nombres
            FROM (
                SELECT DISTINCT e.NombreCompleto
                FROM dbo.ProyectoPersonal pp
                JOIN dbo.Empleado e ON e.Id = pp.EmpleadoId
                WHERE pp.ProyectoId = p.Id AND pp.RolAsignacionId = 2
            ) x
        ) bk
        OUTER APPLY (
            SELECT TOP (1) a.ActividadCodigo, a.ActividadDescripcion
            FROM dbo.ProyectoActividad a
            WHERE a.ProyectoId = p.Id
            ORDER BY
                CASE WHEN CAST(SWITCHOFFSET(SYSDATETIMEOFFSET(), '-05:00') AS DATE)
                          BETWEEN a.FechaInicio AND a.FechaFin THEN 0 ELSE 1 END,
                a.Version DESC
        ) act
        WHERE p.Eliminado = 0;
        """;

    /// <summary>
    /// V2 (TAREA-18b, O3): la actividad usa la regla común ActividadVigente (Application): referencia =
    /// max(FechaInicio, min(hoy en Ecuador, FechaFin)); la que la cubre, la de mayor versión si se solapan; NULL si ninguna.
    /// Sin el respaldo de V1 ("la de mayor versión" cuando ninguna cubre hoy). Migración ActividadVigenteVista.
    /// </summary>
    public const string VwProyectoResumen_V2 = """
        CREATE OR ALTER VIEW dbo.vwProyectoResumen
        AS
        SELECT
            p.Id,
            p.Uid,
            p.Codigo,
            p.NombreVisual,
            gp.Codigo            AS Grupo,
            p.FechaInicio,
            p.FechaFin,
            ep.Codigo            AS Estado,
            p.PropietarioUsuarioId,
            p.DepartamentoId,
            p.HoraEntrada,
            p.HoraSalida,
            p.SalidaAlmuerzo,
            p.RegresoAlmuerzo,
            resp.NombreCompleto  AS ResponsableNombre,
            bk.Nombres           AS BacksNombres,
            act.ActividadCodigo,
            act.ActividadDescripcion,
            p.FechaCreacion
        FROM dbo.Proyecto p
        JOIN dbo.EstadoProyecto ep ON ep.Id = p.EstadoProyectoId
        JOIN dbo.GrupoProyecto  gp ON gp.Id = p.GrupoProyectoId
        OUTER APPLY (
            SELECT TOP (1) e.NombreCompleto
            FROM dbo.ProyectoPersonal pp
            JOIN dbo.Empleado e ON e.Id = pp.EmpleadoId
            WHERE pp.ProyectoId = p.Id AND pp.RolAsignacionId = 1 AND pp.EsPrincipalInicial = 1
            ORDER BY pp.FechaFin DESC
        ) resp
        OUTER APPLY (
            SELECT STRING_AGG(x.NombreCompleto, N' | ') AS Nombres
            FROM (
                SELECT DISTINCT e.NombreCompleto
                FROM dbo.ProyectoPersonal pp
                JOIN dbo.Empleado e ON e.Id = pp.EmpleadoId
                WHERE pp.ProyectoId = p.Id AND pp.RolAsignacionId = 2
            ) x
        ) bk
        CROSS APPLY (
            -- Hoy en Ecuador: misma zona que FechaNegocio.ZonaNegocio ("SA Pacific Standard Time", UTC-5 sin horario de verano).
            SELECT CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'SA Pacific Standard Time' AS DATE) AS Hoy
        ) hoy
        CROSS APPLY (
            -- Regla ActividadVigente (TAREA-18b, O3): referencia = max(FechaInicio, min(hoy, FechaFin)).
            SELECT CASE WHEN hoy.Hoy < p.FechaInicio THEN p.FechaInicio
                        WHEN hoy.Hoy > p.FechaFin THEN p.FechaFin
                        ELSE hoy.Hoy END AS Fecha
        ) ref
        OUTER APPLY (
            -- La actividad que cubre la referencia; si hay varias, la de mayor versión; si ninguna, NULL.
            SELECT TOP (1) a.ActividadCodigo, a.ActividadDescripcion
            FROM dbo.ProyectoActividad a
            WHERE a.ProyectoId = p.Id AND a.FechaInicio <= ref.Fecha AND a.FechaFin >= ref.Fecha
            ORDER BY a.Version DESC
        ) act
        WHERE p.Eliminado = 0;
        """;

    public const string VwNovedadDiaVigente_V1 = """
        CREATE OR ALTER VIEW dbo.vwNovedadDiaVigente
        AS
        SELECT
            nd.Fecha,
            n.Id                AS NovedadId,
            tan.Codigo          AS TipoAplicacion,
            tn.Codigo           AS TipoNovedad,
            tn.Nombre           AS TipoNovedadNombre,
            tn.SiglaCronograma,
            tn.ColorHex,
            tn.EstadoReporte,
            tn.ObservacionReporte,
            n.EmpleadoId,
            n.ProyectoId,
            o.Codigo            AS Origen
        FROM dbo.NovedadDia nd
        JOIN dbo.Novedad n                 ON n.Id   = nd.NovedadId AND n.Anulada = 0
        JOIN dbo.TipoNovedad tn            ON tn.Id  = n.TipoNovedadId
        JOIN dbo.TipoAplicacionNovedad tan ON tan.Id = n.TipoAplicacionNovedadId
        JOIN dbo.OrigenNovedad o           ON o.Id   = n.OrigenNovedadId;
        """;

    public const string EliminarVwProyectoResumen = "DROP VIEW IF EXISTS dbo.vwProyectoResumen;";
    public const string EliminarVwNovedadDiaVigente = "DROP VIEW IF EXISTS dbo.vwNovedadDiaVigente;";
}
