# TAREA-26d-1 — Backend: empleados desde la API (opción C) y retiro de la 26b

**Fecha:** 2026-10-07 (Fase A de la TAREA-26d y Fase B de la 26d-1)
**Resultado:** implementada. Falta la verificación del usuario (V1, V2, V3, V8, V9, V10; sección 6).
- `dotnet build Profesiograma.slnx -c Release`: **0 advertencias, 0 errores**.
- `dotnet test --solution Profesiograma.slnx -c Release`: **581/581**. Son las 549 del commit de la 26a (547 sin cambios en sus aserciones y 2 retiradas: las de traducción de la búsqueda local) más 34 nuevas.
- `dotnet ef migrations has-pending-model-changes … --configuration Release`: "No changes have been made to the model since the last migration." **Sin migraciones.**
- `npm run build` (frontend, sin tocar): compila.
- `grep` de "sincronizar" y "profesiograma:empleados" en `backend/src`: **0 restos**. Solo queda la columna `FechaSincronizacion`, que se conserva por P9.
- Scripts `tarea26d`: probados **solo con `SIMULAR=1`**, 3/3 OK.

**Lo que no se hizo:**
- ninguna llamada a una API (ni la nuestra ni `backstack.sedemi.com`);
- `dotnet run`, `npm run dev`, `user-secrets`;
- SQL;
- comandos git que modifiquen (la 26b se deshizo editando y borrando archivos; el contenido de HEAD se leyó con `git show`);
- cambios en el frontend.

No se leyeron los `resultado-*.txt` ni los `cuerpo-empleados-*.json` reales.

## 1. Decisiones aprobadas
| # | Decisión |
|---|---|
| Opción C | La API EvolutionEmployee es la única fuente de los empleados; `Empleado` solo con las personas asignadas (alta puntual); la 26b se descarta |
| P2 | Personas nuevas por `codigoEkon`; `empleadoId` como alternativa excluyente hasta la 26d-3. Mensaje "El empleado {identificador} no existe o no está activo." |
| P3 | `ServiciosExternos:CacheEmpleadosMinutos` = 10; descarga única (`SemaphoreSlim` en un singleton) |
| P4 | El buscador usa la lista anterior si tiene menos de 60 min (`CacheEmpleadosMaxAntiguedadMinutos`), con `avisoErp` "Lista de empleados de las HH:mm; el ERP no respondió." (hora de Ecuador); si no hay lista → 503 |
| Errores | `result` vacío, `isSuccess = false`, nulo, timeout, HTTP distinto de 200 o JSON inválido = no disponible → 503 "Servicio de empleados no disponible" |
| P5 | Al registrar, lectura fresca (renueva la caché); si falla → 503 sin abrir la transacción. La vista previa usa la caché |
| P6 | Refresco de los datos del empleado solo en la alta puntual, también si la fila ya existía |
| P8 | Alta del puesto en `CargoInfor` dentro de la misma transacción |
| P9 | Sin migración; se actualizan los comentarios de `EstadoErp` y `FechaSincronizacion` |
| P11 | La 26b nunca se ejecutó: no hay datos que limpiar |
| P12 | Simulado: DEV001–DEV008 idénticos al sembrador (con prueba) y SIM001–SIM200 repartidos entre los 7 departamentos (mayoría SIG) |
| P13 | Búsqueda sin distinguir mayúsculas ni tildes (nombre, apellidos, nombres, código) |
| P14 | Ajustes mecánicos de pruebas aprobados de antemano (sección 4.1) |

Contrato, mapeo, sensibles, caché, errores y alta puntual: `docs/fases/FASE_4_Empleados_API.md`.

## 2. Qué se hizo
**Retiro de la 26b (tabla A del plan):**

