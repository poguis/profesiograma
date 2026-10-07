# TAREA-19c — Frontend: reactivación (SUSPENDIDO → ACTIVO)

**Fecha:** 2026-10-07 (Fase A y Fase B)
**Resultado:** ✅ completada. Verificación visual del usuario del 07/10/2026: **V1–V16 OK**. V17 no se realizó porque no hay empleados libres; queda cubierta por pruebas (sección 6).
- `npm run build` (tsc -b + vite build): sin errores ni advertencias.
- `npm run lint` (oxlint): sin hallazgos.
- `npm test`: **205/205**. Son las 177 anteriores (1 ajustada y aprobada, P10) más 28 nuevas: 26 en `reactivacion.test.ts` y 2 en `utils/formato.test.ts`.
- `grep "X-Dev-User"` fuera de `src/auth/`: 0 resultados.

**Lo que no se hizo:** cambios en `backend/`, `npm run dev`, `dotnet run`, clientes HTTP contra la API, ni comandos git que modifiquen el repositorio.

## 1. Decisiones aprobadas (Fase A)
| # | Decisión |
|---|---|
| P1 (B2) | El diálogo "Cambiar estado" (TAREA-15) habilita "Reactivar" desde SUSPENDIDO. Al elegirlo oculta la fecha y "Ver impacto", muestra "La reactivación se completa en una pantalla propia (fecha, nueva fecha fin y personal)." y el botón "Continuar a reactivación", que navega a `/proyectos/:id/reactivar` (carga diferida; ruta antes de `proyectos/:id`) |
| P2 | R inicial = max(`fechaMinima`, hoy en Ecuador), con `hoyEnNegocio()` (`Intl.DateTimeFormat`, `America/Guayaquil`) |
| P3 | Fecha fin nueva inicial = `fechaFin` de la etapa anterior a la última SUSPENSION del detalle, si es ≥ R; si no, vacía y obligatoria. Si el detalle no carga, vacía, sin bloquear |
| P4 | Arrastre de fechas de las personas nuevas; primer principal nuevo con el inicio bloqueado en R ("Empieza en la fecha de reactivación."), también tras ↑↓ |
| P5 | Propuesto con empleado inactivo: precargado con aviso en la fila; "Generar vista previa" bloqueado hasta cambiar el empleado o quitar la fila |
| P6 | R6 solo con backs: ayuda que no bloquea; el servidor responde 400 `backs` (sección Backs) |
| P7 | Éxito → detalle con "Proyecto reactivado (versión n)." en Historial (`replace`); se invalida `todos` |
| P8 | Sin casilla; "Esta acción no se puede deshacer." junto a "Registrar"; protección contra doble clic |
| P9 | `CamposPersona`, `ListaPersonalEdicion` y `VistaPreviaPersonal` generalizados con props opcionales |
| P10 | Ajuste de `cambioEstado.test.ts:65-71` (sección 4.1) |
| P12 | Corrección de comentarios obsoletos en `tipos.ts` |
| Token base | `versionBase` = `versionProyecto` del GET …/reactivacion, fijado al montar y renovado solo con "Recargar datos del proyecto" (con confirmación si hay cambios sin registrar) |

## 2. Qué se hizo
**Lógica pura `reactivacion.ts`.** Se monta sobre `EstadoEdicionPersonal` y `reducerEdicionPersonal` de la 19b.
- **Estado:**
  - R, fecha fin nueva, revisión propia de fechas y token base;
  - Id de los principales históricos iniciales;
  - `claveInactivo` (P5);
  - `personal`, con todo lo guardado como histórico. Corte = R; el fin del proyecto es la fecha fin nueva.
- **Estado inicial:**
  - `crearEstadoReactivacion(dto, hoy, finPlanificado)` adapta `PersonaReactivacion` a histórica y precarga el propuesto (R7: empleado, jornada, cargo) como primer principal nuevo, sin marcar edición;
  - `fechaReactivacionInicial`, `finAntesDeSuspension` y `fechaFinInicial` (P2/P3).
