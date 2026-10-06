-- TAREA-18b parte 4 - SOLO LECTURA (ejecutar en SSMS sobre PROFESIOGRAMA_DEV) despues de parte4.cmd.
-- H15: dias DESCANSO de los backs del proyecto C (Id 11) posteriores a su FechaFin (descanso posterior insertado al acortar).
-- Esperado: DESCANSO MANUAL del back recortado desde F+1 hasta F+DiasDescanso.
SELECT pp.Numero AS Back, e.CodigoEkon, pp.FechaInicio, pp.FechaFin, pp.DiasDescanso,
       d.Fecha, d.RolAsignacionId, d.TipoAsignacion, d.Bloque
FROM dbo.ProyectoAsignacionDia d
JOIN dbo.ProyectoPersonal pp ON pp.Id = d.ProyectoPersonalId
JOIN dbo.Empleado e ON e.Id = d.EmpleadoId
WHERE d.ProyectoId = 11
  AND pp.RolAsignacionId = 2   -- BACK
  AND d.RolAsignacionId = 3    -- DESCANSO
  AND d.Fecha > pp.FechaFin
ORDER BY pp.Numero, d.Fecha;
