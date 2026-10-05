# TAREA-17b — Reactivación (SUSPENDIDO → ACTIVO), backend

**Fechas:** 2026-10-05 (Fase A y Fase B)
**Resultado:** ✅ terminada. Prueba manual del usuario del 05/10/2026 (`parte1.cmd` y `parte2.cmd`): todos los casos coinciden con lo esperado (6.2). La primera ejecución se detuvo sin escribir por cruces de DEV008; los scripts ahora eligen el par de empleados (6.0).
- `dotnet build Profesiograma.slnx -c Release --no-incremental`: **0 advertencias, 0 errores**.
- `dotnet test --solution Profesiograma.slnx -c Release`: **413/413** (Domain 100, Application 217, Infrastructure 96). Antes: 375. Nuevas: 38. De las 375 anteriores solo cambiaron las 2 del mensaje de `cambio-estado` (autorizadas); las de la TAREA-17 pasan sin modificarse, también con H12.
- `has-pending-model-changes`: sin cambios. Sin modelo ni migraciones nuevas.

Sin `dotnet run`, sin SQL, sin cambios en `frontend/`, sin comandos git que modifiquen el repositorio y **sin ningún cliente HTTP contra la API** (los scripts solo se probaron con `SIMULAR=1`, sección 6.3).

## 1. Decisiones aprobadas
- R1–R10 del enunciado; respuestas P1–P5 de la Fase A; ajustes O1, O3–O6 y la regla nueva **H12**.
- Documentadas en `docs/fases/FASE_5_Estados_Proyecto.md` §8 (API, reglas, mensajes, escritura, hallazgos H9–H12, diferencias D9–D13).

## 2. Verificación del origen (Fase A)
| Id | Resultado | Evidencia (`ConfigurarProyecto_1.pa.yaml`) |
|---|---|---|
| H9 | Confirmado | Solo valida `fecha ≥ inicio` y omite `fecha > fin` para REACTIVACION (l. 589–604); vacía los vigentes (l. 499–500); Registrar guarda históricos con `FechaFin < R` + nuevos (l. 2283–2286) |
| H10 | Confirmado | Filtro de backs históricos `FechaFin + DiasDescanso < R` (l. 2285) |
| H11 | Confirmado (hallazgo nuestro) | El original no recorta ni crea actividades al reactivar (solo CAMBIO_ACTIVIDAD, l. 2297–2334) |
| Pendiente 22 | La premisa "el inicial empieza en el inicio del proyecto" **no se cumple** | En el original el bloqueo de la fecha del inicial está comentado (`GestionProyecto.pa.yaml` l. 1316–1324); aquí el inicial es `Numero == 1` (`ProyectoRepositorio.cs:78`) sin exigir la fecha. Redacción nueva (O1) en `00_ESTADO_ACTUAL.md` §7 |

