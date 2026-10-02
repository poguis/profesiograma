# TAREA-16 — Motor de cronograma con fecha de corte (RN11) — Domain

**Fecha:** 2026-10-01 (Fase A y Fase B)
**Resultado:** ✅ completada.
- `dotnet build Profesiograma.slnx -c Release --no-incremental`: **0 advertencias, 0 errores**.
- `dotnet test --solution Profesiograma.slnx -c Release`: **342/342** (308 + 34 nuevas; 341 antes de agregar B6a a X1, sección 3.1).
- **Ninguna prueba existente modificada**: E1–E10 y B1–B7 pasan igual tras la extracción a `ReglasCronograma`.

Solo se tocaron `backend/src/App.Domain`, `backend/tests/App.Domain.Tests` y la documentación. Sin EF, sin fecha del sistema, sin NuGet nuevos, sin comandos git que modifiquen el repositorio.

## 1. Fase A: hallazgos y decisiones
| # | Resultado |
|---|---|
| H7 | **Confirmado.** Regenerar genera, revisa y muestra los días anteriores al corte de una persona nueva (R‑978–997, R‑1085–1100, R‑1245–1389, R‑1441–1494), pero Registrar inserta solo `Fecha >= varCorteGuardar` (l. 2403, 2424). Se corrige con M1 |
| H8 | **Refutado para el original:** `ResumenProyectos` excluye al cargar todos los descansos AUTO de principales (l. 1150–1153), así que el "primer día libre" no los ve. Nuestra base sí los trae → **M2** (prueba X3) |
| D‑A1 | M2 como el original: en la revisión "primer día libre" se excluyen **todos** los descansos AUTO de principales de la base (siguen en la base y en `Tramos`) |
| D‑A2 | Clave de persona `string` en entradas y salidas |
| D‑A3 | Tramos de la base: continuos por persona, rol, tipo y bloque (días < corte) |
| X14 corregido | Regla de `Generar` (prueba **E6** de la TAREA-10; B3 como contraste): si el primer día tras el bloque está ocupado por cualquier día regenerado, no hay descanso AUTO. Esperado: el 12 no se inserta (gana la base), 13–14 DESCANSO MANUAL y **ningún** AUTO del bloque 1 |
| Pendiente 23 | El motor recibe `ForzarHistorica` (principal y back) para que Application decida en la TAREA-17. Caso X17 |
| Sección 5 | Reglas para la TAREA-17 documentadas en `FASE_5_Edicion_Cronograma.md` §6 (persona vigente: no cambia el empleado; `FechaInicio` fija si es anterior a C; `FechaFin ≥ C − 1`; jornada editable) |

Nota de lectura del origen: el `OnSelect` de Regenerar está en la línea física 1910 como una sola cadena. Las líneas R‑1 a R‑785 son versiones antiguas **comentadas**; el código activo va de R‑786 a R‑1516.

## 2. Qué se hizo
| Archivo | Contenido |
|---|---|
| `App.Domain/Proyectos/Cronograma/ReglasCronograma.cs` (nuevo, internal) | Extraído de `Generar`: `BloquesPrincipal`, `TramosBack`, `DescansoAutomatico`, `Expandir`, `Deduplicar`, validaciones, `Minimo` / `Maximo` |
| `App.Domain/Proyectos/Cronograma/MotorCronograma.cs` | `Generar` usa `ReglasCronograma` en el mismo orden; la clase pasa a `partial`. Sin cambio de comportamiento |
| `App.Domain/Proyectos/Cronograma/RegeneracionCronograma.cs` (nuevo) | Entradas `PrincipalEdicion` / `BackEdicion` (clave, `EsNuevo`, `ForzarHistorica`), `DiaExistente`, `SolicitudRegeneracion`. Salidas `ClasePersona`, `DiaRegenerado`, `CruceHistorico`, `ResultadoRegeneracion`. Método `MotorCronograma.Regenerar` (R1–R8) |
| `App.Domain.Tests/Proyectos/Cronograma/RegeneracionCronogramaTests.cs` (nuevo) | X1–X17 |

**Funcionamiento de `Regenerar`:**
1. Clasifica a cada persona.
2. Arma la base con los días < C.
3. Genera los bloques con las mismas reglas de `Generar`: las personas vigentes se recortan al corte; las nuevas, completas.
4. Calcula los cruces internos e históricos.
5. Calcula los descansos AUTO sobre base + regenerados (con M2), emitiendo solo los días ≥ C salvo para personas nuevas.
6. Deduplica con la base primero.
7. Devuelve tramos, días a insertar, indicador M1, días de trabajo para cruces externos y clases.

