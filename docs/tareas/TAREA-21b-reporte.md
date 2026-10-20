# TAREA-21b — Pestaña "Cronograma" en el detalle del proyecto (frontend)

**Fecha:** 2026-10-20 (Fase A: plan; Fase B: implementación)
**Resultado:** implementada; **pendiente de la verificación visual del usuario** (§6: X1–X10).
- `npm run build` (tsc -b + vite build): OK.
- `npm run lint` (oxlint): OK, 0 avisos.
- `npm test`: **281/281** = 273 anteriores + 8 nuevas. **0 pruebas ajustadas**: los diffs de los archivos de prueba existentes solo tienen inserciones.

**Lo que no se hizo:**
- `npm install` (sin dependencias nuevas), `npm run dev`;
- llamadas a la API, SQL, `dotnet run`;
- git que modifique el repositorio;
- cambios en `backend/` (el contrato alcanzó).

## 1. Decisiones aplicadas (Q1–Q9, 20/10/2026)
Detalle en `docs/fases/FASE_5_Cronograma.md` §7.1:
- Q1: `VistaCronograma` y `SelectorRango` extraídos.
- Q2: estado local + `location.state.pestana`.
- Q3: `estadoEnContexto`.
- Q4: `coloresNovedadEstables` / `coloresEstables`.
- Q5: clave bajo `['proyectos']` + `invalidarTrasEscritura`.
- Q6: filtro local de persona.
- Q7: `contexto` `'proyecto'`.
- Q8: leyenda sin cambios de estructura.
- Q9: enlace con el rango efectivo.

**Nota sobre Q8:** la leyenda conserva su estructura. Solo su última línea depende del contexto: "Otro proyecto (cualquier otro)" en la pestaña, frente a "(de otro usuario)" en `/cronograma`, por la misma razón que Q7 (en la pestaña, "de otro usuario" sería falso).

## 2. Qué se hizo
**Lógica pura:**
- **`cronograma.ts`:**
  - `ContextoCronograma` (`'general' | 'proyecto'`);
  - `estadoEnContexto(celda, proyectoId?)`: un CHOQUE sin asignación del proyecto → OTRO_PROYECTO;
  - `resumenCelda` y `etiquetaCelda` con `contexto = 'general'` por defecto;
  - `enlaceCronogramaGeneral(id, desde, hasta)`.
- **`coloresProyecto.ts`:** `coloresNovedadEstables(tiposNovedad?)` (base ∪ catálogo, en mayúsculas y sin repetir) y `coloresEstables(ids, tiposNovedad?)`. El algoritmo de colores no cambió.

**Componentes y páginas:**
- **`components/SelectorRango.tsx` (nuevo):** desde/hasta con `fechasAlCambiar`, accesos de mes y botón de reinicio configurable. Devuelve un fragmento, así que se ve igual dentro de los filtros.
- **`components/VistaCronograma.tsx` (nuevo):**
  - `VistaCronograma`: resumen, aviso de recorte (+ complemento), vacío, cuadrícula y leyenda;
  - `ErrorCronograma`: error + "Reintentar"; no muestra los 400, que van en su campo.
- **`FiltrosCronograma`:** usa `SelectorRango`. "Limpiar filtros" lo vuelve a montar (`key`) para borrar sus errores de formato, igual que antes.
- **`PaginaCronograma`:** usa `VistaCronograma` y `ErrorCronograma`, y los colores estables del catálogo (Q4). Por lo demás, mismo comportamiento.
- **`CuadriculaCronograma`, `DetalleCelda` y `LeyendaCronograma`:** props opcionales `contexto` (por defecto `'general'`) y `proyectoContexto` (Q3, en la cuadrícula).

**Consulta, claves e invalidación:**
- **`cronograma/api.ts`:** `clavesCronograma.todos`, `RangoCronograma`, `obtenerCronogramaProyecto` y `useCronogramaProyecto(id, rango, habilitada)` (`enabled`, `retry: false`, `keepPreviousData`, clave `clavesProyectos.cronograma(id, consulta)`).
- **`proyectos/api.ts`:** `clavesProyectos.cronograma(id, consulta)`.
- **`proyectos/invalidacion.ts` (nuevo):** `invalidarTrasEscritura(queryClient)`, que invalida `['proyectos']` y `['cronograma']`.
- **`proyectos/hooks.ts`:** los 5 `onSuccess` (`useCrearProyecto`, `useAplicarCambioEstado`, `useRegistrarCabecera`, `useRegistrarPersonal`, `useRegistrarReactivacion`) usan `invalidarTrasEscritura`.

