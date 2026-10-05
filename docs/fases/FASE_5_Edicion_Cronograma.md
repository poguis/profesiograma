# FASE 5 — Diseño: Edición del cronograma con fecha de corte (RN11)

**Fecha:** 2026-10-01 (TAREA-16: motor en Domain). **Uso:** TAREA-17 (actualización de personal, §8) y TAREA-17b (reactivación, `FASE_5_Estados_Proyecto.md` §8).

**Origen** (`docs/origen/powerapps/`):
- `ConfigurarProyecto_1.pa.yaml`:
  - botón **Regenerar**: su `OnSelect` está en la línea física 1910 como una sola cadena; las citas "R‑n" son líneas de esa cadena decodificada. R‑1 a R‑785 son versiones antiguas comentadas; el código activo va de R‑786 a R‑1516;
  - **Registrar**: líneas 2266–2456.
- `ResumenProyectos.pa.yaml`: carga de históricos, vigentes y detalle (l. 1066–1153).

## 1. Reglas del motor (`MotorCronograma.Regenerar`)

| Id | Regla | Origen |
|---|---|---|
| R1 | Corte **C** (entrada). La **base** son los días guardados con `Fecha < C`; no se modifican. Los días ≥ C que traiga la entrada se ignoran | R‑812–818 |
| R2 | Clasificación:<br>• **Histórica:** principal con `Fin < C`; back con `Fin + DiasDescanso < C`; o marcada con `ForzarHistorica` por Application. No se regenera.<br>• **Vigente:** el resto de las existentes.<br>• **Nueva:** aún no guardada.<br>Cada persona trae una **clave** estable (Id de ProyectoPersonal o clave local) | ResumenProyectos l. 1091–1092, 1121–1122 |
| R3 | **Principal:** bloques como en `Generar`, con el ciclo anclado a SU inicio y la jornada actual (M3).<br>• Vigente: inicio del bloque = `max(inicio, C)`; se incluye si `inicio ≤ fin` y `fin ≥ C`.<br>• Nueva: bloques completos (M1) | R‑964–997 |
| R4 | **Back:**<br>• Vigente: inicio = `max(Inicio, C)`.<br>• Nuevo: conserva su inicio.<br>• JORNADA con `DiasDescanso`: descanso posterior `Fin+1 .. Fin+DiasDescanso`, recortado igual.<br>• DESCANSO: rol DESCANSO/MANUAL | R‑1085–1100, R‑1170–1187 |
| R5 | **Cruces** sobre los días regenerados con rol ≠ DESCANSO, antes de los descansos AUTO:<br>• **INTERNO:** regenerado contra regenerado;<br>• **HISTORICO_PROPIO:** regenerado contra la base, misma persona y fecha.<br>Los cruces con otros proyectos los consulta Infrastructure con `DiasTrabajoRegenerados` | R‑1245–1307 (internos e histórico), R‑1310–1389 (otros proyectos) |
| R6 | **Descansos AUTO** sobre base + regenerados, con la regla de `Generar` (y el corte M4, §2) (solo si el **primer** día después del bloque está libre, cualquier rol: prueba E6 de la TAREA-10).<br>• Los bloques son tramos **continuos** de días PRINCIPAL por bloque, uniendo base y regenerados.<br>• **M2:** al revisar el primer día libre se excluyen **todos** los descansos AUTO de principales de la base.<br>• Solo se emiten días ≥ C, salvo para las personas nuevas (M1) | Registrar l. 2341–2395; exclusión de los AUTO: ResumenProyectos l. 1150–1153 |
| R7 | **Deduplicación** por (EmpleadoId, Fecha, Rol), con la base primero: nunca se inserta una clave que ya existe (`UQ_ProyectoAsignacionDia_Clave`) | Registrar l. 2442–2448 |
| R8 | **Salida:**<br>• `DiasAInsertar` (con clave);<br>• `HayDiasAnterioresAlCorte` (M1);<br>• `Tramos` (base continua + regenerados como en `Generar`);<br>• cruces internos e históricos;<br>• `DiasTrabajoRegenerados`;<br>• `Clases`.<br>**Contrato:** Infrastructure borra los días con `Fecha ≥ C` e inserta `DiasAInsertar` en la misma transacción con applock | Registrar l. 2454–2456 |

## 2. Correcciones y criterios