## 3. Qué se hizo
| Capa | Archivo | Cambio |
|---|---|---|
| Application | `Proyectos/Personal/EdicionPersonalValidador.cs` | Núcleo `ValidarPersonal` (internal) con `ReglasValidacionPersonal` (corte, rango RN08, `Reactivacion`). `Validar` (TAREA-17) lo llama con las reglas de siempre. Reactivación: cualquier `id` → un solo error en `…id`; todo el personal guardado histórico; inicio ≥ R |
| | `Proyectos/Personal/CalculoPersonal.cs` (nuevo) | Extraído de `EdicionPersonalServicio`: motor, cruces internos/históricos/externos, tramos, personas, días, actividad vigente y snapshot. Las históricas entran al motor con `ForzarHistorica = true` (R5, H12) |
| | `Proyectos/Personal/EdicionPersonalServicio.cs` | Usa `CalculoPersonal` (mismo comportamiento) |
| | `Proyectos/Personal/EdicionPersonalContratos.cs` | `PersonaGuardada.SinDescansoPosterior` y `EsHistorica` con H12; `ActividadNueva`, `ReactivacionAplicar` y `CambioPersonal.Reactivacion` (opcional, null en la TAREA-17) |
| | `Proyectos/Reactivacion/ReactivacionContratos.cs` (nuevo) | `ReactivarProyectoSolicitud`, `ActividadParaReactivar`, `IReactivacionRepositorio` |
| | `Proyectos/Reactivacion/ReactivacionDtos.cs` (nuevo) | `ReactivacionDto`, `PrincipalPropuestoDto`, `PrevisualizacionReactivacionDto`, `ProyectoReactivadoDto`, `ResultadoReactivacion` |
| | `Proyectos/Reactivacion/ReactivacionValidador.cs` (nuevo) | R2, R5, R6 + núcleo de la TAREA-17 |
| | `Proyectos/Reactivacion/ReactivacionServicio.cs` (nuevo) | GET (R7), vista previa (R3, R8) y registro (applock, relectura de estado y `FechaFin`, R9) |
| | `Proyectos/Estados/CambioEstadoValidador.cs` | Mensaje "La reactivación se registra con la opción Reactivar." |
| | `DependencyInjection.cs` | `ReactivacionValidador`, `ReactivacionServicio` |
| Infrastructure | `Persistencia/Proyectos/EdicionPersonalRepositorio.cs` | Implementa `IReactivacionRepositorio`; `AplicarAsync` con `Reactivacion` (inicial nuevo y proyecto ACTIVO/FechaFin en SaveChanges 1; actividad REACTIVACION en SaveChanges 3); H12 en la lectura; consultas nuevas `ConsultaBacksConDescansoPosterior` y `ConsultaActividadParaReactivar` |
| | `Persistencia/PersistenciaServiceCollectionExtensions.cs` | `IReactivacionRepositorio` |
| Api | `Endpoints/ProyectoEndpoints.cs` | `GET /{id}/reactivacion`, `POST /{id}/reactivacion/previsualizar`, `POST /{id}/reactivacion` |
| | `Profesiograma.Dev.http` | Casos X1–X10 (no ejecutados) |
| Pruebas | `App.Application.Tests/Proyectos/Reactivacion/DoblesReactivacion.cs`, `ReactivacionServicioTests.cs` (nuevos) | 34 pruebas (sección 5) |
| | `App.Application.Tests/Proyectos/Personal/EdicionPersonalH12Tests.cs` (nuevo) | 2 pruebas H12 |
| | `App.Infrastructure.Tests/Consultas/ReactivacionSqlTests.cs` (nuevo) | 2 pruebas `ToQueryString` |
| | `App.Application.Tests/Proyectos/Estados/CambioEstadoServicioTests.cs:91`, `CambioEstadoValidadorTests.cs:40` | Solo el texto del mensaje nuevo |
| Manual | `backend/tests/manual/tarea17b/` (nuevo) | `parte1.cmd`, `parte2.cmd` y plantillas `p0-crear.json`, `s1-suspender.json`, `r-c.json`, `r-d.json`, `r-e.json`, `i-cambio.json`, `h-personal.json` (ASCII, CRLF) |
| Docs | `FASE_5_Estados_Proyecto.md`, `FASE_5_Edicion_Cronograma.md`, `00_ESTADO_ACTUAL.md` | §8 nueva; §6 puntos 10–11 y §8.4 (H12); §6, §7 (22, 23, 26, 27), §8 |

## 4. SQL de las consultas nuevas (pruebas de traducción)
| Consulta | Prueba | Puntos verificados |
|---|---|---|
| `ConsultaActividadParaReactivar` | `ActividadParaReactivar_VigenteEnLaFecha_MayorVersion_ConDescripcionYTipo` | `FROM [dbo].[ProyectoActividad]`, `FechaInicio <= @` y `FechaFin >= @`, descripción y tipo, `ORDER BY [p].[Version] DESC` |
| `ConsultaBacksConDescansoPosterior` | `BacksConDescansoPosterior_DescansoDespuesDeLaFechaFinDelBack` | `SELECT DISTINCT`, `INNER JOIN [dbo].[ProyectoPersonal]`, rol del día = 3 (DESCANSO), rol de la persona = 2 (BACK), `Fecha > FechaFin` del back |

El resto reutiliza consultas con prueba (cabecera, personal, días base y desde el corte, actividades, última versión, tipo de movimiento, proyecto y personal con seguimiento).

