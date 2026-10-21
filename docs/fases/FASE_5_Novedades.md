# FASE 5 — Novedades (TAREA-23)

Novedades de asistencia: aplicaciones PERSONA, PROYECTO y GENERAL, origen PROFESIOGRAMA (editable) o PERMISOS_MEDICOS (sincronizado, solo lectura). El modelo (`Novedad`, `NovedadDia`, catálogos y semillas) existe desde la TAREA-03: **sin migraciones**.

| Subtarea | Alcance | Estado |
|---|---|---|
| 23-1 | Lectura, opciones y **creación** | Implementada (21/10/2026), pendiente de la verificación del usuario |
| 23-2 | Edición y anulación (`RowVer`, N3, N8–N10) | Siguiente |
| 23-3 | Sincronización de PERMISO MEDICO: puerto, mapeador, fuente simulada y endpoint Admin | Pendiente |
| 23-4 | Fuente real (Graph / SharePoint) | Bloqueada por la App Registration (pendiente 7) |
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
