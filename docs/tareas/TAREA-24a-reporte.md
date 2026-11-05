# TAREA-24a — Reporte: frontend de novedades (menú, listado y detalle de solo lectura, ajuste móvil)

**Fecha:** 2026-10-22 · **Fase:** B (implementación) del plan de la TAREA-24 aprobado (U1–U8).
**Estado:** implementada; pendiente de la verificación visual del usuario (§5).

## 1. Qué se hizo
- Menú lateral «Novedades» (icono `CalendarCancel24Regular`, existe en la versión instalada) y rutas con carga diferida `/novedades` y `/novedades/:id`, antes de `proyectos/:id`.
- Módulo `src/features/novedades/`:
  - `tipos.ts`: DTO de `FASE_5_Novedades.md` §1 (resumen, detalle con `version`, `puedeEditar`, `puedeAnular`, `motivoNoEditable`, `registro` anulable) y los filtros.
  - `api.ts`: `clavesNovedades` (`todos`, `listado(f)`, `detalle(id)`, `opciones`) y los GET.
  - `hooks.ts`: `useNovedades` (`keepPreviousData`, `retry: false`) y `useNovedad` (`retry: false`), H9.
  - `filtrosUrl.ts`: patrón de proyectos; los valores ilegibles vuelven al valor por defecto (aplicación desconocida, fecha sin `yyyy-MM-dd`, `incluirAnuladas` distinto de `true`, página < 1 o no entera, tamaño fuera de 10/20/50).
  - `diasNovedad.ts`: `agruparPorMes` y `etiquetaDia` (UTC, sin depender de la zona del equipo).
  - `presentacion.ts`: nombres de aplicación y origen desde `GET /api/catalogos`; texto del sujeto.
  - `invalidacion.ts`: `invalidarTrasNovedad` (N15), para la 24b–24d.
  - `components/`: `FiltrosNovedades`, `TablaNovedades`, `EtiquetaTipoNovedad`. `pages/`: `ListadoNovedades`, `DetalleNovedad`.
- Listado: filtros en la URL, DataGrid dentro de un contenedor con desplazamiento horizontal (ancho mínimo 960 px), sigla sobre el color del tipo (texto negro o blanco según `contrasteTexto` del cronograma), «Anulada» (fila atenuada) o «Vigente», paginación 10/20/50, 400 bajo su filtro, 503 o red con «Reintentar» manual.
- Detalle de solo lectura: código, tipo, «Anulada» con fecha y motivo, `motivoNoEditable` como texto, datos (aplicación, persona o proyecto con enlace a `/proyectos/:id`, origen, fechas y días, fecha de registro, «Registrado por» **solo si el backend envía `registro`**, observación) y días agrupados por mes. Sin botones Nueva, Editar ni Anular. 404 → «La novedad no existe o no tiene acceso.» con enlace al listado.
- U4: `DisenoPrincipal` y `MenuLateral` solo cambian dentro de `@media (max-width: 767px)` (menú arriba en una fila desplazable, barra superior con salto de línea, menos relleno). Por encima de 768 px no cambió ningún estilo.
- `Catalogos` (proyectos/tipos.ts): `tiposAplicacionNovedad` y `origenesNovedad`, opcionales como los demás campos agregados (el backend ya los enviaba).

## 2. Archivos tocados
| Archivo | Cambio |
|---|---|
| `frontend/src/features/novedades/` (15 archivos nuevos) | `tipos.ts`, `api.ts`, `hooks.ts`, `filtrosUrl.ts` (+test), `diasNovedad.ts` (+test), `invalidacion.ts` (+test), `presentacion.ts`, `components/EtiquetaTipoNovedad.tsx`, `components/FiltrosNovedades.tsx`, `components/TablaNovedades.tsx`, `pages/ListadoNovedades.tsx`, `pages/DetalleNovedad.tsx` |
| `frontend/src/app/App.tsx` | 2 rutas |
| `frontend/src/components/MenuLateral.tsx` | Entrada «Novedades»; fila por debajo de 768 px |
| `frontend/src/components/DisenoPrincipal.tsx` | Media query por debajo de 768 px |
| `frontend/src/features/proyectos/tipos.ts` | 2 campos opcionales en `Catalogos` |
| `docs/fases/FASE_5_Novedades.md` | 23-3 ✅, filas simuladas (6 de 10 se guardan), §8.7, §9 Frontend |
| `docs/00_ESTADO_ACTUAL.md` | Cabecera, §6 (23-3 ✅; 24 con 24a–24e), §8 (23-3 ✅), §9 (fila 24a), pendiente 62 reformulado |

Sin cambios en `backend/`.

## 3. Comandos (desde `frontend/`)
| Comando | Resultado |
|---|---|
| `npm run build` | OK (tsc -b + vite), sin errores ni advertencias. Chunks nuevos: `ListadoNovedades` 7,4 kB y `DetalleNovedad` 4,8 kB |
| `npm run lint` (oxlint) | Sin hallazgos (exit 0) |
| `npm test` | **305/305** (294 + 11 nuevas: `filtrosUrl` 6, `diasNovedad` 4, `invalidacion` 1). Ninguna prueba existente se ajustó |

No se ejecutaron `npm run dev`, la API ni git de escritura.

