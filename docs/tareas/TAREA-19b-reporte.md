# TAREA-19b — Frontend: actualización de personal

**Fecha:** 2026-10-06 (Fase A y Fase B)
**Resultado:** ✅ completada. Verificada por el usuario el 06/10/2026: **P1–P9 y P11–P15 OK**; **P10 resuelto por la TAREA-19b2** (B1–E2 OK). Historial: **P10 mostró un defecto** (sobrescritura con dos pestañas), corregido en la **TAREA-19b2** (token base; ver `TAREA-19b2-reporte.md`).
- `npm run build`: sin errores ni advertencias.
- `npm run lint` (oxlint): sin hallazgos. Hubo 2 avisos `only-export-components` en `CamposPersona.tsx`, corregidos moviendo las constantes a `camposVisibles.ts`.
- `npm test`: **170/170**. Las 128 existentes no cambiaron; se suman 33 de `edicionPersonal.test.ts` y 9 de `tramos.test.ts`.

**Lo que no se hizo:** cambios en `backend/`, `npm run dev`, `dotnet run`, clientes HTTP contra la API, ni comandos git que modifiquen el repositorio. `X-Dev-User` sigue solo en `src/auth/` (verificado con `grep`).

## 1. Decisiones aprobadas (Fase A)
| # | Decisión |
|---|---|
| P1 | Pantalla propia `/proyectos/:id/personal` (carga diferida en `App.tsx`) |
| P2 | Se reordenan (↑↓) solo los principales **nuevos**; las vigentes conservan su número. "Será el principal inicial (responsable)" se recalcula al reordenar (prueba Vitest) |
| P3 | Vigentes eliminadas: atenuadas, con "Se eliminará" y "Restaurar" |
| P4 | Históricos en tabla compacta de solo lectura |
| P5 | Unión de tramos contiguos también en `VistaPrevia` de creación |
| P6 | Al registrar: detalle con el aviso "Personal actualizado (versión n)." y la pestaña Historial |
| P7 | Extracción de `CamposPrincipal`/`CamposBack` y `AccionEditarCabecera` → `AccionSegunConsulta`, sin cambiar comportamiento (ninguna prueba existente cambió) |
| P8 | Escrituras de la verificación en el Id 13. Para P7: DEV008 o, si tiene cruces, DEV002, DEV001, DEV006 y DEV007, en ese orden (DEV005 no está libre). Se puede cambiar el empleado de una persona nueva sin perder el resto del formulario |

## 2. Qué se hizo
**API y hooks**
- `obtenerEdicionPersonal`, `previsualizarPersonal`, `registrarPersonal`.
- Clave `clavesProyectos.edicion(id)` = `['proyectos','edicion',id]`.
- `useEdicionPersonal`, `usePrevisualizarPersonal`, `useRegistrarPersonal` (este invalida `todos`).

**`edicionPersonal.ts`** (lógica pura; 33 pruebas)
- **Estado desde el GET:**
  - claves estables `p{id}`/`k{id}` (guardadas) y `pn{n}`/`kn{n}` (nuevas);
  - relación del back por clave (principal del cuerpo) o `historicoId` (principal histórico);
  - los iniciales salen del detalle, porque el GET de edición no trae `EsPrincipalInicial`.
- **Reducer:**
  - actualizar, respetando permisos;
  - agregar (fechas sugeridas del corte o del inicio al fin del proyecto);
  - cambiar el empleado (solo nuevas);
  - quitar: una nueva desaparece; una vigente que aún no empieza queda marcada; una que ya empezó no se quita;
  - restaurar y mover nuevos;
  - casilla de confirmación;
  - al quitar un principal, sus backs pasan a "Sin relación" con aviso.
- **Cuerpo:**
  - nunca lleva las históricas; las eliminadas se omiten;
  - relación como `principalClave` o `principalId`;
  - cargo de la vigente null si no cambió;
  - `aSolicitudRegistroPersonal` agrega `versionProyecto`;
  - `clavesDelEnvioEdicion` da el orden del envío para los índices de los 400.
