/* =====================================================================
   PROFESIOGRAMA — Verificación del Paso 2 (EF Core + datos de prueba)
   Ejecutar en SSMS conectado a NIQUEL\SSDEV con el login profesiograma_dev.
   Solo lectura: no modifica nada.
   ===================================================================== */
USE PROFESIOGRAMA_DEV;
SET NOCOUNT ON;

PRINT '1) Migraciones aplicadas (esperado: 2 filas: ..._Inicial y ..._Vistas)';
SELECT MigrationId, ProductVersion FROM dbo.__EFMigrationsHistory ORDER BY MigrationId;

PRINT '2) Tablas del modelo (esperado: 22)';
SELECT COUNT(*) AS Tablas
FROM sys.tables
WHERE schema_id = SCHEMA_ID('dbo') AND name <> '__EFMigrationsHistory';

SELECT t.name AS Tabla,
       SUM(CASE WHEN p.index_id IN (0,1) THEN p.rows ELSE 0 END) AS Filas
FROM sys.tables t
JOIN sys.partitions p ON p.object_id = t.object_id
WHERE t.schema_id = SCHEMA_ID('dbo') AND t.name <> '__EFMigrationsHistory'
GROUP BY t.name
ORDER BY t.name;

PRINT '3) Vistas (esperado: vwNovedadDiaVigente, vwProyectoResumen)';
SELECT name AS Vista FROM sys.views WHERE schema_id = SCHEMA_ID('dbo') ORDER BY name;

PRINT '4) Restricciones (esperado: CHECK 21, FK 72, DEFAULT 47)';
SELECT
    (SELECT COUNT(*) FROM sys.check_constraints   WHERE schema_id = SCHEMA_ID('dbo')) AS CheckConstraints,
    (SELECT COUNT(*) FROM sys.foreign_keys        WHERE schema_id = SCHEMA_ID('dbo')) AS ForeignKeys,
    (SELECT COUNT(*) FROM sys.default_constraints WHERE schema_id = SCHEMA_ID('dbo')) AS Defaults;

PRINT '4b) Constraints sin nombre del estándar (esperado: 0 filas)';
SELECT o.name AS Objeto, o.type_desc
FROM sys.objects o
WHERE o.schema_id = SCHEMA_ID('dbo')
  AND o.type IN ('C','F','D','PK')
  AND o.name NOT LIKE 'CK[_]%' AND o.name NOT LIKE 'FK[_]%'
  AND o.name NOT LIKE 'DF[_]%' AND o.name NOT LIKE 'PK[_]%'
  AND OBJECT_NAME(o.parent_object_id) <> '__EFMigrationsHistory';

PRINT '4c) Única FK en cascada (esperado: FK_NovedadDia_Novedad)';
SELECT name, delete_referential_action_desc
FROM sys.foreign_keys
WHERE delete_referential_action <> 0;

PRINT '5) Semillas de catálogos (esperado según columna Esperado)';
SELECT Tabla, Filas, Esperado, CASE WHEN Filas = Esperado THEN 'OK' ELSE 'REVISAR' END AS Resultado
FROM (VALUES
    ('EstadoProyecto',        (SELECT COUNT(*) FROM dbo.EstadoProyecto),        4),
    ('TipoMovimiento',        (SELECT COUNT(*) FROM dbo.TipoMovimiento),        7),
    ('GrupoProyecto',         (SELECT COUNT(*) FROM dbo.GrupoProyecto),         3),
    ('Jornada',               (SELECT COUNT(*) FROM dbo.Jornada),               4),
    ('RolAsignacion',         (SELECT COUNT(*) FROM dbo.RolAsignacion),         3),
    ('TipoAplicacionNovedad', (SELECT COUNT(*) FROM dbo.TipoAplicacionNovedad), 3),
    ('OrigenNovedad',         (SELECT COUNT(*) FROM dbo.OrigenNovedad),         2),
    ('TipoNovedad',           (SELECT COUNT(*) FROM dbo.TipoNovedad),           7),
    ('CargoInfor',            (SELECT COUNT(*) FROM dbo.CargoInfor),            3),
    ('Departamento',          (SELECT COUNT(*) FROM dbo.Departamento),          7),
    ('Parametro',             (SELECT COUNT(*) FROM dbo.Parametro),             9)
) v(Tabla, Filas, Esperado);

