# TAREA-23-3 — Sincronización manual de PERMISO MEDICO (backend)

**Fecha:** 2026-10-22 (Fase B; plan de la Fase A aprobado con P1–P6 y los ajustes A1–A6).
**Resultado:** implementada; **pendiente de la verificación del usuario** con `backend/tests/manual/tarea23-3/sincronizacion.cmd`.
- `dotnet build Profesiograma.slnx -c Release`: **0 advertencias, 0 errores** (esta vez sin bloqueo de `bin\Release`; antes se compiló también con `--artifacts-path`).
- `dotnet test --solution Profesiograma.slnx -c Release`: **1039/1039** = 984 anteriores + 55 nuevas; 0 ajustadas.
- `dotnet ef migrations has-pending-model-changes … --configuration Release`: «No changes have been made to the model since the last migration.» **Sin migración.**
- **Sin paquetes NuGet nuevos:** ningún `.csproj` cambió; el CSV se lee con `Microsoft.VisualBasic.FileIO.TextFieldParser`, que viene en .NET 10 (P6; no hizo falta un parser propio).
- `ToQueryString`: las 6 consultas nuevas (`ConsultaExistentesPorUid`, `ConsultaCodigos`, `ConsultaLegacyIds`, `ConsultaEmpleadosPorCorreo`, `ConsultaUidsPermisosMedicos`, `ConsultaParaSincronizar`).

**Lo que no se hizo:** Graph (23-4), frontend (TAREA-24), carga masiva (Fase 3); ejecutar la API, clientes HTTP o el script; SQL; git que modifique el repositorio; graphify. **No se leyó ni se creó ningún CSV real** de la lista NOVEDADES ASISTENCIA (A6).

## 1. Qué se hizo
- **Application/Novedades/Sincronizacion (nuevo):**
  - `FuenteNovedadesExternas.cs`: puerto `IFuenteNovedadesExternas` (con `Fuente` y `AdmiteEscenarios`), `FilaNovedadExterna`, `LecturaNovedadesExternas`, `FuenteNovedadesNoDisponibleException` (no configurada / no disponible) y `FuenteNovedades` (`MaxFilas` = 5000, escenarios).
  - `MapeoPermisoMedico.cs`: mapeador puro (reutilizable en la Fase 3), `PermisoMedicoMapeado` (su `ToString` no muestra el correo), `OmisionSincronizacion`, `CategoriaOmision`.
  - `PlanSincronizacion.cs`: `Clasificar` y `Armar` (puros).
  - `SincronizacionContratos.cs`: `ResultadoSincronizacionDto`, `NovedadExistente`, `EmpleadoPorCorreo`, `ItemSincronizacion`, `LoteSincronizacion`, `ResultadoLote` y el puerto `ISincronizacionNovedades`.
  - `SincronizacionPermisosMedicosServicio.cs`: R3, A4, P2, lotes de 100 y `Parcial` para el log.
  - `FechaNegocio.AUtc` (hora de Ecuador → UTC).
- **Infrastructure:**
  - `NovedadesExternas/NovedadesExternasServiceCollectionExtensions.cs`: modos (H1) y `FuenteNovedadesNoConfigurada`.
  - `NovedadesExternas/FuenteNovedadesSimulada.cs`: 10 filas ficticias, escenarios.
  - `NovedadesExternas/FuenteNovedadesArchivoLocal.cs`: fuente y `LectorCsvSharePoint` (puro).
  - `Persistencia/Novedades/SincronizacionNovedadesRepositorio.cs`: consultas por bloques de 500 y `AplicarLoteAsync` (alta puntual con cédula D15, A1, insertar, actualizar, anular, reactivar).
  - Registro en `DependencyInjection` y `AddPersistencia`.