**Detalle del proyecto:**
- **`proyectos/components/PestanaCronograma.tsx` (nuevo):** rango local, persona, `SelectorRango` con "Todo el proyecto", enlace al general, estados (404, error/reintentar, cargando) y `VistaCronograma` en el contexto del proyecto.
- **`DetalleProyecto`:** pestaña "Cronograma" después de Personal e Historial; el panel solo se monta con la pestaña activa.
- **`ListadoProyectos`:** `EstadoNavegacionProyectos.pestana` admite `'cronograma'`.
- **`proyectos/tipos.ts`:** `Catalogos.tiposNovedad?`. `GET /api/catalogos` ya lo devolvía.

## 3. Archivos
| Archivo | Cambio |
|---|---|
| `frontend/src/features/cronograma/components/SelectorRango.tsx` | Nuevo |
| `frontend/src/features/cronograma/components/VistaCronograma.tsx` | Nuevo |
| `frontend/src/features/proyectos/components/PestanaCronograma.tsx` | Nuevo |
| `frontend/src/features/proyectos/invalidacion.ts` + `.test.ts` | Nuevo + 1 prueba |
| `frontend/src/features/cronograma/cronograma.ts` (+ `.test.ts`) | `estadoEnContexto`, contexto, `enlaceCronogramaGeneral` + 5 pruebas |
| `frontend/src/features/cronograma/coloresProyecto.ts` (+ `.test.ts`) | `coloresNovedadEstables`, `coloresEstables` + 2 pruebas |
| `frontend/src/features/cronograma/api.ts` | Hook por proyecto y clave general |
| `frontend/src/features/cronograma/components/FiltrosCronograma.tsx`, `CuadriculaCronograma.tsx`, `DetalleCelda.tsx`, `LeyendaCronograma.tsx` | Generalizados |
| `frontend/src/features/cronograma/pages/PaginaCronograma.tsx` | Usa `VistaCronograma` y los colores estables |
| `frontend/src/features/proyectos/api.ts`, `hooks.ts`, `tipos.ts` | Clave, invalidación y `tiposNovedad?` |
| `frontend/src/features/proyectos/pages/DetalleProyecto.tsx`, `ListadoProyectos.tsx` | Pestaña y tipo de navegación |
| `docs/fases/FASE_5_Cronograma.md` | §3 (por vista; CHOQUE con asignaciones vacío) y §7.1 nueva |
| `docs/00_ESTADO_ACTUAL.md` | Encabezado, §6 (21b), §7 (56 resuelto), §9 |
| `docs/tareas/TAREA-21b-reporte.md` | Este reporte |

## 4. Pruebas nuevas (8)
**`cronograma.test.ts` (5):**
- `estadoEnContexto`: CHOQUE sin asignación del proyecto → OTRO_PROYECTO; con asignación → CHOQUE; con asignación de otro proyecto → OTRO_PROYECTO;
- sin contexto, y PROYECTO, DESCANSO, NOVEDAD y OTRO_PROYECTO, sin cambios;
- `resumenCelda` / `etiquetaCelda` con `'proyecto'` y el texto por defecto;
- `enlaceCronogramaGeneral`;
- `fechasAlCambiar` con el estado local y D12 recortado (conserva el `hasta` solicitado).

**`coloresProyecto.test.ts` (2):**
- `coloresNovedadEstables`: sin catálogo es la base; con catálogo, la base ∪ el catálogo, sin repetir y en mayúsculas;
- `colorProyecto(17)` es igual en `/cronograma` y en la pestaña con leyendas de respuesta distintas (`coloresEstables`); con el cálculo anterior por leyenda de respuesta podía diferir.

**`invalidacion.test.ts` (1):** con un `QueryClient` de prueba, `invalidarTrasEscritura` invalida el detalle, el cronograma del proyecto y el general, y no toca `['erp', …]`.

