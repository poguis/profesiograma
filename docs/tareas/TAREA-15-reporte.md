# TAREA-15 — Cambio de estado: SUSPENSIÓN y CIERRE (frontend)

**Fecha:** 2026-10-01 (Fase A y Fase B)
**Resultado:** ✅ completada. Verificación visual del usuario V1–V15: todas OK (sección 6), con evidencia en la API (sección 6.1).
- `npm run build`: sin errores ni advertencias.
- `npm run lint` (oxlint): sin hallazgos.
- `npm test`: **72/72** (44 anteriores + 28 nuevas).

No se modificó `backend/`; la API de la TAREA-14 cubre todo. No se ejecutaron `npm run dev` ni la API. No se usaron comandos git que modifiquen el repositorio.

## 1. Decisiones aprobadas (Fase A)
| # | Decisión |
|---|---|
| D1 | Cambiar el destino reinicia la fecha (al valor por defecto del nuevo destino) y desmarca la casilla |
| D2 | Tras un 200 se pasa a la pestaña Historial |
| D3 | `SelectorFecha` (compartido) recibe `fechaMinima` / `fechaMaxima` opcionales. Sin esas props se comporta igual que antes. Al escribir una fecha fuera de rango aparece "La fecha debe estar entre … y …." |
| D4 | "Ver impacto" solo exige destino; sin fecha, el servidor responde 400 bajo "Fecha" |
| D5 | Cerrar el diálogo sin confirmar no pide confirmación (no se guardó nada) |
| Corrección V12 | Una carrera entre dos pestañas produce **400**, no 409: el servicio valida con datos recién leídos antes de la transacción. El 409 solo ocurre si el cambio cae entre la validación y la lectura dentro del applock (no reproducible a mano) |
| Cambio 1 | Ante un 400 o un 409, el diálogo muestra además "Recargar datos del proyecto". Recarga el detalle y reinicia el diálogo; si el estado ya no admite cambios, el diálogo se cierra. Con el 400, los errores siguen bajo sus campos |

## 2. Qué se hizo

| Requisito | Implementación |
|---|---|
| R1 | Botón "Cambiar estado" junto a la etiqueta del estado (espacio `acciones` de `CabeceraProyecto`), solo si `puedeCambiarEstado` (ACTIVO / SUSPENDIDO) |
| R2 | `RadioGroup` con `opcionesDestino`: ACTIVO → Suspender / Terminar; SUSPENDIDO → Terminar y "Reactivar (Disponible próximamente)" deshabilitado. Fecha con `SelectorFecha` limitado a [inicio, fin] y el texto "Entre dd/MM/yyyy y dd/MM/yyyy (inclusive)". `fechaPorDefecto` propone la fecha fin solo en SUSPENDIDO → TERMINADO (H1) |
| R3 | "Ver impacto" → `ImpactoCambioEstado`: fecha fin actual → nueva; advertencias (MessageBar de advertencia); días eliminados por persona y rol (cantidad y rango); personal eliminado; personal recortado; actividades (recortada / se elimina); "Sin cambios" en las secciones vacías. Al cambiar destino o fecha, la vista previa queda desactualizada (atenuada, con aviso) |
| R4 | "Confirmar suspensión" / "Confirmar cierre" solo con `puedeConfirmar` (vista vigente, sin envío y, para TERMINAR, la casilla "Entiendo que el proyecto quedará TERMINADO"). Texto "Esta acción no se puede deshacer.". Doble clic: `ref` + botón deshabilitado. El diálogo no se cierra mientras se envía |
| R5 | **200:** cierra el diálogo; la mutación invalida `['proyectos']` (detalle y listado); aviso "Proyecto suspendido/terminado (versión n)." y pestaña Historial.<br>**400:** bajo `estadoDestino` / `fecha` + "Recargar datos del proyecto".<br>**409:** mensaje del servidor + "Recargar datos del proyecto".<br>**503 y red:** "Reintentar", que repite la última operación.<br>**404:** "El proyecto no existe o no tiene acceso." con enlace al listado.<br>Editar un campo borra su error del servidor |
| R6 | `cambioEstado.ts` (puro) con pruebas en `cambioEstado.test.ts` |

## 3. Archivos (frontend)
**Nuevos**
- `src/features/proyectos/cambioEstado.ts`: opciones, fecha por defecto, reducer con revisión, `aSolicitudCambio`, `vistaCambioVigente`, `puedeConfirmar`, `mensajeExito`, `interpretarErrorCambio`.
- `src/features/proyectos/cambioEstado.test.ts`: 28 pruebas.
- `src/features/proyectos/components/DialogoCambioEstado.tsx` y `ImpactoCambioEstado.tsx`.

