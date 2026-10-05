# TAREA-17 — Edición: actualización de personal (backend)

**Fechas:** 2026-10-02 (Fase A y Fase B)
**Resultado:** ✅ terminada. Prueba HTTP P0–P15 del 05/10/2026 (sección 6.1), correcciones aplicadas (6.2), `parte3.cmd` según lo esperado (6.3). Incidente de registros no autorizados en el Id 9, aceptado por el usuario (6.5).
- `dotnet build Profesiograma.slnx -c Release --no-incremental`: **0 advertencias, 0 errores**.
- `dotnet test --solution Profesiograma.slnx -c Release`: **375/375** tras las correcciones de la sección 6.2 (372 en la entrega inicial: 342 + 30 nuevas; +3 con las correcciones). Ninguna prueba existente modificada.
- `has-pending-model-changes`: sin cambios. Sin modelo ni migraciones nuevas.

Sin `dotnet run`, sin SQL manual, sin cambios en `frontend/`, sin comandos git que modifiquen el repositorio.

## 1. Decisiones aprobadas (Fase A)
| # | Decisión |
|---|---|
| D1 | `GET …/edicion` sobre un proyecto no ACTIVO o ya terminado: 200 con `puedeEditar = false` y `motivo`. Los POST responden 400 |
| D2 | `FechaInicio` de una persona vigente que aún no empieza: se puede cambiar, pero **≥ corte** |
| D3 | Relación de un back con un principal histórico: campo `principalId`, excluyente con `principalClave` |
| D4 | Empleado activo: se valida **solo en las nuevas** |
| D5 | `FechaFin ≥ corte − 1` **solo si cambia** |
| D6 | `EsPrincipalInicial` de las nuevas = `false` (H4 en la TAREA-17b) |
| D7 | Orden de escritura: días ≥ corte → vigentes + nuevas → relaciones y omitidas → días + etapa. **Reemplaza el orden de E7 del enunciado**, por la FK autorreferenciada y porque las nuevas necesitan su Id antes |
| D8 | Snapshot de la etapa: todo el personal resultante, incluidas las históricas |
| D9 | `ReglasPersonal` extraído de `CrearProyectoValidador` **sin cambiar sus mensajes**: las pruebas de la creación pasan sin modificarse |
| D10 | `personal` en la vista previa: clase y acción por persona |

## 2. Verificaciones antes de implementar (pedidas por el usuario)
| # | Verificación | Resultado |
|---|---|---|
| 1 | P7: ¿el 05 y el 06/10/2026 son días PRINCIPAL de DEV005 en el Id 5? | **Sí.**<br>• `GET /api/proyectos/5` (admin): DEV005 TIPO_1 (22/8) del 01/10 al 31/12/2026. El detalle no expone los días.<br>• Para verlos se usó `POST /api/proyectos/5/cambio-estado/previsualizar` (admin, SUSPENDIDO al 04/10), **que no guarda** (comprobado en la TAREA-14, S1b). Resultado: DEV005 PRINCIPAL 64 días (05/10–31/12) y DESCANSO 24 días **desde el 23/10**, así que del 05 al 22/10 todos son de trabajo.<br>• Después, el Id 5 seguía ACTIVO, con fin 31/12 y 1 etapa.<br>• **P7 se mantiene** con el 05 y el 06/10 |
| 2 | ¿`CrearProyectoValidador` acepta `FechaInicio` < hoy? | **Sí.** No usa el reloj: solo exige la fecha, fin ≥ inicio y personal dentro del rango. El "Proyecto A" (21/09–29/11/2026) se puede crear por la API |
| 3 | Fecha de Ecuador al terminar la Fase B | **2026-10-02** (`Intl.DateTimeFormat`, zona `America/Guayaquil`). Los resultados esperados de la sección 6 siguen valiendo |

