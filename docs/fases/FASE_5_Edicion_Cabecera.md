# FASE 5 — Diseño: Edición de cabecera y cambio de actividad

**Fecha:** 2026-10-06 (TAREA-18, backend). **Pendiente:** prueba manual del usuario; frontend en la TAREA-19.

**Origen** (`docs/origen/powerapps/ConfigurarProyecto_1.pa.yaml`):
- controles: `cmbActividadEdit_1` (l. 269–297), `fechaActividadDesdeEdit_1` (l. 313–326), `inicioProyectoEdit_1` (l. 363–375), `finProyectoEdit_1` (l. 395–408);
- Registrar: validaciones (l. 2214–2258), historial de actividades (l. 2296–2334), etapa (l. 2590–2616 y 2655–2676), reemplazo de `SIG_HISTORIAL_ACTIVIDADES` (l. 2679–2715), `INF_GENERAL` (l. 2718–2790), Patch a `SIG PROYECTOS` (l. 2875–2888);
- Regenerar (cadena de la l. 1910): RN08 R‑834–935.

## 1. API (política Gestor, visibilidad R1 → 404)

| Endpoint | Respuesta |
|---|---|
| `GET /api/proyectos/{id:int}/cabecera` | 200 `CabeceraDto`:<br>• valores actuales: fechas, horario, almuerzo, grupo, actividades y actividad vigente en `max(hoy, inicio)` (P3);<br>• `puedeEditar` + `motivo` (solo ACTIVO);<br>• `permisos`: `fechaInicioEditable` + `motivoFechaInicio`, `fechaFinMinima` = hoy, `actividadEditable`;<br>• opciones de almuerzo (`ReglasAlmuerzo`);<br>• `corte` = hoy en Ecuador |
| `POST …/cabecera/previsualizar` | 200 `PrevisualizacionCabeceraDto`: `cambios`, impacto del recorte (`diasEliminados`, `personalEliminado`, `personalRecortado`, con los DTO de la vista previa de cambio-estado), `actividades` resultantes (SIN_CAMBIO / MODIFICADA / ELIMINADA / NUEVA), `tipoEtapa` y `advertencias`. 400 / 404 / 409. No guarda |
| `POST …/cabecera` | 200 `{ id, version }`. 400; 404; 409 "El proyecto cambió; vuelve a cargarlo."; 503 (applock o ERP no disponible) |

Cuerpo: `{ fechaInicio?, fechaFin?, horarioCodigo?, salidaAlmuerzo?, regresoAlmuerzo?, actividad?: { actividadId, desde } }`. Un campo ausente (null) no cambia; un valor igual al guardado tampoco cuenta como cambio.
Título del `ValidationProblem`: "Los datos de la cabecera no son válidos.".

## 2. Reglas