**Modificados**
- `src/components/SelectorFecha.tsx` (D3).
- `features/proyectos/tipos.ts`: DTO del cambio de estado.
- `features/proyectos/api.ts`: `previsualizarCambioEstado`, `aplicarCambioEstado`.
- `features/proyectos/hooks.ts`: dos mutaciones; la de aplicar invalida `['proyectos']`.
- `features/proyectos/components/CabeceraProyecto.tsx`: prop `acciones`.
- `features/proyectos/pages/DetalleProyecto.tsx`: botón, diálogo y aviso.

**Documentación:** `docs/00_ESTADO_ACTUAL.md` (§6 fila 15, §9 fila TAREA-15) y este reporte.

## 4. Tipos frente a los DTO (TAREA-14)
Coinciden con los records de C# en camelCase. A tener en cuenta:
- `empleado.id` es el Id del empleado.
- `fechaFinNueva` de una actividad es `null` cuando se elimina.
- El 409 solo trae `title`.
- El 404 llega sin cuerpo (el mensaje lo pone el cliente).

## 5. Comandos y bundle
| Comando | Resultado |
|---|---|
| `npx tsc -b` (intermedios) | Sin errores |
| `npm test` | 72/72 (3 archivos) |
| `npm run build` | Sin errores ni advertencias |
| `npm run lint` / `npx oxlint` | Sin hallazgos (código de salida 0) |

| Chunk | Tamaño | gzip |
|---|---|---|
| Inicial (`index` + compartido + runtime) | 273,3 + 306,4 + 0,6 = **580,3 kB** | ≈ 175,7 kB |
| `DetalleProyecto` (diferido) | 47,5 kB (antes 37,4 kB) | 13,6 kB |

## 6. Verificación visual V1–V15 (usuario `gestor`, 2026-10-01)
**Datos:**
- Solo escriben **V7 y V11 (Id 8)** y **V12–V13 (Id 6)**.
- PRY-DEV-0001 (Id 1) solo recibe una vista previa **sin confirmar**.
- No se modifican PRY-DEV-0001..0003 ni el Id 4; el Id 7 (TERMINADO) solo se abre.

Proyectos de partida:
- **Id 8** PRY-20261001-669d73: ACTIVO, 01–30/04/2027, P1 DEV005 TIPO_3, actividad DEV.01.
- **Id 6** PRY-20261001-294792: ACTIVO, 01/01–28/02/2027, P1 DEV003 TIPO_3.

| # | Pasos | Esperado | Resultado |
|---|---|---|---|
| V1 | Abrir `/proyectos/7` (TERMINADO) | Sin botón "Cambiar estado" | OK |
| V2 | `/proyectos/8` (ACTIVO) → "Cambiar estado" | Opciones Suspender y Terminar, sin Reactivar. Fecha vacía. Texto "Entre 01/04/2027 y 30/04/2027 (inclusive)." | OK |
| V3 | En el selector: los días de mayo aparecen deshabilitados; escribir `01/05/2027` | No se puede elegir; mensaje "La fecha debe estar entre 01/04/2027 y 30/04/2027." | OK |
| V4 | Suspender, fecha vacía → "Ver impacto" | 400 bajo Fecha: "La fecha del movimiento es obligatoria." + "Recargar datos del proyecto" | OK |
| V5 | **PRY-DEV-0001** (`/proyectos/1`): Suspender, **25/09/2026** → "Ver impacto". **No confirmar**: Cancelar | Advertencia "Se eliminarán días ya transcurridos."; fecha fin 19/11/2026 → 25/09/2026, con días y personal afectados. Tras cancelar, el detalle sigue ACTIVO con fin 19/11/2026 | OK |
| V6 | Id 8: Suspender 20/04/2027 → "Ver impacto"; cambiar la fecha a 21/04; luego cambiar el destino a Terminar | Tras cada cambio: "Vista previa desactualizada" y "Confirmar" deshabilitado. Al pasar a Terminar, la fecha se vacía (D1) | OK |
| V7 | Id 8: Suspender **20/04/2027** → "Ver impacto" → **doble clic** en "Confirmar suspensión" | Impacto: DEV005 Principal **7 días (22/04–30/04)** y Descanso **3 días (21/04–28/04)**; P1 fin 30/04 → 20/04; DEV.01 fin 30/04 → 20/04. Se muestra "Esta acción no se puede deshacer.". Resultado: aviso "Proyecto suspendido (versión 2).", pestaña Historial con la v2 SUSPENSION y corte 20/04, estado SUSPENDIDO; en el listado, SUSPENDIDO. **Una sola** etapa nueva | OK |
| V8 | Id 8 (SUSPENDIDO) → "Cambiar estado" | Terminar habilitado; "Reactivar (Disponible próximamente)" deshabilitado | OK |
| V9 | Terminar | Fecha propuesta **20/04/2027** (H1); selector limitado a 01/04–20/04 | OK |
| V10 | "Ver impacto" con 20/04 | Fecha fin 20/04/2027 → 20/04/2027; secciones "Sin cambios". "Confirmar cierre" deshabilitado hasta marcar "Entiendo que el proyecto quedará TERMINADO" | OK |
| V11 | Marcar la casilla → Confirmar | "Proyecto terminado (versión 3).", Historial con la v3 CIERRE y el botón "Cambiar estado" desaparece | OK |
| V12 | **Carrera, Id 6 (corregida).** Pestaña A: Suspender **15/02/2027** → "Ver impacto". Pestaña B: Suspender **10/02/2027** → "Ver impacto" → Confirmar. Volver a A → "Confirmar suspensión" | B: "Proyecto suspendido (versión 2).". A: **400** con "El proyecto ya está en estado SUSPENDIDO." bajo "Nuevo estado" y "La fecha del movimiento no puede ser mayor a la fecha fin del proyecto (10/02/2027)." bajo la fecha, más el botón "Recargar datos del proyecto" | OK |
| V13 | En A: "Recargar datos del proyecto" → Terminar con la fecha propuesta 10/02/2027 → "Ver impacto" → casilla → Confirmar | Tras recargar: SUSPENDIDO, diálogo reiniciado (Terminar / Reactivar deshabilitado) y fecha propuesta 10/02/2027. Al final: "Proyecto terminado (versión 3)." | OK |
| V14 | Ancho de 375 px con el diálogo y el impacto abiertos (Id 1, **sin confirmar**) | Una columna, sin desplazamiento horizontal; el diálogo se desplaza verticalmente | OK |
| V15 | `anonimo` en `/proyectos/8` | No hay detalle (401) ni botón | OK |

