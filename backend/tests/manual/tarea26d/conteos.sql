/* ==========================================================================
   TAREA-26d-1 - Conteos de la tabla Empleado con el modelo "empleados desde la API" (SOLO LECTURA; lo ejecuta el
   usuario en SSMS sobre PROFESIOGRAMA_DEV). Sin nombres, cédulas ni correos: solo conteos, Id y códigos EKON.
   La tabla Empleado debe tener SOLO a las personas asignadas (alta puntual) y los DEV001–DEV008 del sembrador.
   ========================================================================== */
SET NOCOUNT ON;

-- 1) Totales: DEV (prueba), no DEV (alta puntual) y empleados no DEV con cédula (esperado 0: la alta nunca la escribe).
SELECT COUNT(*) AS Total,
       SUM(CASE WHEN CodigoEkon LIKE 'DEV%' THEN 1 ELSE 0 END) AS Dev,
       SUM(CASE WHEN CodigoEkon NOT LIKE 'DEV%' THEN 1 ELSE 0 END) AS NoDev,
       SUM(CASE WHEN CodigoEkon NOT LIKE 'DEV%' AND Cedula IS NOT NULL THEN 1 ELSE 0 END) AS NoDevConCedula
FROM dbo.Empleado;

-- 2) Por estado (EstadoErp = estado en la API en la última alta puntual).
SELECT ISNULL(EstadoErp, '(null)') AS EstadoErp, COUNT(*) AS Empleados
FROM dbo.Empleado
GROUP BY ISNULL(EstadoErp, '(null)');

-- 3) Empleados no DEV: Id y código (para transicion.cmd: EMPLEADO_ID / CODIGO_EKON), cuándo se copiaron de la API y
--    en cuántos proyectos están asignados. Esperado: todos asignados al menos una vez.
SELECT TOP (20) e.Id, e.CodigoEkon, e.EstadoErp, e.FechaSincronizacion AS UltimaCopiaApiUtc,
       (SELECT COUNT(DISTINCT pp.ProyectoId) FROM dbo.ProyectoPersonal AS pp WHERE pp.EmpleadoId = e.Id) AS Proyectos
FROM dbo.Empleado AS e
WHERE e.CodigoEkon NOT LIKE 'DEV%'
ORDER BY e.Id DESC;

-- 4) Empleados sin ninguna asignación (esperado 0 fuera de los DEV: con la opción C no hay filas sueltas).
SELECT COUNT(*) AS NoDevSinAsignacion
FROM dbo.Empleado AS e
WHERE e.CodigoEkon NOT LIKE 'DEV%'
  AND NOT EXISTS (SELECT 1 FROM dbo.ProyectoPersonal AS pp WHERE pp.EmpleadoId = e.Id);

-- 5) CargoInfor (puestos de las personas asignadas, P8).
SELECT COUNT(*) AS Cargos,
       SUM(CASE WHEN CodigoInfor IS NULL OR CodigoInfor = '' THEN 1 ELSE 0 END) AS SinCodigoInfor
FROM dbo.CargoInfor;