| Id | Regla |
|---|---|
| C1 | Solo proyectos ACTIVO. Otro estado: GET con `puedeEditar` false y `motivo`; POST 400 `proyecto` "Solo se puede editar la cabecera de un proyecto ACTIVO (estado actual: X)." |
| C2 | Fecha de inicio: editable solo si el inicio actual es posterior a hoy; si no, 400 `fechaInicio` "La fecha de inicio ya no se puede cambiar: el proyecto empezó el dd/MM/yyyy.". El nuevo inicio debe ser ≥ hoy ("La nueva fecha de inicio no puede ser anterior a hoy (dd/MM/yyyy).") y ≤ fin. Si alguna persona empieza antes del nuevo inicio → "Hay personal que empieza antes de la nueva fecha de inicio; ajusta primero el personal.". Mover el inicio no cambia personal ni días |
| P1 | Al mover el inicio (atrasarlo o adelantarlo), las actividades con `FechaInicio` = inicio anterior pasan al nuevo inicio. Si alguna actividad termina antes del nuevo inicio → 400 `fechaInicio` "Hay actividades que terminan antes de la nueva fecha de inicio." (`MovimientoInicioProyecto`, Domain) |
| C3 | Ampliar la fecha fin: el personal no cambia; las actividades que terminaban en el fin anterior se extienden al nuevo (H14; `AmpliacionProyecto`, Domain) |
| C4 | Acortar la fecha fin: `RecorteProyecto` (TAREA-14) con F = nueva fin, sin cambio de estado: días > F, personal que empieza después (eliminado) o termina después (recortado), actividades, backs sin principal (advertencia). Si el recorte elimina a **todos** los principales con `EsPrincipalInicial`: advertencia "El proyecto quedará sin principal inicial." (no bloquea). Escritura con el orden de la TAREA-14 (`EscrituraRecorte`) |
| P2 | La nueva fecha fin debe ser ≥ hoy (al acortar y al ampliar) y ≥ el inicio resultante: "La fecha fin no puede ser anterior a hoy (dd/MM/yyyy)." / "La fecha fin debe ser mayor o igual a la fecha de inicio." |
| C5 | Horario: código del catálogo ERP (solo status "A"), con los mismos campos que guarda la creación. Almuerzo: `ReglasAlmuerzo` con los mismos mensajes que la creación (`ReglasHorarioAlmuerzo`, extraído de `CrearProyectoValidador`). Solo se validan los rangos de las horas enviadas; "regreso > salida" se valida con los valores resultantes. No afectan al cronograma |
| C6 | Cambio de actividad: solo grupos con `RequiereProyectoErp` ("El grupo X no usa actividad."); actividad del catálogo ERP del proyecto ERP del proyecto ("La actividad no existe en el proyecto ERP."); `desde` dentro de [inicio, fin resultantes]. Como el original: las actividades que empiezan antes de `desde` y llegan a él se recortan a `desde − 1`; se **eliminan** las que empiezan en `desde` o después; la nueva va de `desde` al fin, versión máx + 1 sobre todas las existentes, TipoMovimiento CAMBIO_ACTIVIDAD (`CambioActividad`, Domain). La misma actividad que la vigente en `desde` → 400 `actividad.actividadId` "La actividad X ya está vigente el dd/MM/yyyy.". `desde` < hoy: advertencia "La nueva actividad aplica desde una fecha ya transcurrida.". Combinado con la fecha fin: primero la fecha fin y después la actividad |
| C7 | Una etapa por registro: solo cambia la actividad → CAMBIO_ACTIVIDAD; cualquier otro cambio → EDICION_CABECERA. Estado sin cambios; `FechaInicio`/`FechaFin` = las resultantes; `FechaCorte` = hoy; snapshot del personal resultante (`SnapshotPersonal`) |
| P3 | Actividad de la etapa: la vigente en `max(hoy, inicio resultante)` (la de mayor versión, contando la nueva) |
| C8 | Una transacción con applock y relectura: si cambiaron el estado, las fechas o el `RowVer` del proyecto, o lo que era válido fuera ya no lo es → 409. **Sin motor ni cruces**: ningún cambio agrega días (ampliar no regenera, acortar solo borra, el inicio no toca días, la actividad no tiene días) |
| C9 | Sin cambios: registro → 400 `general` "No hay cambios para registrar."; vista previa → 200 con la advertencia "No hay cambios.". También en `POST …/personal` (TAREA-17) |
| C10 | Creación: al menos 1 principal (`CrearProyectoValidador.MinimoPrincipales = 1`; "Se requiere al menos 1 principal(es)."). Pendiente 28 |
| C11 | `CambioEstadoValidador` no valida `fecha` si el movimiento es SIN_CAMBIO, NO_PERMITIDO, REACTIVACION o el estado es desconocido. Pendiente 29 |

**Orden de aplicación:** inicio (C2, P1) → fecha fin (C3 o C4) → actividad (C6) sobre el resultado.

## 3. Escritura (`EdicionCabeceraRepositorio`, dentro del applock)
1. Si se acorta: `EscrituraRecorte` → `ExecuteDelete` de los días > F (y del personal eliminado), referencias a principales eliminados en null y recorte de `FechaFin` del personal → **SaveChanges 1**.
2. Personal eliminado; actividades eliminadas, con fechas nuevas (P1, C3, C4, C6) y la nueva CAMBIO_ACTIVIDAD; proyecto (`FechaInicio`, `FechaFin`, horario, almuerzo; `RowVer` por seguimiento); etapa con versión máx + 1 → **SaveChanges 2**.
3. `DbUpdateConcurrencyException` o duplicado (`UQ_ProyectoEtapa_Version`, `UQ_ProyectoActividad_Version`) → 409.

## 4. Hallazgos del origen

