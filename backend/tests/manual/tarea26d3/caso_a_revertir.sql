/* ==========================================================================
   TAREA-26d-3 - Caso (a): DESHACER caso_a_preparar.sql (devuelve Puesto y Departamento a sus valores originales).
   SOLO PROFESIOGRAMA_DEV. Lo ejecuta el usuario en SSMS (Claude Code no ejecuta SQL).

   ANTES DE EJECUTAR, copie aqui los valores de la fila "ANTES" de caso_a_preparar.sql:
     - en los comentarios de "Valores originales" (como registro);
     - en las variables @CodigoEkon, @PuestoOriginal y @DepartamentoOriginal
       (use NULL, sin comillas, si el valor original era NULL).

   Valores originales (copiados del SELECT "ANTES" de caso_a_preparar.sql):
     -- CodigoEkon:   ____________
     -- Puesto:       ____________
     -- Departamento: ____________
   ========================================================================== */
USE PROFESIOGRAMA_DEV;
GO

IF DB_NAME() <> N'PROFESIOGRAMA_DEV'
    THROW 50001, N'Base incorrecta: este script es solo para PROFESIOGRAMA_DEV.', 1;
GO

SET XACT_ABORT ON;
SET NOCOUNT ON;

DECLARE @CodigoEkon varchar(20) = '';                                  -- <== PARAMETRO: el mismo de caso_a_preparar.sql
DECLARE @PuestoOriginal nvarchar(200) = N'<COMPLETAR>';                 -- <== valor original (o NULL)
DECLARE @DepartamentoOriginal nvarchar(200) = N'<COMPLETAR>';           -- <== valor original (o NULL)

IF DB_NAME() <> N'PROFESIOGRAMA_DEV'
    THROW 50001, N'Base incorrecta: este script es solo para PROFESIOGRAMA_DEV.', 1;
IF LTRIM(RTRIM(@CodigoEkon)) = ''
    THROW 50002, N'Indique @CodigoEkon al inicio del script.', 1;
IF @PuestoOriginal = N'<COMPLETAR>' OR @DepartamentoOriginal = N'<COMPLETAR>'
    THROW 50005, N'Complete @PuestoOriginal y @DepartamentoOriginal con los valores de la fila ANTES.', 1;
IF (SELECT COUNT(*) FROM dbo.Empleado WHERE CodigoEkon = @CodigoEkon) <> 1
    THROW 50003, N'El codigo no corresponde a exactamente una fila de dbo.Empleado.', 1;

-- ANTES DE REVERTIR (deberia mostrar los valores de prueba)
SELECT N'ANTES DE REVERTIR' AS Momento, Id, CodigoEkon, Puesto, Departamento
FROM dbo.Empleado
WHERE CodigoEkon = @CodigoEkon;

BEGIN TRANSACTION;

UPDATE dbo.Empleado
SET Puesto = @PuestoOriginal,
    Departamento = @DepartamentoOriginal
WHERE CodigoEkon = @CodigoEkon;

IF @@ROWCOUNT <> 1
BEGIN
    ROLLBACK TRANSACTION;
    THROW 50004, N'Se esperaba exactamente 1 fila actualizada; no se cambio nada.', 1;
END;

COMMIT TRANSACTION;

-- REVERTIDO
SELECT N'REVERTIDO' AS Momento, Id, CodigoEkon, Puesto, Departamento
FROM dbo.Empleado
WHERE CodigoEkon = @CodigoEkon;
