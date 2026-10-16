# FASE 4 — Empleados desde la API (EvolutionEmployee), opción C

**Fecha:** 2026-10-07; actualizado el 2026-10-13 (TAREA-26d-3). Contrato verificado con un sondeo de solo agregados (TAREA-26a); diseño en la Fase A de la TAREA-26d; backend en la TAREA-26d-1; frontend en la TAREA-26d-2; comparación con el ERP y retiro de `empleadoId` en la TAREA-26d-3.
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

- **Normalización:** espacios extremos fuera; vacío → null; los textos se recortan a su columna (`MapeoEmpleado`).
- **Registro válido:** activo (`"A"`), con código de ≤ 20 caracteres, con nombre y con todos los `cod*` de ≤ 10 caracteres. Los no válidos no entran a la lista: para la app, "no existe o no está activo".

**Datos sensibles (C31): nunca en el DTO, ni en la base, ni en los logs.**
- Campos excluidos: `cedula`, `cedulaReportaA`, `telefono`, `mailPersonal`, `provincia`, `canton`, `barrio`, `callePrincipal`, `calleSecundaria`, `numeroCasa`, `fechaNacimiento`, `fechaAntiguedad`, `salario`, `bpr`, `sexo`, `nivelDireccion`, `nombresReportaA`, `reportaA`.
- El cliente deserializa solo a `EmpleadoJson` (campos permitidos) y nunca registra el cuerpo ni la URL.
- La columna `Empleado.Cedula` existe, pero la alta puntual no la escribe.
- El buscador no devuelve cédula ni correo.

## 4. Caché en memoria (`CacheEmpleadosErp`, singleton)
| Regla | Detalle |
|---|---|
| TTL (P3) | `ServiciosExternos:CacheEmpleadosMinutos` = 10: dentro del TTL no se llama a la API |
| Descarga única | Un `SemaphoreSlim`: las peticiones simultáneas esperan la misma descarga |
| Lista anterior (P4) | Si la API no responde y la lista anterior tiene menos de `CacheEmpleadosMaxAntiguedadMinutos` (60), el buscador y la vista previa la usan. El buscador devuelve `avisoErp` "Lista de empleados de las HH:mm; el ERP no respondió." (hora de Ecuador). Tras un fallo no se reintenta durante 1 minuto. Sin lista utilizable → 503 |
| Lectura fresca (P5) | Al **registrar** se descarga siempre (y se renueva la caché); nunca se usa la lista anterior. Si falla → 503 **sin abrir la transacción** |
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
- **Dentro del applock** no se vuelve a llamar a la API: se reutiliza el catálogo validado fuera.

## 6. Alta puntual (`AltaPuntualEmpleados`, dentro de `ITransaccionAsignaciones`)
1. Antes de crear `ProyectoPersonal`: **upsert** por `CodigoEkon` de los empleados de las personas nuevas, con los datos de la API leídos frescos fuera de la transacción.
   - Fila existente → se refrescan los datos laborales, `EstadoErp = "A"` y `FechaSincronizacion` (P6).
   - Fila nueva → se crea.
   - La cédula no se toca.
2. **`CargoInfor`** (P8): alta del puesto normalizado si no existe.
3. `SaveChanges` → conversión de Id temporal a Id real en el personal y los días (`IdReal`: **nunca se persiste un Id negativo**).
4. Duplicado en `UQ_Empleado_CodigoEkon` (o `UQ_CargoInfor_Cargo`) → 409: "Otro registro se guardó al mismo tiempo; vuelva a intentarlo." en la creación, y "El proyecto cambió; vuelve a cargarlo." en personal y reactivación.
5. Auditoría: `AuditoriaInterceptor`, con el usuario que asigna.

## 7. Modo Simulado (SOLO Development)
- `EmpleadosErpSimulado` devuelve **DEV001–DEV008** con datos idénticos a las filas del sembrador (definición común en `EmpleadosPrueba`): la alta puntual no cambia sus filas.
- Además, **SIM001–SIM200** ficticios: el 60 % en "UNIDAD SISTEMA INTEGRADO DE GESTION" y el resto repartido entre los otros 6 departamentos de la tabla, sin datos sensibles.
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
