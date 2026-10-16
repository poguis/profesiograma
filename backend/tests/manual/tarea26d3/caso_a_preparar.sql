/* ==========================================================================
   TAREA-26d-3 - Caso (a): provocar un aviso CAMBIO_CARGO + CAMBIO_DEPARTAMENTO en la comparacion con el ERP.
   SOLO PROFESIOGRAMA_DEV. Lo ejecuta el usuario en SSMS (Claude Code no ejecuta SQL).

   Que hace: cambia Puesto y Departamento de UNA fila de dbo.Empleado (por CodigoEkon) a valores de prueba, en una
   transaccion, con SELECT antes y despues. No toca ProyectoPersonal (el cargo asignado al proyecto no cambia) ni
   ninguna otra tabla. Como la API sigue devolviendo los datos reales, la comparacion mostrara la diferencia.

   ANTES DE EJECUTAR:
     1. Ponga en @CodigoEkon el codigo de una persona GUARDADA en un proyecto (p. ej. una de V3: 9940 u 11358).
     2. Ejecute el script completo.
     3. Copie los valores de la fila "ANTES" (o la sentencia de la ultima consulta) en caso_a_revertir.sql.
   Para deshacer: caso_a_revertir.sql.
   Nota: es un UPDATE directo (sin EF): las columnas de auditoria no cambian.
   ========================================================================== */
USE PROFESIOGRAMA_DEV;
GO

IF DB_NAME() <> N'PROFESIOGRAMA_DEV'
    THROW 50001, N'Base incorrecta: este script es solo para PROFESIOGRAMA_DEV.', 1;
GO

SET XACT_ABORT ON;
SET NOCOUNT ON;

DECLARE @CodigoEkon varchar(20) = '';                                  -- <== PARAMETRO: codigo EKON de la persona
DECLARE @PuestoPrueba nvarchar(200) = N'PUESTO DE PRUEBA 26D3';
DECLARE @DepartamentoPrueba nvarchar(200) = N'DEPARTAMENTO DE PRUEBA 26D3';

IF DB_NAME() <> N'PROFESIOGRAMA_DEV'
    THROW 50001, N'Base incorrecta: este script es solo para PROFESIOGRAMA_DEV.', 1;
IF LTRIM(RTRIM(@CodigoEkon)) = ''
    THROW 50002, N'Indique @CodigoEkon al inicio del script.', 1;
IF (SELECT COUNT(*) FROM dbo.Empleado WHERE CodigoEkon = @CodigoEkon) <> 1
    THROW 50003, N'El codigo no corresponde a exactamente una fila de dbo.Empleado.', 1;

-- ANTES (copie estos valores en caso_a_revertir.sql)
SELECT N'ANTES' AS Momento, Id, CodigoEkon, Puesto, Departamento
FROM dbo.Empleado
WHERE CodigoEkon = @CodigoEkon;

BEGIN TRANSACTION;

UPDATE dbo.Empleado
SET Puesto = @PuestoPrueba,
    Departamento = @DepartamentoPrueba
WHERE CodigoEkon = @CodigoEkon;

IF @@ROWCOUNT <> 1
BEGIN
    ROLLBACK TRANSACTION;
    THROW 50004, N'Se esperaba exactamente 1 fila actualizada; no se cambio nada.', 1;
END;

COMMIT TRANSACTION;

-- DESPUES
SELECT N'DESPUES' AS Momento, Id, CodigoEkon, Puesto, Departamento
FROM dbo.Empleado
WHERE CodigoEkon = @CodigoEkon;
