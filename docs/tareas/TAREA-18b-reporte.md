# TAREA-18b — Correcciones H15, O2 y O3 tras la prueba manual de la TAREA-18

**Fechas:** 2026-10-06 (Fase A y Fase B)
**Resultado:** implementada. **Prueba manual pendiente** (`backend/tests/manual/tarea18/parte3.cmd` y `parte4.cmd`, sección 6). **Migración `ActividadVigenteVista` pendiente de aplicar** por el usuario (`dotnet ef database update`).
- `dotnet build Profesiograma.slnx -c Release --no-incremental`: **0 advertencias, 0 errores**.
- `dotnet test --solution Profesiograma.slnx -c Release`: **486/486** (Domain 115, Application 270, Infrastructure 101). Antes: 468. Nuevas: 18. De las 468 anteriores solo cambiaron las **2 aprobadas** (sección 5); ninguna otra.
- `has-pending-model-changes`: sin cambios.
- `dotnet ef migrations list --configuration Release`: `20260930161754_Inicial`, `20260930161822_Vistas`, `20261006125310_ActividadVigenteVista` **(Pending)**.

Sin `dotnet run`, sin `dotnet ef database update`, sin SQL que modifique datos, sin cambios en `frontend/`, sin comandos git que modifiquen el repositorio y **sin ningún cliente HTTP contra la API** (scripts probados solo con `SIMULAR=1` y sin `curl.exe` en el `PATH`).

## 1. Decisiones aprobadas
- **H15:** al acortar (C4, proyecto ACTIVO) se insertan los días DESCANSO MANUAL posteriores de **todos los backs que quedan** (recortados o no) con fecha > F. SUSPENSION y CIERRE sin cambios.
- **O2:** la etapa CAMBIO_ACTIVIDAD lleva la actividad nueva.
- **O3:** regla única `ActividadVigente` (referencia = max(inicio, min(fecha, fin)); la que la cubre; mayor versión; null si ninguna). Vistas con hoy; etapas con su FechaCorte; excepción CAMBIO_ACTIVIDAD. Listado con una migración nueva (`ALTER VIEW`).
- `parte4.cmd` aprobado (escribe en el Id 11).
- Detalle en `docs/fases/FASE_5_Edicion_Cabecera.md` §7.

## 2. Verificación (Fase A)
- **H15:** `RecorteProyecto.cs:58–70` borra los días > F de cualquier rol y no toca `DiasDescanso`; `ReglasCronograma.TramosBack` (`ReglasCronograma.cs:32–44`) vuelve a emitir el descanso. Confirmado con h, q y s1 de la prueba del 06/10.
- **O2:** `EdicionCabeceraServicio.cs:228` (P3) se aplicaba también a CAMBIO_ACTIVIDAD.
- **O3:** seis cálculos distintos (tabla de `FASE_5_Edicion_Cabecera.md` §7.3); el detalle tenía el respaldo "la de mayor versión" (`ProyectoConsultas.cs:166–173`) y la vista V1 el mismo (`VistasSql.cs`, `OUTER APPLY act`).