- **Reducer `reducerReactivacion`:**
  - acciones `fecha` y `fechaFin`, con arrastre (P4);
  - acción `personal`: delega en la 19b y luego normaliza. Las nuevas agregadas van de R a la fecha fin nueva (o vacía), se ignora el cambio manual del inicio del primer principal, el primer principal queda anclado en R (también tras ↑↓) y `claveInactivo` se limpia al cambiar el empleado o quitar la fila.
- **Consultas:**
  - `claveInicialReactivacion`: el primer principal nuevo (R6), o null con solo backs;
  - `ayudaReactivacion` / `puedeGenerarVistaPreviaReactivacion`: fechas, mínimo sobre las nuevas según `exigePrincipal` (`minimoPersonal.ts`) y propuesto inactivo;
  - `ayudaBacksEnR` (P6);
  - `validarFechasReactivacion` (R2);
  - `validarPersonalReactivacion`: obligatorios, inicio ≥ R, RN08 con la fecha fin nueva, fin ≥ inicio y jornada. Usa los textos del servidor.
- **Solicitud:**
  - `aSolicitudReactivar` (R, fecha fin y `aSolicitudPersonal` de la 19b: solo las nuevas, sin `id`, relación por `principalClave` o `principalId` histórico);
  - `aSolicitudRegistroReactivacion` (`conToken` con la base);
  - `clavesDelEnvioReactivacion`.
- **Registro:**
  - `vistaReactivacionVigente` (revisión de fechas + revisión de personal);
  - `puedeRegistrarReactivacion` / `motivoSinRegistroReactivacion` (sin casilla: vigente, sin cruces, sin `cambioPorOtro`, sin envío en curso);
  - `mensajeExitoReactivacion`.
- **Errores:** `interpretarErrorReactivacion` separa `fecha` y `fechaFin` hacia sus campos, porque `distribuirErrores` mandaría `fechaFin` a la cabecera de la creación y `fecha` a los generales. El resto lo procesa `interpretarErrorPersonal` (19b).

**Pantalla `ReactivarProyecto.tsx`.** Carga la reactivación, los catálogos y el detalle (este solo para P3; si falla, no bloquea).
- Con `puedeReactivar` false: "No se puede reactivar el proyecto." y el `motivo`.
- Formulario:
  - advertencias del GET (R7);
  - fechas R (mínimo `fechaMinima`) y fecha fin nueva (mínimo R);
  - `ListaPersonalEdicion` con las props nuevas: marca de inicial, inicio fijo, límites R y fecha fin nueva, marca "Inicial" en los históricos, aviso P5, ayuda R6 y "Sin principales/backs nuevos.";
  - vista previa `VistaPreviaReactivacion` (fecha fin actual → nueva, actividad que se creará o "Sin actividad", y debajo `VistaPreviaPersonal` con su propio texto de corte);
  - aviso de cambio por otro y "Recargar" con confirmación;
  - barra fija con "Generar vista previa", "Registrar", "Esta acción no se puede deshacer." y el motivo;
  - buscador (agregar o cambiar empleado);
  - foco en el primer error tras un 400;
  - "Reintentar" (503/red) y enlace al listado (404);
  - confirmación al salir (`useBlocker` + `beforeunload`), desactivada tras registrar.

**Otros archivos.**
- `cambioEstado.ts`: Reactivar habilitado y `esReactivacion`.
- `DialogoCambioEstado.tsx`: selección "Reactivar" fuera del reducer y prop `onReactivar`.
- `DetalleProyecto.tsx`: navegación con `{ busqueda }`.
- `App.tsx`: ruta nueva.
- `api.ts` / `hooks.ts`: GET, vista previa y registro; clave `['proyectos','reactivacion',id]`; el registro invalida `todos`.
- `utils/formato.ts`: `hoyEnNegocio()`.