- **Consultas:**
  - P3: `claveSeraInicial`;
  - mínimo sobre el personal resultante (históricas + vigentes + nuevas) según `exigePrincipal`;
  - ayudas con los textos del servidor: fin < inicio, fin < corte − 1, fuera de rango, jornada vacía.
- **Vista previa y registro:**
  - `sinCambiosPersonal` (todas en `SIN_CAMBIO`; pendiente 25);
  - `requiereConfirmacionPersonal` (ELIMINADO o fin acortado);
  - `puedeRegistrarPersonal`, `motivoSinRegistroPersonal` y `mensajeExitoPersonal`.
- **Errores (`interpretarErrorPersonal`):**
  - 400 con `distribuirErrores` (TAREA-13): índice del envío → fila; `principales`/`backs` → sección; `personal`, `general` y las demás → generales; `versionProyecto` y `proyecto` ofrecen recarga;
  - 409 con `extensions.cruces` → cruces (con `crucesDeConflicto`);
  - 409 sin cruces, 503/red y 404 → `interpretarErrorDialogo` (19a).

**Otros módulos**
- **`tramos.ts`:** `unirTramosContiguos` (pendiente 26). Une, solo para mostrar, los tramos de la misma persona, empleado, rol, tipo y bloque con inicio = fin anterior + 1, y suma los días. Tiene 9 pruebas. Se aplica en la vista previa de personal y en la de creación (que ahora cuenta los tramos unidos).
- **`minimoPersonal.ts`:** `cumpleMinimo`/`ayudaMinimo`, comunes a "Nuevo proyecto" (`ayudaPersonal` y `puedeGenerarVistaPrevia` delegan en ellas con el mismo resultado) y a la edición.

**Componentes**
- **`CamposPersona.tsx` (`CamposPrincipal`, `CamposBack`):** campos presentacionales extraídos de `ListaPrincipales`/`ListaBacks` con el mismo marcado; agregan bloqueos y límites de fecha opcionales. Las constantes de campos visibles están en `camposVisibles.ts`.
- **`ListaPersonalEdicion.tsx`:**
  - por rol: tabla de históricos;
  - tarjetas de vigentes (marca "Ya empezó"; inicio y jornada bloqueados según permisos; fin con mínimo corte − 1) y de nuevas (marca "Nuevo"; ↑↓ solo en principales nuevos; "Cambiar empleado"; "Quitar");
  - eliminadas atenuadas con "Restaurar";
  - marca "Será el principal inicial (responsable)".
- **`VistaPreviaPersonal.tsx`:** tabla persona/clase/acción, advertencias, cruces (`TablaCruces`) y tramos unidos por persona. Los días se cuentan desde los tramos, porque esta vista previa no trae `diasPorPersona`.
- **`ActualizarPersonal.tsx`:**
  - carga la edición, el detalle y los catálogos;
  - si `puedeEditar` es false, muestra el motivo;
  - formulario con ayuda del mínimo, vista previa, casilla, barra fija, `enviandoRef` y buscador (agregar o cambiar);
  - errores con foco en el primer campo con `aria-invalid` o en el resumen;
  - "Reintentar", "Recargar datos del proyecto" (vuelve a montar el formulario, sin navegar, así que no pide confirmación) y enlace al listado;
  - confirmación al salir (`useBlocker` + `beforeunload`) desactivada tras registrar.
- **`AccionSegunConsulta.tsx`:** generaliza `AccionEditarCabecera`, con el mismo comportamiento. La usan "Editar datos generales" y "Actualizar personal" en `DetalleProyecto`.
- **`DetalleProyecto`/`ListadoProyectos`:** `EstadoNavegacionProyectos` gana `aviso` y `pestana`, y el detalle los lee al abrirse.

