# TAREA-24c — Reporte: frontend de novedades, edición y anulación

**Fecha:** 26/10/2026 · **Fase:** B (implementación del plan de la TAREA-24) · **Estado:** ✅ Verificada por el usuario el 26/10/2026

## 1. Qué se hizo
- `apiPut` en `clienteHttp.ts` (mismo `solicitar` que `apiPost`; `apiPost` no tiene pruebas propias, así que no se agregaron).
- Ruta `/novedades/:id/editar` y página `EditarNovedad`: el formulario se monta con el GET que lo abrió (su `version` se envía), aplicación y sujeto de solo lectura, `CamposNovedad` compartido con el registro, tipo guardado no elegible como «(no disponible)», chips de días por mes (`SelectorDiasExcluidos`), nota de A4 al acortar el rango, resumen «Se guardará:», «No hay cambios para registrar.», «Recargar» con confirmación si hay cambios, `useBlocker` + `beforeunload`. 200 → «Novedad {codigo} actualizada.».
- Detalle: botones «Editar» y «Anular» según `puedeEditar`/`puedeAnular` (sin inferir permisos) y `DialogoAnularNovedad` (motivo 5–500 con contador, «Esta acción no se puede deshacer.», casilla «Entiendo», doble clic protegido; 200 → «Novedad {codigo} anulada.» y el detalle se recarga; 409 → «Recargar»).
- Lógica pura `edicionNovedad.ts` (estado inicial, días resultantes, `conRango`, `hayCambios`, `validarEdicion`, `resumenCambios`, solicitud, `validarMotivo`) y `erroresNovedad.ts` ampliado (`CAMPOS_EDICION`, `CAMPOS_ANULACION`, `interpretarErrorDesconocido`, que también usa ahora `NuevaNovedad`).
- Hooks `useEditarNovedad` y `useAnularNovedad` (sin reintentos, `invalidarTrasNovedad`).
- Documentación: `FASE_5_Novedades.md` §9.4 (24c), §9.3 solo con la 24d y la lista S, y la tabla del encabezado; `00_ESTADO_ACTUAL.md` (24b ✅, 24c implementada, siguiente 24d; **24 945 B**; el texto reemplazado va textual a `HISTORIAL.md` H9, con 0 líneas perdidas); `TAREA-24b-reporte.md` marcada «Verificada por el usuario el 26/10/2026».

## 2. Archivos tocados
| Archivo | Cambio |
|---|---|
| `frontend/src/api/clienteHttp.ts` | `apiPut` |
| `frontend/src/features/novedades/pages/EditarNovedad.tsx` | Nuevo |
| `…/components/SelectorDiasExcluidos.tsx`, `…/components/DialogoAnularNovedad.tsx` | Nuevos |
| `…/edicionNovedad.ts` (+test) | Nuevo |
| `…/erroresNovedad.ts` (+test) | Claves de edición y anulación; error desconocido |
| `…/api.ts`, `…/hooks.ts`, `…/tipos.ts` | PUT, anular y sus tipos |
| `…/pages/DetalleNovedad.tsx` | Botones, diálogo, aviso local; sin el correo del registrante |
| `…/pages/NuevaNovedad.tsx` | Usa `interpretarErrorDesconocido` (mismo comportamiento) |
| `frontend/src/app/App.tsx` | Ruta `novedades/:id/editar` |
| `docs/fases/FASE_5_Novedades.md`, `docs/00_ESTADO_ACTUAL.md`, `docs/HISTORIAL.md`, `docs/tareas/TAREA-24b-reporte.md` | §9.3, §9.4, estado, H9 |

## 3. Comandos y resultado (desde `frontend/`)
| Comando | Resultado |
|---|---|
| `npm run build` | OK, sin errores ni advertencias (un import sobrante en `NuevaNovedad` se corrigió en la primera corrida) |
| `npm run lint` (oxlint) | Sin hallazgos (exit 0) |
| `npm test` | **340/340** (323 + 17: `edicionNovedad` 8, `erroresNovedad` 9). Ninguna prueba existente se ajustó |