**No verificables en desarrollo** (cubiertos por revisión de código y pruebas):
- **409 por carrera:** solo si el cambio cae entre la validación y la lectura dentro del applock. Cubierto por `interpretarErrorCambio` / `puedeConfirmar` (Vitest) y por el servicio de la TAREA-14 (prueba "EstadoCambioDentroDeLaTransaccion_409").
- **503:** registro ocupado.
- **404 dentro del diálogo:** el proyecto tendría que dejar de ser visible con el diálogo abierto.
- El error de **red** sí se puede probar deteniendo la API con el diálogo abierto: aparece "Reintentar".

**Resultado del usuario:** V1–V15 todas OK según lo esperado. V12 respondió 400 con los dos mensajes bajo sus campos y el botón "Recargar datos del proyecto"; V13 terminó el Id 6.

### 6.1 Evidencia en la API
Consulta `GET /api/proyectos/{id}` de solo lectura con `gestor`, después de las pruebas:

| Id | Estado y fechas | Etapas | Personal | Comprobación |
|---|---|---|---|---|
| 8 PRY-20261001-669d73 | **TERMINADO**, 01/04–**20/04/2027** | v1 CREACION (corte 01/04, 17:16:08 UTC) · v2 SUSPENSION/SUSPENDIDO (corte **20/04**, 18:06:42) · v3 CIERRE/TERMINADO (corte **20/04**, 18:09:27). **Exactamente 3** | P1 DEV005 01/04–20/04 | Una sola v2 → el **doble clic de V7 no registró dos veces** |
| 6 PRY-20261001-294792 | **TERMINADO**, 01/01–**10/02/2027** | v1 CREACION (corte 01/01) · v2 SUSPENSION/SUSPENDIDO (corte **10/02**, 18:11:55) · v3 CIERRE/TERMINADO (corte **10/02**, 18:13:07). Ninguna con corte 15/02 | P1 DEV003 01/01–10/02 | La pestaña A de V12 **no registró** (400) |
| 1 PRY-DEV-0001 | **ACTIVO**, 20/09–19/11/2026 | Solo v1 CREACION (de la semilla) | DEV001, DEV002, back DEV003 sin cambios | V5 y V14 fueron solo vista previa |
| 4 PRY-20261001-4ede0f | ACTIVO, 01–31/12/2026 | Solo v1 | DEV006, DEV007, back DEV008 | Sin cambios |
| 7 PRY-20261001-454325 | TERMINADO, fin 10/03/2027 | v1–v3 de la TAREA-14 (la última, 17:46:45 UTC) | DEV004 01/03–10/03 | Sin etapas nuevas (V1 solo lo abrió) |

**Datos de prueba que quedan:** Id 6 e Id 8 **TERMINADOS (versión 3)**; Id 7 TERMINADO (TAREA-14).

## 7. Pendientes
- Datos de prueba: Id 6, 7 y 8 TERMINADOS (versión 3) en `PROFESIOGRAMA_DEV`.
- Reactivación en la interfaz: TAREA-17 (backend) y su frontend.
