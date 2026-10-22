# FASE 5 — Novedades (TAREA-23)

Novedades de asistencia: aplicaciones PERSONA, PROYECTO y GENERAL, origen PROFESIOGRAMA (editable) o PERMISOS_MEDICOS (sincronizado, solo lectura). El modelo (`Novedad`, `NovedadDia`, catálogos y semillas) existe desde la TAREA-03: **sin migraciones**.

| Subtarea | Alcance | Estado |
|---|---|---|
| 23-1 | Lectura, opciones y **creación** | ✅ Verificada por el usuario (21/10/2026, 22/22 en la segunda corrida) |
| 23-2 | Edición y anulación (`RowVer`, N3, N8–N10; §7) | ✅ Verificada por el usuario (21/10/2026: 984/984, `edicion.cmd` 0 FALLA y 0 OMITIDOS) |
| 23-3 | Sincronización de PERMISO MEDICO: puerto, mapeador, plan, fuentes Simulada y ArchivoLocal, endpoint Admin (§8) | Implementada (22/10/2026), pendiente de la verificación del usuario |
| 23-4 | Fuente real (Graph / SharePoint) | Bloqueada por la App Registration (pendiente 7) y el permiso de Graph (pendiente 63) |
| TAREA-24 | Frontend | Pendiente |

## 1. Contratos (23-1, política Gestor)
| Ruta | Request → Response | Errores |
|---|---|---|
| `GET /api/novedades` | `aplicacion, tipo, origen, texto, desde, hasta` (solapamiento), `incluirAnuladas` (false), `pagina`, `tamano` (≤ 100) → `PaginaResultado<NovedadResumenDto>` | 400 en el campo |
| `GET /api/novedades/opciones-formulario` | → `{ puedeRegistrarGeneral, tiposPorAplicacion: { PERSONA, PROYECTO, GENERAL }, maxDias: 366 }` | — |
| `GET /api/novedades/{id:int}` | → `NovedadDetalleDto` | 404 (no existe o no es visible) |
| `POST /api/novedades` | `{ tipoAplicacion, tipoNovedad, codigoEkon?, proyectoId?, fechaInicio, fechaFin, observacion? }` → 201 + Location `{ id, codigo, version }` | 400 por campo, 403, 409, 503 |

- `NovedadResumenDto`: `id, codigo, aplicacion, tipo { codigo, nombre, sigla, color }, origen, sujeto, fechaInicio, fechaFin, totalDias, anulada, fechaCreacion`. `sujeto` = `{ empleado: { codigoEkon, nombre } }` (PERSONA) o `{ proyecto: { id, codigo, nombre } }` (PROYECTO); GENERAL: `null`.
- `NovedadDetalleDto`: lo anterior más `dias` (ordenados), `observacion`, `fechaAnulacion`, `motivoAnulacion`, `registro` (solo Admin), `version` (RowVer en Base64), `puedeEditar`, `puedeAnular`, `motivoNoEditable`.
- **Nunca** viajan `DetalleOrigenJson` ni la cédula. `texto` busca en el código de la novedad, el código y el nombre del empleado y el código y el nombre del proyecto.
- Errores: 403 «Solo los administradores pueden registrar novedades GENERAL.»; 409 «Ya existe una novedad de ese tipo para ese sujeto en esas fechas.» (extensión `codigo` solo si quien registra la ve, R6) o «Otro registro se guardó al mismo tiempo; vuelva a intentarlo.» (alta puntual concurrente); 503 de empleados, registro ocupado o base caída (manejadores comunes). Un cuerpo ilegible → 400 en su campo (`SolicitudIlegibleExceptionHandler`).

