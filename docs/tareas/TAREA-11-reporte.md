# TAREA-11 — Catálogos del ERP (Simulado y Http) + búsqueda de empleados

**Fecha:** 2026-09-30
**Resultado:** completada. Todo es de solo lectura y no cambia el modelo.
- `dotnet build -c Release`: **0 advertencias, 0 errores**.
- `dotnet test`: **123/123 correctas**.
- `has-pending-model-changes`: sin cambios.
- **Pruebas HTTP E1–E11 correctas**, y P8 y P9 de la TAREA-07 repetidas tras el refactor, también correctas.
- No se ejecutaron `dotnet run` (la API la reinició el usuario), migraciones ni SQL manual. No se tocó `frontend/` ni se usaron comandos git que modifiquen el repositorio.

## 1. Fase A — Campos reales del ERP

### 1.1 Del flujo `docs/origen/sharepoint/esquemas/ConsultarProyecto.json` (Parse JSON / Select)
| Operación | Endpoint | Forma | Campos usados |
|---|---|---|---|
| Compañías | `GET :7048/api/Company/list_company` | Envoltura `{statusCode, isSuccess, errorMessages, result[]}` | `companyId:int`, `name`, `tradeName?`, `ruc?` |
| Proyectos | `GET :7048/api/Project/GetProject/{companyId}` | Arreglo directo | `projectId`, `description`, `status` (además: `companyId`, `sectorId`, `sectorDescription`, `technicalOfficeId/Name`, `uploadDate`) |
| Dimensiones | `GET :7048/api/Uegp/GetUegpCompany/{companyId}` | Arreglo directo | `uegpId`, `description` |
| Actividades | `GET :7055/api/Activity/GetActivitiesProject/{projectId}/{companyId}` | Envoltura | `activityId`, `description`, `activityType`. Si el HTTP no es 200, el flujo respondía `[]` |
| Horarios | `GET :7055/api/PayrollSchedule/ListPayrollSchedule` | Envoltura | `codHorario:int`, `descripcion`, `horaEntrada`, `horaSalida`, `horas`, `horasTrab`, `tipoHorario`, `status` |

### 1.2 Verificación contra el ERP real (GET de solo lectura, autorizados; solo estructura)
Se usó un script con **validación de certificados activada**. Solo imprimió nombres de campos, tipos, patrones y valores de `status`. **No se imprimió ni se guardó ningún dato real** (nombres, RUC, Id).

| Verificación | Resultado |
|---|---|
| TLS de `backstack.sedemi.com` (7048 y 7055) | **Válido** (HTTP 200 con validación estándar) |
| `list_company` | Envoltura confirmada. `companyId:int`, `name:str`, `ruc:str\|null`, `tradeName:str\|null` |
| `ListPayrollSchedule` | Envoltura confirmada. Horas `horaEntrada`, `horaSalida`, `horas` y `horasTrab` **siempre con el patrón `9999` (HHmm)**. `status` solo **A / I**. `tipoHorario` solo **M / D** (códigos) |
| `GetProject` (1.ª compañía) | 0 proyectos. No permitió confirmar `status` |
| `GetProject` (compañías 2.ª a 6.ª, autorización adicional) | Valores de `status`: **Activo** (657), Cerrado (687), Libre (52), Terminado (34), Borrado (1). La 6.ª no tenía proyectos. **"Activo" confirmado** tal cual, sin variantes de mayúsculas |

La app original filtra igual (`GestionProyecto.pa.yaml` línea 412: `Filter(colProyectosApi, Text(ThisRecord.Value.status) = "Activo")`), así que **el filtro no cambia**.

### 1.3 Nombre de la compañía (app original, `GestionProyecto.pa.yaml` líneas 203–222)
El desplegable mostraba `name` y guardaba `Coalesce(tradeName, name)`, con el comentario "Razón Social". Por eso:
- `CompaniaErp.Nombre = tradeName ?? name` (razón social);
- `NombreCorto = name`.

### 1.4 Decisiones aprobadas
| # | Decisión |
|---|---|
| 1 | GET de solo lectura al ERP (solo estructura) |
| 2 | `CompaniaErp(Id, Nombre, NombreCorto, Ruc)` |
| 3 | NuGet `Microsoft.Extensions.Http` 10.0.12. `Options.ConfigurationExtensions` **no fue necesario**: las opciones se leen con el indexador de `IConfiguration` |
| 4 | Nuevos proyectos `tests/App.Application.Tests` y `tests/App.Infrastructure.Tests` (xUnit v3 4.0.1 sobre MTP) |
| 5 | 404 si la compañía o el proyecto ERP no existe |
| 6 | Refactor de la lectura de parámetros a `LectorParametros` |