| Id | Hallazgo | Evidencia |
|---|---|---|
| H13 | El Patch a `SIG PROYECTOS` no guarda `FECHA_INICIO`. **Corrección:** el nuevo inicio tampoco llega a `INF_GENERAL`: `Planificacion.FechaInicio` (y `ASIGNACIONES.Configuracion.FechaInicio`) usan `gblFechaInicioProyecto`, el valor cargado al abrir, que Registrar no actualiza. Solo llega a la etapa | l. 2875–2888; l. 2757 y 2793; ResumenProyectos l. 656 y 1051; etapa l. 2608 y 2664 |
| H14 | Al ampliar la fecha fin, la actividad vigente queda terminando en la fecha anterior: `SIG_HISTORIAL_ACTIVIDADES` solo se reescribe si cambió la actividad | l. 2883 y 2681 |
| — | Cambio de actividad: se conservan las que empiezan antes de `desde`; la última se recorta a `desde − 1`; la nueva va de `desde` al fin con `CountRows + 1` y CAMBIO_ACTIVIDAD; la tabla se reemplaza completa (Id nuevos) | l. 2302–2329, 2681–2715 |
| — | **No exige Regenerar:** cambiar la actividad no activa `varRequiereRegenerar`. Sin Regenerar se guardan actividades y cabecera **sin etapa** (la etapa solo se escribe con `varAsignacionRegenerada`); con Regenerar, la etapa queda como ACTUALIZACION_PERSONAL | l. 286, 2599, 2590–2600 |
| — | Actividad solo visible en el grupo CAMPO | l. 266 y 295 |
| — | Horario y almuerzo no se editan: `INF_GENERAL` se reescribe con los valores cargados | l. 2744–2753 |
| — | **Al acortar no recorta el personal:** RN08 solo valida a los vigentes contra el rango y Regenerar falla con "Las fechas de los principales deben estar dentro del rango del proyecto"; el usuario ajusta a mano | R‑885–935 |

## 5. Diferencias con el original

| Id | Original | Esta implementación |
|---|---|---|
| D14 | El inicio nuevo solo llega a la etapa (H13) | Se guarda en `Proyecto.FechaInicio` y en la etapa |
| D15 | Las actividades no se mueven con el inicio | P1: las que empezaban en el inicio anterior pasan al nuevo; 400 si alguna termina antes |
| D16 | Ampliar no extiende la actividad (H14) | C3: se extiende la que terminaba en el fin anterior |
| D17 | Acortar no recorta el personal (falla RN08) | C4: recorte automático con `RecorteProyecto` y vista previa del impacto |
| D18 | Cambio de actividad sin etapa si no se regenera, o etapa ACTUALIZACION_PERSONAL | C7: siempre una etapa, CAMBIO_ACTIVIDAD o EDICION_CABECERA |
| D19 | Reemplaza toda la tabla de actividades (Id nuevos), versión `CountRows + 1` | Cambios en el lugar; versión máx + 1 dentro del applock |
| D20 | Recorta solo la última actividad anterior a `desde` | Recorta todas las que llegan a `desde` (sin solapes es lo mismo) |
| D21 | Horario y almuerzo no editables | C5: editables con las reglas de la creación |
| D22 | Sin control de cambios simultáneos | C8: relectura en el applock y `RowVer` → 409 |

## 6. Ubicación por capa

| Capa | Archivos |
|---|---|
| Domain | `Proyectos/Cabecera/ActividadesCabecera.cs` (`MovimientoInicioProyecto`, `AmpliacionProyecto`, `CambioActividad`) |
| Application | `Proyectos/Cabecera/` (`EdicionCabeceraContratos`, `EdicionCabeceraDtos`, `EdicionCabeceraValidador`, `EdicionCabeceraServicio`); `Proyectos/Crear/ReglasHorarioAlmuerzo.cs`; `Proyectos/Estados/VistaRecorte.cs` |
| Infrastructure | `Persistencia/Proyectos/EdicionCabeceraRepositorio.cs` (consultas `ConsultaCabeceraEdicion` y `ConsultaActividadesEdicion`), `Persistencia/Proyectos/EscrituraRecorte.cs` |
| Api | `Endpoints/ProyectoEndpoints.cs` (GET + dos POST) |