## 2. Validación (400 por campo)
| Campo | Regla y mensaje |
|---|---|
| `tipoAplicacion` | Falta: «El tipo de aplicación es obligatorio.»; otro valor: «El tipo de aplicación no es válido.»; GENERAL sin permiso → **403** antes de validar lo demás |
| `tipoNovedad` (código) | Falta: «El tipo de novedad es obligatorio.»; inexistente o inactivo: «El tipo de novedad no es válido.»; RN16 (`SeleccionableManual` y bandera de la aplicación): «El tipo de novedad no aplica a {APLICACIÓN}.» |
| `codigoEkon` | PERSONA: obligatorio («El empleado es obligatorio.»); lectura **fresca** de la API: «El empleado no existe o no está activo.»; API caída → 503 «Servicio de empleados no disponible». En otra aplicación: «No corresponde en una novedad {APLICACIÓN}.» |
| `proyectoId` | PROYECTO: obligatorio («El proyecto es obligatorio.»); visible (R1), no eliminado y `EsVigente`: «El proyecto no existe, no está vigente o no tienes acceso.». En otra aplicación: «No corresponde…» |
| `fechaInicio`, `fechaFin` | Obligatorias; fin ≥ inicio («La fecha fin debe ser mayor o igual a la fecha inicio.»); máximo 366 días («La novedad no puede superar 366 días.»); fechas pasadas permitidas |
| `observacion` | Opcional, recortada, ≤ 500 («La observación no puede superar 500 caracteres.») |

La API de empleados y la base se consultan **solo si el resto es válido**: un 400 no gasta una llamada. Sin filtro por familia de puesto ni por departamento (J6).

## 3. Escritura (crear)
- Fuera de la transacción: validación (lectura fresca de empleados, D15: cédulas solo ahí).
- **Dentro de `ITransaccionAsignaciones`** (desviación aprobada del plan, ver §5): duplicado (N7) → código (R7) → alta puntual de la persona (con cédula, D15) → `Novedad` + una `NovedadDia` por fecha → commit. Espera máx. 15 s → 503 «registro ocupado».
- Origen PROFESIOGRAMA; `OrigenArea` y `DetalleOrigenJson` = null; `RegistradoPorNombre/Correo` = usuario actual; auditoría por `AuditoriaInterceptor`.
- Si algo falla antes del commit no se guarda nada; después del commit no se vuelve a la base (regla 4 de §7.1). La `version` devuelta es el `RowVer` leído por EF al insertar.

## 4. Permisos (`PermisosNovedad`, único punto de decisión)
| Regla | Detalle |
|---|---|
| J1 `PuedeRegistrarGeneral` | Solo el rol Admin |
| J2 Visibilidad | Admin: todas. Gestor (`VisiblePara`, traducida a SQL): las que creó, las PROYECTO de sus proyectos visibles (propietario, no eliminado) y todas las GENERAL; **nunca** las PERMISOS_MEDICOS ni las PERSONA de otros. No visible → 404 |
| N3 `Modificacion` | Origen no editable → no («Las novedades de PERMISOS_MEDICOS son de solo lectura.»); anulada → no («La novedad está anulada.»); Admin: sí; PERSONA: el creador; PROYECTO: el propietario del proyecto; GENERAL: quien `PuedeRegistrarGeneral`; si no: «No tienes permiso para modificar esta novedad.». Se informa en el detalle (`puedeEditar`, `puedeAnular`, `motivoNoEditable`) |
| Registrante | `registro` solo para Admin |