## 3. Archivos
| Archivo | Cambio |
|---|---|
| `frontend/src/features/proyectos/reactivacion.ts` (nuevo) | Lógica pura (sección 2) |
| `frontend/src/features/proyectos/reactivacion.test.ts` (nuevo) | 26 pruebas |
| `frontend/src/features/proyectos/pages/ReactivarProyecto.tsx` (nuevo) | Pantalla |
| `frontend/src/features/proyectos/components/VistaPreviaReactivacion.tsx` (nuevo) | Vista previa |
| `frontend/src/utils/formato.test.ts` (nuevo; no estaba en la lista F del plan, lo pidió P2) | 2 pruebas de `hoyEnNegocio` |
| `frontend/src/utils/formato.ts` | `hoyEnNegocio()` |
| `frontend/src/app/App.tsx` | Ruta `proyectos/:id/reactivar` antes de `proyectos/:id` |
| `frontend/src/features/proyectos/api.ts`, `hooks.ts` | API, clave y hooks de la reactivación |
| `frontend/src/features/proyectos/cambioEstado.ts`, `cambioEstado.test.ts` | Reactivar habilitado; `esReactivacion`; prueba ajustada (P10) |
| `frontend/src/features/proyectos/components/DialogoCambioEstado.tsx` | Opción "Reactivar" → "Continuar a reactivación" (P1) |
| `frontend/src/features/proyectos/pages/DetalleProyecto.tsx` | `onReactivar` |
| `frontend/src/features/proyectos/components/CamposPersona.tsx` | Props opcionales `fechaMinimaInicio`, `motivoInicioBloqueado` (por defecto, el texto de siempre) |
| `frontend/src/features/proyectos/components/ListaPersonalEdicion.tsx` | Props opcionales `claveInicial`, `claveInicioFijo`, `motivoInicioFijo`, `fechaMinimaInicio`, `fechaMinimaFinNuevas`, `fechaMaxima`, `inicialesHistoricas`, `avisosFila`, `ayudasSeccion`, `textoSinFilas`. Sin ellas, la salida es la de la 19b |
| `frontend/src/features/proyectos/components/VistaPreviaPersonal.tsx` | Prop opcional `textoCorte` |
| `frontend/src/features/proyectos/tipos.ts` | Solo comentarios (P12) |
| `docs/fases/FASE_5_Estados_Proyecto.md` | Encabezado, "Estado" de §8, §8.1, tabla de mensajes de §8.2 y §8.7 nueva |
| `docs/00_ESTADO_ACTUAL.md` | Fecha, §6 (19c), §7 (26 y nuevo 37), §9 (fila 19c) |
| `docs/tareas/TAREA-19c-reporte.md` (nuevo) | Este reporte |

## 4. Pruebas
### 4.1 Prueba existente ajustada (aprobada, P10)
| Archivo | Antes | Ahora |
|---|---|---|
| `cambioEstado.test.ts` (líneas 65-71) | "SUSPENDIDO: Terminar y Reactivar deshabilitado con ayuda": `['ACTIVO', false]` y `ayuda: 'Disponible próximamente'` | "SUSPENDIDO: Terminar y Reactivar habilitado, sin ayuda (TAREA-19c, P10)": `['ACTIVO', true]`, `etiqueta: 'Reactivar'` y `ayuda` undefined |

Ninguna otra prueba existente cambió. Las generalizaciones no tienen pruebas de componente; se revisó que, sin las props nuevas, los valores que reciben `CamposPrincipal`/`CamposBack` son los mismos de antes:
- `fechaMaxima` = fin del proyecto;
- `fechaMinimaFin` = `permisos.fechaFinMinima`;
- inicio bloqueado = "ya empezó", con el mismo texto;
- avisos y campos visibles iguales.

### 4.2 Pruebas nuevas (28)
- **`utils/formato.test.ts` (2):** `hoyEnNegocio` usa la fecha de Ecuador (22:00 del 07/10 en Ecuador = 03:00Z del 08/10 → 07/10) y el formato `yyyy-MM-dd`.
- **`reactivacion.test.ts` (26):**

