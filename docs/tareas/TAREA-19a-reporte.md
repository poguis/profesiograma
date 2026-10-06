# TAREA-19a — Edición de cabecera y cambio de actividad (frontend) + P6

**Fecha:** 2026-10-06 (Fase A y Fase B)
**Resultado:** ✅ completada. Verificación visual del usuario del 06/10/2026: **V1–V17 OK** (sección 6). Proyecto D = **Id 12** (creado en V11; versiones 2–5 por V12–V15). V15 reveló la falta de un token de concurrencia desde el cliente (pendiente 32).
- `npm run build`: sin errores ni advertencias.
- `npm run lint` (oxlint): sin hallazgos.
- `npm test`: **117/117** (72 anteriores sin cambios + 41 de `edicionCabecera.test.ts` + 4 de P6 en `formularioProyecto.test.ts`).

No se modificó `backend/`: la API de la TAREA-18/18b cubre todo. No se ejecutaron `npm run dev`, `dotnet run` ni ningún cliente HTTP contra la API. No se usaron comandos git que modifiquen el repositorio. `X-Dev-User` sigue solo en `src/auth/` (verificado con `grep`).

## 1. Decisiones aprobadas (Fase A)
| # | Decisión |
|---|---|
| P1 | División: 19a (cabecera + P6), 19b (personal + pendiente 25 lado frontend + unión de tramos), 19c (reactivación) |
| P2 | Confirmación con la casilla "Entiendo que esta acción no se puede deshacer." (patrón de la TAREA-15) |
| P3 | `useCabecera(id)` se consulta al cargar el detalle. Si falla (503/red): botón deshabilitado, "No se pudo verificar si el proyecto se puede editar." y "Reintentar"; el detalle se muestra igual. Si `puedeEditar` es false: botón deshabilitado con el motivo |
| P4 | Mensajes de éxito: "Datos generales actualizados (versión n)." y "Actividad cambiada (versión n)." |
| P5 | La verificación que escribe se hace en un proyecto nuevo D que crea el usuario desde la pantalla |
| P6 | Extracción de `ImpactoRecorte` y `erroresDialogo` sin cambiar resultados ni pruebas de la TAREA-15 (ninguna cambió) |
| Contradicción 2 | El detalle muestra "Sin actividad vigente." en lugar de "Sin actividad registrada." |

## 2. Qué se hizo
1. **Contratos** (`tipos.ts`): `CabeceraEdicion`, `ActividadCabecera`, `PermisosCabecera`, `SolicitudEditarCabecera`, `CambioCampoCabecera`, `DiasAgregadosCabecera`, `ActividadResultante`, `PrevisualizacionCabecera` y `CabeceraActualizada` (sección 4). Reutilizan `EmpleadoCambio`, `DiasEliminadosCambio`, `PersonaEliminadaCambio` y `PersonaRecortadaCambio` de la TAREA-15.
2. **API y hooks:** `obtenerCabecera`, `previsualizarCabecera`, `registrarCabecera`; clave `clavesProyectos.cabecera(id)` = `['proyectos','cabecera',id]`, de modo que la invalidación de `todos` (registro, recarga) también la refresca. `useCabecera`, `usePrevisualizarCabecera`, `useRegistrarCabecera` (invalida `todos` al registrar; mutaciones sin reintentos, como en el resto del cliente).
3. **Lógica pura `edicionCabecera.ts`** (Vitest):
   - estado con `revision`, `original`, `valores`, `editado` y `entiende`;
   - `aSolicitudCabecera` (solo los campos distintos de los guardados, C1);
   - rangos (`rangoFechaInicio` mínimo hoy; `rangoFechaFin` el mayor entre `fechaFinMinima` e inicio; `rangoDesde` [inicio, fin] resultantes);
   - ayudas `validarEdicionCliente` con los textos del servidor;
   - `camposIncompletos` / `puedeVerImpacto`: una fecha vacía se enviaría como "no cambia", así que bloquea "Ver impacto";
   - `vistaCabeceraVigente`;
   - `sinCambios` (lista `cambios` vacía, pendiente 25);
   - `requiereConfirmacion` (días o personal eliminados, personal recortado, actividad eliminada o con fin acortado);
   - `puedeRegistrar`, `motivoSinRegistro`, `mensajeExitoCabecera`, `etiquetaCampo`, `textoValorCambio`;
   - `interpretarErrorCabecera`.