## 3. Qué se hizo
| Capa | Archivo | Cambio |
|---|---|---|
| Domain | `Proyectos/Estados/DescansoBacksTrasRecorte.cs` (nuevo) | H15: descanso posterior de los backs que quedan, con `TramosBack` |
| Application | `Proyectos/ActividadVigente.cs` (nuevo) | O3: `FechaReferencia` y `Elegir` |
| | `Proyectos/Cabecera/EdicionCabeceraValidador.cs` | H15: `PlanCabecera.DescansosAgregados` |
| | `Proyectos/Cabecera/EdicionCabeceraServicio.cs` | O3 en el GET y en la etapa; O2; `diasAgregados` en la vista previa |
| | `Proyectos/Cabecera/EdicionCabeceraDtos.cs`, `EdicionCabeceraContratos.cs` | `DiasAgregadosDto`; `CambioCabeceraAplicar.DescansosAgregados` |
| | `Proyectos/Personal/EdicionPersonalServicio.cs`, `CalculoPersonal.cs` | O3 en la etapa ACTUALIZACION_PERSONAL (se quita `CalculoPersonal.ActividadVigente`) |
| | `Proyectos/Reactivacion/ReactivacionServicio.cs` | O3 en la etapa REACTIVACION (corte R; mismo resultado) |
| | `Proyectos/Estados/CambioEstadoServicio.cs`, `CambioEstadoContratos.cs` | O3 en la etapa SUSPENSION / CIERRE (`CambioEstadoAplicar.ActividadCodigo`; mismo resultado que `ActividadVigenteEnF`) |
| Infrastructure | `Consultas/ProyectoConsultas.cs` | O3 en el detalle: referencia con la regla de Application y `ConsultaActividadVigente(db, id, referencia)` con filtro de cobertura (sin `CASE` ni respaldo) |
| | `Persistencia/Proyectos/EdicionCabeceraRepositorio.cs` | H15: inserta los días en el SaveChanges 2 |
| | `Persistencia/Proyectos/CambioEstadoRepositorio.cs` | La etapa usa `cambio.ActividadCodigo` |
| | `Persistencia/Vistas/VistasSql.cs` | `VwProyectoResumen_V2` (regla O3; hoy con `AT TIME ZONE 'SA Pacific Standard Time'`) |
| | `Persistencia/Migraciones/20261006125310_ActividadVigenteVista.cs` + `.Designer.cs` (nuevos) | Up: `EXEC(N'…V2…')`; Down: V1. Snapshot del modelo sin cambios |
| SQL | `backend/sql/ef/002_actividad_vigente_vista.sql` (nuevo) | Script idempotente de la migración (`dotnet ef migrations script 20260930161822_Vistas 20261006125310_ActividadVigenteVista --idempotent`) |
| Pruebas | `App.Domain.Tests/Proyectos/Estados/DescansoBacksTrasRecorteTests.cs` (nuevo) | 5, incluida la equivalencia con `Regenerar` |
| | `App.Application.Tests/Proyectos/ActividadVigenteTests.cs` (nuevo) | 8 |
| | `App.Application.Tests/Proyectos/Cabecera/EdicionCabeceraH15Tests.cs` (nuevo) | 3 |
| | `App.Infrastructure.Tests/Consultas/VistasSqlTests.cs` (nuevo) | 2 |
| | `ConsultasExistentesSqlTests.Detalle_ActividadVigente`, `EdicionCabeceraServicioTests.Registrar_SoloActividad…` | Ajustadas (aprobadas, sección 5) |
| Manual | `backend/tests/manual/tarea18/parte3.cmd`, `parte4.cmd`, `parte3-vista.sql`, `parte4-descansos.sql` (nuevos) | Sección 6 |
| Docs | `FASE_5_Edicion_Cabecera.md` (§7), `FASE_5_Estados_Proyecto.md` (E4), `00_ESTADO_ACTUAL.md` (§5, §6, §7: pendiente 31, §8), `TAREA-18-reporte.md` (resultados reales del 06/10) | — |

**Hoy en Ecuador en SQL:** `CAST(SYSDATETIMEOFFSET() AT TIME ZONE 'SA Pacific Standard Time' AS DATE)` es la misma zona que `FechaNegocio.ZonaNegocio` (UTC-5 sin horario de verano). La V1 usaba `SWITCHOFFSET(…, '-05:00')`, equivalente hoy pero atado a un desfase fijo.

## 4. SQL (pruebas de traducción)
| Consulta / definición | Prueba | Puntos verificados |
|---|---|---|
| `ProyectoConsultas.ConsultaActividadVigente` (modificada) | `Detalle_ActividadVigente` (ajustada) | `FechaInicio <= @`, `FechaFin >= @`, `ORDER BY [p].[Version] DESC`, **sin `CASE`** |
| `VistasSql.VwProyectoResumen_V2` (nueva) | `VwProyectoResumen_V2_FiltroDeCobertura_SinRespaldoPorVersion` | `AT TIME ZONE 'SA Pacific Standard Time'`, cálculo de la referencia, filtro de cobertura, mayor versión, **sin** el respaldo de V1 ni `SWITCHOFFSET` |
| `VistasSql.VwProyectoResumen_V1` | `VwProyectoResumen_V1_NoCambia` | La V1 (usada por `Vistas` y por el Down) conserva su definición |