| Grupo | Casos |
|---|---|
| Valores iniciales (3) | R = max(fechaMinima, hoy); fin antes de la última suspensión (null sin suspensión o sin etapa anterior); fin planificado solo si es ≥ R |
| Estado inicial (3) | Históricas + propuesto precargado sin edición; sin propuesto y sin fin; P5 (aviso, bloqueo, se resuelve al cambiar el empleado o al quitar la fila) |
| Fechas (5) | Arrastre de R; arrastre del fin (y del fin vacío); nuevas de R a la fecha fin nueva (o vacía); inicio del primer principal fijo y ↑↓; invalidación por revisión |
| Reglas (5) | Mínimo solo con las nuevas (0 y 1); fechas obligatorias; R6 solo backs sin bloquear; R2; por fila (inicio < R, RN08 con la fecha fin nueva, fin < inicio, obligatorios, jornada) |
| Solicitud (3) | Cuerpo (sin id; `principalClave` y `principalId` histórico; cargo; DESCANSO; sin token); opciones de relación; token base |
| Registro (3) | `puedeRegistrar` y motivo; cambio por otro (19b2); mensaje de éxito |
| Errores (3) | 400 `fecha`/`fechaFin`/fila/sección/`personal`; 400 `proyecto` con recarga; 409 con y sin cruces, 503, 404 |
| Diálogo (1) | `esReactivacion` |

### 4.3 Cobertura de la sección E del plan (Fase A)
Las 26 pruebas de `reactivacion.test.ts` cubren todos los grupos previstos en la sección E:

| Grupo de la sección E | Pruebas |
|---|---|
| Estado inicial | "valores iniciales" (3) y "estado inicial" (3): históricas con la marca de inicial, precarga del propuesto, propuesto null, propuesto inactivo, R por defecto, fin con y sin fin planificado, `versionBase` |
| Fechas | "fechas (P4)" (5): arrastre de R y del fin, inicio fijo del primer principal, ↑↓, invalidación por revisión |
| Reglas | "reglas de ayuda" (5): mínimo solo con nuevas (0 y 1), históricas que no cuentan, R6 solo backs, inicio < R, RN08 con la fecha fin nueva, fecha fin < R, jornada; inicial null con solo backs |
| Cuerpo | "solicitud" (3): sin `id`, `principalClave` / `principalId` histórico, cargo, DESCANSO, token base |
| Registro | "vista previa y registro" (2 de 3): `puedeRegistrar` y motivo, cambio por otro |
| Errores | "interpretarErrorReactivacion" (3) |
| Texto | "mensaje de éxito (P7)" (1) |

Además hay 1 prueba del diálogo (`esReactivacion`). **No falta ningún grupo.** El plan estimaba unas 32 pruebas; se agruparon en 26 sin dejar casos fuera.

## 5. Comandos
| Comando (desde `frontend/`) | Resultado |
|---|---|
| `npm run build`, `npm run lint`, `npm test` (línea base, Fase A) | OK; sin hallazgos; 177/177 |
| `npx vitest run …/reactivacion.test.ts …/formato.test.ts` | 27/27 (antes de la prueba de `esReactivacion`) |
| `npm run build` | Sin errores ni advertencias |
| `npm run lint` | Sin hallazgos |
| `npm test` (final) | **205/205** (9 archivos) |
| `grep -rn "X-Dev-User" frontend/src` fuera de `src/auth/` | 0 |
| `git status`, `git diff --stat` (lectura) | Solo los archivos de la sección 3 |

**Errores y cómo se resolvieron:**
- Un heredoc de Bash con comillas invertidas falló ("unexpected EOF"); los reemplazos se hicieron con scripts de Python en el scratchpad.
- Al envolver el bloque de la fecha en `DialogoCambioEstado.tsx`, la sangría quedó desalineada; se corrigió. No hubo errores de compilación, lint ni pruebas.

