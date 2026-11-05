# TAREA-62-1 — Reporte: criterio de personal elegible, backend

**Fecha:** 27/10/2026 · **Fase:** B (implementación del plan de la TAREA-62) · **Estado:** ✅ Verificada por el usuario el 28/10/2026

## 1. Qué se hizo
- **Regla única** `ReglasPersonalElegible.EsAsignable(EmpleadoErp, familias)` (Application/Empleados): lista vacía = todos; sin familia con filtro = no asignable; comparación de `ComparacionEmpleadosErp.Iguales`. Mensaje «El empleado {código} no pertenece a un grupo asignable.».
- **Lectura sin caché** `IDatosReferenciaProyecto.ObtenerFamiliasAsignablesAsync` (`DatosReferenciaProyecto.LeerFamiliasAsignablesAsync` + `SepararLista`; vacío o ausente = sin filtro con un Warning). La reutiliza el buscador.
- **Buscador:** `BusquedaEmpleados.Buscar(lista, filtro, familias)` filtra antes de paginar y se suma al filtro de departamentos. `ResultadoBusquedaEmpleadosDto` agrega `FamiliasAsignables` (opcional; el buscador siempre la llena).
- **Personas nuevas:** `CatalogoEmpleados.CargarAsync(…, exentos)` lee el parámetro después de la API y `Validar` responde 400 después de «no existe o no está activo». `CatalogoEmpleados.NoEsAsignable` sirve a `NovedadValidador` (mismo mensaje en `codigoEkon`; el parámetro se lee solo si el resto es válido).
- **Exención (P3):** `EdicionPersonalServicio` y `ReactivacionServicio` pasan `proyecto.Personal` (vigentes e históricas, incluido el principal de R7). Crear proyecto y novedades no tienen exención. La sincronización de PERMISO MEDICO no se tocó: no filtra (P2, con prueba).
- **Administración (`Politica.Admin`):** `GET /api/admin/empleados/familias` y `PUT /api/admin/empleados/familias-asignables` (`CriterioPersonalServicio` en Application; `CriterioPersonalRepositorio` con `ExecuteUpdate` compare-and-set, `ModificadoPorId` y `FechaModificacion` explícitos; log Information en el endpoint).
- **Modo Simulado:** SIM con i % 10 == 0 (20) = `OPERATIVO`. DEV y el sembrador sin cambios.
- **Documentación:**
  - `FASE_4_Empleados_API.md`: §10 nueva (regla, exención, endpoints, mensajes, decisiones P2–P8) y §7.
  - `FASE_5_Novedades.md` §2: el 400 nuevo.
  - `00_ESTADO_ACTUAL.md`: 24d ✅, 62-0 cerrada, 62-1 implementada, 62-2 siguiente, pendientes 62 (resumido), 68 y 69. Pesa **24 986 B**; lo reemplazado va textual a `HISTORIAL.md` H11, con 0 líneas perdidas.
  - `TAREA-24d-reporte.md` marcada verificada.

## 2. Archivos tocados
| Archivo | Cambio |
|---|---|
| `src/App.Application/Empleados/ReglasPersonalElegible.cs`, `CriterioPersonal.cs` | Nuevos (regla; DTO, repositorio y servicio de administración) |
| `src/App.Application/Empleados/BusquedaEmpleados.cs`, `EmpleadoDtos.cs` | Filtro y `FamiliasAsignables` |
| `src/App.Application/Proyectos/Crear/CatalogoEmpleados.cs`, `CrearProyectoContratos.cs` | Exentos, no asignables, `ObtenerFamiliasAsignablesAsync` |
| `src/App.Application/Proyectos/Personal/EdicionPersonalServicio.cs`, `Reactivacion/ReactivacionServicio.cs`, `Novedades/NovedadValidador.cs` | Exención y mensaje |
| `src/App.Application/DependencyInjection.cs`, `src/App.Infrastructure/Persistencia/PersistenciaServiceCollectionExtensions.cs` | Registro |
| `src/App.Infrastructure/Consultas/DatosReferenciaProyecto.cs`, `EmpleadoConsultas.cs` | Lectura sin caché con Warning (logger nuevo en el constructor) |
| `src/App.Infrastructure/Persistencia/Empleados/CriterioPersonalRepositorio.cs` | Nuevo (ExecuteUpdate) |
| `src/App.Infrastructure/Persistencia/DatosPrueba/EmpleadosPrueba.cs` | SIM OPERATIVO |
| `src/App.Api/Endpoints/EmpleadoEndpoints.cs` | GET y PUT de administración |
| `tests/…/Empleados/PersonalElegibleTests.cs`, `…/Consultas/PersonalElegibleSqlTests.cs`, `…/App.Api.Tests/CriterioPersonalEndpointsTests.cs` | Nuevos |
| `tests/…/Proyectos/Crear/Dobles.cs`, `tests/…/Novedades/NovedadDobles.cs` | Dobles: `FamiliasAsignables` (vacío por defecto) y `Datos` expuesto en `EscenarioNovedad` |
| `backend/tests/manual/tarea62/criterio.cmd`, `.ps1`, `LEEME.md`, `simulacion/*` | Script manual (no ejecutado) |