## 5. Decisiones
| # | Decisión |
|---|---|
| N1 / J1 | GENERAL: solo Admin, decidido en `PermisosNovedad` (pasar a una bandera o a un departamento no toca endpoints) |
| N2 / J2 | Visibilidad del gestor: §4 |
| N3 | §4 |
| N4 | PERSONA: `CatalogoEmpleados` con lectura fresca; alta puntual con cédula (D15) en la misma transacción |
| N5 | Estado vigente solo al **crear**; las fechas no tienen que caer en el rango del proyecto (D1 ya filtra por día) |
| N6 | fin ≥ inicio; tope 366 días (constante `DiasNovedad.MaxDias`); fechas pasadas permitidas |
| N7 / J3 | 409 si hay otra novedad NO anulada con la misma aplicación, el mismo sujeto, el mismo tipo y al menos un día en común. Tipos distintos se permiten (D9 decide la celda) |
| N8 | (23-2) Aplicación y sujeto inmutables; editables tipo, fechas/días y observación |
| N9 | (23-2) `RowVer` como token `version`; una edición solo de días debe marcar `Novedad` como modificada |
| N10 / J4 | (23-2) Anulación con motivo obligatorio (5–500), irreversible; nunca a mano si el origen es PERMISOS_MEDICOS |
| N11 / R7 | `Uid = Guid.NewGuid()`; `Codigo = NOV-{yyyyMMdd de Ecuador}-{6 últimos hex del Uid, minúsculas}`; si chocan el código o el Uid, nuevo Uid (hasta 3 intentos) |
| N12 / J7 | (23-3) Puerto `IFuenteNovedadesExternas` con fuente simulada; Graph en la 23-4 |
| N13 | `DetalleOrigenJson` nunca en un DTO; en PROFESIOGRAMA, null. (23-3) Lista blanca para PERMISO MEDICO |
| N14 / J5 | Observación libre opcional (≤ 500) en PROFESIOGRAMA; null en PERMISO MEDICO |
| N15 | Cronograma y reporte sin cambios (leen `Anulada = 0` y `NovedadDia`). La TAREA-24 debe invalidar el cronograma (las dos vistas), el reporte (`reporteResumen`) y el listado de novedades |
| N16 | Sin migración |
| N17 | `opciones-formulario` mínimo; sin previsualizar (los días los calcula el frontend con la misma regla) |
| R1 | Visibilidad de proyectos (propietario; Admin todos) |
| R6 | El 409 de duplicado incluye el código existente solo si quien registra lo ve |
| J6 | Sin filtro por familia de puesto (pendiente 62) |
| **Desviación** | El plan ponía `ITransaccionAsignaciones` solo en PERSONA; se usa en **toda** creación para que N7 no tenga carrera (aprobada en la Fase B) |

## 6. Datos de desarrollo y pruebas
- Consultas con prueba `ToQueryString` (`NovedadSqlTests`): listado (gestor y admin), detalle, días, tipos, proyecto elegible, duplicado (PERSONA, PROYECTO, GENERAL), visibilidad del duplicado y código/Uid.
- Script manual: `backend/tests/manual/tarea23-1/` (`novedades.cmd`, 14 casos con `CONTROL … OK|FALLA`, `CORRIDA` para repetir sin 409).

## 7. Edición y anulación (TAREA-23-2)
### 7.1 Contratos (política Gestor)
| Ruta | Request → Response | Errores |
|---|---|---|
| `PUT /api/novedades/{id:int}` | `{ version, tipoNovedad (código), fechaInicio, fechaFin, diasExcluidos[], observacion? }` → 200 `{ id, version }` | 400 (`version`, `origen`, campos, `diasExcluidos`, `general`), 403, 404, 409 (anulada, cambió, duplicado), 503 |
| `POST /api/novedades/{id:int}/anular` | `{ version, motivo }` → 200 `{ id, version }` | 400 (`version`, `origen`, `motivo`), 403, 404, 409 (ya anulada, cambió), 503 |

`version` = `RowVer` en Base64 (8 bytes). La respuesta trae la versión **nueva**, leída por EF tras el UPDATE y antes del commit (A2); el servicio no vuelve a la base después del commit. Para ver los días, el cliente pide `GET /api/novedades/{id}`.

### 7.2 Orden de errores (H5)
`version` ausente o ilegible (400) → no existe o no visible (404) → origen no editable (400 `origen`) → sin permiso (403, N3 con `PermisosNovedad.PuedeModificar`) → anulada (409) → versión distinta (409) → campos (400) → dentro de `ITransaccionAsignaciones`: anulada y versión otra vez (409), duplicado (409, solo al editar) → escritura (otro cambio entre medio → 409).