| Acción | Archivos |
|---|---|
| Borrados | `Application/Empleados/Sincronizacion/` (plan masivo y servicio), `Infrastructure/Persistencia/Empleados/SincronizacionEmpleadosRepositorio.cs` (repositorio masivo y applock `profesiograma:empleados`), sus pruebas (`tests/App.Application.Tests/Empleados/Sincronizacion/`, `SincronizacionEmpleadosSqlTests.cs`), `backend/tests/manual/tarea26b/` y `docs/fases/FASE_4_Sincronizacion_Empleados.md` |
| Restaurados al contenido de HEAD (y luego adaptados) | `Application/DependencyInjection.cs`, `PersistenciaServiceCollectionExtensions.cs`, `Api/Endpoints/EmpleadoEndpoints.cs` (sin `/api/admin/empleados/sincronizar`) |
| Conservados y adaptados | Cliente `FuenteEmpleadosErpHttp` (ahora descarga para la caché; `result` vacío = no disponible), `EmpleadoErp` (movido a `Application/Empleados`), normalización y mapeo (`MapeoEmpleado`), `TimeoutEmpleadosSegundos` y las pruebas de campos sensibles |

**Application:**
- `Empleados/FuenteEmpleados.cs`: `EmpleadoErp`, `ListaEmpleadosErp` (activos válidos, índice por código, `EsAnterior`) e `IFuenteEmpleadosErp` (`ObtenerActivosAsync` en caché y `ObtenerActivosFrescosAsync`).
- `Empleados/MapeoEmpleado.cs`: validez (activo, código ≤ 20, nombre, `cod*` ≤ 10), `Datos` (normalizados y recortados) y `NormalizarCargo`.
- `Empleados/BusquedaEmpleados.cs` (puro): texto sin mayúsculas ni tildes, departamentos por nombre (departamento o unidad), orden por nombre, paginación, `avisoErp`.
- `EmpleadoBusquedaDto(CodigoEkon, NombreCompleto, Cargo, Departamento, Unidad)` (sin `Id`) y `ResultadoBusquedaEmpleadosDto(Items, Pagina, Tamano, Total, AvisoErp)`. `IEmpleadoConsultas.BuscarAsync` devuelve el resultado nuevo; `EmpleadoConsultaServicio` no cambia su lógica.
- `Proyectos/Crear/CatalogoEmpleados.cs`: carga base + API (fresca al registrar), `Validar(empleadoId, codigoEkon, clave, e)` con la transición y sus mensajes, e Id temporales negativos deterministas.
- `EmpleadoRef` → `EmpleadoAsignable(Id, CodigoEkon, NombreCompleto, Puesto, Erp?)`.
- `IDatosReferenciaProyecto`: `ObtenerEmpleadosActivosAsync` → `ObtenerEmpleadosPorIdsAsync` (sin filtro de estado) + `ObtenerIdsPorCodigosAsync`.
- Solicitudes: `CodigoEkon` opcional al final de `PrincipalSolicitud`, `BackSolicitud`, `PrincipalEdicionSolicitud` y `BackEdicionSolicitud` (compatible con los cuerpos actuales).
- **Creación:** `CrearProyectoValidador` (con `IFuenteEmpleadosErp`, `ValidarAsync(..., empleadosFrescos)`); `CrearProyectoServicio` registra con lectura fresca, no consulta cruces externos de Id negativos y responde `Cambiado` (409) ante un duplicado de la alta.
- **Personal y reactivación:** `CalcularAsync` recibe el catálogo (fresco al registrar; dentro del applock se reutiliza el de afuera, sin otra llamada a la API). `CambioPersonal.AltasEmpleados` lleva los empleados de las personas nuevas. `CalculoPersonal` no consulta cruces externos de Id negativos. `ResolverEmpleado` acepta `codigoEkon` (y lo compara al validar una vigente).
- **R7:** "activo" según la API (caché). Si cae: se propone con la advertencia "No se pudo verificar los datos en el ERP." (sin 503).
- `FechaNegocio.Hora` (hora de Ecuador para el aviso).