## 6. Verificación visual (usuario) — V1–V17
- Usuario `gestor`.
- **H = 07/10/2026.** Con otra fecha, las de V7–V14 cambian (se indica la regla).
- **Escrituras:** solo en los **Id 10 y 11**, más el proyecto nuevo de V17 (opcional). El Id 2 (semilla) solo se lee. Los Id 12 y 13 no se tocan.

**Datos de partida:**

| Proyecto | Estado y fechas | Personal | Fuente |
|---|---|---|---|
| Id 2 (PRY-DEV-0002) | SUSPENDIDO, 31/08–30/10/2026. Por eso `fechaMinima` = 31/10 [PENDIENTE DE CONFIRMAR en el detalle: la semilla usó hoy + 30 el 30/09] | — | Semilla |
| Id 10 | ACTIVO, 02/10–29/11, versión 3 | P1 DEV007 02–03/10 (inicial); P2 DEV007 04/10–29/11 (inicial); Back 1 DEV001 03/10 | 17b §6.2 y 19b P3 |
| Id 11 | ACTIVO, 11/10–16/10, versión 6 | P1 DEV003; Back 1 DEV001 | 18b |

| # | Proyecto | Pasos | Esperado | ¿Escribe? | Resultado |
|---|---|---|---|---|---|
| V1 | Id 12 (ACTIVO) | Detalle → "Cambiar estado" | Solo Suspender y Terminar; sin Reactivar | No | OK |
| V2 | Id 2 | Detalle → "Cambiar estado" → Reactivar | Reactivar habilitado; se ocultan la fecha y "Ver impacto"; texto "La reactivación se completa en una pantalla propia…"; "Continuar a reactivación" → `/proyectos/2/reactivar` | No | OK |
| V3 | Id 2 | Ver el formulario | R = 31/10/2026 (`fechaMinima`, mayor que hoy). Fecha fin nueva vacía si la etapa anterior a la suspensión termina el 30/10 (P3) [PENDIENTE DE CONFIRMAR]. Propuesto según R7 (o ninguno, con la advertencia del GET). Históricos de solo lectura con la marca "Inicial". Primer principal con "Empieza en la fecha de reactivación." y "Será el principal inicial (responsable)" | No | OK |
| V4 | — | Abrir `/proyectos/12/reactivar` y `/proyectos/999/reactivar` | Id 12: "No se puede reactivar el proyecto." + "Solo se puede reactivar un proyecto SUSPENDIDO (estado actual: ACTIVO)." Id 999: "Proyecto no encontrado o sin acceso" + enlace al listado | No | OK |
| V5 | Id 2 | Intentar R ≤ 30/10 (calendario y escrita) y una fecha fin < R | En el calendario, los días anteriores al 31/10 deshabilitados. Si se escribe: ayuda "La fecha de reactivación debe ser posterior a la fecha fin actual del proyecto (30/10/2026)." / "La fecha fin no puede ser anterior a la fecha de reactivación (…)."; si llega al servidor, 400 en el campo. Sin fecha fin: "Generar vista previa" deshabilitado con la ayuda | No | OK |
| V6 | Id 2 | Quitar el propuesto; agregar un back DEV003 desde el 31/10 con una fecha fin → vista previa | Cruces EXTERNOS de DEV003 (según g2 de la 19y; si ya no hay, anotarlo); "Registrar" deshabilitado ("No se puede registrar con cruces de asignación."). Salir sin registrar: "¿Salir sin registrar?" | No | OK |
| V7 | Id 10 | "Cambiar estado" → **Suspender** con F = 05/10/2026 (H−2) → Ver impacto → Confirmar | Advertencia "Se eliminarán días ya transcurridos."; queda SUSPENDIDO, versión 4 | **Sí (Id 10)** | OK |
| V8 | Id 10 | "Cambiar estado" → Reactivar → Continuar. Cambiar R al 06/10/2026 (= `fechaMinima`, pasado) → vista previa | R inicial 07/10 (hoy). Fecha fin nueva 29/11 (fin de la etapa 3, anterior a la suspensión). Propuesto DEV007 (TIPO_2) con inicio 07/10 que sigue a 06/10. Vista previa: "Se generarán días ya transcurridos."; fecha fin 05/10 → 29/11; actividad DEV.01 del 06/10 al 29/11; tramos unidos. **No registrar todavía** | No | OK |
| V9 | Id 10 | Agregar un back DEV005 desde R → vista previa; luego quitarlo | Cruce EXTERNO de DEV005 con el Id 5 (según 17b e/f; se verifica; si no hay, anotarlo) | No | OK |
| V10 | Id 10 | Con la vista previa lista, cambiar R al 07/10 o el fin de una persona | "Vista previa desactualizada."; "Registrar" deshabilitado; el inicio del primer principal sigue a R | No | OK |
| V11 | Id 10 | Tres pestañas en `/proyectos/10/reactivar` (A, B, C) con la vista previa de R = 06/10. **B** genera la vista previa. **A** → Registrar. Después, **B** → Registrar; **C** → Generar vista previa | A: detalle con "Proyecto reactivado (versión 5)." en Historial; en el listado, ACTIVO. B: 409 "El proyecto cambió; vuelve a cargarlo." + "Recargar datos del proyecto" (con confirmación si B tiene cambios); al recargar, "No se puede reactivar el proyecto." con el motivo. C: 400 "Solo se puede reactivar un proyecto SUSPENDIDO (estado actual: ACTIVO)." + "Recargar" | **Sí (Id 10, solo A)** | OK |
| V12 | Id 11 | "Cambiar estado" → **Suspender** con F = 13/10/2026 | SUSPENDIDO, versión 7 (sin advertencia E6 si H < 13/10) | **Sí (Id 11)** | OK |
| V13 | Id 11 | Reactivar: R inicial 14/10 y fecha fin nueva 16/10 (P3). Quitar el propuesto (DEV003); agregar un back DEV001 desde el 15/10 → vista previa | Ayuda en Backs "Al menos un back debe empezar en la fecha de reactivación (14/10/2026)." sin bloquear; el servidor responde 400 `backs` en la sección Backs. Si DEV001 tuviera cruces, probar DEV003 | No | OK |
| V14 | Id 11 | El back desde el 14/10 → vista previa → "Registrar" con doble clic rápido | Sin la advertencia "sin principal" (DEV003 sigue como inicial histórico); una sola versión nueva (8); "Proyecto reactivado (versión 8)." en Historial | **Sí (Id 11)** | OK |
| V15 | Id 2 | Cambiar R o agregar una persona; pulsar "Volver al proyecto" y luego Atrás del navegador | "¿Salir sin registrar?" | No | OK |
| V16 | Id 2 | Agregar principales hasta 20 | "Agregar principal" deshabilitado y "Se alcanzó el máximo de 20." (si el buscador ofrece empleados suficientes; si no, cubierto por pruebas) | No | OK |
| V17 (opcional) | Proyecto nuevo | "Nuevo proyecto" solo con un back sin cruces, del 20/10 al 30/10/2026 → Registrar. Suspender con F = 20/10. Reactivar solo con un back desde el 21/10 → vista previa → Registrar | Vista previa con "El proyecto no tendrá principal: el responsable quedará vacío."; tras registrar, "Sin responsable" en el listado | **Sí (proyecto nuevo)** | No realizada: sin empleados libres, cubierto por pruebas |