### 7.3 Reglas y mensajes
| Tema | Regla |
|---|---|
| `version` | Falta: «Falta la versión de la novedad; vuelve a cargarla.»; ilegible o de otro largo: «La versión de la novedad no es válida; vuelve a cargarla.» (400, nunca 500); distinta: 409 «La novedad cambió; vuelve a cargarla.» |
| Anulada | Editar: 409 «La novedad está anulada.»; anular otra vez: 409 «La novedad ya está anulada.» |
| Origen | 400 `origen` «Las novedades de PERMISOS_MEDICOS son de solo lectura.» |
| N8 | Aplicación y sujeto inmutables. Editables: tipo (RN16 con la aplicación **guardada**), rango (obligatorio, fin ≥ inicio, ≤ 366 días; inicio = fin permitido), días excluidos y observación |
| `diasExcluidos` | Repetidos se ignoran; fuera del rango: 400 «Los días excluidos deben estar dentro del rango.» (P2); sin días: 400 «La novedad debe tener al menos 1 día.». FechaInicio/FechaFin guardadas = mínimo y máximo de los días resultantes |
| `observacion` | Recortada, ≤ 500; null o vacía la **borra** (P1: el PUT reemplaza) |
| Sin cambios | Mismo tipo, mismos días y misma observación: 400 `general` «No hay cambios para registrar.» (P3) |
| N7 en la edición | Duplicado con los días **resultantes** (con huecos) y excluyendo la propia novedad; 409 con el código solo si es visible (R6) |
| N10 | `motivo` 5–500, recortado: «El motivo debe tener entre 5 y 500 caracteres.». Irreversible. Guarda `FechaAnulacion` (UTC truncada a segundos), `AnuladaPorId` y `MotivoAnulacion`. También dentro de `ITransaccionAsignaciones` (P4) |
| Empleados | La edición no llama a la API de empleados (el sujeto es inmutable): sin 503 por el ERP |

### 7.4 Escritura: diff de días y R1/R2/A5
- **Diff de días** (una sola lectura **con seguimiento** de la novedad y sus días dentro de la transacción): se quitan (`Remove`) los días que salen y se agregan los que entran; los que quedan no se tocan (conservan su auditoría). Nunca se borra y se vuelve a insertar la misma fecha, así que `UQ_NovedadDia` no choca; la cascada de `FK_NovedadDia_Novedad` no interviene.
- **R1:** en el **mismo** `SaveChanges` que los días, `Property(RowVer).OriginalValue = token del cliente` (`WHERE RowVer = token`: si otro la cambió, 0 filas → `DbUpdateConcurrencyException` → 409) y `Property(FechaModificacion).IsModified = true`. Sin esto, una edición que solo cambia días (o dos en el mismo segundo, porque `AuditoriaInterceptor` trunca a segundos) no emitiría UPDATE de `Novedad` y su `rowversion` no cambiaría.
- **R2/A5:** que el `rowversion` cambie solo se puede probar contra SQL Server: caso e04/e05 de `tarea23-2/edicion.cmd` (dos pestañas que solo cambian días: la segunda recibe 409; sin R1 daría 200). Las pruebas con dobles cubren el flujo 400/409, y una prueba sin base verifica el estado del `ChangeTracker` (FechaModificacion marcada, OriginalValue de RowVer, días Deleted/Added).
- **A3:** EF 10 traduce `dias.Contains(d.Fecha)` a `IN (@dias1, …)` con un parámetro por valor (relleno hasta 400 con 366 días), lejos del límite de 2100 de SQL Server: no hizo falta el plan B.

### 7.5 Nota para la TAREA-24 (A4)
- Al acortar el rango, quitar de `diasExcluidos` los días que queden fuera (P2 responde 400).
- Tras editar o anular, invalidar: el detalle y el listado de novedades, el cronograma general y el del proyecto (las dos vistas) y `reporteResumen`.
- Enviar siempre la `version` con que se abrió el formulario (patrón de la 19b2) y, con 409, ofrecer «Recargar».