## 4. Desviaciones, errores y decisiones
- **Acoplamiento (reportado):** los 409 del backend no traen un discriminador (`type`); solo `title` y, en el duplicado, la extensión `codigo`. El duplicado se reconoce por `codigo` o por el title exacto `MENSAJE_DUPLICADO`. Los demás 409 («cambió», «está anulada», «ya está anulada») ofrecen «Recargar» por defecto. Si el backend cambia el texto del duplicado sin `codigo`, ese 409 ofrecería «Recargar» por error.
- **Correo del registrante:** el detalle de la 24a mostraba «nombre · correo» para el Admin. Por la regla de esta tarea («nunca muestres cédula ni correo») ahora muestra solo el nombre.
- La regla «más de 366 días» de la edición deshabilita «Guardar cambios» igual que en la 24b (y oculta los chips mientras el rango sea inválido).
- El resumen «Se guardará:» solo aparece con cambios y sin errores del cliente.
- Tras anular se muestra el aviso local; el detalle se recarga por `invalidarTrasNovedad` y los botones quedan como digan los flags nuevos.
- Sin cambios de backend (sin B0).

## 5. Verificación para el usuario (`npm run dev`; **fechas de 2070**; al final `tarea23-2\limpiar.cmd` con `IDS` = los Id creados o anulados)

Preparación: como gestor, registrar en 2070 una PERSONA VACACIONES (A) del 03/01 al 10/01 y otra PERSONA PERMISO (B) del mismo empleado del 12/01 al 13/01.

| # | Caso (usuario DevAuth) | Esperado |
|---|---|---|
| E1 | gestor: A → «Editar» sin tocar nada | «Guardar cambios» deshabilitado y «No hay cambios para registrar.» |
| E2 | Apagar el 09/01 y guardar; volver a «Editar» y cambiar «Hasta» al 08/01 | El 09/01 se quita de los excluidos con la nota; «Se guardará:» con rango y días; 200 y «Novedad … actualizada.» |
| E3 | Apagar todos los chips | «La novedad debe tener al menos 1 día.» y botón deshabilitado; «Incluir todos» los vuelve a encender |
| E4 | Abrir «Editar» de A en dos pestañas; guardar un cambio en A y luego otro en B | B: 409 «La novedad cambió; vuelve a cargarla.» con «Recargar» (pide confirmación porque B tiene cambios) |
| E5 | Cambiar algo y salir por el menú o recargar (F5) | «¿Salir sin guardar?» / aviso del navegador; no aparece tras guardar |
| E6 | gestor: abrir **NOV-DEV-0003** (GENERAL del admin, visible para el gestor por J2) | «Editar» y «Anular» deshabilitados con «No tienes permiso para modificar esta novedad.». Admin: **NOV-DEV-0004** (PERMISOS_MEDICOS del sembrador) deshabilitados con «Las novedades de PERMISOS_MEDICOS son de solo lectura.» |
| E7 | Duplicado al editar: en B cambiar el tipo a VACACIONES y el rango a 05/01–06/01 (choca con A) | 409 «Ya existe una novedad…» con «Ver NOV-…» y sin «Recargar» |
| E8 | 375 px | Una columna; chips con salto de línea; botones a todo el ancho |
| A1 | Anular B con motivo de 4 caracteres | «El motivo debe tener entre 5 y 500 caracteres.»; no se envía |
| A2 | Motivo válido y «Entiendo» → «Anular» | 200; «Novedad … anulada.»; insignia «Anulada» con fecha y motivo; botones deshabilitados con «La novedad está anulada.» |
| A3 | Abrir A en dos pestañas; anular en la primera y luego en la segunda | Segunda: 409 «La novedad ya está anulada.» con «Recargar» (recarga y cierra el diálogo) |
| R | Regresión: «Nueva novedad» (24b) y el detalle de una novedad | Igual que antes; el admin ve «Registrado por» solo con el nombre |

## 6. Pendientes
- Verificación visual (§5) y limpieza de las novedades de 2070.
- 24d (sincronización Admin; plan en `FASE_5_Novedades.md` §9.3) y 24e (opcional, B0).
