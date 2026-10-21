/* ==========================================================================
   TAREA-22-2 - R1: agregados para validar la regla Z7 del reporte (SOLO LECTURA; lo ejecuta el usuario en SSMS sobre
   PROFESIOGRAMA_DEV). Z7: EMPRESA, EKON y PROYECTO se exportan como NÚMERO solo si tienen únicamente dígitos, no
   empiezan en "0" y tienen 15 dígitos o menos; si no, como texto. Solo conteos: nunca valores.
   ========================================================================== */
SET NOCOUNT ON;

-- 1) Proyecto.ProyectoErpId (columna PROYECTO; vacía en PLANTA/OFICINAS, supuesto 53).
SELECT COUNT(*) AS Proyectos,
       SUM(CASE WHEN ProyectoErpId IS NULL OR ProyectoErpId = '' THEN 1 ELSE 0 END) AS Vacios,
       SUM(CASE WHEN ProyectoErpId NOT LIKE '%[^0-9]%' AND ProyectoErpId <> '' THEN 1 ELSE 0 END) AS SoloDigitos,
       SUM(CASE WHEN ProyectoErpId LIKE '0%' THEN 1 ELSE 0 END) AS ConCeroInicial,
       SUM(CASE WHEN LEN(ProyectoErpId) > 15 THEN 1 ELSE 0 END) AS LargoMayorA15,
       SUM(CASE WHEN ProyectoErpId LIKE '%[^0-9]%' THEN 1 ELSE 0 END) AS ConNoDigitos
FROM dbo.Proyecto
WHERE Eliminado = 0;

-- 2) Proyecto.CompaniaId (columna EMPRESA; es un entero: solo interesa el largo).
SELECT COUNT(*) AS Proyectos,
       MIN(LEN(CAST(CompaniaId AS varchar(20)))) AS LargoMinimo,
       MAX(LEN(CAST(CompaniaId AS varchar(20)))) AS LargoMaximo,
       SUM(CASE WHEN CompaniaId <= 0 THEN 1 ELSE 0 END) AS CeroONegativo
FROM dbo.Proyecto
WHERE Eliminado = 0;

-- 3) Empleado.CodigoEkon (columna EKON; los DEV del sembrador quedan como texto).
SELECT COUNT(*) AS Empleados,
       SUM(CASE WHEN CodigoEkon = '' THEN 1 ELSE 0 END) AS Vacios,
       SUM(CASE WHEN CodigoEkon NOT LIKE '%[^0-9]%' AND CodigoEkon <> '' THEN 1 ELSE 0 END) AS SoloDigitos,
       SUM(CASE WHEN CodigoEkon LIKE '0%' THEN 1 ELSE 0 END) AS ConCeroInicial,
       SUM(CASE WHEN LEN(CodigoEkon) > 15 THEN 1 ELSE 0 END) AS LargoMayorA15,
       SUM(CASE WHEN CodigoEkon LIKE '%[^0-9]%' THEN 1 ELSE 0 END) AS ConNoDigitos
FROM dbo.Empleado;
