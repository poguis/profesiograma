# TAREA-17b — Reactivación (SUSPENDIDO → ACTIVO), backend

**Fechas:** 2026-10-05 (Fase A y Fase B)
**Resultado:** implementada. **Prueba manual pendiente** (`parte1.cmd` y `parte2.cmd`, sección 6): la ejecuta el usuario.
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

### 6.1 Cómo se ejecuta
1. Con la API corriendo: `backend\tests\manual\tarea17b\parte1.cmd` (crea y suspende el proyecto B).
2. Después: `backend\tests\manual\tarea17b\parte2.cmd` (lee `valores-parte1.txt`; solo **g** escribe).
- Fechas relativas al día en que se ejecuta `parte1.cmd` (H): INICIO = H−3, **F** = H−2, **R** = H−1, FIN = H+55. P1 = DEV007 TIPO_2 INICIO–FIN; Back 1 = DEV008 JORNADA solo el día F, 3 días de descanso, relacionado con P1. Grupo CAMPO, actividad DEV.01 (O3).
- **Requisito:** H ≥ **07/10/2026**. DEV008 trabaja en el Id 9 hasta el 04/10 (y DEV007 hasta el 30/09); con H = 05/10 o 06/10 la vista previa de la creación (P0a) tiene cruces y `parte1.cmd` se detiene **sin crear nada**.
- Protección: cada parte no se ejecuta si existe su `resultado-parteN.txt` (se crea justo antes del primer paso que escribe; si se detiene antes, la salida queda en `tmp\parteN-previo.txt`).
- No tocan los Id 1–9: Id 2 solo GET (b); Id 5 solo GET como admin (O5).

### 6.2 Resultados esperados
Con H = 05/10/2026 las fechas serían INICIO 02/10, F 03/10, R 04/10, FIN 29/11, **pero P0a da cruces** (DEV008 el 03/10 en el Id 9) y la parte 1 se detiene. Por eso el ejemplo se da con **H = 09/10/2026**: INICIO 06/10, F 07/10, R 08/10, FIN 03/12 (los mismos valores de la simulación de 6.3).

| Caso | Esperado (relativo) | Ejemplo H = 09/10/2026 | Resultado |
|---|---|---|---|
| P0a | 200, 0 cruces | 0 cruces | [PENDIENTE] |
| P0 | 201 `{ id B }` | Id B (se espera 10) | [PENDIENTE] |
| S1 | 200 `{ B, SUSPENDIDO, 2 }` | Suspendido el 07/10 | [PENDIENTE] |
| S1b | SUSPENDIDO, fin F; P1 INICIO–F; Back 1 F–F, descanso 3 | Fin 07/10; P1 06/10–07/10 | [PENDIENTE] |
| a | 200, `puedeReactivar` true, `fechaMinima` F+1, propuesta DEV007 TIPO_2 activo, sin advertencias | `fechaMinima` 08/10 | [PENDIENTE] |
| b | 200 (Id 2 SUSPENDIDO) | — | [PENDIENTE] |
| c | 400 `fecha` "…posterior a la fecha fin actual del proyecto (F)." | "(07/10/2026)" | [PENDIENTE] |
| d | 200; corte R; advertencia "Se generarán días ya transcurridos."; DEV008 HISTORICO, 0 tramos desde R; P2 NUEVO número 2, PRINCIPAL desde R (11 días) y DESCANSO AUTO 4 días; actividad DEV.01 R–FIN; 0 cruces | PRINCIPAL 08/10–18/10, DESCANSO 19/10–22/10; actividad 08/10–03/12 | [PENDIENTE] |
| i | 400 `estadoDestino` "La reactivación se registra con la opción Reactivar." | — | [PENDIENTE] |
| O5 | GET Id 5 (admin): si DEV005 no es PRINCIPAL del Id 5 ACTIVO en R → e y f "OMITIDO (sin datos)" | DEV005 01/10–31/12 en el Id 5 → se ejecutan | [PENDIENTE] |
| e | 200 con ≥ 1 cruce EXTERNO (si R es día de descanso de DEV005: "OMITIDO sin datos") | 08/10 es día PRINCIPAL de DEV005 (05–22/10) | [PENDIENTE] |
| f | 409 "El proyecto tiene cruces de asignación." con cruces; no escribe | — | [PENDIENTE] |
| g | 200 `{ B, ACTIVO, 3 }` | — | [PENDIENTE] |
| h1 | ACTIVO, fin FIN; etapa v3 REACTIVACION ACTIVO R–FIN, corte R, DEV.01; actividad vigente v2 REACTIVACION DEV.01 R–FIN; P1 y P2 iniciales | v3 08/10–03/12 corte 08/10 | [PENDIENTE] |
| h2 | `puedeReactivar` false, "Solo se puede reactivar un proyecto SUSPENDIDO (estado actual: ACTIVO)." | — | [PENDIENTE] |
| h3 | GET edición: DEV008 **HISTORICO** (H12); se lee el Id de P2 | — | [PENDIENTE] |
| h4 | Vista previa de personal sin cambios: 200; DEV008 HISTORICO; 0 tramos de DEV008 con fin ≥ corte; 0 cruces (O6) | — | [PENDIENTE] |
| j | 400 `proyecto` "Solo se puede reactivar un proyecto SUSPENDIDO (estado actual: ACTIVO)." (O4) | — | [PENDIENTE] |

### 6.3 Prueba de los scripts con `SIMULAR=1` (Claude Code)
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
- Ejecutar `parte1.cmd` y `parte2.cmd` (usuario) desde el 07/10/2026 y registrar sus controles; después, marcar la TAREA-17b como ✅.
- TAREA-18: cabecera y cambio de actividad (incluido R10).
- TAREA-19: frontend de edición y de reactivación (pendientes 25 y 26).