PRINT '6) Datos de prueba (después del primer "dotnet run")';
SELECT Tabla, Filas, Esperado, CASE WHEN Filas = Esperado THEN 'OK' ELSE 'REVISAR' END AS Resultado
FROM (VALUES
    ('Usuario (total)',        (SELECT COUNT(*) FROM dbo.Usuario),                                          3),
    ('Compania 9001',          (SELECT COUNT(*) FROM dbo.Compania WHERE Id = 9001),                         1),
    ('Empleado DEV*',          (SELECT COUNT(*) FROM dbo.Empleado WHERE CodigoEkon LIKE 'DEV%'),            8),
    ('UsuarioDepartamento',    (SELECT COUNT(*) FROM dbo.UsuarioDepartamento),                              2),
    ('Proyecto PRY-DEV-*',     (SELECT COUNT(*) FROM dbo.Proyecto WHERE Codigo LIKE 'PRY-DEV-%'),           3),
    ('ProyectoPersonal',       (SELECT COUNT(*) FROM dbo.ProyectoPersonal),                                 5),
    ('ProyectoAsignacionDia',  (SELECT COUNT(*) FROM dbo.ProyectoAsignacionDia),                          244),
    ('ProyectoEtapa',          (SELECT COUNT(*) FROM dbo.ProyectoEtapa),                                    5),
    ('ProyectoActividad',      (SELECT COUNT(*) FROM dbo.ProyectoActividad),                                3),
    ('Novedad NOV-DEV-*',      (SELECT COUNT(*) FROM dbo.Novedad WHERE Codigo LIKE 'NOV-DEV-%'),            4),
    ('NovedadDia',             (SELECT COUNT(*) FROM dbo.NovedadDia),                                      10)
) v(Tabla, Filas, Esperado);

PRINT '7) Usuarios (SISTEMA + admin + gestor)';
SELECT Id, EntraObjectId, Email, NombreMostrar, EsSistema, Activo, CreadoPorId, FechaCreacion
FROM dbo.Usuario ORDER BY Id;

PRINT '8) Vista de proyectos';
SELECT Codigo, NombreVisual, Grupo, Estado, FechaInicio, FechaFin, ResponsableNombre, BacksNombres, ActividadCodigo
FROM dbo.vwProyectoResumen ORDER BY Codigo;

PRINT '9) Vista de novedades vigentes por día';
SELECT Fecha, TipoAplicacion, TipoNovedad, SiglaCronograma, EmpleadoId, ProyectoId, Origen
FROM dbo.vwNovedadDiaVigente ORDER BY Fecha, NovedadId;

PRINT '10) Auditoría UTC: FechaCreacion debe estar ~5 h por delante de la hora local de Ecuador';
SELECT TOP (5)
    p.Codigo, p.CreadoPorId, u.Email AS CreadoPor, p.FechaCreacion AS FechaCreacionUtc,
    SYSUTCDATETIME() AS AhoraUtc,
    CAST(SWITCHOFFSET(CAST(p.FechaCreacion AS DATETIMEOFFSET(0)), '-05:00') AS DATETIME2(0)) AS FechaCreacionEcuador
FROM dbo.Proyecto p
JOIN dbo.Usuario u ON u.Id = p.CreadoPorId
ORDER BY p.Id;
-- Nota: el sembrador asigna CreadoPorId = propietario del proyecto (gestor/admin); el interceptor respeta
-- valores asignados en Added (igual que hará la carga de Fase 3). Sin valor asignado usaría SISTEMA (Id 1).