**Infrastructure:**
- `Erp/CacheEmpleadosErp.cs` (singleton): TTL, descarga única, lista anterior (< 60 min, sin reintentar durante 1 min tras un fallo), lectura fresca, solo registros válidos.
- `Erp/EmpleadosErpSimulado.cs` y `Persistencia/DatosPrueba/EmpleadosPrueba.cs`: definición común de los DEV (la usa también el sembrador, sin cambiar los datos sembrados) y SIM001–SIM200.
- `Erp/ErpServiceCollectionExtensions.cs`: descarga Http (cliente tipado con `TimeoutEmpleadosSegundos`) o Simulado + caché; opciones `CacheEmpleadosMinutos` (10) y `CacheEmpleadosMaxAntiguedadMinutos` (60), validadas.
- `Consultas/EmpleadoConsultas.cs`: busca en la fuente y filtra en memoria; los departamentos del usuario siguen saliendo de la base.
- `Consultas/DatosReferenciaProyecto.cs`: `ConsultaEmpleadosPorIds` y `ConsultaIdsPorCodigos`.
- `Persistencia/Empleados/AltaPuntualEmpleados.cs`: `Preparar` (puro) + `AplicarAsync` (upsert por código, refresco, `CargoInfor`, `SaveChanges`, duplicado → `ConflictoConcurrenciaException`) e `IdReal` (nunca negativo). Lo usan `ProyectoRepositorio.AgregarAsync` y `EdicionPersonalRepositorio.AplicarAsync` antes de crear el personal.
- `Domain/Maestros/Empleado.cs` y `EmpleadoConfiguracion.cs`: comentarios del nuevo significado (P9).

**Api:**
- `GET /api/empleados` devuelve `ResultadoBusquedaEmpleadosDto`.
- `ErpNoDisponibleExceptionHandler`: título "Servicio de empleados no disponible" si `Operacion = "empleados"`.
- `POST /api/proyectos`: 409 "Otro registro se guardó al mismo tiempo; vuelva a intentarlo." si choca la alta.

**Manual (`backend/tests/manual/tarea26d/`):** `buscar.cmd`, `crear-proyecto.cmd` (+ `crear-proyecto.ejemplo.json`), `transicion.cmd`, `conteos.sql`, `.gitignore` y `LEEME.md`. Reutilizan `..\tarea26a\comun.ps1`.

## 3. Archivos
| Tipo | Archivos |
|---|---|
| Creados | `App.Application/Empleados/{FuenteEmpleados,MapeoEmpleado,BusquedaEmpleados}.cs`; `App.Application/Proyectos/Crear/CatalogoEmpleados.cs`; `App.Infrastructure/Erp/{CacheEmpleadosErp,EmpleadosErpSimulado}.cs`; `App.Infrastructure/Persistencia/DatosPrueba/EmpleadosPrueba.cs`; `App.Infrastructure/Persistencia/Empleados/AltaPuntualEmpleados.cs`; pruebas `App.Application.Tests/Empleados/EmpleadosApiTests.cs`, `App.Infrastructure.Tests/Erp/CacheEmpleadosErpTests.cs`, `App.Infrastructure.Tests/Consultas/EmpleadosApiSqlTests.cs`; `backend/tests/manual/tarea26d/*`; `docs/fases/FASE_4_Empleados_API.md`; este reporte |
| Conservados de la 26b y adaptados (sin commit) | `App.Infrastructure/Erp/FuenteEmpleadosErpHttp.cs`, `App.Infrastructure.Tests/Erp/FuenteEmpleadosErpHttpTests.cs`, `docs/tareas/TAREA-26b-reporte.md` (marcado DESCARTADA) |
| Modificados (código) | `App.Api/Configuracion/ErpNoDisponibleExceptionHandler.cs`, `App.Api/Endpoints/{EmpleadoEndpoints,ProyectoEndpoints}.cs`, `App.Application/Comun/FechaNegocio.cs`, `App.Application/Empleados/{EmpleadoConsultaServicio,EmpleadoDtos,IEmpleadoConsultas}.cs`, `App.Application/Proyectos/Crear/{CalculadorCruces,CrearProyectoContratos,CrearProyectoDtos,CrearProyectoServicio,CrearProyectoSolicitud,CrearProyectoValidador,ReglasPersonal}.cs`, `App.Application/Proyectos/Personal/{CalculoPersonal,EdicionPersonalContratos,EdicionPersonalServicio,EdicionPersonalValidador}.cs`, `App.Application/Proyectos/Reactivacion/{ReactivacionServicio,ReactivacionValidador}.cs`, `App.Domain/Maestros/Empleado.cs`, `App.Infrastructure/Consultas/{DatosReferenciaProyecto,EmpleadoConsultas}.cs`, `App.Infrastructure/Erp/{ErpServiceCollectionExtensions,ServiciosExternosOpciones}.cs`, `App.Infrastructure/Persistencia/Configuraciones/EmpleadoConfiguracion.cs`, `App.Infrastructure/Persistencia/DatosPrueba/DatosPruebaSembrador.cs`, `App.Infrastructure/Persistencia/Proyectos/{EdicionPersonalRepositorio,ProyectoRepositorio}.cs` |
| Modificados (pruebas existentes) | Sección 4.1 |
| Modificados (docs) | `00_ESTADO_ACTUAL.md` (§3, §6, §7 pendientes 2 y 39, §8; se conserva todo el cierre de la 26a), `FASE_2_Modelo_Datos.md` (J1 reemplazada, decisión 8 en la sección 7, fila 10), `FASE_5_Crear_Proyecto.md`, `FASE_5_Edicion_Cronograma.md` §8, `FASE_5_Estados_Proyecto.md` §8 |
| Borrados | Ver "Retiro de la 26b" (sección 2) |
| Sin cambios | `backend/tests/manual/tarea26a/comun.ps1` (se conserva el ajuste de la 26b: ruta absoluta de salida) y el frontend |