## 8. Sincronización de PERMISO MEDICO (TAREA-23-3)
### 8.1 Contrato
`POST /api/admin/novedades/sincronizar-permisos-medicos[?escenario=base|fechas|anuladas]` (Politica.Admin), sin cuerpo.
- 200 → `{ sincronizadaUtc, fuente, leidas, nuevas, actualizadas, anuladas, reactivadas, sinCambios, ausentesEnOrigen, omitidas: { categoría: n }, codigosOmitidos: [NOV-… o "fila ID n"] }` (máx. 100 códigos). Sin nombres, correos, cédulas ni JSON. La fecha de la sincronización no se guarda (va en la respuesta y en el log).
- 400 `escenario` (solo con la fuente Simulada, P1: «El escenario solo se admite con la fuente Simulada.» / «El escenario debe ser base, fechas, anuladas.»); 401; 403 (gestor); 503 «Fuente de novedades externas no configurada» / «…no disponible» (archivo ausente, ilegible o sin las columnas esperadas; más de 5000 filas); 503 de empleados si hace falta la API y está caída (sin escribir); 503 registro ocupado; 503 base caída.

### 8.2 Modos (`NovedadesExternas:Modo`, H1)
| Modo | Entorno | Fuente |
|---|---|---|
| `Simulado` | Solo Development (en otro, la API no arranca) | 10 filas ficticias NOV-SIMPM-0001…0010 (2075, DEV001–DEV006), escenarios `base`, `fechas` y `anuladas` |
| `ArchivoLocal` | Solo Development | CSV exportado de la lista, ruta **solo en user-secrets** (`NovedadesExternas:RutaArchivo`). UTF-8 con detección de BOM; salta la línea `ListSchema=`; `TextFieldParser` del framework (P6, sin NuGet); columnas ID, Título/Title, NOVEDAD_UID, ESTADO_NOVEDAD, DETALLE_JSON y CORREO_PERSONA (nunca «Creado por»/«Modificado por»); fila con otro número de columnas → `filaIlegible` |
| sin modo o `Graph` | Cualquiera (la API arranca) | 503 «no configurada». Graph llega en la 23-4 (pendientes 7 y 63) |
| otro valor | — | la API no arranca |

`appsettings.Development.json` trae `Simulado`.

### 8.3 Mapeador (`MapeoPermisoMedico`, puro, **reutilizable en la Fase 3**)
- Uid = NOVEDAD_UID (si no se lee → `uidInvalido`); LegacyId = ID; Codigo = Título (si falta: `GeneradorCodigoNovedad(fecha de registro, Uid)`, determinista).
- Filtros: `OrigenPermiso = aplicacionPermisosMedicos` (si no, `otroOrigen`); TipoNovedad «PERMISO MEDICO»/«PERMISO_MEDICO» sin distinguir mayúsculas ni tildes (si no, `tipoNoSoportado`); TipoAplicacion PERSONA (si no, `aplicacionNoSoportada`); JSON ilegible o que no es objeto → `jsonInvalido`.
- Días: `Dias[].Fecha` (yyyy-MM-dd, sin repetidos) o, si no vienen, el rango de `Fechas`; ilegibles o rango > 366 → `fechasInvalidas`. No usa FECHA_INICIO/FECHA_FIN del CSV (traen Z).
- Empleado: `Fechas.Ekon` → `Persona.Ekon` (sin puntos de miles, [SUPUESTO], pendiente 64) → correo (`Persona.Correo` o CORREO_PERSONA; formato válido, pendiente 45). Sin ninguno → `sinEmpleado`. `Persona.Nombre` no se lee; el correo nunca se guarda ni se imprime.
- Estado: cualquier valor distinto de «ACTIVA» (sin distinguir mayúsculas ni espacios) = anulada [H8, pendiente 64].
- Registro: `FechaRegistro` en hora de Ecuador [SUPUESTO, pendiente 64] → UTC truncada a segundos (ilegible → null); nombre y correo del registrante → `RegistradoPor*`.
- N13: `DetalleOrigenJson` = lista blanca en orden fijo (NovedadUID, TipoAplicacion, TipoNovedad, OrigenPermiso, Origen, Fechas{FechaInicio, FechaFin, Ekon}, Dias[].Fecha, Registro.FechaRegistro), sin `Persona.*`; `Observacion` = null; `OrigenArea` ← `Origen`.