## 4. Desviaciones, hallazgos y notas
- **B1–B4:** el prompt de la 24a los menciona como ajustes aprobados, pero no trae su contenido. Se siguió el «Diseño aprobado» del mismo prompt. [PENDIENTE DE CONFIRMAR] si algún B1–B4 pedía algo distinto.
- `diasNovedad.ts`: además de `agruparPorMes` incluye `etiquetaDia` («lun 05») para los chips del detalle. Nombres de mes y día fijos en español, sin `Intl`, para que el texto no dependa del ICU del navegador.
- `presentacion.ts` (no estaba en el plan): nombres de catálogo y el texto del sujeto, que comparten el listado y el detalle.
- El sujeto GENERAL se muestra como «General» y la etiqueta del dato es «Alcance» [SUPUESTO de texto].
- `invalidarTrasNovedad` usa los prefijos literales `['proyectos','cronograma']` y `['proyectos','reporte-resumen']`. La prueba los compara con las claves reales `clavesProyectos.cronograma(17, …)` y `clavesProyectos.reporteResumen(17, …)`: si cambian, la prueba falla.
- El filtro «Tipo» lista todos los tipos del catálogo (incluido PERMISO MEDICO); para un gestor el backend decide qué devuelve (J2).
- El listado no oculta filtros según el rol: la visibilidad la decide solo la API.

## 5. Verificación visual para el usuario (`npm run dev`, DevAuth)
**L — Listado (`/novedades`)**
| # | Caso | Esperado |
|---|---|---|
| L1 | gestor: abrir «Novedades» en el menú | Lista sin NOV-SIMPM-* ni NOV-DEV-0004; spinner o esqueleto mientras carga |
| L2 | admin: lo mismo | Aparecen NOV-DEV-0004 y las NOV-SIMPM-* (si no están anuladas) |
| L3 | Elegir aplicación, tipo y origen; escribir texto «NOV-DEV»; elegir desde y hasta | La URL cambia con cada filtro; recargar (F5) conserva la búsqueda; «Limpiar filtros» la vacía |
| L4 | Encender «Incluir anuladas» | Aparecen las anuladas (Id 5–14 de la 23-2 y, para el admin, NOV-SIMPM anuladas si las hay) con «Anulada» y fila atenuada |
| L5 | Paginación: tamaño 10, ir a la página 2 | Funciona; cambiar un filtro vuelve a la página 1 |
| L6 | Sigla y color | Cada tipo muestra su sigla sobre su color, con texto legible |
| L7 | URL a mano `/novedades?aplicacion=OTRA&tamano=30&desde=05/10/2026` | Se ignora lo ilegible (todas las aplicaciones, tamaño 20, sin desde) |
| L8 | Clic en una fila (o Enter) | Abre el detalle; «Volver al listado» regresa con los mismos filtros |

**D — Detalle (`/novedades/:id`)**
| # | Caso | Esperado |
|---|---|---|
| D1 | NOV-DEV-0001 (PERSONA) | Persona «DEV00x · nombre», tipo, origen, fechas, días por mes con chips «lun 05» |
| D2 | NOV-DEV-0002 (PROYECTO) | «Proyecto» con enlace al detalle del proyecto |
| D3 | NOV-DEV-0003 (GENERAL) | «Alcance: General» |
| D4 | El mismo detalle como admin y como gestor | «Registrado por» solo para el admin |
| D5 | admin: NOV-DEV-0004 y una NOV-SIMPM-* | Solo lectura; aviso con el `motivoNoEditable` («Las novedades de PERMISOS_MEDICOS son de solo lectura.») |
| D6 | Una anulada (Id 5–14, o una NOV-SIMPM anulada) | Insignia «Anulada» y aviso con fecha y motivo |
| D7 | gestor: abrir el Id de NOV-DEV-0004; también `/novedades/abc` y `/novedades/999999` | «La novedad no existe o no tiene acceso.» con enlace al listado |
| D8 | Que no haya botones Nueva, Editar ni Anular | No aparecen (llegan en la 24b y la 24c) |

**M — Celular (375 px, herramientas del navegador)**
| # | Caso | Esperado |
|---|---|---|
| M1 | Menú | Arriba, en una fila desplazable a lo ancho; el contenido usa todo el ancho |
| M2 | Listado | Filtros en una columna; la tabla se desplaza a lo ancho; botones a todo el ancho |
| M3 | Detalle | Tarjetas en una columna; datos (etiqueta y valor) uno debajo del otro; chips de días con salto de línea |

**Regresión**
| # | Caso | Esperado |
|---|---|---|
| R1 | A 1280 px: lista de proyectos, detalle de un proyecto y `/cronograma` | Idénticos a antes (menú lateral de 220 px; solo se suma «Novedades») |
| R2 | A 375 px: menú en `/proyectos` y `/cronograma` | Fila superior desplazable (las páginas de proyectos no se rediseñaron) |

## 6. Pendientes
- Verificación visual del usuario (§5).
- 24b registro, 24c edición y anulación (`apiPut`), 24d sincronización, 24e (opcional, con B0).
- Pendientes abiertos: 7 y 63 (bloquean la 23-4), 62 (reformulado) y 64.