## 4. Pruebas
### 4.1 Pruebas existentes ajustadas (mecánicas, aprobadas en P14)
| Archivo / prueba | Antes | Ahora |
|---|---|---|
| `Proyectos/Crear/Dobles.cs` (`DatosFalsos`) | `Dictionary<int, EmpleadoRef>` 1..8 y `ObtenerEmpleadosActivosAsync` (el 9 "existe pero inactivo") | `EmpleadoAsignable` 1..9 con `ObtenerEmpleadosPorIdsAsync` y `ObtenerIdsPorCodigosAsync`, más el doble nuevo `EmpleadosErpFalsos` (activos en la API: 1..8). Mismo significado: 1..8 activos, 9 existe e inactivo, 99 no existe |
| Construcción de `CrearProyectoValidador`, `EdicionPersonalServicio` y `ReactivacionServicio` en `ActividadVigenteTests`, `CrearProyectoServicioTests`, `CrearProyectoValidadorTests`, `SnapshotCreacionTests`, `EdicionPersonalC9Tests`, `EdicionPersonalH12Tests`, `EdicionPersonalServicioTests`, `PrincipalOpcionalTests` (4), `ReactivacionServicioTests` y `VersionProyectoTests` (2) | Sin fuente de empleados | Último argumento `new EmpleadosErpFalsos()` |
| `EdicionPersonalServicioTests.CalculadorCruces_Historicos_YInternosDesdeLista` | `Dictionary<int, EmpleadoRef>` | `Dictionary<int, EmpleadoAsignable>` (mismas aserciones) |
| `EmpleadoConsultaServicioTests` (doble `ConsultasFalsas`) | `BuscarAsync` devolvía `PaginaResultado<EmpleadoBusquedaDto>` | `ResultadoBusquedaEmpleadosDto` (mismos campos y `AvisoErp` null); mismas aserciones del filtro |
| `ConsultasExistentesSqlTests.EmpleadoConsultasSqlTests` | `Busqueda_ConTextoYDepartamentos_YPagina_SinDatosSensibles` y `Busqueda_SinFiltros` | **Retiradas** (la búsqueda ya no consulta la base; aprobado) |
| `ConsultasExistentesSqlTests.CrearProyectoConsultasSqlTests.DatosReferencia_GrupoJornadasLimitesEmpleados` | `ConsultaEmpleadosActivos`: `Contains("[EstadoErp] = 'A'")`, `DoesNotContain("[Cedula]")` | `ConsultaEmpleadosPorIds`: `DoesNotContain("[EstadoErp]")`, `DoesNotContain("[Cedula]")` |