**Estado final de los datos de prueba (07/10/2026):**
- **Id 10:** ACTIVO, **versión 5** (v4 SUSPENSION al 05/10; v5 REACTIVACION con R 06/10 y fin 29/11).
- **Id 11:** ACTIVO, **versión 8** (v7 SUSPENSION al 13/10; v8 REACTIVACION con R 14/10 y fin 16/10, solo con un back nuevo). Sigue con responsable: DEV003, principal inicial histórico.
- Id 2, 12 y 13 sin cambios.

**Cubierto por pruebas (no se puede reproducir con los datos de desarrollo sin SQL o sin carreras):**
- **V17 / advertencia "El proyecto no tendrá principal…":** DEV001–DEV008 ya tienen cruces en las fechas disponibles (pendiente 39). Lo cubren `ayudaBacksEnR`, `claveInicialReactivacion` null con solo backs y la prueba del backend de la 19y.
- **Propuesto inactivo (P5):** requiere desactivar un empleado.
- **`exigePrincipal = 1`:** requiere SQL aprobado; la ayuda de principal y el 400 `principales` están cubiertos.
- **Aviso de cambio por otro:** mientras el proyecto está SUSPENDIDO, las únicas escrituras posibles desde la pantalla lo sacan de ese estado (sección 7, G6).
- **503 / red** con "Reintentar".
- **400 por índice:** los selectores limitan las fechas.