## 2. Qué se hizo

### Application
| Archivo | Contenido |
|---|---|
| `Comun/LectorParametros.cs` | `LeerPagina`, `LeerTamano`, `LeerFecha`, `LeerBooleano` y `Normalizar`, con mensajes en español **idénticos** a los de la TAREA-07 |
| `Proyectos/ProyectoConsultaServicio.cs` | Refactor: usa `LectorParametros` (se eliminaron los métodos privados duplicados). Sin cambio de comportamiento |
| `Erp/ErpDtos.cs` | `CompaniaErp`, `ProyectoErp`, `DimensionErp`, `ActividadErp`, `HorarioErp` |
| `Erp/ICatalogoErp.cs` | `Listar*` y `Obtener*` (por Id, para la TAREA-12) |
| `Erp/ErpNoDisponibleException.cs` | Excepción que la API traduce a 503 |
| `Erp/CatalogoErpConsultaServicio.cs` | Verifica la compañía (y el proyecto, en actividades) antes de listar → `ResultadoErp<T>` con motivo de 404 |
| `Empleados/EmpleadoDtos.cs`, `IEmpleadoConsultas.cs`, `EmpleadoConsultaServicio.cs` | Búsqueda. **La regla `soloMisDepartamentos` vive aquí**: `true` por defecto; departamentos del usuario; sin departamentos, todos; `false`, todos; un valor inválido, 400 |
| `DependencyInjection.cs` | + `EmpleadoConsultaServicio` y `CatalogoErpConsultaServicio` |

### Infrastructure
| Archivo | Contenido |
|---|---|
| `Erp/ServiciosExternosOpciones.cs` | `Modo` (Http \| Simulado), `ErpBase7048`, `ErpBase7055`, `TimeoutSegundos` (15) |
| `Erp/ReglasErp.cs` | Filtros "Activo" y "A" (sin distinguir mayúsculas); `tradeName ?? name`; horas `HHmm` o `HH:mm` → `TimeOnly`; `HHmm` → minutos. Compartido por Http y Simulado |
| `Erp/CatalogoErpBase.cs` | `Obtener*` a partir de `Listar*` (una sola definición) |
| `Erp/CatalogoErpHttp.cs` | `HttpClient` tipado (timeout), URIs absolutas con las dos bases y System.Text.Json. **Caché `IMemoryCache`**: compañías y horarios 10 min, resto 5 min; solo respuestas correctas. **Errores:** red, TLS, timeout, código HTTP no exitoso o JSON inválido → `ErpNoDisponibleException`. En actividades, 404 → `[]`, como el flujo original. La cancelación del llamador no se convierte. **La validación de certificados no se modificó** |
| `Erp/CatalogoErpSimulado.cs` | Datos fijos (sección 3) con los mismos filtros que Http |
| `Erp/ErpServiceCollectionExtensions.cs` | `AddCatalogoErp(configuration, esDesarrollo)`. **Simulado fuera de Development → `InvalidOperationException`**: la API no arranca. También falla con un modo desconocido, bases que no sean https absolutas o un timeout fuera de 1–120 |
| `Consultas/EmpleadoConsultas.cs` | `EstadoErp = "A"`, texto en `CodigoEkon`/`NombreCompleto`, departamentos **por nombre** contra `Departamento` o `Unidad` (`IN` en SQL, collation CI), orden por nombre, `AsNoTracking`, paginación en SQL. Proyección **sin cédula ni correo** |
| `DependencyInjection.cs` | `AddInfrastructure(configuration, esDesarrollo)` + `AddCatalogoErp` |
| `Persistencia/PersistenciaServiceCollectionExtensions.cs` | + `IEmpleadoConsultas → EmpleadoConsultas` |
| `App.Infrastructure.csproj` | + `Microsoft.Extensions.Http` 10.0.12; `InternalsVisibleTo App.Infrastructure.Tests` |