4. **Errores comunes `erroresDialogo.ts`:** `interpretarErrorDialogo(error, { campos, clavesRecarga })`.
   - `interpretarErrorCambio` (TAREA-15) lo usa sin `clavesRecarga`, así que todo 400 sigue ofreciendo recarga y sus pruebas no cambian.
   - La cabecera solo ofrece recarga con 400 `proyecto` (el proyecto dejó de ser ACTIVO) y con 409.
5. **Componentes:**
   - **`DialogoEditarCabecera`.** Diálogo de `min(960px, 100vw)` con scroll interno. Campos:
     - fecha de inicio: editable, o solo lectura con `motivoFechaInicio`;
     - fecha fin;
     - horario: si el actual ya no está activo en el ERP, se muestra igual con una ayuda;
     - salida y regreso de almuerzo (`opcionesAlmuerzo`);
     - sección Actividad: vigente hoy; "Cambiar a la actividad" con la opción "Sin cambio de actividad"; "Aplica desde"; historial de actividades con la vigente marcada.

     Comportamiento:
     - "Ver impacto"; aviso de vista desactualizada; casilla de confirmación;
     - "Registrar" con el motivo junto al botón; doble clic protegido con un ref;
     - errores por campo (`Field validationMessage`), generales arriba del botón, "Reintentar", "Recargar datos del proyecto" y enlace al listado.
   - **Confirmación de cierre:** con cambios sin registrar ("Hay cambios sin registrar. ¿Cerrar de todos modos?") al pulsar Cancelar, Esc o el fondo, y al navegar (`useBlocker` + `beforeunload`). Criterio de la TAREA-13: hubo un cambio efectivo desde que se abrió o recargó.
   - **`ImpactoCabecera`:**
     - etapa (nombre del catálogo) y rango resultante;
     - advertencias;
     - cambios (anterior → nuevo, con fechas dd/MM/yyyy);
     - `ImpactoRecorte`;
     - "Días que se agregan" (H15);
     - "Actividades resultantes" con su acción.
   - **`ImpactoRecorte`:** días eliminados, personal eliminado y personal recortado, extraídos de `ImpactoCambioEstado`, que ahora lo usa con el mismo DOM y los mismos textos.
   - **`textos.ts`:** `textoPersona`, `textoRol`, `textoRango` y `textoHorario`. `textoHorario` se movió desde `SeccionCabecera.tsx`, sin cambiar su salida.
6. **`DetalleProyecto`:**
   - acción "Editar datos generales" (P3) junto a "Cambiar estado";
   - un solo estado `dialogo` para los dos diálogos;
   - al registrar: aviso de éxito y pestaña Historial;
   - recarga: invalida `todos` y devuelve la cabecera nueva al diálogo, que se reinicia con ella. Si ya no se puede editar, el diálogo se cierra (mismo criterio que la TAREA-15).
7. **O3:** `CabeceraProyecto` muestra `actividadVigente` tal como llega de la API (sin recalcular). El texto vacío es ahora "Sin actividad vigente.".
8. **P6 (Nuevo proyecto):**
   - `MINIMO_PRINCIPALES = 1`, `ayudaPrincipales` y `puedeGenerarVistaPrevia` en `formularioProyecto.ts`;
   - sin principales, "Generar vista previa" queda deshabilitado (y por lo tanto "Registrar");
   - ajuste aprobado tras la entrega: la sección Principales muestra una **ayuda neutra** "Agrega al menos 1 principal para generar la vista previa." (`ayudaPrincipales`), también en la barra inferior;
   - el error rojo "Se requiere al menos 1 principal(es)." solo aparece si llega un 400 del servidor en `principales`;
   - el texto "Sin principales (no son obligatorios)." pasó a "Sin principales.".
9. **Accesibilidad:**
   - todos los campos tienen etiqueta (`Field label`);
   - los errores están asociados al campo (`validationMessage`);
   - foco inicial: el diálogo de Fluent enfoca el primer elemento enfocable de la superficie. Por eso los campos van primero y la fecha de inicio de solo lectura es texto (no enfocable): el foco cae en la fecha de inicio si es editable, o en la fecha fin.