## 3. Archivos (frontend)
| Archivo | Cambio |
|---|---|
| `src/app/App.tsx` | Ruta `proyectos/:id/personal` (antes de `:id`) |
| `features/proyectos/api.ts`, `hooks.ts` | API, clave y hooks de personal |
| `features/proyectos/edicionPersonal.ts` + `.test.ts` (nuevos) | Lógica pura y 33 pruebas |
| `features/proyectos/tramos.ts` + `.test.ts` (nuevos) | Unión de tramos y 9 pruebas |
| `features/proyectos/minimoPersonal.ts` (nuevo) | Regla común del mínimo |
| `features/proyectos/camposVisibles.ts` (nuevo) | Campos visibles de las tarjetas |
| `features/proyectos/formularioProyecto.ts` | `ayudaPersonal`/`puedeGenerarVistaPrevia` delegan en `minimoPersonal.ts` |
| `components/CamposPersona.tsx` (nuevo) | `CamposPrincipal`, `CamposBack` |
| `components/ListaPrincipales.tsx`, `ListaBacks.tsx` | Usan `CamposPrincipal`/`CamposBack` (misma salida) |
| `components/ListaPersonalEdicion.tsx`, `VistaPreviaPersonal.tsx`, `AccionSegunConsulta.tsx` (nuevos) | Sección 2 |
| `components/VistaPrevia.tsx` | Tramos unidos (pendiente 26) |
| `pages/ActualizarPersonal.tsx` (nuevo) | Pantalla |
| `pages/DetalleProyecto.tsx` | Acción "Actualizar personal"; `AccionSegunConsulta`; aviso y pestaña desde la navegación |
| `pages/ListadoProyectos.tsx` | `EstadoNavegacionProyectos.aviso`/`pestana` |
| `docs/00_ESTADO_ACTUAL.md` | §6, §7 (pendientes 25 y 26), §9 |
| `docs/tareas/TAREA-19b-reporte.md` (nuevo) | Este reporte |

## 4. Comandos
| Comando (desde `frontend/`) | Resultado |
|---|---|
| `npm test` (línea base) | 128/128 |
| `npm test` (tras extraer `minimoPersonal.ts`) | 128/128 |
| `npx vitest run …/tramos.test.ts` | 9/9 |
| `npx vitest run …/edicionPersonal.test.ts` | 33/33 |
| `npm run build` | Sin errores ni advertencias |
| `npm run lint` | 2 avisos `only-export-components` → constantes movidas a `camposVisibles.ts` → sin hallazgos |
| `npm test` (final) | **170/170** (6 archivos) |

## 5. Contradicciones y observaciones
1. **El GET de edición no trae `EsPrincipalInicial`.** Para P3 ("Será el principal inicial") la pantalla lo toma del detalle (`GET /api/proyectos/{id}`), que se carga a la vez.
2. **La vista previa de personal no trae `diasPorPersona`.** Los días por persona se cuentan desde los tramos.
3. **Restaurar un principal eliminado no restablece las relaciones de sus backs.** Quedan "Sin relación" con aviso, como en "Nuevo proyecto" (R8). El usuario los vuelve a relacionar.
4. **Vista previa de creación:** el resumen "n tramos" ahora cuenta los tramos unidos. Solo cambia la presentación.
5. **Foco tras un 400:** se busca el primer control con `aria-invalid="true"` (lo pone `Field` con `validationMessage`). Si el error es de sección o general, el foco va al resumen de errores (`tabIndex=-1`).

## 6. Verificación visual (usuario, 06/10/2026) — P1–P15 OK (P10 tras la TAREA-19b2)
- Usuario `gestor`.
- **H** = día de la prueba.
- **Fechas límite:** P2 hasta el 16/10, P5 hasta el 26/10 y P8 hasta el 05/11. Pasada esa fecha, la persona ya empezó o el proyecto terminó.
- **Escrituras:** solo en el **Id 13** (P7–P10).

