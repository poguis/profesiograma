# FASE 4 — Empleados desde la API (EvolutionEmployee), opción C

**Fecha:** 2026-10-07; actualizado el 2026-10-13 (TAREA-26d-3) y el 2026-10-20 (TAREA-22-1: cédula, D15). Contrato verificado con un sondeo de solo agregados (TAREA-26a); diseño en la Fase A de la TAREA-26d; backend en la TAREA-26d-1; frontend en la TAREA-26d-2; comparación con el ERP y retiro de `empleadoId` en la TAREA-26d-3.
**Decisión del usuario (07/10/2026):** la API EvolutionEmployee es la **única fuente** de los empleados. No hay sincronización masiva ni tarea programada; la TAREA-26b se descartó. Motivo: no depender de una réplica que puede desincronizarse; la API es el contrato.
**Reemplaza** a FASE_2 J1 ("Empleado como caché sincronizada") y a `FASE_4_Sincronizacion_Empleados.md`.

## 1. Modelo
| Elemento | Opción C |
|---|---|
| Fuente | API EvolutionEmployee (y, en modo Simulado, una lista ficticia en memoria) |
| Buscador (`GET /api/empleados`) | Lista de la API en **caché en memoria**; nada en la base |
| Tabla `Empleado` | **Solo las personas asignadas** a algún proyecto (y DEV001–DEV008 del sembrador). En la Fase 3, también las que vengan de las asignaciones de SharePoint |
| Alta puntual | Al **registrar** una persona nueva (crear proyecto, actualizar personal, reactivar): se crea su fila, o se refresca la existente, con los datos de la API leídos en ese momento |
| `EstadoErp` | Estado en la API en la última alta puntual. Si el empleado sigue activo **hoy** lo dice la API |
| `FechaSincronizacion` | Fecha (UTC) de la última copia desde la API (nombre heredado; sin migración, P9) |
| Personas guardadas | Se ven y se editan con los datos guardados, aunque la API no responda. Las vigentes e históricas no exigen estar activas (D4) |

## 2. Contrato (verificado en la TAREA-26a)
| Elemento | Valor |
|---|---|
| Método y URL | `POST {ServiciosExternos:ErpBase7048}/api/EvolutionEmployee/EmployeesEvolution` |
| Cuerpo (fijo) | `{ "parameter": "", "estado": "A", "codEmpresa": "", "codDepartamento": "" }`: todos los activos de todas las empresas. `parameter` admite un código o una cédula; no se usa |
| Autenticación | Ninguna |
| Respuesta | Envoltura `{ statusCode, isSuccess, errorMessages, result[] }`, ~1633 activos, ~2,5 MB, ~2 s, sin paginación |
| `codPersona` | Texto numérico de 1 a 5 caracteres, único; 0 con prefijo "DEV" |
| No disponible | Red, timeout (`TimeoutEmpleadosSegundos`, 60), HTTP distinto de 200, JSON inválido, `isSuccess = false`, `result` nulo **o vacío** |

## 3. Mapeo (DTO interno solo con estos campos)
| API | Empleado | Columna |
|---|---|---|
| `codPersona` (texto o número → texto, sin espacios) | `CodigoEkon` | VARCHAR(20), único |
| `nombrecompleto` (minúsculas en la API) | `NombreCompleto` | NVARCHAR(200) |
| `apellidos` / `nombres` | `Apellidos` / `Nombres` | NVARCHAR(120) |
| `mailEmpresa` | `CorreoEmpresa` | NVARCHAR(256) |
| `codEmpresa` / `empresa` | `CodEmpresa` / `Empresa` | VARCHAR(10) / NVARCHAR(200) |
| `codPuesto` / `puesto` | `CodPuesto` / `Puesto` (y `CargoInfor`) | VARCHAR(10) / NVARCHAR(200) |
| `codDepartamento` / `departamento` | `CodDepartamento` / `Departamento` | VARCHAR(10) / NVARCHAR(200) |
| `codUnidad` / `unidad`, `codArea` / `area`, `codSeccion` / `seccion` | Columnas homónimas | VARCHAR(10) / NVARCHAR(200) |
| `familiaPuesto` | `FamiliaPuesto` | NVARCHAR(100) |
| `estado` | `EstadoErp` | CHAR(1) |
| `cedula` (solo texto; D15, TAREA-22-1) | `Cedula` (aparte, ver abajo) | VARCHAR(20), NULL |