### Api
| Archivo | Contenido |
|---|---|
| `Endpoints/ErpEndpoints.cs` | `/api/erp/companias`, `…/{id}/proyectos`, `…/{id}/dimensiones`, `…/{id}/proyectos/{proyectoErpId}/actividades`, `/api/erp/horarios` (política Gestor) |
| `Endpoints/EmpleadoEndpoints.cs` | `GET /api/empleados` (Gestor) → 200 `PaginaResultado` o 400 `ValidationProblem` |
| `Configuracion/ErpNoDisponibleExceptionHandler.cs` | `IExceptionHandler` → **503** ProblemDetails "Servicio ERP no disponible" (sin detalles internos) |
| `Program.cs` | `AddInfrastructure(builder.Configuration, builder.Environment.IsDevelopment())`, `AddExceptionHandler<ErpNoDisponibleExceptionHandler>()`, `MapErpEndpoints()`, `MapEmpleadoEndpoints()` |
| `appsettings.json` | `ServiciosExternos`: `Http`, `https://backstack.sedemi.com:7048`, `…:7055`, `TimeoutSegundos` 15 |
| `appsettings.Development.json` | `ServiciosExternos`: `Simulado`. Nota: al reescribir el JSON, los arreglos `Roles` de DevAuth quedaron en varias líneas (formato; mismo contenido) |
| `Profesiograma.Dev.http` | + E1–E11 (con E6b y E8b) |

### Solución
- `Profesiograma.slnx`: + `tests/App.Application.Tests` y `tests/App.Infrastructure.Tests`.

## 3. Datos del modo Simulado (coherentes con PRY-DEV-0001..0003)
| Catálogo | Datos |
|---|---|
| Compañías | 9001 · name `PRUEBA` · tradeName `COMPAÑÍA DE PRUEBA S.A.` · RUC 0999999999001 → `Nombre` = COMPAÑÍA DE PRUEBA S.A. (igual que la tabla `Compania`) |
| Proyectos 9001 | DEV-ERP-001 "PROYECTO ERP DE PRUEBA" **Activo**; DEV-ERP-002 "PROYECTO ERP INACTIVO DE PRUEBA" **Inactivo** (se filtra) |
| Actividades | DEV-ERP-001 → DEV.01 "ACTIVIDAD DE PRUEBA" (tipo PRUEBA) |
| Dimensiones 9001 | DEV-DIM-01 PLANTA DE PRUEBA · DEV-DIM-02 OFICINAS DE PRUEBA |
| Horarios (formato ERP) | 1 "07:00 - 18:00 (PRUEBA)" `0700`/`1800`/`1100`/`1000`, tipo null (igual que PRY-DEV-0001), A · 2 "08:00 - 17:00 (PRUEBA)" 540/480 min, tipo M, A · 3 "06:00 - 14:00 (INACTIVO)" **I** (se filtra) |

## 4. Pruebas unitarias
| Proyecto | Pruebas | Qué cubren |
|---|---|---|
| App.Domain.Tests | 25 | Sin cambios (TAREA-10) |
| **App.Application.Tests** | 34 | `LectorParametros` (valores válidos, mensajes exactos de pagina, tamano, fecha y booleano, normalización). `EmpleadoConsultaServicio` con dobles: soloMis por defecto usa los departamentos del usuario; `false` no los consulta; usuario sin departamentos o sin usuario → todos; texto recortado con página y tamaño; parámetros inválidos → 3 errores en español sin consultar. `CatalogoErpConsultaServicio`: 404 de compañía y de proyecto, y listas cuando existen |
| **App.Infrastructure.Tests** | 64 | `ReglasErp`: horas `0700`/`07:00`/`7:05`, inválidas (`2400`, `0760`, `700`…), minutos, filtros y nombre. `CatalogoErpHttp` con `HttpMessageHandler` falso y datos **ficticios** con la forma real del JSON: mapeo de las 5 APIs y la URL exacta de cada una (actividades `{projectId}/{companyId}`); filtro Activo y A; actividades 404 → `[]`; 400/500/502/503 → excepción; red → excepción; timeout → excepción; cancelación del llamador no se convierte; JSON inválido, vacío o `null` → excepción; caché (una sola solicitud); un error no se cachea; `Obtener*`. `CatalogoErpSimulado`: datos. Registro: Simulado fuera de Development lanza, en Development usa el Simulado, Http usa el cliente Http, configuraciones inválidas lanzan |
| **Total** | **123** | |

```
Resumen de la serie de pruebas: Correcta!
  total: 123
  error: 0
  correcto: 123
  omitido: 0
  duración: 2s 968ms
```

