/* =====================================================================
   PROFESIOGRAMA — Diagnóstico de SQL Server (ejecutar en SSMS sobre la BD de pruebas)
   Requisito del modelo Fase 2: SQL Server 2017+ (versión mayor 14) y compatibilidad >= 130
   ===================================================================== */
SELECT
    CAST(SERVERPROPERTY('ServerName')     AS nvarchar(128)) AS Servidor,
    DB_NAME()                                                AS BaseDatos,
    SUSER_SNAME()                                            AS Usuario,
    CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)) AS VersionProducto,
    CASE LEFT(CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)),
              CHARINDEX('.', CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128))) - 1)
        WHEN '11' THEN 'SQL Server 2012' WHEN '12' THEN 'SQL Server 2014'
        WHEN '13' THEN 'SQL Server 2016' WHEN '14' THEN 'SQL Server 2017'
        WHEN '15' THEN 'SQL Server 2019' WHEN '16' THEN 'SQL Server 2022'
        WHEN '17' THEN 'SQL Server 2025' ELSE 'Otra' END     AS NombreVersion,
    CAST(SERVERPROPERTY('ProductLevel')   AS nvarchar(128)) AS NivelProducto,
    CAST(SERVERPROPERTY('Edition')        AS nvarchar(128)) AS Edicion,
    (SELECT compatibility_level FROM sys.databases WHERE name = DB_NAME()) AS NivelCompatibilidad,
    CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS nvarchar(128))      AS Collation,
    HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'CREATE TABLE')  AS PuedeCrearTablas,
    HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'CREATE SCHEMA') AS PuedeCrearEsquemas,
    HAS_PERMS_BY_NAME(DB_NAME(), 'DATABASE', 'CREATE VIEW')   AS PuedeCrearVistas,
    IS_ROLEMEMBER('db_owner')                                 AS EsDbOwner;

/* Tablas existentes en la base (para saber si está vacía) */
SELECT s.name AS Esquema, t.name AS Tabla, t.create_date
FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
ORDER BY s.name, t.name;