- **Normalización:** espacios extremos fuera; vacío → null; los textos se recortan a su columna (`MapeoEmpleado`).
- **Registro válido:** activo (`"A"`), con código de ≤ 20 caracteres, con nombre y con todos los `cod*` de ≤ 10 caracteres. Los no válidos no entran a la lista: para la app, "no existe o no está activo".

**Datos sensibles (C31): nunca en el DTO, ni en la base, ni en los logs.**
- Campos excluidos: `cedulaReportaA`, `telefono`, `mailPersonal`, `provincia`, `canton`, `barrio`, `callePrincipal`, `calleSecundaria`, `numeroCasa`, `fechaNacimiento`, `fechaAntiguedad`, `salario`, `bpr`, `sexo`, `nivelDireccion`, `nombresReportaA`, `reportaA`.
- El cliente deserializa solo a `EmpleadoJson` (campos permitidos) y nunca registra el cuerpo ni la URL.
- El buscador no devuelve cédula ni correo.

**Medido (sondeo M1 de la TAREA-22-1, solo agregados, 20/10/2026):** 1630 activos, todos con `cedula` como **texto**; largo de 8 a 10 (2 de 8 dígitos y 1628 de 10); 358 empiezan con "0"; sin vacíos ni repetidos. [PENDIENTE DE CONFIRMAR con RR. HH. las 2 cédulas de 8 dígitos.]

**Cédula (D15, TAREA-22-1, 20/10/2026): se GUARDA solo en `Empleado.Cedula`** (para la columna CEDULA del reporte Excel de la TAREA-22-2). Nunca en un DTO, la caché, un log, un mensaje de excepción ni una respuesta de la API (salvo ese reporte).
- **Lectura:** `EmpleadoJson.Cedula` con un convertidor TOLERANTE (`SoloTexto`), que nunca lanza:
  - texto → sin espacios extremos; vacío o solo espacios → null;
  - número, null, ausente, objeto o arreglo → null. Un número no se convierte a texto (habría perdido los ceros iniciales) y no invalida al empleado ni a la lista.
- **Tipo aparte:** `CedulasEmpleados` (código → cédula; clase sellada sin enumeración; `ToString()` = "CedulasEmpleados(n)"). `EmpleadoErp`, `DatosEmpleado` y `EmpleadoAsignable` (records) **no** la llevan; `EmpleadoJson.ToString()` tampoco la muestra.
- **Solo en la lectura fresca** (§4): `ObtenerActivosFrescosAsync` → `CatalogoEmpleados.Cedulas` (solo las de los códigos de la solicitud) → `ProyectoValidado.Cedulas` / `CambioPersonal.Cedulas` → alta puntual (§6).
- **Modo Simulado:** cédulas FICTICIAS: DEV001–DEV008, la misma de su fila del sembrador ("99999999" + n, así la alta puntual no cambia esas filas); SIM001–SIM200, 10 dígitos que empiezan en "00".

## 4. Caché en memoria (`CacheEmpleadosErp`, singleton)
| Regla | Detalle |
|---|---|
| TTL (P3) | `ServiciosExternos:CacheEmpleadosMinutos` = 10: dentro del TTL no se llama a la API |
| Descarga única | Un `SemaphoreSlim`: las peticiones simultáneas esperan la misma descarga |
| Lista anterior (P4) | Si la API no responde y la lista anterior tiene menos de `CacheEmpleadosMaxAntiguedadMinutos` (60), el buscador y la vista previa la usan. El buscador devuelve `avisoErp` "Lista de empleados de las HH:mm; el ERP no respondió." (hora de Ecuador). Tras un fallo no se reintenta durante 1 minuto. Sin lista utilizable → 503 |
| Lectura fresca (P5) | Al **registrar** (y en «completar cédulas») se descarga siempre (y se renueva la caché); nunca se usa la lista anterior. Si falla → 503 **sin abrir la transacción**. Es la **única** que devuelve las cédulas (TAREA-22-1) |
| Cédulas (D15) | La lista que queda en caché va **sin** cédulas, aunque la haya renovado una lectura fresca: el buscador, la vista previa y la comparación nunca las ven |
| Memoria | Solo los registros válidos y normalizados: ~1633, unos 1–2 MB |
| 503 | `ErpNoDisponibleException("empleados", …)` → "Servicio de empleados no disponible" |

