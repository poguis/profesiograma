# FASE 5 — Diseño: Edición del cronograma con fecha de corte (RN11)

**Fecha:** 2026-10-01 (TAREA-16: motor en Domain). **Uso:** TAREA-17 (actualización de personal y reactivación).

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
| R6 | **Descansos AUTO** sobre base + regenerados, con la regla de `Generar` (solo si el **primer** día después del bloque está libre, cualquier rol: prueba E6 de la TAREA-10).<br>• Los bloques son tramos **continuos** de días PRINCIPAL por bloque, uniendo base y regenerados.<br>• **M2:** al revisar el primer día libre se excluyen **todos** los descansos AUTO de principales de la base.<br>• Solo se emiten días ≥ C, salvo para las personas nuevas (M1) | Registrar l. 2341–2395; exclusión de los AUTO: ResumenProyectos l. 1150–1153 |
| R7 | **Deduplicación** por (EmpleadoId, Fecha, Rol), con la base primero: nunca se inserta una clave que ya existe (`UQ_ProyectoAsignacionDia_Clave`) | Registrar l. 2442–2448 |
| R8 | **Salida:**<br>• `DiasAInsertar` (con clave);<br>• `HayDiasAnterioresAlCorte` (M1);<br>• `Tramos` (base continua + regenerados como en `Generar`);<br>• cruces internos e históricos;<br>• `DiasTrabajoRegenerados`;<br>• `Clases`.<br>**Contrato:** Infrastructure borra los días con `Fecha ≥ C` e inserta `DiasAInsertar` en la misma transacción con applock | Registrar l. 2454–2456 |

## 2. Correcciones y criterios

| Id | Original | Motor nuevo |
|---|---|---|
| M1 | Los días anteriores al corte de una persona nueva se previsualizan y revisan, pero **no se guardan** (H7) | Se insertan; `HayDiasAnterioresAlCorte` permite advertirlo |
| M2 | Al cargar se excluyen todos los descansos AUTO de principales (ResumenProyectos l. 1150–1153), así que el "primer día libre" no los ve | La base sí trae los AUTO (están en `ProyectoAsignacionDia`): se excluyen en la revisión del primer día libre (D‑A1: todos, como el original). Siguen en la base y en `Tramos` |
| M3 | El ciclo de un principal se ancla a su `FechaInicio` (R‑964–968), también si cambia la jornada | Igual |

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
10. **H4 / pendiente 22:** principal inicial duplicado o eliminado.
11. **Pendiente 23 (decisión en la TAREA-17):** un back recortado por una suspensión conserva `DiasDescanso`. Al reactivar con `FechaFin < C ≤ FechaFin + DiasDescanso`, el motor lo considera vigente y regeneraría el descanso que la suspensión borró.
    - **Recomendación preliminar:** en la REACTIVACION, Application marca ese back con `ForzarHistorica = true` y su descanso no se regenera (prueba X17).
    - Alternativa: poner `DiasDescanso = 0` al recortar en la suspensión.

## 7. Ubicación

| Archivo | Contenido |
|---|---|
| `App.Domain/Proyectos/Cronograma/ReglasCronograma.cs` | Reglas comunes (internal) |
| `App.Domain/Proyectos/Cronograma/MotorCronograma.cs` | `Generar` (sin cambio de comportamiento; `partial`) |
| `App.Domain/Proyectos/Cronograma/RegeneracionCronograma.cs` | Entradas, salidas y `Regenerar` |
| `App.Domain.Tests/Proyectos/Cronograma/RegeneracionCronogramaTests.cs` | X1 (invariante, 18 casos) y X2–X17 |