## 3. Pruebas nuevas (34)
| # | Caso | Verificación |
|---|---|---|
| X1 | **Invariante** (Theory, 18 casos: E1–E10, B1–B4, B6a, B6b, B6c, B7) | `Tramos`, días, cruces internos y días de trabajo **iguales** a `Generar`; sin históricos; todas las personas `Nueva` |
| X2 | Existente TIPO_2, corte 20/10 (a mitad del bloque 2) | Inserta P 20–26, P 31, D 27–30; tramos de la base 01–11, 12–15, 16–19 |
| X3 | M2: corte 13/10 (a mitad del descanso 12–15) | Inserta D AUTO **13–15**, P 16–26, P 31, D 27–30 |
| X4 | M3: TIPO_2 → TIPO_3, corte 20/10 | P 22–26, P 29–31, D 20–21 (tras el tramo base 16–19), D 27–28 |
| X5 | Existente con fin 19/10, corte 20/10 | Histórica, nada a insertar |
| X6 | M1: nuevo TIPO_3 15–31/10, corte 20/10 | Inserta todo (15–19 incluido); indicador `true` |
| X7 | Back nuevo de la misma persona 18–21/10 | Históricos 18 y 19; internos 20 y 21 [P1, K1] |
| X8 | Nuevo P2 de la misma persona 22–28/10 | 5 cruces internos [P1, P2] |
| X9 | Back existente 15–18/10 + 3 días, corte 20/10 | Vigente; D MANUAL 20–21 |
| X10 | Back nuevo DESCANSO 18–24/10 | D MANUAL 18–24, sin posterior; indicador `true` |
| X11 | Principal y back históricos | Sin días; sus tramos de base aparecen |
| X12 | Corte = fin del proyecto (31/10) | Solo P 31 |
| X13 | Corte después del fin: nueva 01–10/10 y existente 01–15/10 | Nueva completa (P 01–05, 08–10, D 06–07); existente histórica |
| X14 | Deduplicación + primer día ocupado (corregido) | El 12 no se inserta; D MANUAL 13–14; ningún AUTO del bloque 1; AUTO 27 presente |
| X15 | Días existentes ≥ corte en la entrada | Se ignoran (P 25 se inserta) |
| X16 | Entradas inválidas | `ArgumentNullException` / `ArgumentException` (fechas, DiasTrabajo 0, clave repetida, nueva + `ForzarHistorica`, rango del proyecto) |
| X17 | `ForzarHistorica` (pendiente 23) | Sin marca: regenera D 16–17. Con marca: histórica, nada a insertar |

### 3.1 Casos B del plan de la Fase A en X1 (aclaración del usuario)
- **B5 queda fuera de X1, con razón.** Sus 5 variantes son entradas inválidas: `Generar` lanza una excepción y no hay resultado que comparar. Las 5 están cubiertas en **X16** con las mismas entradas:

| B5 (TAREA-10) | X16 (`Regenerar`) |
|---|---|
| Fin del proyecto anterior al inicio | `SolicitudRegeneracion(31/10, 01/10, …)` → `ArgumentException` |
| Fin del principal anterior al inicio (10/10–09/10) | Mismo principal → `ArgumentException` |
| Fin del back anterior al inicio (15/10–12/10) | Mismo back → `ArgumentException` |
| Principal con DiasTrabajo = 0 | Mismo principal → `ArgumentException` |
| Solicitud nula | `Regenerar(null!)` → `ArgumentNullException` |

- **B6a no tenía razón para quedar fuera.** Su solicitud (principal TIPO_2 01–31/10 sin backs) es idéntica a la de E1, y se omitió por esa duplicación. **Se agregó** a la Theory, que ahora tiene 18 casos.

## 4. Comandos ejecutados y resultado
| Comando | Resultado |
|---|---|
| Decodificar el `OnSelect` de Regenerar (Python, al scratchpad) | 1516 líneas; código activo R‑786–1516 |
| `dotnet build tests/App.Domain.Tests -c Release` + `App.Domain.Tests.exe` (tras la extracción) | 0/0; **65/65** (las existentes, sin cambios) |
| `App.Domain.Tests.exe -class …RegeneracionCronogramaTests` | 33/33; con B6a en X1: **34/34** |
| `dotnet build Profesiograma.slnx -c Release --no-incremental` | 0 advertencias, 0 errores |
| `dotnet test --solution Profesiograma.slnx -c Release` | 341/341; con B6a: **342/342** |

Errores: ninguno. No hubo pruebas fallidas durante la implementación.

## 5. Documentación
- `docs/fases/FASE_5_Edicion_Cronograma.md` (nuevo): R1–R8, M1–M3, H7–H8, diferencias D1–D6, invariante y reglas para la TAREA-17.
- `docs/00_ESTADO_ACTUAL.md`:
  - §1: documento nuevo;
  - §6: tarea 16 ✅;
  - §7: pendiente 23 actualizado (decisión en la TAREA-17, opción `ForzarHistorica`);
  - §8: fila de la TAREA-16.

## 6. Pendientes
- TAREA-17: aplicar las reglas de `FASE_5_Edicion_Cronograma.md` §6 y decidir el pendiente 23.
- Pendiente 22 (H4): principal inicial, en la TAREA-17.
