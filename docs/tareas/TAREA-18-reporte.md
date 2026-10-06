# TAREA-18 — Edición de cabecera (fechas, horario, almuerzo) y cambio de actividad, backend

**Fechas:** 2026-10-06 (Fase A y Fase B)
**Resultado:** implementada. **Prueba manual pendiente** (`backend/tests/manual/tarea18/parte1.cmd` y `parte2.cmd`, sección 6): la ejecuta el usuario. DEV.02 agregado al ERP simulado (pendiente 30 resuelto, sección 9); hay que **reiniciar la API** antes de la prueba manual.
- `dotnet build Profesiograma.slnx -c Release --no-incremental`: **0 advertencias, 0 errores**.
- `dotnet test --solution Profesiograma.slnx -c Release`: **468/468** (Domain 110, Application 259, Infrastructure 99). Antes: 413. Nuevas: 55. De las 413 anteriores solo cambiaron las **5 aprobadas** (sección 5) y, con DEV.02, `CatalogoErpSimuladoTests.Actividades_DevErp001` (aprobada).
- `has-pending-model-changes`: sin cambios. Sin modelo ni migraciones nuevas (EDICION_CABECERA y CAMBIO_ACTIVIDAD ya estaban en la semilla; C10 con una constante).

Sin `dotnet run`, sin SQL, sin cambios en `frontend/`, sin comandos git que modifiquen el repositorio y **sin ningún cliente HTTP contra la API** (los scripts solo se probaron con `SIMULAR=1` y sin `curl.exe` en el `PATH`).

## 1. Decisiones aprobadas
- C1–C11 del enunciado; respuestas P1–P6 de la Fase A; ajuste adicional de C4 (advertencia "El proyecto quedará sin principal inicial.").
- Documentadas en `docs/fases/FASE_5_Edicion_Cabecera.md` (nuevo).

## 2. Verificación del origen (Fase A)
| Hallazgo | Resultado |
|---|---|
| H13 | Confirmado con una corrección: el nuevo inicio **tampoco** llega a `INF_GENERAL` (l. 2757 usa `gblFechaInicioProyecto`, el valor cargado); solo llega a la etapa (l. 2608, 2664). El Patch no guarda `FECHA_INICIO` (l. 2875–2888) |
| H14 | Confirmado (l. 2883 y 2681) |
| Cambio de actividad | Confirmado (l. 2302–2329, reemplazo de la tabla l. 2681–2715). **No exige Regenerar**: sin Regenerar se guarda sin etapa; con Regenerar la etapa es ACTUALIZACION_PERSONAL (l. 286, 2599) |
| Horario y almuerzo | Confirmado: no se editan (l. 2744–2753) |
| Acortar | El original no recorta el personal: Regenerar falla RN08 (R‑885–935). Aquí C4 recorta (diferencia D17) |