## 5. Pruebas automáticas nuevas (38)
- **GET (R1, R7):** suspendido con propuesta del inicial, `fechaMinima` y personal; ACTIVO / TERMINADO con `puedeReactivar` false; varios iniciales → el de mayor `FechaFin`; sin inicial → último principal con advertencia; empleado inactivo con advertencia; sin principales → null con advertencia; no visible / admin.
- **Validación (400):** R = FechaFin actual (H9); fecha y fecha fin obligatorias; fecha fin < R; proyecto ACTIVO / TERMINADO; primer principal que no empieza en R; persona con inicio < R; fin > nueva fecha fin (RN08); sin principales; `id` presente (un solo error por fila); empleado inactivo; `principalId` que no es principal; máximo 20 principales.
- **Vista previa:** corte R, tramos desde R (P2 nuevo 11/10–21/10 y descanso AUTO desde el 22/10), actividad DEV.01 de R a la nueva fecha fin, sin advertencia con R futura; **pendiente 23** (back recortado histórico, sin descanso); advertencia R3 con R pasada; sin actividad no se crea nada; cruces externos 200 / registro 409 sin abrir la transacción.
- **Registro:** etapa REACTIVACION (corte = inicio = R, fin = nueva), proyecto, actividad v2, nuevas con número máx + 1; **H4** (inicial = primer principal, el anterior sin tocar); relaciones con principal nuevo e histórico; snapshot; relectura con estado cambiado → 409; relectura con `FechaFin` cambiada → 409; conflicto de concurrencia → 409; no visible → 404.
- **H12 (edición):** back recortado sin descanso guardado → HISTORICO, no se exige y no se regenera; back con descanso guardado → VIGENTE y se regenera como antes.
- **SQL:** 2 pruebas `ToQueryString` (sección 4).

## 6. Prueba manual (`backend/tests/manual/tarea17b/`, la ejecuta el usuario)

### 6.0 Primera ejecución del usuario (05/10/2026) y ajuste de los empleados
- `parte1.cmd` se detuvo en P0a, como estaba previsto: 1 cruce EXTERNO de DEV008 el 03/10/2026 con PRY-20261005-da396e (Id 9). **No se creó nada** (no hay `resultado-parte1.txt`; la salida quedó en `tmp\parte1-previo.txt`).
- Ajuste (solo `backend/tests/manual/tarea17b/`, sin tocar el backend ni las pruebas automáticas):
  - **Empleados configurables** por código EKON: `parte1.cmd DEV003 DEV004` o un `empleados.txt` opcional con `EMP_P1=DEV003` y `EMP_BACK=DEV004`. La línea de comandos tiene prioridad. Si se indican, se prueba **solo** ese par. DEV005 no se acepta (se reserva para e/f) y los dos deben ser distintos.
  - **Selección automática** si no se indican: primero el par por defecto DEV007/DEV008 y después los pares ordenados de DEV001–DEV008 sin DEV005 (P1 recorre DEV001, DEV002, DEV003, DEV004, DEV006, DEV007, DEV008 y, para cada uno, el back recorre la misma lista). Cada par se prueba **solo con la vista previa de la creación** (no guarda) y se usa el primero sin cruces. Pantalla y archivo muestran `DESCARTADO P1 x / back y - cruces: n - <empleado fecha origen proyecto>…` (hasta 6 cruces) y `ELEGIDO …`. Si ninguno sirve, termina sin escribir.
  - Los Id de los empleados se leen con `GET /api/empleados?texto=<código>&soloMisDepartamentos=false` (solo lectura). También el de DEV005 para el caso e.
  - `valores-parte1.txt` guarda `EMP_P1`, `EMP_BACK`, `ID_EMP_P1`, `ID_EMP_BACK` e `ID_DEV005`. `parte2.cmd` los lee y sus textos y líneas CONTROL usan esos códigos en lugar de DEV007/DEV008 fijos. Si no hay Id de DEV005, e y f quedan "OMITIDO sin datos".
  - Se mantienen: back que termina en F con `DiasDescanso` 3 (pendiente 23 / H12), protecciones `resultado-parteN.txt` (creadas justo antes del primer paso que escribe), O3–O6, `chcp 65001` y redirecciones al inicio de la línea.
  - Plantillas: `__ID_EMP_P1__`, `__ID_EMP_BACK__` e `__ID_DEV005__` en lugar de Id fijos.