## 3. Archivos (frontend)
| Archivo | Cambio |
|---|---|
| `src/features/proyectos/tipos.ts` | Sección "Edición de cabecera" (contratos) |
| `src/features/proyectos/api.ts` | `clavesProyectos.cabecera`; `obtenerCabecera`, `previsualizarCabecera`, `registrarCabecera` |
| `src/features/proyectos/hooks.ts` | `useCabecera`, `usePrevisualizarCabecera`, `useRegistrarCabecera` |
| `src/features/proyectos/edicionCabecera.ts` (nuevo) | Lógica pura del diálogo |
| `src/features/proyectos/edicionCabecera.test.ts` (nuevo) | 41 pruebas |
| `src/features/proyectos/erroresDialogo.ts` (nuevo) | Intérprete común de errores de los diálogos |
| `src/features/proyectos/textos.ts` (nuevo) | Textos de presentación compartidos |
| `src/features/proyectos/cambioEstado.ts` | `ErroresCambio` / `SIN_ERRORES_CAMBIO` / `interpretarErrorCambio` sobre `erroresDialogo` (mismo resultado) |
| `src/features/proyectos/components/DialogoEditarCabecera.tsx` (nuevo) | Diálogo "Editar datos generales" |
| `src/features/proyectos/components/ImpactoCabecera.tsx` (nuevo) | Impacto de la vista previa de cabecera |
| `src/features/proyectos/components/ImpactoRecorte.tsx` (nuevo) | `ImpactoRecorte` y `SeccionImpacto` extraídos de la TAREA-15 |
| `src/features/proyectos/components/ImpactoCambioEstado.tsx` | Usa `ImpactoRecorte`, `SeccionImpacto` y `textos.ts` (misma salida) |
| `src/features/proyectos/components/SeccionCabecera.tsx` | `textoHorario` importado de `textos.ts` |
| `src/features/proyectos/components/CabeceraProyecto.tsx` | "Sin actividad vigente." |
| `src/features/proyectos/components/ListaPrincipales.tsx` | "Sin principales." y prop `ayuda` (texto neutro) |
| `src/features/proyectos/pages/DetalleProyecto.tsx` | Acción y diálogo de cabecera; `AccionEditarCabecera` |
| `src/features/proyectos/formularioProyecto.ts` | `MINIMO_PRINCIPALES`, `ayudaPrincipales`, `puedeGenerarVistaPrevia` |
| `src/features/proyectos/formularioProyecto.test.ts` | 4 pruebas de P6 (las existentes sin cambios) |
| `src/features/proyectos/pages/NuevoProyecto.tsx` | P6: botón, aviso en la sección y texto de la barra |
| `docs/00_ESTADO_ACTUAL.md` | §6 (19a/19b/19c), §7 (pendientes 25 y 26), §9 (fila TAREA-19a) |
| `docs/tareas/TAREA-19a-reporte.md` (nuevo) | Este reporte |

## 4. Tipos frente a los DTO (TAREA-18/18b)
Origen: `backend/src/App.Application/Proyectos/Cabecera/EdicionCabeceraDtos.cs` y `EdicionCabeceraContratos.cs`.

| TypeScript | DTO |
|---|---|
| `CabeceraEdicion` | `CabeceraDto` (+ `HorarioCabeceraDto`, `OpcionesAlmuerzoDto`) |
| `ActividadCabecera` | `ActividadCabeceraDto` |
| `PermisosCabecera` | `PermisosCabeceraDto` |
| `SolicitudEditarCabecera` | `EditarCabeceraSolicitud` + `ActividadCabeceraSolicitud` |
| `PrevisualizacionCabecera` | `PrevisualizacionCabeceraDto` |
| `CambioCampoCabecera` | `CambioCampoDto` |
| `DiasAgregadosCabecera` | `DiasAgregadosDto` |
| `ActividadResultante` | `ActividadResultanteDto` |
| `CabeceraActualizada` | `CabeceraActualizadaDto` |

Formatos: fechas "yyyy-MM-dd". Horas del horario "HH:mm:ss" (`TimeOnly`, solo se muestran). Almuerzo y opciones "HH:mm" (`ReglasAlmuerzo.Formato`). En `cambios`, las fechas llegan "yyyy-MM-dd" y la actividad como "DEV.02 desde 2026-10-14"; se muestran como dd/MM/yyyy con `textoValorCambio`.

## 5. Comandos

| Comando (desde `frontend/`) | Resultado |
|---|---|
| `npm test` (línea base, antes de cambiar) | 72/72 |
| `npm test` (tras extraer `erroresDialogo`) | 72/72: las pruebas de la TAREA-15 no cambiaron |
| `npx vitest run src/features/proyectos/edicionCabecera.test.ts` | 41/41 |
| `npm test` (final) | 116/116 (4 archivos); tras el ajuste de P6: **117/117** |
| `npm run build` | Sin errores ni advertencias. `DetalleProyecto` 63,85 kB (gzip 17,70 kB) |
| `npm run lint` | Sin hallazgos (salida 0) |
| `grep -rn "X-Dev-User" frontend/src` fuera de `src/auth/` | Sin resultados |