## 3. Qué se hizo
| Capa | Archivo | Cambio |
|---|---|---|
| Domain | `Proyectos/Cabecera/ActividadesCabecera.cs` (nuevo) | `MovimientoInicioProyecto` (P1), `AmpliacionProyecto` (C3), `CambioActividad` (C6) |
| Application | `Proyectos/Cabecera/EdicionCabeceraContratos.cs`, `EdicionCabeceraDtos.cs`, `EdicionCabeceraValidador.cs`, `EdicionCabeceraServicio.cs` (nuevos) | Solicitud, DTO, reglas C1–C7 y P1–P3, servicio con applock y relectura (C8), C9 |
| | `Proyectos/Crear/ReglasHorarioAlmuerzo.cs` (nuevo) | Horario ERP y almuerzo, extraídos de `CrearProyectoValidador` con los mismos mensajes |
| | `Proyectos/Crear/CrearProyectoValidador.cs` | Usa `ReglasHorarioAlmuerzo`; `MinimoPrincipales = 1` (C10) |
| | `Proyectos/Estados/VistaRecorte.cs` (nuevo) | Impacto del recorte y snapshot, extraídos de `CambioEstadoServicio` (mismo resultado) |
| | `Proyectos/Estados/CambioEstadoServicio.cs` | Usa `VistaRecorte` |
| | `Proyectos/Estados/CambioEstadoValidador.cs` | C11: la fecha solo se valida con SUSPENSION o CIERRE |
| | `Proyectos/Personal/EdicionPersonalServicio.cs`, `EdicionPersonalDtos.cs` | C9: sin cambios → 400 `general`; advertencia "No hay cambios." |
| | `DependencyInjection.cs` | Validador y servicio de cabecera |
| Infrastructure | `Persistencia/Proyectos/EdicionCabeceraRepositorio.cs` (nuevo) | Lectura, escritura y consultas `ConsultaCabeceraEdicion`, `ConsultaActividadesEdicion` |
| | `Persistencia/Proyectos/EscrituraRecorte.cs` (nuevo) | Pasos de escritura del recorte, extraídos de `CambioEstadoRepositorio` (mismo orden) |
| | `Persistencia/Proyectos/CambioEstadoRepositorio.cs` | Usa `EscrituraRecorte` |
| | `Persistencia/PersistenciaServiceCollectionExtensions.cs` | `ICabeceraRepositorio` |
| Api | `Endpoints/ProyectoEndpoints.cs` | `GET /{id}/cabecera`, `POST /{id}/cabecera/previsualizar`, `POST /{id}/cabecera` |
| | `Profesiograma.Dev.http` | Casos Y1–Y10 (no ejecutados) |
| Pruebas | `App.Domain.Tests/Proyectos/Cabecera/ActividadesCabeceraTests.cs` (nuevo) | 10 |
| | `App.Application.Tests/Proyectos/Cabecera/DoblesCabecera.cs`, `EdicionCabeceraServicioTests.cs` (nuevos) | 36 |
| | `App.Application.Tests/Proyectos/Personal/EdicionPersonalC9Tests.cs` (nuevo) | 3 (C9) |
| | `App.Application.Tests/Proyectos/Estados/CambioEstadoValidadorTests.cs` | +3 (C11, teoría) y 1 ajustada |
| | `App.Infrastructure.Tests/Consultas/EdicionCabeceraSqlTests.cs` (nuevo) | 3 `ToQueryString` |
| Manual | `backend/tests/manual/tarea18/` (nuevo) | `parte1.cmd`, `parte2.cmd` y 17 plantillas JSON (ASCII, CRLF) |
| Docs | `FASE_5_Edicion_Cabecera.md` (nuevo), `FASE_5_Crear_Proyecto.md`, `FASE_5_Estados_Proyecto.md`, `FASE_5_Edicion_Cronograma.md` (§8.5), `00_ESTADO_ACTUAL.md` | §1, §6, §7 (25, 28, 29 resueltos; 26 con nota P6; 30 nuevo), §8 |
| Infrastructure | `Erp/CatalogoErpSimulado.cs` | DEV.02 "ACTIVIDAD DE PRUEBA 2" (tipo PRUEBA) para DEV-ERP-001, solo Development (pendiente 30) |
| Pruebas | `App.Infrastructure.Tests/Erp/CatalogoErpSimuladoTests.cs` | `Actividades_DevErp001` espera DEV.01 y DEV.02, en el orden del simulador |

## 4. SQL de las consultas nuevas (pruebas de traducción)
| Consulta | Prueba | Puntos verificados |
|---|---|---|
| `ConsultaCabeceraEdicion` | `CabeceraEdicion_VisibilidadGrupoHorarioAlmuerzoYRowVer`, `CabeceraEdicion_Admin_SinFiltroDePropietario` | `FROM [dbo].[Proyecto]`, no eliminado, filtro de propietario (y sin él para Admin), `RequiereProyectoErp`, horario, almuerzo, `RowVer`, sin cédula |
| `ConsultaActividadesEdicion` | `ActividadesEdicion_ConDescripcionTipoYTipoDeMovimiento` | `FROM [dbo].[ProyectoActividad]`, `INNER JOIN [dbo].[TipoMovimiento]`, descripción, `ORDER BY [p].[Version]` |

El resto reutiliza consultas con prueba: personal (`ConsultaPersonalEdicion`), días posteriores, filtro del DELETE (`ConsultaDiasAEliminar`), última versión, tipo de movimiento, proyecto y actividades con seguimiento.

## 5. Pruebas
**Pruebas existentes ajustadas (las 5 aprobadas; ninguna otra cambió de resultado):**
| Prueba | Ajuste |
|---|---|
| `P14_DentroDeLaTransaccion_ElPersonalCambio_409` | Cuerpo con un cambio real (P1 a TIPO_2) por C9 |
| `DentroDeLaTransaccion_EstadoCambio_409` | Igual |
| `ConflictoAlGuardar_409_YRevierte` (personal) | Igual |
| `SinPrincipales_EsValido_P1MinimoCero` → `SinPrincipales_400_C10MinimoUno` | Ahora espera 400 "Se requiere al menos 1 principal(es)." |
| `Acumula_ErroresDeDestinoYFecha` → `DestinoInvalido_NoValidaLaFecha_C11` | Ahora espera solo `estadoDestino` |