**El último ajuste está en el límite de "mecánico":** la aserción del filtro de estado se invierte, porque el diseño aprobado (opción C) quita ese filtro de la base y lo pasa a la API. La parte de la prueba que verifica que no se exponen datos sensibles se mantiene. Lo dejo señalado por si prefiere revisarlo.

Las demás aserciones existentes no cambiaron. En particular, los mensajes "El empleado 9 no existe o no está activo." y "El empleado es obligatorio." siguen en las mismas claves, porque con `empleadoId` el mensaje usa el identificador recibido.

### 4.2 Pruebas nuevas (34)
| Archivo | Casos |
|---|---|
| `FuenteEmpleadosErpHttpTests` (10, de la 26b, adaptadas) | Mapeo de solo los campos permitidos (códigos como número o texto), URL, POST y cuerpo fijo; **datos sensibles**: ningún valor sensible ficticio en el resultado, y ni `EmpleadoErp`, ni `DatosEmpleado`, ni el DTO interno tienen esas propiedades; no disponible ante `isSuccess = false`, `result` nulo, **`result` vacío**, tipo de código no admitido o JSON inválido (sin excepción interna ni fragmentos en el log); HTTP 500 con log Debug sin cuerpo ni URL; error de red; opciones por defecto (60 / 10 / 60) |
| `EmpleadosApiTests` (14) | Mapeo: validez, normalización, recorte y cargo. Búsqueda: sin tildes ni mayúsculas en nombre, apellidos y código; orden, página y total; `soloMisDepartamentos` por departamento o unidad; aviso con la hora de Ecuador. Catálogo: Id real o temporal negativo determinista con datos de la API y lectura en caché en la vista previa; **transición** (solo `empleadoId`, ambos → 400 `codigoEkon`, ninguno → 400 `empleadoId`, Id existente pero inactivo, código inactivo, Id inexistente); sin referencias no llama a la API; API caída se propaga. Creación: vista previa con Id temporal en los tramos y sin cruces externos del temporal; registro con **lectura fresca una sola vez** y alta con los datos de la API; código inactivo → 400; **API caída → 503 sin abrir la transacción ni escribir**. Personal: nueva por código, fresca una vez (no se llama a la API dentro del applock), `AltasEmpleados` con el Id temporal. Reactivación: R7 con la API caída → activo y aviso, sin bloquear |
| `CacheEmpleadosErpTests` (6) | TTL (dentro no descarga, después sí; solo válidos); **descarga única con 10 llamadas simultáneas**; lista anterior con `EsAnterior` (sin reintentar durante 1 min) y error con más de 60 min; lectura fresca que siempre descarga, renueva la caché y nunca usa la anterior; sin registros válidos = no disponible; **Simulado**: DEV001–DEV008 idénticos al sembrador (la alta los deja igual) y 200 SIM válidos en los 7 departamentos, mayoría SIG |
| `EmpleadosApiSqlTests` (4) | `ToQueryString` de `ConsultaIdsPorCodigos` (sin cédula ni correo), `ConsultaPorCodigosSeguimiento` y `ConsultaCargosExistentes`; alta puntual pura (nueva, existente refrescada sin tocar la cédula, cargos nuevos, personas guardadas sin tocar); `IdReal` (**nunca negativo**) |

`ConsultaEmpleadosPorIds` (modificada) tiene su prueba `ToQueryString` en `ConsultasExistentesSqlTests`.

## 5. Comandos y simulación
| Comando | Resultado |
|---|---|
| `dotnet build Profesiograma.slnx -c Release` | 0/0 (una advertencia xUnit1051 intermedia, corregida con `TestContext.Current.CancellationToken`) |
| `dotnet test --solution Profesiograma.slnx -c Release` | 557 tras los ajustes mecánicos; luego 571 y **581/581** con las pruebas nuevas |
| `dotnet ef migrations has-pending-model-changes … --configuration Release` | Sin cambios |
| `npm run build` (frontend) | Compila (no se tocó) |
| `grep "sincronizar\|profesiograma:empleados"` en `backend/src` | 0 |
| `run26d.cmd buscar / crear-proyecto / transicion` (scratchpad; `SIMULAR=1`, respuestas ficticias) | Salida 0. Buscar: total, página, `avisoErp`, códigos y las variantes con y sin tildes. Crear: vista previa con "empleadoId -1 (sin fila…)", registro 201. Transición: a/b 200, c/d 400 con los mensajes esperados. Las salidas y el `crear-proyecto-1.json` de prueba se borraron |