## 3. Comandos y resultado (desde `backend/`)
| Comando | Resultado |
|---|---|
| `dotnet build Profesiograma.slnx -c Release` | Compilación correcta, 0 advertencias |
| `dotnet test --solution Profesiograma.slnx -c Release` | **1074/1074** (1039 + 35). Ninguna prueba existente se ajustó |
| `dotnet ef migrations has-pending-model-changes … --configuration Release` | «No changes have been made to the model since the last migration.» (sin migración) |

## 4. Desviaciones, errores y decisiones
- **Fila del parámetro ausente:** el GET la trata como vacía (`valorActual` "", `sinCriterio` true). El PUT, como el `ExecuteUpdate` afecta 0 filas, responde **409** con el mismo mensaje. Propuesta: dejarlo así, porque la fila está en las semillas (Id 8). Si se quiere distinguir el caso, un 409 «El parámetro EMPLEADO_FAMILIAS_ASIGNABLES no existe.» costaría una lectura previa. [PENDIENTE DE DECISIÓN DEL USUARIO]
- **El mensaje usa el código tal como llegó** (por ejemplo «dev007»), igual que «no existe o no está activo».
- **El PUT informa todos los errores de una vez:** revisa la existencia de las familias bien formadas aunque otra tenga un error de forma (caso c09).
- La comparación `Valor = valorAnterior` sigue la collation de la base (`Modern_Spanish_CI_AS`): sin distinguir mayúsculas.
- **Error y corrección:** las 2 pruebas de `CompletarCedulasTests` fallaron porque su host no registra el servicio nuevo y ASP.NET lo infirió como cuerpo. Se corrigió con `[FromServices]` en los endpoints, sin tocar esas pruebas.
- **Dobles:** `DatosFalsos.FamiliasAsignables` está vacío por defecto (sin filtro), así las pruebas anteriores no cambian. `DatosReferenciaProyecto` y `EmpleadoConsultas` reciben un `ILogger` en el constructor; DI lo resuelve.
- Sin frontend (62-2) ni cambios en la comparación con el ERP (pendiente 69).

## 5. Verificación para el usuario
Requisito: la API en marcha con `ServiciosExternos:Modo = Simulado`. Instrucciones completas en `backend/tests/manual/tarea62/LEEME.md`.

| # | Caso | Esperado |
|---|---|---|
| V1 | `criterio.cmd` (MODO REAL) | c00–c10b con CONTROL OK; «Casos con FALLA: 0; OMITIDOS: 0»; restauración del valor original |
| V2 | SQL de solo lectura del LEEME (Parametro) | `ModificadoPorId` = admin; `FechaModificacion` posterior a la hora de c04; `Valor` = el original |
| V3 | `tarea23-2\limpiar.cmd` con el Id de c06 | La novedad de 2070 queda anulada |
| V4 | Log de la API | Un Information por cada PUT 200 (anterior → nuevo, Id del admin); sin nombres ni cédulas |
| V5 | Regresión: «Nuevo proyecto» con SIM011 y novedad PERSONA de DEV001 | Funcionan igual (ADMINISTRATIVO) |

## 6. Nota de la verificación (28/10/2026)
- Las corridas 100 y 101 de `criterio.cmd` y sus anomalías (valor original distinto de la semilla, 409 en c06) se debieron a una **caída del script al restaurar**: `JavaScriptSerializer` no pudo serializar los `PSObject` que un pipeline dejaba en el cuerpo del PUT (`tarea23-2sos.ps1:12`). Cada caída dejaba el criterio cambiado y la novedad de c06 activa.
- Se corrigió en el script: cuerpo plano (`[string[]]`), `try/catch` general con banner de ejecución incompleta, candado de ejecución, archivo de estado, c05b/c06b y autolimpieza (`l06_anular`). **El backend no tuvo defecto.**
- Corrida 170: 0 FALLA; auditoría (V2) y restauración correctas. V5 (regresión) pasa a la 62-2 como A10.

## 7. Pendientes
- Verificación (§5) y decisión sobre el 409 con la fila ausente (§4).
- 62-2 (frontend: menú «Administración», `/admin/personal-elegible`, grupos en el buscador).
- Pendientes 68 (Fase 3) y 69 (comparación con el ERP).