**Nuevas (55):**
- **Domain (10):** inicio atrasado y adelantado (P1), actividad que termina antes del nuevo inicio, ampliar (solo las que terminaban en el fin anterior), ampliar con fin menor (excepción), cambio de actividad (recorte + nueva máx + 1, eliminación de las que empiezan en `desde` o después, `desde` en el inicio de una actividad, misma actividad, hueco y versión sobre todas).
- **Application, cabecera (36):**
  - GET: todo editable; inicio pasado no editable con motivo; no ACTIVO y no visible.
  - C2/P1: personal antes del nuevo inicio; inicio < hoy; proyecto que ya empezó; inicio > fin; adelantar (actividad movida, sin días ni personal); atrasar (actividad movida); actividad que termina antes.
  - C3/C4/P2: ampliar (H14); fin < hoy; acortar (recortes, días > F, actividad); back sin principal + sin principal inicial; inicial conservado.
  - C5: horario inactivo; almuerzo (formato, rangos, regreso ≤ salida guardada); horario y almuerzo válidos.
  - C6: grupo sin proyecto ERP; actividad que no está en el ERP y `desde` fuera del rango; misma actividad; cambio válido (CAMBIO_ACTIVIDAD); `desde` pasado (advertencia); con fin ampliado y con fin acortado.
  - Registro: solo actividad (CAMBIO_ACTIVIDAD, versión, P3); actividad desde el inicio (P3); acortar + horario (recorte, horario, almuerzo, snapshot); relectura con estado, `RowVer` o fechas cambiados (409); conflicto al guardar; no ACTIVO (400) y no visible (404); admin sin filtro.
  - C9 en la cabecera.
- **Application, C9 personal (3)** y **C11 (3, teoría)**.
- **Infrastructure (3):** `ToQueryString`.

## 6. Prueba manual (`backend/tests/manual/tarea18/`, la ejecuta el usuario)

### 6.1 Cómo se ejecuta
1. Con la API corriendo: `backend\tests\manual\tarea18\parte1.cmd` (elige el par de empleados y crea el proyecto C). Opcional: `parte1.cmd DEV003 DEV004` o `empleados.txt`.
2. **El mismo día o el siguiente:** `backend\tests\manual\tarea18\parte2.cmd` (lee `valores-parte1.txt`; escriben solo o, p y q). El caso d adelanta el inicio a H+3, que debe ser ≥ hoy.
- Fechas relativas al día de `parte1.cmd` (H): INICIO H+5, P1 desde H+8, back H+10–H+12 (2 días de descanso), FIN H+40; casos: INICIO_TARDE H+9, INICIO_ANTES H+3, CORTO H+11, LARGO H+60, DESDE H+8; AYER = hoy − 1.
- Elección de empleados como en la TAREA-17b (primero DEV007/DEV008, luego los demás pares de DEV001–DEV008 sin DEV005), solo con la vista previa de la creación; si ninguno sirve, termina sin escribir.
- Protecciones: `resultado-parte1.txt` (creado justo antes de crear C) y `resultado-parte2.txt` (creado justo antes de n, el primer POST de registro). Si se detiene antes, la salida queda en `tmp\parteN-previo.txt`.
- Los Id 1–10 solo reciben GET y vistas previas (b1 Id 10, b2 Id 2, u Id 2).

### 6.2 Resultados esperados
Ejemplo con **H = 06/10/2026**: INICIO 11/10, P1 14/10–15/11, back 16/10–18/10 (descanso 19–20/10), FIN 15/11; INICIO_TARDE 15/10, INICIO_ANTES 09/10, CORTO 17/10, LARGO 05/12, DESDE 14/10, AYER 05/10.