## 3. Qué se hizo
| Capa | Archivo | Contenido |
|---|---|---|
| Application | `Proyectos/Crear/ReglasPersonal.cs` (nuevo) | Reglas comunes (empleado, fechas RN08, jornada, tipo de registro, días de descanso, largos, formato) extraídas de la creación |
| | `Proyectos/Crear/CrearProyectoValidador.cs` | Usa `ReglasPersonal` (mismos mensajes) |
| | `Proyectos/Crear/CalculadorCruces.cs` | `OrigenHistorico`, `Internos(IEnumerable<CruceInterno>, …)` y `Historicos(…)`. Lo existente no cambia |
| | `Proyectos/Personal/EdicionPersonalContratos.cs` (nuevo) | Solicitud, `PersonaGuardada` (con `EsHistorica`), `DatosEdicion`, `CambioPersonal`, `IEdicionPersonalRepositorio` |
| | `Proyectos/Personal/EdicionPersonalDtos.cs` (nuevo) | DTO del GET, de la vista previa y del resultado; `ResultadoEdicionPersonal` |
| | `Proyectos/Personal/EdicionPersonalValidador.cs` (nuevo) | E1–E4, D2–D5 |
| | `Proyectos/Personal/EdicionPersonalServicio.cs` (nuevo) | GET de edición; cálculo común (validar → motor → cruces); registro en el applock con recálculo (E7) |
| | `DependencyInjection.cs` | + validador y servicio |
| Infrastructure | `Persistencia/Proyectos/EdicionPersonalRepositorio.cs` (nuevo) | Lectura (reutiliza consultas de `CambioEstadoRepositorio`) y escritura D7 |
| | `PersistenciaServiceCollectionExtensions.cs` | + repositorio |
| Api | `Endpoints/ProyectoEndpoints.cs` | `GET /{id:int}/edicion`, `POST /{id:int}/personal/previsualizar`, `POST /{id:int}/personal` |
| | `Profesiograma.Dev.http` | P0 (crear el "Proyecto A") y P1–P14 |
| Pruebas | `App.Application.Tests/Proyectos/Personal/` (dobles + 27 pruebas) | Casos P1–P15 con dobles, validaciones, relaciones, registro y conflictos, `CalculadorCruces` |
| | `App.Infrastructure.Tests/Consultas/EdicionPersonalSqlTests.cs` (3) | Traducción de las consultas nuevas |

## 4. SQL de las consultas nuevas (pruebas de traducción)
- **Personal:** `FROM [dbo].[ProyectoPersonal] … INNER JOIN [dbo].[Empleado] … LEFT JOIN [dbo].[Jornada] … ORDER BY [p].[RolAsignacionId], [p].[Numero]`, **sin `[Cedula]` ni `[CorreoEmpresa]`**.
- **Base:** `… FROM [dbo].[ProyectoAsignacionDia] AS [p] WHERE [p].[ProyectoId] = @… AND [p].[Fecha] < @…`.
- **Filtro del DELETE masivo:** `WHERE [p].[ProyectoId] = @… AND [p].[Fecha] >= @…`. `ExecuteDelete` no tiene `ToQueryString()`; se prueba el `IQueryable` del filtro.
- **Reutilizadas con prueba previa:** cabecera, actividades, última versión, Id del tipo de movimiento, personal con seguimiento, cruces externos, jornadas, límites y empleados activos.

## 5. Comandos ejecutados y resultado
| Comando | Resultado |
|---|---|
| `GET /api/proyectos/1..5` (admin, solo lectura) | Ocupación de empleados en octubre y noviembre de 2026 (Fase A) |
| `GET /api/proyectos/5` + `POST …/5/cambio-estado/previsualizar` (admin, no guarda) | Verificación 1 (sección 2) |
| `dotnet build Profesiograma.slnx -c Release` (tras extraer `ReglasPersonal`) + `dotnet test` | 0/0; **342/342** (la creación sin cambios) |
| `App.Application.Tests.exe -namespace …Personal` | 27/27 |
| `dotnet build Profesiograma.slnx -c Release --no-incremental` | 0 advertencias, 0 errores |
| `dotnet test --solution Profesiograma.slnx -c Release` | **372/372** |
| `dotnet ef migrations has-pending-model-changes … --configuration Release` | "No changes have been made to the model since the last migration." |

**Errores y cómo se resolvieron:**
- Ninguno de compilación ni de pruebas.
- Al agregar los casos al `.http`, el heredoc de bash falló por las comillas. Se escribió el bloque en el scratchpad y se agregó con `cat >>`.

## 6. Prueba HTTP P1–P15 (la ejecuta el usuario)
**Si se ejecuta otro día, recalcular con el corte de ese día.** Los resultados de esta tabla suponen corte **C = 02/10/2026**. Usuario `gestor` salvo indicación.

**Preparación**
- **P0:** crear el "Proyecto A" (`.http`). Se espera el Id **9**.
  - P1 **DEV006** TIPO_3, 21/09–29/11 (vigente).
  - P2 **DEV007** TIPO_2, 21/09–30/09 (histórico).
  - K1 **DEV007** JORNADA, 20/10–23/10 + 2 días, relacionado con P1 (vigente que aún no empieza).