## 5. Pruebas
**Pruebas existentes ajustadas (las 2 aprobadas; ninguna otra cambió de resultado):**
| Prueba | Ajuste |
|---|---|
| `ConsultasExistentesSqlTests.Detalle_ActividadVigente` | Verifica el filtro de cobertura y la ausencia de `CASE` |
| `EdicionCabeceraServicioTests.Registrar_SoloActividad_CambioActividad_YActividadDeLaEtapaP3` | Espera DEV.02 (la nueva) en la etapa CAMBIO_ACTIVIDAD (O2) |

**Nuevas (18):**
- **Domain (5):** back recortado; back no recortado cuyo descanso pasa de F; sin descanso / back DESCANSO / descanso antes de F; deduplicación; **equivalencia con `MotorCronograma.Regenerar`** (tras acortar, el cronograma coincide desde el corte en empleado, fecha, rol, tipo y bloque).
- **Application (11):** `FechaReferencia` (3 casos), `Elegir` (cobertura y solape; sin cobertura → null), proyecto que aún no empieza (caso r1/r2), etapa de SUSPENSION igual a `ActividadVigenteEnF`, etapa ACTUALIZACION_PERSONAL de un proyecto que aún no empieza (antes null, ahora la del inicio); H15 en la cabecera (back recortado, back no recortado, registro con `DescansosAgregados` y ampliar sin ellos).
- **Infrastructure (2):** definición de la vista V2 y V1 sin cambios.

## 6. Prueba manual (`backend/tests/manual/tarea18/`, la ejecuta el usuario)

### 6.1 Cómo se ejecuta
1. **Reiniciar la API** (código nuevo). Para el listado, aplicar antes la migración: `dotnet ef database update --project src/App.Infrastructure --startup-project src/App.Api --context ProfesiogramaDbContext` (desde `backend/`).
2. `parte3.cmd` (no escribe; se puede repetir).
3. Opcional: `parte4.cmd` (**escribe** en el Id 11; protegido con `resultado-parte4.txt`, creado justo antes del primer registro). Luego `parte4-descansos.sql` en SSMS (solo lectura).
4. Si se aplicó la migración: `parte3-vista.sql` en SSMS (solo lectura).
- F se elige del GET de edición: F = max(hoy, inicio del back, inicio del proyecto), con F < fin del back y F < fin del proyecto. Con el Id 11 (back DEV001 16–17/10) da **F = 16/10** si se ejecuta hasta el 16/10; después no hay F válida: `parte3` marca "OMITIDO (sin datos)" y `parte4` termina sin escribir.
- `parte4` valida antes de escribir: vista previa del cambio de actividad (200 CAMBIO_ACTIVIDAD) y de acortar (200, back recortado y `diasAgregados`). Si alguna no es válida, termina sin escribir.

### 6.2 Resultados esperados (ejemplo con hoy entre el 07/10 y el 13/10/2026)
| Caso | Esperado | Resultado |
|---|---|---|
| t3a / t3b (O3) | Cabecera y detalle con la **misma** actividad vigente: DEV.01 v1 (referencia 11/10; del 14 al 16/10 sería DEV.02 v2) | [PENDIENTE] |
| t3c (listado) | El listado no expone la actividad; `parte3-vista.sql` (con la migración aplicada) debe dar la misma actividad | [PENDIENTE] |
| t3d / t3e (H15) | F = 16/10; DEV003 y DEV001 recortados a 16/10; `diasAgregados` DEV001 DESCANSO 17/10–18/10 (2) | [PENDIENTE] |
| u4a–u4d | F = 16/10; vigente en F DEV.02, nueva DEV.01; vistas previas válidas (1) | [PENDIENTE] |
| u4e | 200 versión 5; etapa CAMBIO_ACTIVIDAD con DEV.01 (O2) | [PENDIENTE] |
| u4f | 200 versión 6 (EDICION_CABECERA, fin 16/10) | [PENDIENTE] |
| u4g / u4h | Etapas v5 CAMBIO_ACTIVIDAD (DEV.01) y v6 EDICION_CABECERA; cabecera y detalle con la misma actividad vigente | [PENDIENTE] |
| `parte4-descansos.sql` | DEV001 (Back 1, fin 16/10, descanso 2): DESCANSO MANUAL 17/10 y 18/10, bloque 1 | [PENDIENTE] |
| `parte3-vista.sql` | `ActividadCodigo` del Id 11 igual al de la cabecera y el detalle | [PENDIENTE] |