| Caso | Esperado | Ejemplo H = 06/10/2026 | Resultado |
|---|---|---|---|
| P0a | Par elegido, sin cruces | — | [PENDIENTE] |
| P0 / P0b / P0c | 201; ACTIVO INICIO–FIN; cabecera con todo editable, `fechaFinMinima` = H, v1 DEV.01 | 11/10–15/11 | [PENDIENTE] |
| a | GET cabecera de C: `puedeEditar`, inicio editable, `fechaFinMinima` H, actividad editable | — | [PENDIENTE] |
| b1 | GET cabecera del Id 10: inicio no editable con motivo | "…el proyecto empezó el 02/10/2026." | [PENDIENTE] |
| b2 | GET cabecera del Id 2: `puedeEditar` false (SUSPENDIDO) | — | [PENDIENTE] |
| c | 400 `fechaInicio` "Hay personal que empieza antes de la nueva fecha de inicio; ajusta primero el personal." | inicio 15/10 | [PENDIENTE] |
| d | 200; actividad v1 MODIFICADA desde INICIO_ANTES (P1) | 09/10 | [PENDIENTE] |
| e | 200; actividad v1 MODIFICADA desde H+8 (P1, atrasar) | 14/10 | [PENDIENTE] |
| f | 400 `fechaInicio` "La nueva fecha de inicio no puede ser anterior a hoy (…)." | — | [PENDIENTE] |
| g | 400 `fechaFin` "La fecha fin no puede ser anterior a hoy (…)." | — | [PENDIENTE] |
| h | 200; P1 y back recortados a CORTO; días de P1 y del back posteriores eliminados (incluido el descanso del back); actividad v1 hasta CORTO | 17/10 | [PENDIENTE] |
| i | 200; actividad v1 MODIFICADA hasta LARGO (C3, H14); sin recortes | 05/12 | [PENDIENTE] |
| j1 / j2 | 400 `salidaAlmuerzo` (rango) / 400 `horarioCodigo` (horario 3 inactivo) | — | [PENDIENTE] |
| k | 200 EDICION_CABECERA; cambios horario, salida y regreso; actividad SIN_CAMBIO | — | [PENDIENTE] |
| l | 400 `actividad.actividadId` "La actividad DEV.01 ya está vigente el (DESDE)." | 14/10/2026 | [PENDIENTE] |
| m | 200 con "No hay cambios." | — | [PENDIENTE] |
| pv | 200 CAMBIO_ACTIVIDAD (v1 hasta DESDE − 1, NUEVA v2 DEV.02). Si la API no se reinició tras agregar DEV.02: 400 "La actividad no existe en el proyecto ERP." | v1 11/10–13/10; v2 14/10– | [PENDIENTE] |
| n | 400 `general` "No hay cambios para registrar." | — | [PENDIENTE] |
| o | 200 `{ C, 2 }` (ampliar + horario 2 + almuerzo 12:00–13:00) | — | [PENDIENTE] |
| p | 200 `{ C, 3 }` CAMBIO_ACTIVIDAD (OMITIDO solo si pv dio 400) | — | [PENDIENTE] |
| q | 200 `{ C, 4 }` (o `{ C, 3 }` si p se omitió) acortar a CORTO | — | [PENDIENTE] |
| r1 | Cabecera: INICIO–CORTO, horario 2, almuerzo 12:00–13:00; actividades v1 DEV.01 INICIO–DESDE−1 y v2 DEV.02 DESDE–CORTO (sin DEV.02: v1 INICIO–CORTO) | 11/10–17/10; v1 11–13/10, v2 14–17/10 | [PENDIENTE] |
| r2 | Detalle: etapas v1 CREACION, v2 EDICION_CABECERA (INICIO–LARGO, corte H), v3 CAMBIO_ACTIVIDAD, v4 EDICION_CABECERA (INICIO–CORTO, corte H); actividad de las etapas DEV.01 (vigente en INICIO, P3); P1 y back hasta CORTO | — | [PENDIENTE] |
| s1 / s2 | Personal sin cambios: 200 con "No hay cambios." / 400 `general` (C9) | — | [PENDIENTE] |
| t | 400 `principales` "Se requiere al menos 1 principal(es)." (C10) | — | [PENDIENTE] |
| u | 400 con **solo** `estadoDestino` "La reactivación se registra con la opción Reactivar." (C11) | — | [PENDIENTE] |

### 6.3 Prueba de los scripts con `SIMULAR=1` (Claude Code)
- `SIMULAR=1` → `BASE=https://localhost:1`; el helper `:http` copia `%SIMULACION%\NOMBRE.txt` y **nunca** llama a curl. El lanzador dejó en el `PATH` solo PowerShell y una copia de `chcp.com`.
- Respuestas simuladas escritas a mano (H = 06/10/2026). Caminos probados:
  - completo: el par por defecto con cruces se descarta y se elige DEV001/DEV002; todas las líneas CONTROL de 6.2; cuerpo de s armado del GET de edición (P1 con `principalClave` para el back);
  - sin DEV.02: pv 400 → p "OMITIDO" y q queda como versión 3;
  - segunda ejecución: las dos protecciones se activan.