## 5. Solicitudes y validación
- **Personas nuevas** (creación, personal y reactivación): **solo `codigoEkon`** (P2). La transición con `empleadoId` se **retiró en la TAREA-26d-3**: el campo ya no existe en el contrato y, si llega, se ignora como cualquier campo desconocido.
- **Personas guardadas (vigentes):** se identifican por su `id` de `ProyectoPersonal`; el empleado no cambia. `codigoEkon` es opcional y, si llega, debe ser el suyo.
- **Errores 400** (todos en `…codigoEkon`):

  | Caso | Mensaje |
  |---|---|
  | Persona nueva sin código | "El empleado es obligatorio." |
  | No activo en la API o no existe | "El empleado {código recibido} no existe o no está activo." |
  | Vigente con un código distinto del suyo | "No se puede cambiar el empleado de una persona vigente. Para reemplazarla, acorte su fecha fin y agregue una persona nueva." |

- **`CatalogoEmpleados`:**
  - se carga antes de validar con los códigos de las personas nuevas (fresco al registrar, en caché en la vista previa);
  - entrega el Id real si la persona tiene fila o un **Id temporal negativo** (-1, -2…) si no;
  - el motor, los tramos y los cruces internos usan esos Id; los cruces externos solo se consultan para los Id positivos (una persona sin fila no tiene asignaciones).
- **Dentro del applock** no se vuelve a llamar a la API: se reutiliza el catálogo validado fuera (con sus cédulas, si fue fresco).
- **Cédulas (TAREA-22-1):** el catálogo fresco (registro) guarda las cédulas de las personas nuevas; el de la vista previa, ninguna.

## 6. Alta puntual (`AltaPuntualEmpleados`, dentro de `ITransaccionAsignaciones`)
1. Antes de crear `ProyectoPersonal`: **upsert** por `CodigoEkon` de los empleados de las personas nuevas, con los datos de la API leídos frescos fuera de la transacción.
   - Fila existente → se refrescan los datos laborales, `EstadoErp = "A"` y `FechaSincronizacion` (P6).
   - Fila nueva → se crea.
   - **Cédula (D15, TAREA-22-1):** fila nueva → la de la API sin espacios extremos y recortada a 20; sin dato → NULL. Refresco → una cédula no vacía la actualiza; vacía o ausente **no pisa** la guardada.
2. **`CargoInfor`** (P8): alta del puesto normalizado si no existe.
3. `SaveChanges` → conversión de Id temporal a Id real en el personal y los días (`IdReal`: **nunca se persiste un Id negativo**).
4. Duplicado en `UQ_Empleado_CodigoEkon` (o `UQ_CargoInfor_Cargo`) → 409: "Otro registro se guardó al mismo tiempo; vuelva a intentarlo." en la creación, y "El proyecto cambió; vuelve a cargarlo." en personal y reactivación.
5. Auditoría: `AuditoriaInterceptor`, con el usuario que asigna.

### 6.1 «Completar cédulas» (TAREA-22-1)
`POST /api/empleados/completar-cedulas` (Politica.Admin, sin cuerpo) llena `Empleado.Cedula` de las filas **guardadas** que la tienen en NULL (las anteriores a la D15; la alta puntual solo la escribe al asignar).
1. Lectura **fresca** de la API, fuera de la transacción. Si falla → 503 «Servicio de empleados no disponible», sin abrir la transacción ni escribir.
2. Dentro de `ITransaccionAsignaciones` (el applock de asignaciones, donde ya ocurren todas las escrituras de `Empleado`: no se cruza con una alta puntual simultánea): carga **con seguimiento** las filas con `Cedula` NULL (la auditoría llena ModificadoPor) y completa solo las que la API trae con cédula no vacía. Nunca sobrescribe una no nula: es idempotente.
3. Respuesta 200 `{ revisadas, completadas, sinCedulaEnApi, noEncontradasEnApi }`, **solo conteos** (sin cédulas ni códigos). `noEncontradasEnApi` = el código no está entre los activos de la API. Un log Information con los 4 conteos y el Id del usuario.
4. Gestor → 403. Verificación manual: `backend/tests/manual/tarea22-1/LEEME.md` (M1–M9).