## 7. Contradicciones de la Fase A (G1–G6) y cómo quedaron
| Id | Contradicción | Estado |
|---|---|---|
| G1 | `FASE_5_Estados_Proyecto.md` §8 citaba el pendiente 29 como abierto, y el encabezado decía "Pendiente: frontend (TAREA-19)" | Corregido: se marca resuelto en la TAREA-18 (C11); frontend en la TAREA-19c (§8.7) |
| G2 | La tabla de mensajes de §8.2 no reflejaba la 19y (las filas R5/R6 sí) | Corregido: `personal`, `backs` (R6 solo backs), `principales` solo con `PROYECTO_EXIGE_PRINCIPAL = 1` y las advertencias de la vista previa. §8.1 menciona `versionProyecto` y `limites.exigePrincipal` |
| G3 | El Id 13 ya no tiene solo backs (P7 de la 19b le agregó un principal inicial) | Se tuvo en cuenta en el plan de verificación: el Id 13 no se usa; el caso "Sin responsable" va en V17 (opcional) |
| G4 | Comentarios obsoletos en `tipos.ts`: "se envía al registrar" en el token de las vistas previas; origen de `CruceAsignacion` | Corregido (P12): token base (19b2) en los comentarios de cambio de estado, cabecera, personal y reactivación; el origen incluye HISTORICO |
| G5 | Informes históricos (19x §9 "token de la vista previa"; 17b §9 `MinimoPrincipales = 0`) | Sin cambios: son históricos y los documentos vigentes ya los corrigen |
| G6 | El aviso `cambioPorOtro` no se puede provocar en la reactivación desde la pantalla | Implementado igual que en la 19b2 y cubierto por pruebas; en pantalla se ven el 400 `proyecto` o el 409 (V11) |

**Observaciones nuevas:**
1. **Validación de personal.** `validarEdicionPersonal` (19b) no se reutilizó tal cual, porque compara contra el fin del proyecto aunque la fecha fin nueva esté vacía. `validarPersonalReactivacion` usa los mismos textos y agrega inicio ≥ R.
2. **Datos que faltan en el GET.** `PersonaReactivacionDto` no trae `principalRelacionadoId`, `cargo` ni `observacion`. La tabla de históricos no muestra la relación de los backs. Nuevo pendiente 37 (menor, backend).
3. **El GET no devuelve la fecha de hoy.** Se calcula en el cliente con la zona de negocio (`hoyEnNegocio`). La advertencia R3 la da la vista previa del servidor.

## 8. Pendientes
- **Pendiente 38** (menor, frontend): unificar `validarEdicionPersonal` y `validarPersonalReactivacion`, de modo que acepte la fecha fin vacía.
- **Pendiente 39:** los datos de prueba tienen solo 8 empleados (DEV001–DEV008) y ya no alcanzan para pruebas sin cruces (V17).
- **Pendiente 37** (menor, backend): agregar `principalRelacionadoId`, `cargo` y `observacion` a `PersonaReactivacionDto`.
- **Pendiente 36** (menor, backend): `esPrincipalInicial` en el GET `…/edicion`. Sin cambios en esta tarea.