| Id | Original | Motor nuevo |
|---|---|---|
| M1 | Los días anteriores al corte de una persona nueva se previsualizan y revisan, pero **no se guardan** (H7) | Se insertan; `HayDiasAnterioresAlCorte` permite advertirlo |
| M2 | Al cargar se excluyen todos los descansos AUTO de principales (ResumenProyectos l. 1150–1153), así que el "primer día libre" no los ve | La base sí trae los AUTO (están en `ProyectoAsignacionDia`): se excluyen en la revisión del primer día libre (D‑A1: todos, como el original). Siguen en la base y en `Tramos` |
| M3 | El ciclo de un principal se ancla a su `FechaInicio` (R‑964–968), también si cambia la jornada | Igual |
| M4 | El descanso AUTO se emite completo (`DiasDescanso` días o hasta el fin del principal), aunque choque con un día de trabajo de la misma persona | **Corrección (TAREA-17, 05/10/2026):** el descanso AUTO se corta en el primer día en que el mismo empleado ya tiene un día que **no es DESCANSO** en el proyecto (base o regenerado); ese día y los siguientes no se emiten. Está en `ReglasCronograma.DescansoAutomatico`, compartida por `Generar` y `Regenerar`. **Causa: M3.** Al cambiar la jornada, el descanso del último bloque de la base se calcula con los días de la jornada nueva y puede llegar al primer bloque regenerado. Caso real: P1 TIPO_3 desde el 21/09 pasa a TIPO_2 con corte 05/10; el tramo base 28/09–02/10 daba DESCANSO 03–06/10 y el 06/10 quedaba con PRINCIPAL y DESCANSO. Con M4: DESCANSO AUTO solo el 05/10 y PRINCIPAL desde el 06/10 (prueba X18). Con ciclos coherentes (sin cambio de jornada) no cambia nada: E1–E10, B1–B7 y X1–X17 pasan sin modificarse |

## 3. Hallazgos

| Id | Hallazgo | Resultado |
|---|---|---|
| H7 | Los días anteriores al corte de una persona nueva se previsualizan y se revisan sus cruces, pero el guardado filtra `Fecha ≥ corte` | **Confirmado:** generación R‑978–997 / R‑1085–1100; cruces R‑1245–1389; guardado l. 2403, 2424 (`Fecha >= varCorteGuardar`). Ejemplo: C = 20/10, nuevo TIPO_3 15–31/10 → los días 15–19 se ven y se revisan, pero no se guardan. Corregido con M1 |
| H8 | Un descanso AUTO que cruza el corte no se regenera porque su primer día ya está en la base | **Refutado para el original:** la carga excluye los AUTO (ResumenProyectos l. 1150–1153). Ejemplo: TIPO_2 01–31/10, C = 13/10 → el original regenera 13–15. En nuestro modelo la base sí trae el AUTO del 12, por eso existe M2 (prueba X3) |

## 4. Diferencias con el original (además de M1 y M2)

| Id | Original | Motor nuevo |
|---|---|---|
| D1 | Corte = `Today()` o la fecha de reactivación (R‑797–805) | Entrada; lo decide Application |
| D2 | Los descansos AUTO solo se calculan en Registrar; la vista previa no los muestra | Se calculan en `Regenerar` y aparecen en `Tramos` |
| D3 | Los bloques para el descanso se agrupan por `Bloque` sin continuidad (`GroupBy`, l. 2346) | Tramos continuos. Solo difiere si, por M3, el mismo bloque aparece a ambos lados del corte |
| D4 | El principal del descanso se busca por `PrincipalId + Ekon + FechaInicio ≤ fin del bloque` | Por la clave de la persona |
| D5 | No deduplica contra la base (solo guardaba ≥ C) | Deduplica también contra la base (necesario con M1) |
| D6 | Cruces internos fila por fila (repetidos) | Agrupados por empleado y fecha |

## 5. Invariante

`Regenerar` con `Corte = InicioProyecto`, base vacía y todas las personas nuevas produce **exactamente** lo mismo que `Generar`: `Tramos`, días (`DiasAInsertar` = `DiasFinales`), cruces internos y días de trabajo. La prueba X1 lo verifica con E1–E10, B1–B4, B6a–c y B7 (18 casos). B5 (entradas inválidas) no tiene resultado que comparar: sus variantes están en X16.