- Completar `@idA`, `@idP1`, `@idP2` e `@idK1` con lo que devuelve P1.
- Solo **P0 y P13** escriben, y solo en el proyecto A. No se tocan PRY-DEV-0001..0003, el Id 4 ni los Id 6–8; el Id 2 y el Id 5 solo reciben peticiones que no escriben.

| # | Caso | Esperado | Resultado |
|---|---|---|---|
| P1 | `GET …/{A}/edicion` | 200. Corte 2026-10-02, `puedeEditar` true.<br>• P1 VIGENTE: `{fechaInicio: false, fechaFinMinima: 2026-10-01, jornada: true, eliminable: false}`.<br>• P2 HISTORICO, permisos todos false.<br>• K1 VIGENTE: `{true, 2026-10-01, false, true}`.<br>• Límites 20/20/20 | [PENDIENTE] |
| P2 | P1 a **TIPO_2** (vista previa) | 200 sin cruces ni advertencias.<br>• DEV006: DESCANSO AUTO **02–05/10**; PRINCIPAL 06–16/10, 21–31/10, 05–15/11, 20–29/11; DESCANSO 17–20/10, 01–04/11, 16–19/11.<br>• K1 BACK 20–23/10 y DESCANSO 24–25/10.<br>• P1 MODIFICADO | [PENDIENTE] |
| P3 | P1 con fin **01/10** (C − 1) | 200: ningún tramo de DEV006 desde el 02/10; P1 MODIFICADO | [PENDIENTE] |
| P3b | P1 con fin **30/09** | 400 `principales[0].fechaFin`: "La fecha fin no puede ser anterior al 01/10/2026 (día anterior al corte)." | [PENDIENTE] |
| P4 | Omitir K1 | 200: K1 **ELIMINADO**; DEV006 PRINCIPAL 02/10 y DESCANSO 03–04/10 | [PENDIENTE] |
| P5 | Back nuevo **DEV008** 28/09–04/10 (M1) | 200 con advertencia "Se generarán días anteriores al corte (02/10/2026) para el personal nuevo."; BACK 28/09–04/10; número **2**; sin cruces | [PENDIENTE] |
| P6a / P6b | Back nuevo **DEV006** 29–30/09 | Vista previa: 200 con 2 cruces **HISTORICO** (29 y 30/09, MISMO PROYECTO) + advertencia M1. Registro: **409** con `cruces` y `resumen` | [PENDIENTE] |
| P7a / P7b | Back nuevo **DEV005** 05–06/10 | Vista previa: 200 con 2 cruces **EXTERNO** contra PRY-20261001-4c34f3 (ACTIVO). Registro: **409** | [PENDIENTE] |
| P8 | Enviar P2 (histórico) | 400 `principales[1].id`: "La persona P2 es histórica (terminó el 30/09/2026) y no se puede modificar." | [PENDIENTE] |
| P9 | P1 con `empleadoId` 7 | 400 `principales[0].empleadoId`: "No se puede cambiar el empleado de una persona vigente. Para reemplazarla, acorte su fecha fin y agregue una persona nueva." | [PENDIENTE] |
| P10 / P10b | Id 2 (SUSPENDIDO): previsualizar / GET | 400 `proyecto`: "Solo se puede modificar el personal de un proyecto ACTIVO (estado actual: SUSPENDIDO)." / 200 con `puedeEditar` false y ese motivo | [PENDIENTE] |
| P11 | Gestor sobre el Id 5 | 404 | [PENDIENTE] |
| P12 | `anonimo` | 401 | [PENDIENTE] |
| P13 / P13b | **Registrar:** P1 TIPO_2 + omitir K1 + back nuevo DEV008 28/09–04/10 | 200 `{ id: A, version: 2 }`.<br>• Detalle: P1 TIPO_2; K1 eliminado; **Back 2** DEV008.<br>• Etapa v2 ACTUALIZACION_PERSONAL con corte 02/10 y actividad DEV.01.<br>• Estado y fechas del proyecto sin cambios | [PENDIENTE] |
| P14 | Reenviar con el `id` de K1 (ya eliminado) | 409 "El proyecto cambió; vuelve a cargarlo." | [PENDIENTE] |
| P15 | Proyecto ACTIVO con fin anterior al corte | Solo cubierto por pruebas (no hay datos así): 400 `proyecto` "El proyecto finalizó el 01/10/2026; amplía la fecha fin antes de modificar el personal." | Prueba `P15_ProyectoQueYaTermino_400` ✅ |