### 6.1 Cómo se ejecuta
1. Con la API corriendo: `backend\tests\manual\tarea17b\parte1.cmd` (selecciona el par, crea y suspende el proyecto B). Opcional: `parte1.cmd DEV003 DEV004` o `empleados.txt`.
2. Después: `backend\tests\manual\tarea17b\parte2.cmd` (lee `valores-parte1.txt`; solo **g** escribe).
- Fechas relativas al día en que se ejecuta `parte1.cmd` (H): INICIO = H−3, **F** = H−2, **R** = H−1, FIN = H+55. P1 = EMP_P1 TIPO_2 INICIO–FIN; Back 1 = EMP_BACK JORNADA solo el día F, 3 días de descanso, relacionado con P1. Grupo CAMPO, actividad DEV.01 (O3).
- Con el par por defecto hace falta H ≥ 07/10/2026 (DEV008 trabaja en el Id 9 hasta el 04/10); antes de esa fecha la selección automática descarta DEV007/DEV008 y prueba los demás pares. El 05/10/2026 quedó libre DEV007 / DEV001 (6.2).
- La selección automática puede hacer hasta 42 vistas previas (en la simulación tardó unos 2 minutos cuando ninguno servía).
- Protección: cada parte no se ejecuta si existe su `resultado-parteN.txt` (se crea justo antes del primer paso que escribe; si se detiene antes, la salida queda en `tmp\parteN-previo.txt`).
- No tocan los Id 1–9: Id 2 solo GET (b); Id 5 solo GET como admin (O5).

### 6.2 Resultados reales (usuario, 05/10/2026)
Salidas: `backend/tests/manual/tarea17b/resultado-parte1.txt` y `resultado-parte2.txt`.
- **Fechas:** H = 05/10/2026 → INICIO 02/10, **F 03/10**, **R 04/10**, FIN 29/11.
- **Par elegido:** P1 **DEV007** (Id 7) / back **DEV001** (Id 1), con **31 pares descartados**. El primero fue DEV007/DEV008, con 1 cruce de DEV008 el 03/10 en PRY-20261005-da396e (Id 9). Los demás tuvieron entre 4 y 30 cruces, sobre todo con PRY-DEV-0001.
- **Proyecto B = Id 10, PRY-20261005-98f053.** Una vez reactivado, queda ACTIVO en la **versión 3**, con dos principales iniciales.

| Caso | HTTP | Línea CONTROL (resumen) | ¿Coincide? |
|---|---|---|---|
| P0a | — | Par elegido P1 DEV007 Id 7, back DEV001 Id 1; 31 descartados | Sí |
| P0 | 201 | `{"id":10,"codigo":"PRY-20261005-98f053"}` | Sí |
| S1 | 200 | id 10, SUSPENDIDO, versión 2 | Sí |
| S1b | 200 | SUSPENDIDO, fin 03/10; P1 DEV007 02/10–03/10 (descanso 4, inicial); Back 1 DEV001 03/10–03/10 (descanso 3) | Sí |
| a | 200 | `puedeReactivar` True, `fechaMinima` 04/10, propuesta DEV007 TIPO_2 activo, 0 advertencias | Sí |
| b | 200 | GET reactivación del Id 2 (solo lectura) | Sí |
| c | 400 | `fecha`: "La fecha de reactivación debe ser posterior a la fecha fin actual del proyecto (03/10/2026)." | Sí |
| d | 200 | Corte 04/10; "Se generarán días ya transcurridos."; DEV001 HISTORICO, 0 tramos desde R; P2 NUEVO número 2, PRINCIPAL desde 04/10 (04–14/10) y DESCANSO AUTO 15–18/10; actividad DEV.01 04/10–29/11; 0 cruces | Sí |
| i | 400 | `estadoDestino`: "La reactivación se registra con la opción Reactivar." (además un error en `fecha`, observación 1) | Sí |
| O5 | 200 | DEV005 (Id 5) PRINCIPAL del Id 5 ACTIVO en R: 1 → se ejecutan e y f | Sí |
| e | 200 | 1 cruce EXTERNO: DEV005 04/10 contra PRY-20261001-4c34f3 | Sí |
| f | 409 | "El proyecto tiene cruces de asignación.", 1 cruce; no escribe | Sí |
| g | 200 | id 10, ACTIVO, versión 3 | Sí |
| h1 | 200 | ACTIVO, fin 29/11; etapa v3 REACTIVACION ACTIVO 04/10–29/11, corte 04/10, DEV.01; actividad vigente v2 REACTIVACION DEV.01 04/10–29/11; principales 1 DEV007 02/10–03/10 inicial y 2 DEV007 04/10–29/11 inicial (H4, R6) | Sí |
| h2 | 200 | `puedeReactivar` False, "Solo se puede reactivar un proyecto SUSPENDIDO (estado actual: ACTIVO)." | Sí |
| h3 | 200 | Principal nuevo Id 20; DEV001 **HISTORICO** (H12) | Sí |
| h4 | 200 | Corte 05/10; DEV001 HISTORICO; 0 tramos de DEV001 desde el corte; 0 cruces (O6, H12) | Sí |
| j | 400 | `proyecto`: "Solo se puede reactivar un proyecto SUSPENDIDO (estado actual: ACTIVO)." (O4) | Sí |

