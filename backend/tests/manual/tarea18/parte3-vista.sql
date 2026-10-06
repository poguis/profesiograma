-- TAREA-18b parte 3 - SOLO LECTURA (ejecutar en SSMS sobre PROFESIOGRAMA_DEV).
-- Actividad del listado (dbo.vwProyectoResumen) del proyecto C. Requiere la migracion ActividadVigenteVista aplicada.
-- Esperado: la misma actividad que la cabecera y el detalle (lineas CONTROL O3 de resultado-parte3.txt).
SELECT Id, Codigo, FechaInicio, FechaFin, ActividadCodigo, ActividadDescripcion
FROM dbo.vwProyectoResumen
WHERE Id = 11;