## 7. Modo Simulado (SOLO Development)
- `EmpleadosErpSimulado` devuelve **DEV001–DEV008** con datos idénticos a las filas del sembrador (definición común en `EmpleadosPrueba`): la alta puntual no cambia sus filas.
- Además, **SIM001–SIM200** ficticios: el 60 % en "UNIDAD SISTEMA INTEGRADO DE GESTION" y el resto repartido entre los otros 6 departamentos de la tabla, sin datos sensibles. **TAREA-62:** los SIM con i % 10 == 0 (SIM010, SIM020… SIM200, 20 personas) son `OPERATIVO`; el resto y los DEV, `ADMINISTRATIVO`.
- En modo Http los DEV no están en la API: no se pueden agregar como personas nuevas, pero sus asignaciones existentes siguen funcionando.

## 8. Comparación "guardado frente al ERP" (TAREA-26d-3)
- **Endpoint:** `GET /api/proyectos/{id}/personal/comparacion-erp` (misma visibilidad que `GET …/edicion`; 404 si no es visible). Lo usan "Actualizar personal" y "Reactivar"; se carga aparte para no demorar la pantalla.
- **Solo informa (D1):** no refresca `Empleado` ni `ProyectoPersonal.CargoAsignado` (el cargo asignado es histórico). Los datos guardados se refrescan en la próxima alta puntual (P6).
- **Qué compara:** los datos de `Empleado` (`ConsultaEmpleadosPorIds`: código, nombre, puesto, departamento y unidad; sin cédula ni correo) de **todas** las personas guardadas del proyecto con la lista de la API. Cada pantalla decide a quién lo muestra (D3): "Actualizar personal", solo las vigentes; "Reactivar", el principal propuesto (R7) y las históricas.

  | Tipo | Cuándo | Mensaje |
  |---|---|---|
  | `NO_ACTIVO` | El código no está en la lista de activos | "Ya no está activo en el ERP." |
  | `CAMBIO_NOMBRE` | `NombreCompleto` distinto | "El nombre en el ERP es distinto: {guardado} → {ERP}." |
  | `CAMBIO_CARGO` | `Puesto` distinto (ambos normalizados como en `CargoInfor`) | "El cargo en el ERP cambió: {guardado} → {ERP}." |
  | `CAMBIO_DEPARTAMENTO` | Departamento **o unidad** distintos (D5; una unidad igual al departamento no cuenta, como en el buscador) | "El departamento o la unidad en el ERP cambió: {guardado} → {ERP}." |

  Un valor vacío se muestra como "(vacío)".
- **Normalización (como el buscador):** espacios extremos fuera, espacios internos colapsados, vacío = null, sin distinguir mayúsculas ni tildes.
- **Lista usada:** la de la caché (`ObtenerActivosAsync`); **nunca** una lectura fresca.
  - Dentro del TTL de la lista (**10 min**, `CacheEmpleadosMinutos`) no se llama a la API.
  - Si la API no responde, se usa la **lista anterior hasta 60 min** (`CacheEmpleadosMaxAntiguedadMinutos`), con el aviso general "Comparado con la lista de las HH:mm; el ERP no respondió.".
  - Sin lista utilizable: **200** con `avisoGeneral` "No se pudo comparar con el ERP." y sin avisos por persona. **Nunca 503.**
