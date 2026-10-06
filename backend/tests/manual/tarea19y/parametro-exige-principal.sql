-- TAREA-19y - Parametro PROYECTO_EXIGE_PRINCIPAL para la parte 2 (lo ejecuta el usuario en SSMS, base PROFESIOGRAMA_DEV).
-- Requiere la migracion ParametroExigePrincipal aplicada (crea la fila con Valor = '0').
-- La API lee el parametro en cada peticion (sin cache): no hace falta reiniciarla.
USE PROFESIOGRAMA_DEV;
GO

-- Consulta (solo lectura): valor actual.
SELECT Clave, Valor, TipoDato FROM dbo.Parametro WHERE Clave = 'PROYECTO_EXIGE_PRINCIPAL';
GO

-- ---------------------------------------------------------------- BLOQUE 1: antes de parte2.cmd (vuelve C10)
-- UPDATE dbo.Parametro SET Valor = '1' WHERE Clave = 'PROYECTO_EXIGE_PRINCIPAL';
-- GO

-- ---------------------------------------------------------------- BLOQUE 2: despues de parte2.cmd (valor inicial)
-- UPDATE dbo.Parametro SET Valor = '0' WHERE Clave = 'PROYECTO_EXIGE_PRINCIPAL';
-- GO