**Ciclo de referencia de P1 (TIPO_3 desde el 21/09):**
- Trabajo: 21–25/09, 28/09–02/10, 05–09/10, 12–16, 19–23, 26–30, 02–06/11, 09–13, 16–20, 23–27.
- Descanso: 26–27/09, 03–04/10, 10–11, 17–18, 24–25, 31/10–01/11, 07–08/11, 14–15, 21–22, 28–29.

### 6.1 Resultados reales (usuario, 05/10/2026, `parte1.cmd` y `parte2.cmd`)
Las pruebas se ejecutaron el **05/10/2026**, así que el **corte fue 05/10** y no 02/10. Lo esperado se recalculó con ese corte, como indica la nota de la sección 6.
- P0 creó **PRY-20261005-da396e** (Id **9**), con P1 Id 14, P2 Id 15 y K1 Id 16.
- Salidas en `backend/tests/manual/tarea17/resultado-parte1.txt` y `resultado-parte2.txt`.

| Caso | HTTP obtenido | Esperado (corte 05/10) | ¿Coincide? | Diferencia |
|---|---|---|---|---|
| P0 | 201, Id 9 | 201 | Sí | — |
| P1 | 200 | 200, corte 05/10, `fechaFinMinima` 04/10 | Sí | — |
| P2 | 200 | 200 sin cruces | Sí | DESCANSO AUTO **05–06/10** y PRINCIPAL desde el 06/10: **superposición el 06/10** → corrección **M4** (6.2) |
| P3 | 400 | 400 (el fin 01/10 queda antes de 04/10) | Sí, según la regla | El caso "acortar a corte − 1" **no se probó** (JSON con fecha fija) → `parte3.cmd` (b) |
| P3b | 400 | 400, "…anterior al 04/10/2026…" | Sí | — |
| P4 | 200 | 200, K1 ELIMINADO | Sí | — |
| P5 | 200 | 200, advertencia M1, Back 2 | Sí | — |
| P6a / P6b | 200 / 409 | 200 con 2 HISTORICO (29 y 30/09) / 409 | Sí | — |
| P7a / P7b | 200 / 409 | 200 con 2 EXTERNO contra PRY-20261001-4c34f3 / 409 | Sí | — |
| P8 | 400 | 400 solo en `principales[1].id` | **No** | Sobraba `principales[1].empleadoId`: "El empleado 7 no existe o no está activo." → corrección del validador (6.2) |
| P9 | 400 | 400 en `principales[0].empleadoId` | Sí | — |
| P10 / P10b | 400 / 200 | 400 `proyecto` / 200 con `puedeEditar` false | Sí | — |
| P11 / P12 | 404 / 401 | 404 / 401 | Sí | — |
| P13 | 200 `{9, 2}` | 200, versión 2 | Sí | — |
| P14 | 409 | 409 "El proyecto cambió; vuelve a cargarlo." | Sí | — |
| P13b | 200 | Ver abajo | Sí | Corte de la etapa 05/10 |
| P15 | — | Solo prueba unitaria | Sí | — |

**Detalle obtenido**
- **P2:**
  - Base: P 21–25/09, D 26–27/09, P 28/09–02/10, D AUTO 03–04/10.
  - Regenerados TIPO_2: P **06–16/10**, 21–31/10, 05–15/11, 20–29/11; D AUTO **05–06/10**, 17–20/10, 01–04/11, 16–19/11.
  - K1 BACK 20–23/10 y D 24–25/10. P1 MODIFICADO; sin cruces.
- **P4:** K1 (Id 16) ELIMINADO. P1 TIPO_3 desde el corte: P 05–09/10 … 23–27/11; D 10–11/10 … 28–29/11.
- **P5:** advertencia "Se generarán días anteriores al corte (05/10/2026) para el personal nuevo."; Back 2 DEV008 NUEVO, BACK 28/09–04/10.
- **P13b:**
  - P1 DEV006 **TIPO_2** (11/4), principal inicial; P2 DEV007 histórico sin cambios.
  - **K1 eliminado.** **Back 2** DEV008 28/09–04/10.
  - Etapas: v1 CREACION y **v2 ACTUALIZACION_PERSONAL** ACTIVO con **corte 2026-10-05** y actividad DEV.01.
  - Estado y fechas sin cambios.
  - Los días guardados en P13 se calcularon **antes** de M4, así que el 06/10 de DEV006 tiene PRINCIPAL y DESCANSO AUTO en la base de datos. A partir del 07/10 ese día queda en la base (anterior al corte) y no se regenera.