## 5. Comandos y resultados
| Comando | Resultado |
|---|---|
| `npx tsc -b`, `npm run build` | OK |
| `npm run lint` | OK (0) |
| `npm test` | 15 archivos; 281/281 |
| `git diff` de `cronograma.test.ts` y `coloresProyecto.test.ts` | Solo inserciones (0 líneas existentes cambiadas) |

**Error al editar:** al agregar las pruebas de colores, un recorte de texto dejó una llave de más al final del `describe` (fallo de transformación de Vitest). Se quitó esa llave y se verificó con `git diff` que las pruebas existentes no cambiaron.

## 6. Verificación visual (la hace usted: VPN, API y `npm run dev`)
| # | Caso | Esperado |
|---|---|---|
| X1 | Proyecto 17 → pestaña Cronograma | La petición `GET /api/proyectos/17/cronograma` aparece **solo** al abrir la pestaña (herramientas del navegador, Red). Rango desde el 01/10 hasta su fin (máximo 90 días) |
| X2 | Proyecto de más de 90 días | Aviso «El rango supera 90 días… (solicitado hasta el …)» + «Use los accesos de mes o abra el cronograma general»; "Mes siguiente" y "Todo el proyecto" funcionan |
| X3 | `/proyectos/3` como gestor (no visible) | El detalle ya muestra «Proyecto no encontrado o sin acceso»; la pestaña no aparece |
| X4 | Proyecto con choque | "C" + total en el día del choque; el detalle dice «Asignado además en n otro(s) proyecto(s).», sin el nombre del ajeno |
| X5 | Proyecto con turno nocturno | En la leyenda, el proyecto con su horario y la marca «Nocturno» |
| X6 | Días de una persona en otros proyectos | Celdas rayadas («Otro proyecto»); nunca un "C" en un día sin este proyecto (Q3) |
| X7 | "Actualizar personal" → registrar → volver a la pestaña | Datos nuevos sin recargar el navegador; `/cronograma` también actualizado (Q5) |
| X8 | Comparar con `/cronograma?proyectoId=…` | El proyecto tiene el mismo color en las dos vistas (Q4) |
| X9 | «Abrir en el cronograma general» | `/cronograma` con el mismo rango y `proyectoId`; muestra a esas personas con todos sus proyectos visibles |
| X10 | VPN desconectada y "Mes siguiente" | Error y «Reintentar», con una sola espera (P10) |

**Cómo encontrar los proyectos de X2, X4 y X5** (sin consultar la base):
- **X2 (más de 90 días):**
  - en `/proyectos`, mire Inicio y Fin de cada fila: sirve un proyecto con más de 3 meses entre las dos fechas (p. ej. del 01/10 al 31/01);
  - o, en `/cronograma` como admin, ponga un "Hasta" lejano (p. ej. +6 meses) y elija en "Proyecto" uno cuyas celdas lleguen hasta el último día visible.
- **X4 (choque):** en `/cronograma` del mes actual como admin, busque una celda negra con "C2" (o más). Al abrir su detalle se ven los proyectos de ese día; use cualquiera de ellos.
- **X5 (turno nocturno):** en `/cronograma`, la leyenda marca con «Nocturno» los proyectos cuyo horario cruza la medianoche (p. ej. los horarios 73 o 118 de la TAREA-26c). Si ninguno aparece en el mes, amplíe el rango o revise en `/proyectos/:id` el Horario de la cabecera, cuya salida es anterior a la entrada.

## 7. Pendientes y observaciones
- **Rango que se pierde:** la pestaña desmonta su estado al cambiar de pestaña, así que al volver se abre otra vez con D12. Es aceptable; si molesta, el rango puede subir a `DetalleProyecto`.
- **Muchas celdas rayadas:** en la pestaña, si una persona trabaja mucho en otros proyectos, aparecen muchas celdas «Otro proyecto» (ruido visual). Es la alternativa elegida en Q3; si molesta, se pueden ocultar como LIBRE.
- **Cambio visual en `/cronograma`:** con Q4 los colores de los proyectos salen del catálogo. Con las semillas actuales son los mismos que antes; solo cambiarían si el catálogo y la respuesta difirieran.