**Errores durante el desarrollo:**
1. **Dependencia de logging:** Application no tiene el paquete de logging; no hizo falta, porque el registro de la caché vive en Infrastructure.
2. **CS9174:** `CatalogoEmpleados.Vacio` con expresiones de colección sobre `IReadOnlyDictionary`; se usaron diccionarios explícitos.
3. **Lanzador de la simulación:** `cmd /c` no encuentra un `.cmd` en la carpeta actual (`NoDefaultCurrentDirectoryInExePath`); se usó un lanzador con la ruta completa.

## 6. Verificación del usuario
Requisitos:
- API reiniciada con este código;
- V1, V2, V3, V9 y V10 en modo **Http**; V8 en modo **Simulado** (`backend/tests/manual/tarea26d/LEEME.md`);
- **no use las pantallas de crear, personal ni reactivar hasta la 26d-2**.

| # | Caso | Pasos | Esperado | ¿Escribe? | Resultado |
|---|---|---|---|---|---|
| V1 | Buscador desde la API | `buscar.cmd` (con `set TEXTO=<apellido con tilde>`) | 200. Con `soloMisDepartamentos=true`, total ≈ 85 (SIG); con `false`, ≈ 1633; `avisoErp` "(ninguno)"; las variantes con y sin tildes dan el mismo total; solo códigos | No | |
| V2 | Caché | Ejecutar `buscar.cmd` dos veces en menos de 10 min y revisar la consola de la API | Una sola línea `ERP empleados: HTTP 200 en … ms` | No | |
| V3 | Crear con empleados reales | `crear-proyecto-1.json` con datos ERP y códigos reales → `crear-proyecto.cmd` → escribir `REGISTRAR` | Vista previa 200 (empleados sin fila con `empleadoId` negativo); registro 201 con id y código | **Sí (proyecto nuevo + filas `Empleado` + `CargoInfor`)** | |
| V8 | Modo Simulado | Quitar `ServiciosExternos:Modo` → reiniciar → `buscar.cmd`; `crear-proyecto.cmd` con códigos `SIM…` y los datos del simulado (9001, DEV-ERP-001, DEV.01, horario 1) | Buscador con DEV001–DEV008 y SIM001…; creación sin cruces. Volver a Http después | **Sí (si registra)** | |
| V9 | Tabla `Empleado` | `conteos.sql` (SSMS, solo lectura) | Solo los DEV y las personas de V3/V8; `NoDevConCedula = 0`; `NoDevSinAsignacion = 0`; cargos nuevos sin código Infor | No | |
| V10 | Transición `empleadoId` / `codigoEkon` | `set EMPLEADO_ID=<Id de V9>`, `set CODIGO_EKON=<código activo>` → `transicion.cmd` | a) y b) 200; c) 400 `principales[0].codigoEkon` "Indique codigoEkon o empleadoId, no ambos."; d) 400 `principales[0].empleadoId` "El empleado es obligatorio." | No | |

**Cubierto por pruebas** (difícil de provocar sin alterar el ERP):
- lista anterior con aviso y error a los 60 min;
- API caída al registrar (503 sin escribir);
- duplicado en la alta (409);
- código con `cod*` largo;
- descarga única con llamadas simultáneas.

Los casos V4–V7 (personal, reactivación, avisos y API caída en pantalla) quedan para la 26d-2 y la 26d-3.

## 7. Pendientes
- **Verificación del usuario** (sección 6). Después: TAREA-26d-2 (frontend) y 26d-3 (avisos y retiro de `empleadoId`).
- **Pendiente 39:** resuelto en Simulado; en Http, tras la verificación.
- **Pendiente 2:** acotado a los puestos de las personas asignadas.
- [PENDIENTE DE CONFIRMAR] Fase 3: si un cuerpo con `estado` vacío devuelve también a los inactivos (alta de personas históricas de SharePoint).