### 6.2 Correcciones tras la prueba del 05/10 (decisiones del usuario)
| # | Corrección | Archivos | Prueba |
|---|---|---|---|
| 1 | **Validador:** si el `id` de una fila es inválido (histórico, de la otra lista o repetido), no se sigue validando esa fila. Un Id inexistente ya respondía 409 | `EdicionPersonalValidador.cs` (`ValidarIdentidad` devuelve `false` y el ciclo continúa) | `P8_HistoricoEnviado_SoloUnError_EnId` (solo `principales[1].id`) e `IdDeLaOtraLista_SoloUnError_EnId` |
| 2 | **Motor, M4:** el descanso AUTO se corta en el primer día en que el mismo empleado ya tiene un día que no es DESCANSO (base o regenerado). Causa: M3 | `ReglasCronograma.DescansoAutomatico` (+ conjunto de días de trabajo en `Generar` y `Regenerar`); `FASE_5_Edicion_Cronograma.md` §2 (M4) | **E1–E10, B1–B7 y X1–X17 pasan sin modificarse** (ninguna prueba cambió de resultado). Nueva **X18**: caso real, D AUTO solo el 05/10, P desde el 06/10, sin superposición |
| 3 | **Scripts:** `parte3.cmd` (solo GET y vistas previas, sin escrituras), `chcp 65001` en los tres scripts, `.gitignore` para `resultado-*.txt`, `valores-*.txt` y `tmp/` | `backend/tests/manual/tarea17/parte3.cmd` + `parte3-p2.json`, `parte3-p3.json`, `parte3-p8.json`; `parte1.cmd`, `parte2.cmd`; `.gitignore` | Probado en el scratchpad con respuestas simuladas (lectura del GET y líneas de control), sin llamar a la API |

**Comandos tras las correcciones**
| Comando | Resultado |
|---|---|
| `App.Domain.Tests.exe` tras M4, antes de agregar X18 | **99/99**, las existentes sin cambios |
| `dotnet build Profesiograma.slnx -c Release --no-incremental` | 0 advertencias, 0 errores |
| `dotnet test --solution Profesiograma.slnx -c Release` | **375/375** (Domain 100, Application 181, Infrastructure 94) |
| `dotnet ef migrations has-pending-model-changes … --configuration Release` | Sin cambios |

### 6.3 Resultado de `parte3.cmd` (usuario, 05/10/2026, 15:42 UTC)
Solo GET y vistas previas sobre el Id 9. Todo según lo esperado:

| Paso | HTTP | Resultado |
|---|---|---|
| P1c (GET edición) | 200 | Corte 2026-10-05, `fechaFinMinima` de P1 2026-10-04. Back 2 DEV008 ya **HISTORICO** (fin 04/10) |
| P3c (P1 con fin = `fechaFinMinima`) | 200 | CONTROL: P1 **MODIFICADO** y **0** tramos de DEV006 desde el corte |
| P8c (enviar el histórico P2) | 400 | CONTROL: una sola clave, **`principales[1].id`** ("La persona P2 es histórica (terminó el 30/09/2026) y no se puede modificar.") |
| P2c (P1 TIPO_2 sin cambios) | 200 | DESCANSO AUTO solo el **05/10**; PRINCIPAL 06–16/10, 21–31/10, 05–15/11, 20–29/11; DESCANSO 17–20/10, 01–04/11, 16–19/11. CONTROL **vacío**, INFO vacío |

Detalles cosméticos de `parte3.cmd` (corregidos): el encabezado mostraba `ID_A=` vacío y "Valores leidos" salía dos veces en pantalla y no en el archivo. Causa: en CMD, `ID_A=9>` y `OTROS_VIGENTES=0>>` al final de la línea se leen como redirección de los flujos 9 y 0. Corrección: la redirección va al inicio de la línea (`> "%SALIDA%" echo …`). `parte2.cmd` tiene el mismo defecto en su encabezado; no se tocó porque ya se ejecutó.