**Implementación:** las reglas comunes se extrajeron a `ReglasCronograma` (internal): bloques, tramos del back, descanso automático, expansión, deduplicación y validaciones. `Generar` las usa en el mismo orden, y E1–E10 y B1–B7 pasan sin modificarse.

## 6. Reglas para la TAREA-17 (fuera del motor)

1. **Corte:** hoy en Ecuador (ACTUALIZACION_PERSONAL) o la fecha de reactivación. La solicitud de reactivación trae la nueva fecha fin (H5).
2. **Persona vigente:**
   - **no se cambia el empleado** (para reemplazarla se cierra su fila y se agrega una nueva);
   - `FechaInicio` no cambia si es anterior a C;
   - `FechaFin ≥ C − 1`;
   - la jornada se puede cambiar (M3).
3. **Quitar una persona vigente:** `FechaFin = C − 1` si empezó antes de C; eliminar la fila si empieza en C o después.
4. **Numeración:** las personas nuevas toman máx + 1 de su rol, sin reutilizar huecos (`UQ_ProyectoPersonal_Numero`).
5. **Límites 20/20:** cuentan vigentes + nuevas.
6. **RN08:** fechas de vigentes y nuevas dentro del rango del proyecto (R‑885–935).
7. **M1:** advertencia si `HayDiasAnterioresAlCorte`.
8. **Cruces externos:** con `DiasTrabajoRegenerados`, excluyendo el propio proyecto (`excluirProyectoId`).
9. **Escritura:** transacción con applock (§7.1).
   - borrar los días con `Fecha ≥ C`;
   - insertar `DiasAInsertar` (clave → `ProyectoPersonalId`);
   - guardar el personal;
   - etapa ACTUALIZACION_PERSONAL o REACTIVACION con `FechaCorte = C` y snapshot.
10. **H4 / pendiente 22 — resuelto (TAREA-17b):** un inicial por periodo (R6: el primer principal de la reactivación es inicial y no se desmarca el anterior). El recorte puede eliminar al inicial original o a todos los principales; la propuesta del GET de reactivación lo cubre con R7 (respaldo: último principal, o null con advertencia). Ver `FASE_5_Estados_Proyecto.md` §8.
11. **Pendiente 23 — resuelto (TAREA-17b):** en la REACTIVACION todo el personal guardado entra al motor con `ForzarHistorica = true` (R5), así que no se regenera el descanso que la suspensión borró (prueba X17 en el motor; `Pendiente23_BackRecortado_QuedaHistorico_SinRegenerarSuDescanso` en Application). El mismo caso en la edición posterior se resuelve con **H12** (§8.4). `DiasDescanso` no se modifica al recortar.

## 7. Ubicación

| Archivo | Contenido |
|---|---|
| `App.Domain/Proyectos/Cronograma/ReglasCronograma.cs` | Reglas comunes (internal) |
| `App.Domain/Proyectos/Cronograma/MotorCronograma.cs` | `Generar` (sin cambio de comportamiento; `partial`) |
| `App.Domain/Proyectos/Cronograma/RegeneracionCronograma.cs` | Entradas, salidas y `Regenerar` |
| `App.Domain.Tests/Proyectos/Cronograma/RegeneracionCronogramaTests.cs` | X1 (invariante, 18 casos) y X2–X17 |

## 8. TAREA-17 — Actualización de personal (implementado, backend)

Alcance: **ACTUALIZACION_PERSONAL** sobre proyectos **ACTIVO**. La reactivación (H4, H5, pendientes 22 y 23) va en la **TAREA-17b**; la cabecera y el cambio de actividad, en la TAREA-18.

### 8.1 API
| Endpoint | Respuesta |
|---|---|
| `GET /api/proyectos/{id:int}/edicion` | 200 `EdicionPersonalDto`: corte (hoy en Ecuador), `puedeEditar` + `motivo` (D1), personal HISTORICO / VIGENTE con `permisos` (`fechaInicio`, `fechaFinMinima` = C − 1, `jornada`, `eliminable`), límites. 404 si no es visible |
| `POST /api/proyectos/{id:int}/personal/previsualizar` | 200 `PrevisualizacionPersonalDto` (`corte`, `personal` con clase y acción, `tramos`, `cruces` INTERNO / HISTORICO / EXTERNO, `resumen`, `advertencias`). 400 / 404 / 409 (cambiado). No guarda |
| `POST /api/proyectos/{id:int}/personal` | 200 `{ id, version }`. 400; 404; 409 con extensiones `cruces` y `resumen` (cruces) o "El proyecto cambió; vuelve a cargarlo."; 503 (applock) |