## 5. Pruebas HTTP (API reiniciada por el usuario; Development = Simulado)
| Prueba | Esperado | Obtenido | Resultado |
|---|---|---|---|
| E1 gestor `GET /api/erp/companias` | Incluye 9001 | 200 `[{id 9001, nombre "COMPAÑÍA DE PRUEBA S.A.", nombreCorto "PRUEBA", ruc "0999999999001"}]` | OK |
| E2 gestor `…/companias/9001/proyectos` | Solo el Activo | 200 `[DEV-ERP-001 "PROYECTO ERP DE PRUEBA" Activo]` | OK |
| E3 `…/proyectos/DEV-ERP-001/actividades` | DEV.01 | 200 `[DEV.01 "ACTIVIDAD DE PRUEBA" PRUEBA]` | OK |
| E4 `…/companias/9001/dimensiones` | 2 | 200: DEV-DIM-01, DEV-DIM-02 | OK |
| E5 `GET /api/erp/horarios` | Solo activos (2) | 200: códigos 1 (07:00–18:00, 660/600) y 2 (08:00–17:00, 540/480) | OK |
| E6 `…/companias/1234/proyectos` | 404 | **404** ProblemDetails "Compañía no encontrada." | OK |
| E6b `…/9001/proyectos/DEV-ERP-002/actividades` (inactivo) | 404 | **404** "Proyecto ERP no encontrado o no está activo." | OK |
| E7 gestor `GET /api/empleados` | 8 (DEV001–DEV008) | 200, total 8, DEV001…DEV008 | OK |
| E8 `?texto=DEV003` | 1 | 200, total 1: DEV003 | OK |
| E8b `?soloMisDepartamentos=false` | Todos | 200, total 8 | OK |
| E9 Claves del item | Sin cédula ni correo | `cargo`, `codigoEkon`, `departamento`, `id`, `nombreCompleto` | OK |
| E10 anonimo | 401 | 401 | OK |
| E11 `?tamano=500` | 400 | 400: `tamano`: "El tamaño de página debe ser un número entero entre 1 y 100." | OK |
| E12 (extra) `?soloMisDepartamentos=quizas` | 400 | 400: "El valor de 'soloMisDepartamentos' debe ser true o false." | OK |
| **P8** (TAREA-07) admin `/api/proyectos?tamano=500` | 400, mismo mensaje | 400: "El tamaño de página debe ser un número entero entre 1 y 100." | OK |
| **P9** (TAREA-07) admin `?desde=2026-09-30&hasta=2026-09-20` | 400, mismo mensaje | 400: "La fecha 'desde' no puede ser posterior a la fecha 'hasta'." | OK |
| P1 (control) admin `/api/proyectos` | total 3 | total 3 | OK |

**Justificación del 404 en E6:** distingue "la compañía no existe" (error del cliente) de "la compañía existe y no tiene proyectos" (lista vacía). Además, la verificación usa la lista de compañías en caché, así que no se consulta al ERP con Id arbitrarios.

## 6. Comandos ejecutados y resultado
| Comando | Resultado |
|---|---|
| Lectura del flujo `ConsultarProyecto.json` y del YAML de la app original | Sección 1 |
| Script Python de solo lectura al ERP (3 GET + 1 `list_company` y 5 `GetProject` autorizados después) | Sección 1.2; ningún dato real impreso |
| `dotnet build Profesiograma.slnx -c Release` | 0 advertencias, 0 errores |
| `dotnet test --solution Profesiograma.slnx -c Release` | 123/123 |
| `dotnet ef migrations has-pending-model-changes … --configuration Release` | "No changes have been made to the model since the last migration." |
| `curl -k -sS -i -H "X-Dev-User: …" https://localhost:7180/…` | Sección 5 |

## 7. Errores y correcciones
Ninguno de compilación ni de pruebas. `Microsoft.Extensions.Options.ConfigurationExtensions` no se agregó porque no hizo falta.

## 8. Pendientes
- **Menor:** el sembrador guardó `ProyectoErpEstado = "ABIERTO"` en PRY-DEV-0001, pero el ERP usa `status = "Activo"` (confirmado). No se cambió, según lo indicado. [PENDIENTE]
- `tipoHorario` del ERP son códigos (`M`, `D`). Su significado no está documentado (¿mensual/diario?) [PENDIENTE DE CONFIRMAR]. Hoy se expone tal cual en `HorarioErp.Tipo`.
- El modo Http se probó con pruebas unitarias (handler falso). **La API no se ejecutó en modo Http contra el ERP real**: Development usa Simulado. Queda para un entorno de pruebas o QA.
- Otros valores de `status` de proyectos en el ERP (Libre, Cerrado, Terminado, Borrado) quedan excluidos, igual que en la app original.
- **TAREA-12:** usar `ICatalogoErp.Obtener*` al registrar, hacer el upsert de `Compania` (`Nombre` → `Nombre`, `NombreCorto` → `NombreComercial`, `Ruc`) y buscar empleados con `IEmpleadoConsultas`.