- **Api:** `POST /api/admin/novedades/sincronizar-permisos-medicos` en `NovedadEndpoints` (log Information con conteos; Warning con los conteos parciales si un lote falla). `FuenteNovedadesNoDisponibleExceptionHandler` registrado en `AddErroresApi`. `appsettings.Development.json` con `"NovedadesExternas": { "Modo": "Simulado" }`.
- **Scripts:** `backend/tests/manual/tarea23-3/` (`sincronizacion.cmd/.ps1`, `LEEME.md`, `.gitignore`, `simulacion/` con 19 respuestas ficticias); reutiliza `tarea23-2/casos.ps1`.
- **Documentación:** `FASE_5_Novedades.md` (estado de las subtareas y §8), `00_ESTADO_ACTUAL.md` (cabecera, hoja de ruta, 23-2 ✅ con la línea base, fila 23-3, pendiente 64), `CLAUDE.md` («Datos sensibles»: los CSV de NOVEDADES ASISTENCIA) y este reporte.

## 2. Archivos
**Nuevos:**
- `backend/src/App.Application/Novedades/Sincronizacion/` (5 archivos)
- `backend/src/App.Infrastructure/NovedadesExternas/` (3 archivos)
- `backend/src/App.Infrastructure/Persistencia/Novedades/SincronizacionNovedadesRepositorio.cs`
- `backend/src/App.Api/Configuracion/FuenteNovedadesNoDisponibleExceptionHandler.cs`
- `backend/tests/App.Application.Tests/Novedades/Sincronizacion/SincronizacionDobles.cs` y `SincronizacionTests.cs`
- `backend/tests/App.Infrastructure.Tests/Persistencia/SincronizacionNovedadesTests.cs`
- `backend/tests/App.Api.Tests/SincronizacionEndpointsTests.cs`
- `backend/tests/manual/tarea23-3/*`, `docs/tareas/TAREA-23-3-reporte.md`

**Modificados:**
- `backend/src/App.Application/Comun/FechaNegocio.cs`, `App.Application/DependencyInjection.cs`
- `backend/src/App.Infrastructure/DependencyInjection.cs`, `Persistencia/PersistenciaServiceCollectionExtensions.cs`
- `backend/src/App.Api/Endpoints/NovedadEndpoints.cs`, `Configuracion/ErroresApiExtensions.cs`, `appsettings.Development.json`
- `docs/fases/FASE_5_Novedades.md`, `docs/00_ESTADO_ACTUAL.md`, `CLAUDE.md`

## 3. Pruebas nuevas (55)
| Proyecto | Pruebas |
|---|---|
| App.Application.Tests (34) | **Mapeador:** tres formas de EKON y correo de respaldo; EKON normalizado (4, con puntos de miles); 9 omisiones (JSON inválido o no objeto, tipo, origen, aplicación, sin empleado, fecha ilegible, rango de 367 días, sin fechas); Uid ilegible con «fila ID n»; sin días (expande el rango), tipo con guion y tilde, estado; `FechaRegistro` Ecuador → UTC e ilegible; código generado determinista; lista blanca sin `Persona.*`, en orden estable. **Plan:** las seis acciones de `Clasificar`; `Armar` con conflictos, Uid repetido, Uid de PROFESIOGRAMA, sin empleado y no activa nueva. **Servicio (dobles):** R3 (la fuente y la base antes de la transacción, sin API si todos tienen fila); fuente no configurada y más de `MaxFilas` sin escribir; API caída sin escribir; A4 (API una vez, alta con cédula ficticia, sin fila e inactivo → `sinEmpleado`); idempotencia (0 transacciones); lotes 100/100/50; A1; P2 (fila única, ambigua, API → código con fila); resultado (filas ilegibles, ausentes, códigos, fecha); escenario 400 (2) y normalizado |
| App.Infrastructure.Tests (18) | `LectorCsvSharePoint` con y sin BOM y `ListSchema` (4), comillas escapadas y salto de línea dentro del campo; fila corta, línea vacía, `Title`, ID ilegible; sin columnas esperadas (3); `ArchivoLocal` sin ruta o inexistente (sin la ruta en el mensaje); fuente simulada (categorías, escenarios deterministas, LegacyId, códigos; correo de la fila 7 = DEV004); `ToQueryString` (6 consultas); `Nueva` (R5) y `Asignar` (sin cambios → `Unchanged`; cambio de fechas → diff de días) |
| App.Api.Tests (3) | 200 admin con conteos, 403 gestor, 401; 503 «no configurada»; 400 `escenario` con una fuente que no lo admite |