| # | Proyecto | Pasos | Esperado | ¿Escribe? | Resultado |
|---|---|---|---|---|---|
| P1 | Id 2 (SUSPENDIDO) | Detalle | "Actualizar personal" deshabilitado con el motivo (no está ACTIVO) | No | OK |
| P2 | Id 11 | Detalle | Si H ≤ 16/10: habilitado. Si H > 16/10: deshabilitado con el motivo "ya terminó" (D1) | No | OK |
| P3 | Id 10 (con históricos) | Abrir la pantalla | Históricos en tabla de solo lectura; el principal vigente con "Ya empezó", inicio de solo lectura, fin "Desde H−1" y sin "Eliminar". Vista previa sin cambios: "No hay cambios para registrar." y "Registrar" deshabilitado (pendiente 25). Tramos del principal **unidos** (antes 04/10–04/10 y 05/10–…; pendiente 26) | No | OK |
| P4 | Id 10 | Acortar el fin del principal vigente → vista previa | MODIFICADO; casilla "Entiendo que esta acción no se puede deshacer." obligatoria. **No registrar** | No | OK |
| P5 | Id 13 (solo backs; hasta el 26/10) | Eliminar los dos backs | Atenuados con "Restaurar"; ayuda "Agrega al menos 1 persona (principal o back) para generar la vista previa." y "Generar vista previa" deshabilitado. Restaurar y salir sin registrar (confirmación de salida) | No | OK |
| P6 | Id 12 (02/11–25/11) | Agregar un back DEV004 del 05/11 al 09/11 → vista previa | Cruces EXTERNOS con el Id 13; "Registrar" deshabilitado ("No se puede registrar con cruces de asignación.") | No | OK |
| P7 | Id 13 | Agregar un principal **DEV008** en todo el rango → vista previa. Si tiene cruces: "Cambiar empleado" (sin perder fechas ni jornada) y probar DEV002, DEV001, DEV006 y DEV007, en ese orden. Luego Registrar | Marca "Será el principal inicial (responsable)" (P3); acción NUEVO; sin la advertencia de "sin principal". "Personal actualizado (versión 3).", pestaña Historial; en el listado, Responsable = ese empleado | **Sí (Id 13)** | OK |
| P8 | Id 13 (hasta el 05/11) | Eliminar el back 2 → vista previa → marcar la casilla → Registrar con doble clic rápido | ELIMINADO; una sola versión nueva (4) | **Sí (Id 13)** | OK |
| P9 | Id 13 | Relacionar el back 1 con el principal de P7 → vista previa → Registrar | MODIFICADO; versión 5 | **Sí (Id 13)** | OK |
| P10 | Id 13 | Dos pestañas: A cambia la observación del back 1 y registra; B (abierta antes, con su vista previa) cambia el fin del back 1 → Registrar | B: 409 "El proyecto cambió; vuelve a cargarlo." + "Recargar datos del proyecto"; tras recargar (sin pedir confirmación de salida) ve los datos de A | **Sí (Id 13, solo A)** | **Defecto:** B (formulario del GET anterior, v5) cambió el fin del back 1, generó la vista previa DESPUÉS del registro de A (v6) y registró: 200 sin 409, y la observación de A quedó sobrescrita con la vieja. Causa: el registro usaba el `versionProyecto` de la vista previa (decisión de la 19x). **Corregido en la TAREA-19b2** (token base = datos que vio el usuario); se repitió como B1/B2: **OK tras la corrección** |
| P11 | Id 13 | Cambiar un campo después de la vista previa | "Vista previa desactualizada."; "Registrar" deshabilitado | No | OK |
| P12 | Id 13 | Con cambios, pulsar "Volver al proyecto" o Atrás del navegador | "¿Salir sin registrar?" | No | OK |
| P13 | Creación | "Nuevo proyecto" → vista previa | Tramos por persona como antes (unidos si había contiguos) | No | OK |
| P14 | — | `/proyectos/999/personal` | "Proyecto no encontrado o sin acceso" + enlace al listado | No | OK |
| P15 | Opcional | Detener la API y generar la vista previa | Error de red + "Reintentar" | No | OK |

Los 400 por índice del servidor no se pueden provocar fácilmente desde la pantalla, porque los selectores limitan las fechas. Quedan cubiertos por las pruebas de `interpretarErrorPersonal`.

## 7. Pendientes
- Menor: agregar `esPrincipalInicial` al GET …/edicion (hoy se toma del detalle; pendiente 36).
- TAREA-19c (reactivación): reutilizar `CamposPersona`, `unirTramosContiguos`, `minimoPersonal.ts` y el patrón de esta pantalla.