### 6.3 Prueba de los scripts con `SIMULAR=1` (Claude Code)
- `BASE=https://localhost:1`; el helper `:http` copia `%SIMULACION%\NOMBRE.txt` y nunca llama a curl; el lanzador dejó en el `PATH` solo PowerShell y una copia de `chcp.com`. Respuestas simuladas a mano con el estado del Id 11 tras la prueba del 06/10 y HOY = 07/10.
- Caminos: completo (O3 IGUALES; F = 16/10; `diasAgregados` 17–18/10; parte 4 con versiones 5 y 6), **sin F válida** (HOY = 17/10: parte 3 OMITIDO y parte 4 sin escribir), **vista previa inválida** (sin `diasAgregados`: parte 4 sin escribir) y protección de `resultado-parte4.txt`.
- **Ninguna prueba tocó la API real.**

## 7. Comandos ejecutados y resultado
| Comando | Resultado |
|---|---|
| `dotnet ef migrations add ActividadVigenteVista … --configuration Release` | Migración vacía generada; snapshot sin cambios. Se completó Up/Down con la V2 / V1 |
| `dotnet build … -c Release` y `dotnet test` tras O3 | 467/468: solo `Registrar_SoloActividad…` (aprobada); se ajustó |
| `dotnet test` tras H15 | 468/468 |
| `App.Domain.Tests.exe -class …DescansoBacksTrasRecorteTests` | 5/5 |
| `App.Application.Tests.exe -class …ActividadVigenteTests -class …EdicionCabeceraH15Tests` | 11/11 |
| `App.Infrastructure.Tests.exe -class …VistasSqlTests` | 2/2 |
| `dotnet ef migrations script 20260930161822_Vistas 20261006125310_ActividadVigenteVista --idempotent … -o sql/ef/002_actividad_vigente_vista.sql` | Script generado (no se ejecutó) |
| `dotnet build Profesiograma.slnx -c Release --no-incremental` | **0 advertencias, 0 errores** |
| `dotnet test --solution Profesiograma.slnx -c Release` | **486/486** |
| `dotnet ef migrations has-pending-model-changes … --configuration Release` | "No changes have been made to the model since the last migration." |
| `dotnet ef migrations list … --configuration Release` | `ActividadVigenteVista` **(Pending)** |
| `run.cmd` del scratchpad (`SIMULAR=1`, PATH sin curl) | parte 3 y parte 4 OK en los caminos de 6.3 |

## 8. Errores y cómo se resolvieron
- Al escribir el cuerpo JSON desde CMD con comillas escapadas (`\"`), el reemplazo no coincidía; se cambió a `@{ … } | ConvertTo-Json` en PowerShell (sin comillas anidadas en la línea de CMD).

## 9. Contradicciones y observaciones
- **Listado:** `ProyectoResumenDto` (GET `/api/proyectos`) no expone la actividad, aunque la vista la calcula. El CONTROL del listado de `parte3` remite a `parte3-vista.sql`, de solo lectura y con la migración aplicada.
- **Etapas SUSPENSION / CIERRE y REACTIVACION:** pasan a la regla común con el mismo resultado; `RecorteProyecto.ActividadVigenteEnF` se conserva en Domain (lo usan sus pruebas) y una prueba nueva verifica que coincide.
- **Etapa CREACION** y **copia de la actividad al reactivar (R8):** sin cambios (la primera da el mismo resultado; la segunda no es una "actividad vigente" de vista ni de etapa).
- **Etapas ya guardadas:** no se modifican (en el Id 11, la v3 CAMBIO_ACTIVIDAD sigue con DEV.01).

## 10. Pendientes
- Aplicar la migración `ActividadVigenteVista` (usuario; pendiente 31).
- Ejecutar `parte3.cmd` (y, si se desea, `parte4.cmd` y los `.sql`) y registrar los resultados; después, marcar la TAREA-18 y la 18b como ✅.