## 4. Comandos y resultados
| Comando (desde `backend/`) | Resultado |
|---|---|
| `dotnet build … --artifacts-path <scratchpad>` (durante el desarrollo) | 0 advertencias, 0 errores |
| `dotnet test … --artifacts-path … --no-build` (1.ª) | 1031/1039: 7 de mis pruebas nuevas (§5) + `EscritorReporteExcelTests` (solo por la ruta de artefactos) |
| Ídem (2.ª) | 1038/1039: solo `EscritorReporteExcelTests` (entorno) |
| `dotnet test --project tests/App.Infrastructure.Tests -c Release` | 252/252 |
| `dotnet ef migrations has-pending-model-changes … --configuration Release` | sin cambios de modelo |
| `dotnet build Profesiograma.slnx -c Release` (salida normal) | 0 errores (el `bin\Release` ya no estaba bloqueado) |
| `dotnet test --solution Profesiograma.slnx -c Release` (salida normal) | **1039/1039** |
| Simulación en una COPIA en el scratchpad (`SIMULAR=1`) | normal: 20 `CONTROL OK`, «FALLA: 0; OMITIDOS: 0»; con s02b y s03 alteradas: 2 FALLA detectadas; con s00 en 400 (fuente no simulada): aviso, 11 OMITIDOS y s07/s08 OK. Copia borrada |

## 5. Errores y cómo se resolvieron (todos en pruebas nuevas; el código de producción no cambió por ellos)
- Tuplas que contenían arreglos se comparaban por referencia: se separaron las aserciones (2).
- App.Api.Tests: al mapear todas las rutas de novedades, las que no tenían su servicio registrado se inferían como cuerpo (en GET falla). Se registraron con fábricas que lanzan si se usan, como en `ReporteProyectoTests` (3).
- Fuente simulada: la fila 6 (ZZ999) **sí** se mapea; el servicio la omite después como `sinEmpleado`. Se corrigió la expectativa.
- `ConsultaEmpleadosPorCorreo` con un solo correo genera `=` en lugar de `IN`: la prueba usa dos.
- En la creación de las respuestas simuladas, `R` es un alias de `Invoke-History` en PowerShell: se renombró la función auxiliar (solo afectó al comando que las generó).

## 6. Desviaciones (reportadas)
1. **`NovedadesExternasOpciones`:** no se creó como clase aparte. Las constantes de la sección y de los modos viven en `NovedadesExternasServiceCollectionExtensions` (no hay más opciones que `Modo` y `RutaArchivo`).
2. **Logs en el endpoint:** Application no referencia `Microsoft.Extensions.Logging`, y agregarlo sería una referencia nueva. Por eso el endpoint registra el Information y, si un lote falla, el Warning con `servicio.Parcial` (los conteos hasta el último lote confirmado).
3. **`codigosOmitidos`** incluye las referencias de todas las omisiones del mapeador y del plan (máx. 100). Las filas ilegibles del CSV solo se cuentan (no tienen código).
4. **Uid repetido en la fuente** → `uidInvalido` (cuenta la primera fila); **Uid que pertenece a una novedad de PROFESIOGRAMA** → `conflictoCodigo`. No había una categoría para estos casos raros y no se agregaron categorías nuevas.

## 7. Pendientes
- Verificación del usuario con `sincronizacion.cmd` (requiere `NovedadesExternas:Modo = Simulado`, ya en `appsettings.Development.json`; reiniciar la API para que lo tome). Deja las 6 de prueba anuladas al final.
- **64** (nuevo): confirmar los valores de `ESTADO_NOVEDAD`, el formato de EKON y la zona horaria de `FechaRegistro` con la exportación real o la app de permisos.
- **63** vigente (permiso de Graph para la lista) y **7** (App Registration): bloquean la 23-4.
- **62** sin cambios.
- TAREA-24 (frontend; notas en `FASE_5_Novedades.md` §7.5).
