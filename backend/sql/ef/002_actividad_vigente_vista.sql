BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006125310_ActividadVigenteVista'
)
BEGIN
    EXEC(N'CREATE OR ALTER VIEW dbo.vwProyectoResumen
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
        SELECT STRING_AGG(x.NombreCompleto, N'' | '') AS Nombres
        FROM (
            SELECT DISTINCT e.NombreCompleto
            FROM dbo.ProyectoPersonal pp
            JOIN dbo.Empleado e ON e.Id = pp.EmpleadoId
            WHERE pp.ProyectoId = p.Id AND pp.RolAsignacionId = 2
        ) x
    ) bk
    CROSS APPLY (
        -- Hoy en Ecuador: misma zona que FechaNegocio.ZonaNegocio ("SA Pacific Standard Time", UTC-5 sin horario de verano).
        SELECT CAST(SYSDATETIMEOFFSET() AT TIME ZONE ''SA Pacific Standard Time'' AS DATE) AS Hoy
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
    WHERE p.Eliminado = 0;');
END;

IF NOT EXISTS (
    SELECT * FROM [dbo].[__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261006125310_ActividadVigenteVista'
)
BEGIN
    INSERT INTO [dbo].[__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261006125310_ActividadVigenteVista', N'10.0.12');
END;

COMMIT;
GO