Errores durante la implementación: ninguno de compilación ni de pruebas.

## 6. Verificación visual (usuario, 06/10/2026) — V1–V17 OK
Con la API y `npm run dev` corriendo (los ejecuta el usuario), como `gestor`. Fechas calculadas con hoy = 06/10/2026.

**Dependencias de fecha:**
- el Id 11 (inicio 11/10) tiene la fecha de inicio editable **hasta el 10/10/2026**; desde el 11/10, V3–V4 muestran el inicio en solo lectura;
- V6 requiere hoy ≤ 15/10/2026.

**Datos del Id 11 (TAREA-18b):** 11/10–16/10, horario 2, almuerzo 12:00–13:00, P1 DEV003 14/10–16/10, Back 1 DEV001 16/10–16/10 (DESCANSO MANUAL 17–18/10), actividades v1 DEV.01 11–13/10, v2 DEV.02 14–15/10, v3 DEV.01 16–16/10.

**Proyecto D (lo crea el usuario en V11):**
- grupo con proyecto ERP (DEV-ERP-001, actividad DEV.01);
- inicio 20/10/2026, fin 31/10/2026;
- P1 de 20/10 a 31/10;
- un back JORNADA del 25/10 al 28/10 con 2 días de descanso, relacionado con P1;
- empleados sin cruces (la vista previa lo indica).

Solo V11–V15 escriben, y solo en D.

| # | Proyecto | Pasos | Esperado | ¿Escribe? | Resultado |
|---|---|---|---|---|---|
| V1 | Id 2 (SUSPENDIDO) | Abrir el detalle | "Editar datos generales" deshabilitado con "Solo se puede editar la cabecera de un proyecto ACTIVO (estado actual: SUSPENDIDO)." | No | OK |
| V2 | Id 10 (ACTIVO, ya empezó) | Abrir el diálogo | Inicio en solo lectura con "La fecha de inicio ya no se puede cambiar: el proyecto empezó el 02/10/2026."; fin con mínimo hoy; el foco cae en la fecha fin; sección Actividad según el grupo | No | OK |
| V3 | Id 11 | Abrir el diálogo (hasta el 10/10) | Inicio editable (mínimo hoy) con el foco en él; historial con v1, v2 y v3 y la marca "Vigente" en **v1 DEV.01** (O3: referencia = max(inicio 11/10, hoy) = 11/10); "Vigente hoy: DEV.01 …"; la tarjeta del detalle muestra la misma actividad y versión (sin recalcular en el frontend) | No | OK |
| V4 | Id 11 | Inicio 15/10 → Ver impacto | 400 en el campo inicio: "Hay personal que empieza antes de la nueva fecha de inicio; ajusta primero el personal." | No | OK |
| V5 | Id 11 | Fin 20/10 y otro regreso de almuerzo de la lista → Ver impacto | Etapa Edición de cabecera; cambios fecha fin y regreso; v3 DEV.01 MODIFICADA hasta 20/10; sin casilla de confirmación; "Registrar" habilitado (**no pulsarlo**) | No | OK |
| V6 | Id 11 | Fin 15/10 → Ver impacto | DEV003 recortado (16/10 → 15/10); DEV001 eliminado con sus días; v3 DEV.01 ELIMINADA; casilla obligatoria. **No registrar** | No | OK |
| V7 | Id 11 | Abrir y "Ver impacto" sin tocar nada | Advertencia "No hay cambios."; "Registrar" deshabilitado con "No hay cambios para registrar." (pendiente 25) | No | OK |
| V8 | Id 11 | Tras V5, cambiar otro campo | "Vista previa desactualizada.", impacto atenuado y "Registrar" deshabilitado | No | OK |
| V9 | Id 11 | Actividad DEV.01 desde 16/10 → Ver impacto | 400 en "Cambiar a la actividad": "La actividad DEV.01 ya está vigente el 16/10/2026." | No | OK |
| V10 | — | "Nuevo proyecto" sin principales | "Generar vista previa" deshabilitado; ayuda neutra (no roja) "Agrega al menos 1 principal para generar la vista previa." bajo Principales y en la barra; sin error rojo; al agregar un principal la ayuda desaparece y el botón se habilita (P6) | No | OK |
| V11 | D (nuevo) | Crear D desde "Nuevo proyecto" con los datos de arriba | 201 y detalle de D con aviso del código | **Sí (crea D)** | OK |
| V12 | D | Fin 07/11 y otro horario → Ver impacto → Registrar | "Datos generales actualizados (versión 2).", pestaña Historial con la etapa EDICION_CABECERA, detalle recargado | **Sí (D)** | OK |
| V13 | D | Solo actividad DEV.02 desde 27/10 → Ver impacto → Registrar | Etapa CAMBIO_ACTIVIDAD; "Actividad cambiada (versión 3)."; en el historial, la actividad DEV.02 | **Sí (D)** | OK |
| V14 | D | Fin 26/10 → Ver impacto → marcar la casilla → Registrar con doble clic rápido | P1 y back recortados al 26/10; "Días que se agregan": back DESCANSO 27/10–28/10 (2) (H15); DEV.02 ELIMINADA; una sola versión nueva (4) | **Sí (D)** | OK |
| V15 | D | Dos pestañas con el diálogo y el impacto listos; registrar en una y luego en la otra | 409 "El proyecto cambió; vuelve a cargarlo." y "Recargar datos del proyecto", que reinicia el formulario con los datos nuevos | **Sí (D)** | OK (ver nota) |
| V16 | Cualquiera | Opcional: con el diálogo abierto, el usuario detiene la API y pulsa "Ver impacto" | Error de red con "Reintentar" | No | OK |
| V17 | Id 11 | Cambiar un campo y pulsar Cancelar (repetir con Esc y con el botón Atrás del navegador) | "Hay cambios sin registrar. ¿Cerrar de todos modos?": "Seguir editando" conserva los datos; "Cerrar sin registrar" cierra (o navega, con Atrás). Sin cambios, Cancelar cierra sin preguntar | No | OK |