- **Texto único (D4):** "No se pudo comparar con el ERP." reemplaza a "No se pudo verificar los datos en el ERP." de la propuesta R7 de la reactivación; la pantalla lo muestra una sola vez.
- **Respuesta:**
  ```json
  {
    "comparadoUtc": "2026-10-13T15:30:00+00:00",
    "avisoGeneral": null,
    "personas": [
      { "codigoEkon": "900001", "avisos": [
        { "tipo": "CAMBIO_CARGO", "mensaje": "El cargo en el ERP cambió: TECNICO → SUPERVISOR.", "guardado": "TECNICO", "erp": "SUPERVISOR" } ] },
      { "codigoEkon": "DEV003", "avisos": [
        { "tipo": "NO_ACTIVO", "mensaje": "Ya no está activo en el ERP.", "guardado": null, "erp": null } ] }
    ]
  }
  ```
  Solo aparecen las personas con avisos.
- **Frontend:** insignia por fila ("No activo en el ERP" en rojo o "Datos distintos en el ERP" en amarillo) con el detalle en la tarjeta o en la fila de la tabla de históricas; "Comparando con el ERP…" mientras carga; la nota general es informativa y, si el ERP no respondió, ofrece "Reintentar". Nunca bloquea la vista previa ni el registro.

## 9. Pendiente
- **Opcional (D1):** acción "Actualizar datos guardados del empleado" (refrescar la fila de `Empleado` desde la API, con auditoría y control de concurrencia). Hoy la comparación solo informa.
- **Fase 7:** `Usuario.EmpleadoId` por alta puntual desde `mailEmpresa` [PENDIENTE DE CONFIRMAR].
- **TAREA-23:** las novedades de una persona usarán la misma alta puntual.
- **Fase 3:** alta desde las asignaciones de SharePoint con datos de la API si el código existe; si no existe, `EsOrigenLegado = 1` con los datos de SharePoint [PENDIENTE DE CONFIRMAR si un cuerpo con `estado` vacío devuelve también a los inactivos].

## 10. Criterio de personal elegible (TAREA-62, pendiente 62)
**Equivalencia (62-0, confirmada por el usuario con el sondeo de solo agregados `tarea62/sondeo-familias.cmd`):** el «Grupo» del original (`ESTRUCTURA GENERAL`, `Grupo = "ADMINISTRATIVO"`, FASE_1 §2.3 y §2.7) es `familiaPuesto` del ERP: 715 ADMINISTRATIVO y 908 OPERATIVO, igual que SharePoint, sin vacíos ni comas. Sin cambios de mapeo ni migración.