- Se corrigió una colisión de nombres: Windows no distingue mayúsculas y el caso `p0` de la parte 2 pisaba el `P0` de la parte 1 en `simulacion\` y `tmp\`; ahora se llama `pv`.
- **Ninguna prueba tocó la API real.**

## 7. Comandos ejecutados y resultado
| Comando | Resultado |
|---|---|
| `dotnet build Profesiograma.slnx -c Release` y `dotnet test --solution Profesiograma.slnx -c Release` tras C9, C10 y C11 | 0/0; 413/413 (con las 5 pruebas aprobadas ajustadas) |
| Lo mismo tras extraer `ReglasHorarioAlmuerzo`, `VistaRecorte` y `EscrituraRecorte` | 0/0; 413/413 |
| `App.Domain.Tests.exe -namespace …Proyectos.Cabecera` | 10/10 |
| `App.Application.Tests.exe -namespace …Proyectos.Cabecera` | 36/36 |
| `App.Application.Tests.exe -class …EdicionPersonalC9Tests -class …CambioEstadoValidadorTests` | 22/22 |
| `App.Infrastructure.Tests.exe -class …EdicionCabeceraSqlTests` | 3/3 |
| `dotnet build Profesiograma.slnx -c Release --no-incremental` | **0 advertencias, 0 errores** |
| `dotnet test --solution Profesiograma.slnx -c Release` | **468/468** |
| `dotnet ef migrations has-pending-model-changes … --configuration Release` | "No changes have been made to the model since the last migration." |
| `run.cmd` del scratchpad (`SIMULAR=1`, PATH sin curl) | parte1 y parte2 OK en los caminos de 6.3 |
| Pendiente 30: `dotnet build … --no-incremental`, `dotnet test`, `has-pending-model-changes` | 0/0; 468/468 (solo cambió `Actividades_DevErp001`, ajustada); sin cambios |
| Pendiente 30: `run.cmd` con DEV.02 (`SIMULAR=1`, PATH sin curl) | pv 200, p versión 3, q versión 4 |

## 8. Errores y cómo se resolvieron
- Heredocs de Bash con comillas: los generadores de scripts y plantillas se escribieron como archivos de Python en el scratchpad.
- Colisión `p0` / `P0` en la simulación (sección 6.3): caso renombrado a `pv`.
- Una sección nueva de `FASE_5_Edicion_Cronograma.md` quedó antes de §8.4; se movió al final (§8.5).

## 9. Contradicciones, observaciones y pendientes de decisión
- **DEV.02 (P5) — resuelto:** en la Fase B quedó en espera porque `CatalogoErpSimuladoTests.Actividades_DevErp001` esperaba una sola actividad. El usuario aprobó agregarla y ajustar solo esa prueba (pendiente 30): `CatalogoErpSimulado` tiene DEV.01 y DEV.02 para DEV-ERP-001 y la prueba espera las dos, en el orden del simulador. Ninguna otra prueba cambió. Con `SIMULAR=1` (PATH sin `curl.exe`) se confirmó que los scripts no necesitan cambios: pv da 200 y p se ejecuta (versión 3; q queda en la 4). **La API en ejecución usa el simulador en memoria: hay que reiniciarla para que DEV.02 aparezca.**
- **H13:** el inicio tampoco llegaba a `INF_GENERAL` (sección 2).
- **"Exige Regenerar":** sin Regenerar, el original guarda sin etapa (sección 2).
- **C10:** no había parámetro `MinimoPrincipales`, era una constante (0); ahora 1. El formulario de creación (frontend) no exige principal: TAREA-19 (nota en el pendiente 26).
- **Mensajes nuevos** (no venían en el enunciado): "Solo se puede editar la cabecera de un proyecto ACTIVO (estado actual: X).", "La fecha de inicio ya no se puede cambiar: el proyecto empezó el dd/MM/yyyy.", "La nueva fecha de inicio no puede ser anterior a hoy (dd/MM/yyyy).", "La fecha fin no puede ser anterior a hoy (dd/MM/yyyy).", "La actividad X ya está vigente el dd/MM/yyyy.", "La actividad es obligatoria.", "La fecha desde la que aplica la actividad es obligatoria.", "La fecha desde debe estar dentro del rango del proyecto (inicio – fin)."; reutilizados de la creación: "La fecha fin debe ser mayor o igual a la fecha de inicio.", "El grupo X no usa actividad.", "La actividad no existe en el proyecto ERP.".
- **Actividad vigente del GET:** se usa `max(hoy, inicio)` como en P3 (un proyecto que aún no empieza no tiene actividad vigente hoy).

## 10. Pendientes
- Ejecutar `parte1.cmd` y `parte2.cmd` (usuario) y registrar sus controles; después, marcar la TAREA-18 como ✅.
- Reiniciar la API antes de la prueba manual (DEV.02 en el simulador).
- TAREA-19: frontend de edición (personal, cabecera, reactivación) y mínimo de 1 principal en la creación (pendiente 26).
