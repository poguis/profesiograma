# TAREA-24b — Reporte: frontend de novedades, registro

**Fecha:** 26/10/2026 · **Fase:** B (implementación del plan de la TAREA-24) · **Estado:** ✅ Verificada por el usuario el 26/10/2026

## 1. Qué se hizo
- Ruta `/novedades/nueva` (carga diferida, antes de `/novedades/:id`) y botón «Nueva novedad» en el listado (a todo el ancho en el celular).
- `NuevaNovedad`: aplicación (GENERAL solo con `puedeRegistrarGeneral`) → persona (buscador) o proyecto (selector) → tipo → Desde/Hasta → observación con contador; «n días (fines de semana incluidos)»; con más de 366 días, error y «Registrar» deshabilitado. 201 → `replace` al detalle con «Novedad {codigo} registrada.» (el detalle muestra el aviso) e `invalidarTrasNovedad`.
- `CamposNovedad` (tipo, rango, observación; lo reutiliza la 24c) y `SelectorProyectoVigente` (Combobox con espera de 400 ms, una consulta por estado `esVigente`, insignia «Suspendido», «escriba para afinar la búsqueda» con 20 coincidencias).
- `BuscadorEmpleados`: props opcionales `verOtrosInicial` y `textoAccion`; en novedades, selección única con la marca «Seleccionado».
- Lógica pura: `diasNovedad.ts` (`contarDias`, `excedeMaximo`, `diasDelRango`, `MAX_DIAS`), `erroresNovedad.ts` (envoltorio: el 409 de duplicado no ofrece «Recargar» y guarda `codigo` si viene; 404 de la novedad) y `formularioNovedad.ts` (estado, validación con los mensajes del backend, solicitud).
- Hooks: `useOpcionesNovedad`, `useProyectosVigentes` (`useQueries`), `useCrearNovedad` (sin reintentos). `api.ts`: `obtenerOpcionesNovedad`, `crearNovedad`.
- Documentación: `FASE_5_Novedades.md` §9.2 (24b) y §9.3 (resumen operativo de las 24c y 24d con la verificación E, A y S); `00_ESTADO_ACTUAL.md` (24a ✅, 24b implementada, siguiente 24c; 24 991 B).

## 2. Archivos tocados
| Archivo | Cambio |
|---|---|
| `frontend/src/features/novedades/pages/NuevaNovedad.tsx` | Nuevo |
| `…/components/CamposNovedad.tsx`, `…/components/SelectorProyectoVigente.tsx` | Nuevos |
| `…/formularioNovedad.ts` (+test), `…/erroresNovedad.ts` (+test) | Nuevos |
| `…/diasNovedad.ts` (+test) | Días del rango y máximo |
| `…/hooks.ts`, `…/api.ts`, `…/tipos.ts` | Opciones, proyectos vigentes, creación y sus tipos |
| `…/pages/ListadoNovedades.tsx`, `…/pages/DetalleNovedad.tsx` | Botón «Nueva novedad»; aviso de éxito (`aviso` en el estado de navegación) |
| `frontend/src/features/proyectos/components/BuscadorEmpleados.tsx` | 2 props opcionales |
| `frontend/src/app/App.tsx` | Ruta `novedades/nueva` |
| `docs/fases/FASE_5_Novedades.md`, `docs/00_ESTADO_ACTUAL.md` | §9.2, §9.3 y estado |

## 3. Comandos y resultado (desde `frontend/`)
| Comando | Resultado |
|---|---|
| `npm run build` | OK, sin errores ni advertencias |
| `npm run lint` (oxlint) | Sin hallazgos (exit 0) |
| `npm test` | **323/323** (305 + 18: `diasNovedad` 5, `erroresNovedad` 8, `formularioNovedad` 5). `buscadorEmpleados.test.ts` y el resto, sin cambios |

## 4. Desviaciones y decisiones
- **Marca «Seleccionado»:** el buscador escribía «Ya agregado: {etiqueta}». Para no agregar una tercera prop, con `textoAccion` distinto de «Agregar» la marca es la etiqueta tal cual. Proyectos no cambia.
- **`formularioNovedad.ts`** (no estaba en la lista): la validación previa y el armado de la solicitud quedaron en lógica pura, con 5 pruebas.
- **«Registrar»** solo se deshabilita mientras envía o con más de 366 días. Con otros campos incompletos, el primer clic marca todos los errores con «Revise los campos marcados.». Antes de ese clic solo se ven el rango y el largo de la observación.
- El 409 «Otro registro se guardó al mismo tiempo» (alta concurrente) se muestra arriba, sin «Recargar» en el registro; basta con volver a «Registrar».
- GENERAL no muestra un campo de sujeto (no lo tiene).
- Opciones del formulario sin reintentos automáticos (como el listado, H9): si fallan, «Reintentar».
- Sin cambios de backend: el contrato alcanzó (sin B0).

## 5. Verificación para el usuario (`npm run dev`; **fechas de 2070**; al final, `tarea23-2\limpiar.cmd` con `IDS` = los Id creados)
| # | Caso (usuario DevAuth) | Esperado |
|---|---|---|
| R1 | gestor: «Nueva novedad» → PERSONA → «Buscar persona» (el diálogo abre con «Ver otros departamentos» encendido) → «Seleccionar» → VACACIONES 02/03/2070–06/03/2070 → Registrar | Detalle con «Novedad NOV-… registrada.»; 5 días; el listado y el cronograma se refrescan |
| R2 | gestor o admin: PROYECTO → escribir en el selector → elegir uno (los SUSPENDIDO con insignia) → FERIADO en 2070 | 201 y detalle con enlace al proyecto |
| R3 | La lista de aplicaciones como admin y como gestor | GENERAL solo para el admin; el admin registra una GENERAL en 2070 |
| R4 | Desde 01/01/2070, Hasta 02/01/2071 | «La novedad no puede superar 366 días.» y «Registrar» deshabilitado; con 01/01/2071 (366) se habilita |
| R5 | Repetir R1 (mismo empleado, tipo y fechas) | 409 «Ya existe una novedad…» con «Ver NOV-…» (lleva al listado filtrado); sin «Recargar» |
| R6 | Hasta anterior a Desde (escrito a mano); observación de 501 caracteres | «La fecha fin debe ser mayor o igual a la fecha inicio.»; contador 501/500 y su error |
| R7 | Elegir algo y salir por el menú o recargar (F5) | Diálogo «¿Salir sin registrar?» / aviso del navegador; no aparece tras registrar |
| R8 | ERP caído (URL inválida por user-secrets, como en la 26a) y registrar una PERSONA | Buscador: 503 con «Reintentar»; registro: «Servicio de empleados no disponible: …» con «Reintentar» |
| R9 | 375 px | Una columna; fechas una debajo de otra; botones a todo el ancho |
| R10 | Regresión: «Nuevo proyecto» → «Agregar principal» | Igual que antes: «Ver otros departamentos» apagado, botón «Agregar», marcas «Ya agregado: P1» |

## 6. Pendientes
- Verificación visual (§5) y limpieza de las novedades de 2070.
- 24c (edición y anulación, `apiPut`), 24d (sincronización), 24e (opcional, B0). Plan en `FASE_5_Novedades.md` §9.3.