**Todos los casos coinciden con lo esperado.**

### 6.2.1 Observaciones
1. **Caso i:** además del error esperado en `estadoDestino`, el 400 trae otro en `fecha`: "La fecha del movimiento no puede ser mayor a la fecha fin del proyecto (03/10/2026).". `CambioEstadoValidador` valida la fecha aunque el movimiento sea inválido. **Pendiente 29** (→ TAREA-18): no validar `fecha` cuando el movimiento es SIN_CAMBIO, NO_PERMITIDO, REACTIVACION o un estado desconocido.
2. **Tramos partidos en el corte:** el motor devuelve la base (días < corte) y lo regenerado como tramos separados. En h4 (corte 05/10), el primer bloque de P2 sale como PRINCIPAL 04/10–04/10 (base) y PRINCIPAL 05/10–14/10 (regenerado), con el mismo bloque 1. Es correcto (R8 del motor: base continua + regenerados), pero al mostrarlo conviene unir los tramos contiguos del mismo empleado, rol, tipo y bloque. Va a la TAREA-19 (nota en el pendiente 26).
3. **`resumen[].dias`** (lectura del código y de las pruebas de la TAREA-12; no se consultó la API): es la **lista de días del mes**, no una cantidad. `CalculadorCruces.Resumen` (`CalculadorCruces.cs:66`) agrupa por persona, rol, proyecto y mes, y arma `string.Join(", ", días distintos ordenados)`. Las pruebas `Resumen_DiasOrdenadosSinRepetir_YMesEnEspanol` y `Resumen_AgrupaPorMesRolYProyecto` esperan "5, 6, 7", "1, 2", "30". El "4" de los casos e y f es el día 04/10. **No hace falta el pendiente 30.**

### 6.3 Prueba de los scripts con `SIMULAR=1` (Claude Code)
**Después del ajuste de 6.0** (H = 09/10/2026, PATH sin `curl.exe`, respuestas simuladas):

| Escenario | Resultado |
|---|---|
| A. Par por defecto con cruces | DESCARTADO DEV007/DEV008 (1 cruce, DEV008 07/10); ELEGIDO DEV001/DEV002; parte 1 y parte 2 completas con todas las líneas CONTROL usando DEV001/DEV002; plantillas con Id 1, 2 y 5 |
| B. Ningún par sirve | 42 pares DESCARTADO; "Ningun par de empleados sirve: NO se creo nada."; sin `resultado-parte1.txt` ni `valores-parte1.txt` |
| C1. `parte1.cmd DEV003 DEV004` sin cruces | Solo ese par; ELEGIDO DEV003/DEV004 (Id 3 y 4) |
| C2. `parte1.cmd DEV003 DEV004` con cruces | Solo ese par; DESCARTADO; no se escribe |
| C3. `empleados.txt` DEV006 / DEV003 | ELEGIDO DEV006/DEV003 (Id 6 y 3) |
| C4. DEV005 indicado | "DEV005 se reserva para los casos e/f de la parte 2: elija otro empleado." |
| C5. Solo un empleado en `empleados.txt` | "Indique los dos empleados: …" |