Cuerpo: `{ principales: [{ clave, id?, empleadoId, jornada, fechaInicio, fechaFin, cargo? }], backs: [{ clave, id?, empleadoId, tipoRegistro, fechaInicio, fechaFin, diasDescanso, principalClave?, principalId?, observacion? }] }`.
- `id` presente: persona vigente; ausente: persona nueva.
- `principalClave` apunta a un principal del cuerpo; `principalId`, a un principal **histórico** (D3). Son excluyentes.
- `cargo` null en una vigente: se conserva.

### 8.2 Reglas aplicadas
| Id | Regla |
|---|---|
| E1 | Corte C = hoy en Ecuador (`FechaNegocio`). Solo proyectos ACTIVO con `FechaFin ≥ C` (si no, 400 `proyecto`) |
| E2 | Clasificación igual que el motor. Una persona histórica enviada → 400 |
| E3 | Persona vigente: no cambia el empleado; `FechaInicio` fija si es < C; si es ≥ C, puede cambiar pero ≥ C (D2); `FechaFin ≥ C − 1` solo si cambia (D5); la jornada se puede cambiar (M3). Las que ya empezaron son obligatorias; las que aún no empiezan se eliminan si se omiten |
| E4 | Personas nuevas: número máx + 1 por rol sobre **todo** el personal guardado (sin reutilizar huecos), en el orden del arreglo; empleado activo (solo nuevas, D4). Las reglas de la creación se reutilizan con `ReglasPersonal` (mismos mensajes): RN08, jornada, tipo de registro, días de descanso, largos. Máximos sobre vigentes + nuevas |
| E5 | Advertencia M1 si el motor inserta días anteriores al corte |
| E6 | Cruces: internos e históricos (motor) + externos (`IConsultaCrucesExternos` con `excluirProyectoId`). Vista previa: 200 con la lista. Registro: 409 |
| E7 | Dentro del applock se vuelve a leer y recalcular todo. Un Id que ya no existe → 409 (también fuera). Lo que era válido fuera y ya no lo es dentro → 409 "El proyecto cambió" |
| E8 | Política Gestor + visibilidad R1 (404) |

### 8.3 Escritura (D7, reemplaza el orden de E7 del enunciado)
1. `ExecuteDelete` de los días con `Fecha ≥ C`.
2. Personas vigentes actualizadas (relaciones con principales guardados) + nuevas. → SaveChanges 1.
3. Relaciones con principales **nuevos** (ya tienen Id); referencias a personas omitidas en `null` (por defensa); omitidas eliminadas. → SaveChanges 2.
4. `DiasAInsertar` (clave → `ProyectoPersonalId`) + etapa **ACTUALIZACION_PERSONAL** con versión máx + 1, `FechaCorte = C`, la actividad vigente en C y el snapshot del personal resultante, históricas incluidas (D8). → SaveChanges 3.

El estado y las fechas del proyecto no cambian. `EsPrincipalInicial` de las nuevas es `false` (D6; en la reactivación, el primer principal es inicial: R6, TAREA-17b).

### 8.4 H12 (TAREA-17b): back recortado por una suspensión
Después de reactivar, la clasificación R2 (back vigente si `FechaFin + DiasDescanso ≥ C`) regeneraba el descanso que la suspensión borró.
- **Regla:** un back existente con `FechaFin < C` sin días DESCANSO guardados después de su `FechaFin` es **histórico** (equivale a `ForzarHistorica = true`). Si tiene esos días, nada cambia.
- **Implementación:** `PersonaGuardada.SinDescansoPosterior` (lo llena `EdicionPersonalRepositorio` con la consulta `ConsultaBacksConDescansoPosterior`, prueba `ToQueryString`) y `PersonaGuardada.EsHistorica`. Las históricas entran al motor con `ForzarHistorica = true` (`CalculoPersonal`).
- **Pruebas:** `EdicionPersonalH12Tests` (recortado → histórico y sin descanso; con descanso guardado → vigente y se regenera como antes). Las pruebas de la TAREA-17 pasan sin modificarse.
- **Riesgo:** datos migrados (Fase 3) de backs sin días de descanso guardados se tratarían como recortados. [PENDIENTE DE CONFIRMAR en la Fase 3]