| Tema | Regla |
|---|---|
| Punto de decisión | `ReglasPersonalElegible.EsAsignable(EmpleadoErp, familias)` (Application/Empleados): lista vacía = todos; con filtro, un empleado sin familia no es asignable. Comparación como `ComparacionEmpleadosErp.Iguales` (sin mayúsculas ni tildes, espacios colapsados) |
| Parámetro | `EMPLEADO_FAMILIAS_ASIGNABLES` (Id 8, LIST separada por comas; inicial `ADMINISTRATIVO`). Se lee **sin caché** en cada búsqueda o validación (`ObtenerFamiliasAsignablesAsync`, `SepararLista`): el cambio del Admin aplica de inmediato en toda la API. Vacío o ausente = sin filtro, con un Warning en el log |
| Buscador | `GET /api/empleados` filtra por familia **antes de paginar**; se suma a «soloMisDepartamentos» (filtros independientes, J6). La respuesta agrega `familiasAsignables` (texto guardado; vacío = sin filtro), compatible hacia atrás |
| Personas nuevas | `CatalogoEmpleados` (crear proyecto, actualizar personal, reactivar, vista previa y novedades PERSONA): 400 en `…codigoEkon` «El empleado {código} no pertenece a un grupo asignable.» (código tal como llegó), **después** de «no existe o no está activo» |
| Exención (P3) | Quien ya tiene fila en **ese mismo proyecto** (vigente o histórica) no se filtra al actualizar personal o reactivar, incluido el principal que propone R7. En crear proyecto y en novedades no hay exención. Las personas guardadas identificadas por `id` no pasan por la validación |
| Sin filtro | Sincronización de PERMISO MEDICO (P2), comparación con el ERP (solo informa; pendiente 69), personas ya guardadas (incluidas las migradas en la Fase 3; pendiente 68) |
| `GET /api/admin/empleados/familias` (Admin) | `{ valorActual, sinCriterio, familias: [{ familia, activos, asignable }] }`: agrupadas por familia normalizada con la lista en caché, más las del parámetro sin activos (0). Orden: activos descendente. Sin lista utilizable → 503. Sin datos personales |
| `PUT /api/admin/empleados/familias-asignables` (Admin) | `{ familias[], valorAnterior }` → 200 `{ valorActual }`. 400 en `familias`: «Seleccione al menos una familia.», «La familia no puede estar vacía.», «La familia {v} no puede contener comas.», «La familia {v} está repetida.», «La familia {v} no existe.» (no está en la lista de la API ni en el parámetro), «El criterio supera el largo permitido.» (> 400); 400 en `valorAnterior` si falta: «Falta el valor anterior del criterio; recargue.» (vacío es válido). Se informan todos los errores de una vez |
| Concurrencia y auditoría | `ExecuteUpdate … WHERE Clave = 'EMPLEADO_FAMILIAS_ASIGNABLES' AND Valor = valorAnterior` (compare-and-set; Parametro no tiene RowVer; la comparación sigue la collation de la base, `CI_AS`); 0 filas → 409 «Otro usuario cambió el criterio; recargue.». `ModificadoPorId` y `FechaModificacion` (UTC, segundos) se escriben de forma explícita (ExecuteUpdate no pasa por el `AuditoriaInterceptor`). Log Information con el valor anterior, el nuevo y el Id del Admin. Sin tabla de historial (P5). Si faltara la fila del parámetro (está en las semillas), el PUT también responde 409 |
| Modo Simulado | §7: SIM con i % 10 == 0 = `OPERATIVO` |
| Frontend (TAREA-62-2) | Menú «Administración» solo con `esAdmin` (reutiliza `useUsuarioActual`), ruta `/admin/personal-elegible` (carga diferida); un gestor que entra por URL ve «No tienes permiso para ver esta página.» sin llamar al backend. Casillas por familia con sus activos (orden del backend; las de 0 activos se pueden desmarcar), textos fijos «Solo afecta a las asignaciones nuevas…» y «La sincronización de permisos médicos no aplica este criterio.», aviso «Sin criterio: se puede elegir a cualquier persona activa.» si `sinCriterio`. «Guardar» deshabilitado con «No hay cambios para registrar.» o «Seleccione al menos una familia.»; con cambios, «Se guardará: A → B». PUT con el `valorActual` del GET que abrió la pantalla (sin refresco en segundo plano); 200 → «Criterio actualizado.», invalida el criterio y todas las búsquedas de empleados (`['empleados']`) y recarga. Errores: 400 bajo la lista (varios), 409 con «Recargar» (confirmación si hay cambios), 403 sin «Reintentar», 503 y red con «Reintentar». `useBlocker` + `beforeunload`. Buscador: «Solo grupos asignables: A, B.» si `familiasAsignables` no está vacío. El 400 «no pertenece a un grupo asignable» se muestra junto a la persona en Nuevo proyecto, Actualizar personal y Reactivar, y bajo «Persona» en Nueva novedad (con pruebas) |
| Verificación | Pruebas: `PersonalElegibleTests`, `PersonalElegibleSqlTests` (ToQueryString), `CriterioPersonalEndpointsTests`. Manual: `backend/tests/manual/tarea62/criterio.cmd` (cubre el ExecuteUpdate y la auditoría) |

| # | Decisión (Fase A de la TAREA-62, aprobada) |
|---|---|
| P2 | La sincronización de PERMISO MEDICO no aplica el filtro (la decide otra aplicación) |
| P3 | Exención de quien ya está en el mismo proyecto (actualizar personal y reactivar, incluido R7) |
| P4 | El PUT rechaza el criterio vacío; vacío o ausente en la base = sin filtro, con Warning |
| P5 | Auditoría con ModificadoPor/FechaModificacion y el log; sin tabla de historial ni migración |
| P6 | Administración en el menú «Administración» solo para Admin, ruta `/admin/personal-elegible` (62-2) |
| P7 | El buscador muestra qué grupos se aplican (`familiasAsignables`; la línea se agrega en la 62-2) |
| P8 | La comparación con el ERP no marca «ya no asignable» por ahora (pendiente 69) |