**Antes del ajuste:**
- Modo simulación: `SIMULAR=1` → `BASE=https://localhost:1` y el helper `:http` copia `%SIMULACION%\NOMBRE.txt`; **nunca** llama a curl.
- Además, el lanzador de la prueba dejó en el `PATH` solo PowerShell y una copia de `chcp.com`: `curl.exe` no se podía ejecutar ni por error.
- Respuestas simuladas escritas a mano en el scratchpad (no reales), con H = 09/10/2026.
- Caminos probados:
  - completo: todas las líneas CONTROL como en 6.2;
  - O5 = 0: e y f "OMITIDO sin datos";
  - P0a con cruces: se detiene sin crear `resultado-parte1.txt`;
  - segunda ejecución: las dos protecciones se activan;
  - tildes correctas en las líneas CONTROL (UTF-8).
- **Ninguna prueba tocó la API real.**

## 7. Comandos ejecutados y resultado
| Comando | Resultado |
|---|---|
| `dotnet build Profesiograma.slnx -c Release` (tras extraer el núcleo y `CalculoPersonal`) | 0/0 |
| `dotnet test --solution Profesiograma.slnx -c Release` (tras la extracción) | 375/375, sin cambios |
| `dotnet test …` (tras H12, reactivación y el mensaje nuevo) | 373/375: fallaron solo las 2 pruebas del mensaje de `cambio-estado` (esperado); ninguna de la TAREA-17 |
| Actualizar el texto en esas 2 pruebas | — |
| `App.Infrastructure.Tests.exe -class …ReactivacionSqlTests` | 2/2 |
| `App.Application.Tests.exe -namespace …Proyectos.Reactivacion` | 34/34 |
| `App.Application.Tests.exe -class …EdicionPersonalH12Tests` | 2/2 |
| `dotnet build Profesiograma.slnx -c Release --no-incremental` | **0 advertencias, 0 errores** |
| `dotnet test --solution Profesiograma.slnx -c Release` | **413/413** |
| `dotnet ef migrations has-pending-model-changes … --configuration Release` | "No changes have been made to the model since the last migration." |
| `run.cmd` del scratchpad (`SIMULAR=1`, PATH sin curl) | parte1 y parte2 OK en los caminos de 6.3 |
| `git status` / `git diff --stat` (lectura) | — |

## 8. Errores y cómo se resolvieron
- `CS0246 ActividadCorte` en los dobles de prueba: faltaba `using App.Domain.Proyectos.Estados;`.
- Un heredoc de Bash con comillas falló: los reemplazos se hicieron con un script de Python en el scratchpad (archivo de pares).
- Lanzador de la simulación: `printf` y `sed` dañaron las barras invertidas del `PATH` (powershell no se encontraba; los scripts se detuvieron sin hacer nada). Se reescribió con la herramienta de escritura.
- Primera simulación con tildes dañadas en las líneas CONTROL: `chcp` no estaba en el PATH restringido. Se agregó una copia de `chcp.com`; con `chcp 65001` las tildes salen bien.

## 9. Contradicciones y observaciones
- **Pendiente 22:** su premisa no se cumplía (sección 2); redacción nueva (O1) y resuelto con R7.
- **O5:** el detalle del proyecto (`GET /api/proyectos/{id}`) no expone días. El script decide con la asignación de DEV005 (PRINCIPAL del Id 5 ACTIVO que cubre R) y, si R resulta ser un día de descanso, lo detecta con la vista previa (0 cruces EXTERNO) y marca "OMITIDO sin datos".
- **Creación:** `CrearProyectoValidador.MinimoPrincipales = 0`, así que "Se requiere al menos 0 principal(es)." nunca aparece. La reactivación exige 1 con el texto aprobado.
- **H12 y datos migrados:** un back sin días de descanso guardados después de su fin se trata como recortado (pendiente 27). [PENDIENTE DE CONFIRMAR en la Fase 3]
- **Frontend:** "Reactivar" sigue deshabilitado ("Disponible próximamente"); pendiente 26 → TAREA-19.

## 10. Pendientes
- **Pendiente 29** (→ TAREA-18): `CambioEstadoValidador` no debe validar `fecha` cuando el movimiento es inválido (observación 1 de 6.2.1).
- TAREA-18: cabecera y cambio de actividad (incluido R10).
- TAREA-19: frontend de edición y de reactivación (pendientes 25 y 26; unir tramos contiguos al mostrarlos, observación 2 de 6.2.1).