V17 se agrega por el requisito nuevo de confirmación de cierre.

**Proyecto D:** Id 12, creado en V11 (versión 1). V12–V15 registraron las versiones 2 a 5.

**Nota V15.** La pestaña B respondió **400 "No hay cambios para registrar."**, no 409: pidió el mismo almuerzo que A ya había guardado. Es el resultado esperado para ese caso (C9). Pero confirma que el cliente no envía un token de concurrencia. El servidor recalcula sobre los datos actuales (relectura y `RowVer` dentro de la misma petición), así que con un cambio distinto B se habría aplicado sobre los datos de A sin 409, aunque su vista previa era de la versión anterior. Queda como **pendiente 32** de `00_ESTADO_ACTUAL.md` (antes de la TAREA-19b).

## 7. Contradicciones y observaciones
1. **Datos del ERP del diálogo.** El GET de cabecera no trae la compañía ni el proyecto ERP; se toman del detalle (`proyecto.compania.id`, `proyecto.erp.proyectoErpId`). La actividad solo se puede cambiar si `permisos.actividadEditable` y además hay `proyectoErpId`.
2. **Bloque de actividad.** Se envía si hay actividad **o** "desde"; si falta uno, el servidor responde 400 en ese campo. El plan decía "solo si se eligió una actividad"; así el usuario ve el mensaje del servidor en lugar de perder el "desde" sin aviso.
3. **Fecha de inicio sin máximo** en el selector, para poder moverla después del fin actual y luego ampliar el fin. La regla fin ≥ inicio se avisa en el campo fin (ayuda del cliente con el texto del servidor).
4. **P6 — resuelto por el usuario:** sin principales se muestra una ayuda neutra; el error rojo solo con un 400 del servidor en `principales`. Decisiones 1–5 de la entrega aprobadas.
5. **Etiqueta de la etapa en el impacto.** El nombre sale del catálogo `tiposMovimiento`; si EDICION_CABECERA o CAMBIO_ACTIVIDAD faltaran en él, se muestra el código.

## 8. Pendientes
- **Pendiente 32** (antes de la 19b): token de concurrencia desde el cliente. La vista previa devuelve la versión del proyecto (o RowVer) y el registro la envía; si no coincide → 409 "El proyecto cambió; vuelve a cargarlo.". Alcance: cabecera, personal, cambio de estado y reactivación (backend + frontend de las TAREA-15/19a).
- TAREA-19b: actualización de personal, pendiente 25 del lado del personal y unión de tramos contiguos (pendiente 26).
- TAREA-19c: reactivación (pendiente 26).