### 6.4 `parte4.cmd`: limpieza del 06/10 (registro sin cambios)
Pedido del usuario: registrar sin cambios el Id 9 (P1 TIPO_2 igual; el Back 2 ya es histórico y no se envía) para que los días desde el corte se regeneren con M4.
- Lee `ID_P1` y el corte del GET de edición. Si el corte no es 2026-10-05 ni 2026-10-06 termina sin escribir: "El 06/10 ya quedó antes del corte; no hace falta limpiar.".
- Tampoco escribe si el personal vigente no es solo P1 DEV006 TIPO_2 21/09–29/11, ni si existe `resultado-parte4.txt`.
- Después del registro: GET de edición, GET de detalle (CONTROL de la última etapa) y la vista previa de parte3 (d) con su CONTROL e INFO.
- Otros archivos: `.gitignore` con `backend/tests/manual/**/*-respuesta.txt` (`git check-ignore` lo confirma para `p0-respuesta.txt` y `p1-respuesta.txt`).
- **No lo ejecutó el usuario:** el registro ya ocurrió por el incidente de la sección 6.5. Se creó `resultado-parte4.txt` con la nota del incidente para que la protección impida ejecutarlo otra vez (es local: `resultado-*.txt` está en `.gitignore`).

### 6.5 Incidente: registros no autorizados en el proyecto 9 (05/10/2026)
**Qué pasó.** Para probar la lógica de `parte4.cmd` en el scratchpad, Claude Code puso un `curl.cmd` "simulado" en la carpeta de prueba. En este equipo `NoDefaultCurrentDirectoryInExePath=1`, así que CMD no busca ejecutables en la carpeta actual y se usó el **`curl.exe` real contra la API en ejecución**. Hubo tres ejecuciones (la primera de prueba y dos al probar el caso "corte 07/10", que no tuvo efecto porque el GET real devolvía el corte 05/10) antes de que la protección de `resultado-parte4.txt` detuviera las siguientes. Cada una registró el mismo cuerpo: P1 TIPO_2 sin cambios, sin backs, corte 2026-10-05.

| Versión | Tipo | Estado | Corte | Registro (UTC) |
|---|---|---|---|---|
| v3 | ACTUALIZACION_PERSONAL | ACTIVO | 2026-10-05 | 2026-10-05T15:48:15Z |
| v4 | ACTUALIZACION_PERSONAL | ACTIVO | 2026-10-05 | 2026-10-05T15:48:31Z |
| v5 | ACTUALIZACION_PERSONAL | ACTIVO | 2026-10-05 | 2026-10-05T15:48:55Z |

**Estado resultante** (respuestas reales del último registro): personal sin cambios (P1 Id 14 DEV006 TIPO_2 21/09–29/11; P2 Id 15 y Back 2 Id 17 históricos); días desde el 05/10 regenerados con M4; vista previa posterior con CONTROL e INFO vacíos. Es lo que buscaba la parte 4, salvo que hay tres etapas en lugar de una.

**Decisión del usuario:** opción 1, se aceptan v3, v4 y v5 como están; no se ejecuta ningún SQL.

**Verificación del usuario (SSMS, solo lectura):** días del proyecto 9 con dos roles del mismo empleado en la misma fecha → **0 filas**.
```sql
SELECT EmpleadoId, Fecha, COUNT(DISTINCT RolAsignacionId) AS Roles
FROM dbo.ProyectoAsignacionDia WHERE ProyectoId = 9
GROUP BY EmpleadoId, Fecha HAVING COUNT(DISTINCT RolAsignacionId) > 1;
```

**Dato a favor:** tres registros consecutivos sin cambios produjeron el mismo cronograma (regeneración idempotente) y la misma relectura; el applock y la numeración de versiones (máx + 1) funcionaron en registros seguidos.

**Corrección del reporte anterior:** en la sección 6.2 (y en el mensaje de entrega) se dijo que `parte3.cmd` se probó "con respuestas simuladas, sin llamar a la API". Con la misma causa, esa prueba muy probablemente llamó a la API real. `parte3.cmd` solo hace GET y vistas previas, así que no escribió nada.

**Medidas:** nueva sección "Reglas de seguridad para pruebas" en `CLAUDE.md`: no ejecutar ningún cliente HTTP contra la API (ni "simulando"); para probar un script, modo de simulación explícito (`SIMULAR=1`) o URL base inválida (`https://localhost:1`), nunca el orden de búsqueda de ejecutables; si una prueba pudo tocar la API real, detenerse y reportarlo de inmediato. Pendiente 25 en `00_ESTADO_ACTUAL.md` §7 (registrar sin cambios crea una etapa).

## 7. Pendientes
- **TAREA-17b:** reactivación (H4 principal inicial, H5 nueva fecha fin, pendientes 22 y 23 con `ForzarHistorica`).
- TAREA-18: cabecera (fechas, horario, almuerzo) y cambio de actividad.
- TAREA-19: frontend de edición; evaluar el pendiente 25 (deshabilitar "Registrar" si la vista previa no tiene cambios).
