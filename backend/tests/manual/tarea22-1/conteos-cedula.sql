/* ==========================================================================
   TAREA-22-1 - M2 / M5: conteos de Empleado.Cedula (SOLO LECTURA; lo ejecuta el usuario en SSMS sobre
   PROFESIOGRAMA_DEV). Solo agregados: NUNCA lista cédulas, nombres ni códigos.
   Los DEV001–DEV008 del sembrador tienen una cédula FICTICIA ("99999999" + n): se cuentan aparte.
   ========================================================================== */
SET NOCOUNT ON;

-- 1) Totales, con y sin cédula (DEV aparte).
SELECT COUNT(*) AS Total,
       SUM(CASE WHEN Cedula IS NOT NULL THEN 1 ELSE 0 END) AS ConCedula,
       SUM(CASE WHEN Cedula IS NULL THEN 1 ELSE 0 END) AS SinCedula,
       SUM(CASE WHEN CodigoEkon LIKE 'DEV%' THEN 1 ELSE 0 END) AS Dev,
       SUM(CASE WHEN CodigoEkon LIKE 'DEV%' AND Cedula IS NOT NULL THEN 1 ELSE 0 END) AS DevConCedula,
       SUM(CASE WHEN CodigoEkon NOT LIKE 'DEV%' AND Cedula IS NULL THEN 1 ELSE 0 END) AS NoDevSinCedula
FROM dbo.Empleado;

-- 2) Largos de las cédulas guardadas (largo: filas).
SELECT LEN(Cedula) AS Largo, COUNT(*) AS Filas
FROM dbo.Empleado
WHERE Cedula IS NOT NULL
GROUP BY LEN(Cedula)
ORDER BY Largo;

-- 3) Formato: 10 dígitos exactos, con algún carácter que no es dígito, con ceros iniciales.
SELECT SUM(CASE WHEN Cedula LIKE REPLICATE('[0-9]', 10) THEN 1 ELSE 0 END) AS DiezDigitos,
       SUM(CASE WHEN Cedula LIKE '%[^0-9]%' THEN 1 ELSE 0 END) AS ConNoDigitos,
       SUM(CASE WHEN Cedula LIKE '0%' THEN 1 ELSE 0 END) AS ConCerosIniciales
FROM dbo.Empleado
WHERE Cedula IS NOT NULL;

-- 4) Duplicados: cuántos valores se repiten y en cuántas filas (sin mostrar los valores).
SELECT COUNT(*) AS ValoresRepetidos, ISNULL(SUM(Filas), 0) AS FilasConValorRepetido
FROM (SELECT COUNT(*) AS Filas
      FROM dbo.Empleado
      WHERE Cedula IS NOT NULL
      GROUP BY Cedula
      HAVING COUNT(*) > 1) AS d;