### 8.4 Plan y escritura
- **R3:** la fuente, la base (existentes por Uid, dueños de los códigos y de los LegacyId, empleados por correo, Uid de PERMISOS_MEDICOS) y, solo si alguna fila la necesita, la API de empleados (lectura fresca con cédulas, **una sola vez**, A4) se leen **fuera** de la transacción. API caída → 503 antes de escribir.
- **Empleado:** con fila en `Empleado` se usa aunque ya no esté activo; sin fila, alta puntual (con cédula, D15) solo si está activo en la API. Solo correo (P2): primero la fila con ese `CorreoEmpresa` (coincidencia única; varias → `sinEmpleado`), después la API (coincidencia única).
- **`PlanSincronizacion`** (puro): nueva no activa → `noActivaSinRegistro` (P4); Uid repetido en la fuente → `uidInvalido`; Uid de una novedad de PROFESIOGRAMA o código de otro Uid → `conflictoCodigo`; LegacyId de PERMISOS_MEDICOS con otro Uid → `conflictoLegacyId`. Guardadas: no activa y vigente → anular; activa y anulada → reactivar; si cambió Codigo, LegacyId, EmpleadoId, fechas, días, OrigenArea, RegistradoPor* o DetalleOrigenJson → actualizar; si no → **sin cambios, no entra a ninguna transacción** (sin UPDATE: RowVer y FechaModificacion iguales).
- **Lotes de 100 (P3)**, cada uno en su `ITransaccionAsignaciones` (mismo applock que la alta puntual; sin applock propio). Dentro se relee **con seguimiento** y se reclasifica con la misma regla (**A1**: un Uid «nuevo» que ya existe no se duplica). Si un lote falla, los anteriores quedan confirmados (el log registra los conteos parciales) y repetir la sincronización completa el resto (idempotente).
- Nueva (R5): `CreadoPorId` = SISTEMA, `FechaCreacion` = FechaRegistro en UTC, origen 2. Actualización: `ModificadoPorId` = el admin (auditoría). Anular: «Anulada en el origen (PERMISOS_MEDICOS)», `AnuladaPorId` = SISTEMA (P5), `FechaAnulacion` = ahora; reactivar limpia los cuatro campos. **Ausentes en el origen: solo se cuentan, nunca se anulan (R4).** NOV-DEV-0004 (sembrador) siempre cuenta como ausente.
- Logs: Information con fuente, conteos, milisegundos, Id del admin y códigos NOV omitidos; Warning si un lote falla. Nunca nombres, correos, cédulas, el JSON ni la ruta del archivo.

### 8.5 Categorías de omisión
`filaIlegible`, `uidInvalido`, `jsonInvalido`, `otroOrigen`, `tipoNoSoportado`, `aplicacionNoSoportada`, `fechasInvalidas`, `sinEmpleado`, `conflictoCodigo`, `conflictoLegacyId`, `noActivaSinRegistro`.

### 8.6 Ajustes de la Fase B (A1–A6)
A1 reclasificación dentro del lote; A2 BOM y `ListSchema`; A3 fuente simulada con LegacyId ≥ 2.000.000.001; A4 una sola lectura de la API; A5 la versión de una novedad sin cambios no cambia (caso s02b del script); A6 pruebas solo con CSV y JSON ficticios (nunca un CSV real de la lista).
