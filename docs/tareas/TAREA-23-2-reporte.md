# TAREA-23-2 — Novedades, backend: edición y anulación

**Fecha:** 2026-10-21 (Fase B; plan de la Fase A aprobado con P1–P5 y los ajustes A1–A5).
**Resultado:** implementada; **pendiente de la verificación del usuario** con `backend/tests/manual/tarea23-2/edicion.cmd`, y de la limpieza de las novedades de la 23-1 con `limpiar.cmd`.
- `dotnet build Profesiograma.slnx -c Release`: el código compila (sin `error CS`), pero la copia a `src/App.Api/bin/Release` falla por un `App.Api.exe` en ejecución (PID 56588; §5.1). Con `--artifacts-path` en el scratchpad: **0 advertencias, 0 errores**.
- `dotnet test --solution Profesiograma.slnx -c Release --artifacts-path …`: **983/984**; la única falla es `EscritorReporteExcelTests`, **solo por la ruta de artefactos** (entorno, §5.1). Con la salida normal, App.Infrastructure.Tests da **234/234**. Total de pruebas: **984** = 947 + 37 nuevas.
- `dotnet ef migrations has-pending-model-changes … --configuration Release`: «No changes have been made to the model since the last migration.» **Sin migración.**
- `ToQueryString`: las consultas nuevas (`ConsultaParaModificar`, `ConsultaDatosModificacion`) y la modificada (`ConsultaDuplicado` con `Dias` y `ExcluirNovedadId`) tienen prueba.

**Lo que no se hizo:** sincronización (23-3), Graph (23-4), frontend; ejecutar la API, clientes HTTP o los scripts; SQL; `dotnet ef database update`; git que modifique el repositorio; build o update de graphify.

## 1. Qué se hizo
- **Domain:** `DiasNovedad.Resultantes(inicio, fin, excluidos)`.
- **Application:**
  - `NovedadModificacionContratos.cs` (nuevo): `EditarNovedadSolicitud`, `AnularNovedadSolicitud`, `NovedadEditadaDto`, `VersionNovedad` (`Leer`: Base64 de 8 bytes; `Igual`), `NovedadParaModificar`, `EdicionValidada`, `EdicionNovedad`, `AnulacionNovedad`, el puerto `INovedadModificacion` y `ResultadoModificacionNovedad`.
  - `CriterioDuplicado`: parámetros **opcionales** `Dias` y `ExcluirNovedadId` (la creación no cambia).
  - `NovedadValidador.ValidarEdicionAsync`: N8, P1, P2, P3; no consulta la API de empleados.
  - `NovedadModificacionServicio` (nuevo): `EditarAsync` y `AnularAsync` con el orden H5 y la doble comprobación dentro de `ITransaccionAsignaciones` (también para anular, P4).
  - `PermisosNovedad`: se **extrajeron** `PuedeModificar` (solo el permiso de N3, para el 403) y `MensajeSoloLectura`; `Modificacion` los usa y no cambia de comportamiento.
- **Infrastructure:** `NovedadRepositorio` implementa además `INovedadModificacion`: lectura sin seguimiento (`ConsultaDatosModificacion`), lectura con seguimiento + días (`ConsultaParaModificar`), `PrepararEdicion` (diff de días), `MarcarModificacion` (R1), `EditarAsync`/`AnularAsync` y `DbUpdateConcurrencyException` → `ConflictoConcurrenciaException`. `ConsultaDuplicado` con `IN` de los días resultantes y sin la propia. Registro de `INovedadModificacion`.
- **Api:** `PUT /api/novedades/{id:int}` y `POST /api/novedades/{id:int}/anular` en `NovedadEndpoints`.
- **Scripts:** arreglos de `tarea23-1/novedades.ps1` y su LEEME; `tarea23-2/` nuevo (`casos.ps1`, `edicion.cmd/.ps1`, `limpiar.cmd/.ps1`, `LEEME.md`, `.gitignore`, `simulacion/` con 35 respuestas ficticias).
- **Documentación:** `FASE_5_Novedades.md` (estado de las subtareas y §7), `00_ESTADO_ACTUAL.md` (cabecera, hoja de ruta, filas 23-1 ✅ y 23-2, pendiente 63) y este reporte.

## 2. Archivos
**Nuevos:**
- `backend/src/App.Application/Novedades/NovedadModificacionContratos.cs`, `NovedadModificacionServicio.cs`
- `backend/tests/App.Application.Tests/Novedades/NovedadModificacionTests.cs`, `backend/tests/App.Infrastructure.Tests/Consultas/NovedadModificacionSqlTests.cs`
- `backend/tests/manual/tarea23-2/*`, `docs/tareas/TAREA-23-2-reporte.md`

**Modificados:**
- `backend/src/App.Domain/Novedades/DiasNovedad.cs`
- `backend/src/App.Application/Novedades/NovedadContratos.cs` (`CriterioDuplicado`), `PermisosNovedad.cs`, `NovedadValidador.cs`, `DependencyInjection.cs`
- `backend/src/App.Infrastructure/Persistencia/Novedades/NovedadRepositorio.cs`, `Persistencia/PersistenciaServiceCollectionExtensions.cs`
- `backend/src/App.Api/Endpoints/NovedadEndpoints.cs`
- `backend/tests/App.Domain.Tests/Novedades/NovedadDominioTests.cs` (1 prueba nueva), `backend/tests/App.Api.Tests/NovedadEndpointsTests.cs` (3 pruebas nuevas y 2 registros nuevos en la preparación del pipeline; ninguna aserción existente cambió)
- `backend/tests/manual/tarea23-1/novedades.ps1`, `backend/tests/manual/tarea23-1/LEEME.md`
- `docs/fases/FASE_5_Novedades.md`, `docs/00_ESTADO_ACTUAL.md`

## 3. Pruebas nuevas (37)
| Proyecto | Pruebas |
|---|---|
| App.Domain.Tests (1) | `Resultantes`: rango completo, excluidos dentro (con repetidos) y fuera, todos excluidos, inicio = fin |
| App.Application.Tests (28) | `version` ausente, en blanco, ilegible y de 3 y 10 bytes (editar y anular, sin leer la novedad); 404 con la visibilidad del gestor; orden H5 (origen > permiso > anulada > versión > campos); matriz N3 (5 casos) y Admin con GENERAL; RN16 con la aplicación guardada (3); fechas, excluidos fuera de rango, sin días y observación larga; sin cambios (400 `general`) y observación vacía que borra; edición exitosa (días, token, transacción, dos lecturas); versión cambiada dentro de la transacción; `ConflictoConcurrenciaException` → 409 (editar y anular); duplicado con días resultantes, `ExcluirNovedadId` y R6 (2); anular: motivo inválido (2), éxito (fecha UTC truncada, usuario, token), ya anulada y origen no editable, anulada entre la lectura y la transacción. La fuente de empleados lanza si se la llama |
| App.Infrastructure.Tests (5) | `ToQueryString` de `ConsultaParaModificar`, `ConsultaDatosModificacion` (gestor y admin), `ConsultaDuplicado` de edición (366 días: `IN` con parámetros y `Id <>`) y de creación (sin cambios); **R1 sin base**: tras `PrepararEdicion`, `Novedad` Modified, `FechaModificacion.IsModified`, `OriginalValue` de `RowVer` = token, días Unchanged/Deleted/Added |
| App.Api.Tests (3) | PUT con `version` ilegible 400 y sin sesión 401; PUT y anular como gestor sobre la GENERAL del admin 403, inexistente 404; PUT y anular como admin 200 con la versión nueva |

## 4. Comandos y resultados
| Comando (desde `backend/`) | Resultado |
|---|---|
| `dotnet build Profesiograma.slnx -c Release` | Sin `error CS`; 6 errores MSB3027 de copia (PID 56588, §5.1) |
| `dotnet build Profesiograma.slnx -c Release --artifacts-path <scratchpad>\artefactos` | 0 advertencias, 0 errores |
| `dotnet test … --artifacts-path … --no-build` (1.ª) | 981/984: `EscritorReporteExcelTests` (entorno) + 2 de mis pruebas nuevas (§5.2) |
| Ídem (2.ª) | **983/984**: solo `EscritorReporteExcelTests` (entorno) |
| `dotnet test --project tests/App.Infrastructure.Tests -c Release` (salida normal) | **234/234** |
| `dotnet ef migrations has-pending-model-changes … --configuration Release` (con `ArtifactsPath`/`UseArtifactsOutput` solo en ese proceso) | sin cambios de modelo |
| Simulación en una COPIA en el scratchpad (`SIMULAR=1`, `CORRIDA=0`) | 23-1: «FALLA: 0; OMITIDOS: 0»; 23-2 `edicion`: «FALLA: 0; OMITIDOS: 0» (tras corregir 3 respuestas simuladas, §5.3); `limpiar` (`IDS=3-9`, vista previa y `CONFIRMAR=1`): semillas 3–4 omitidas, 5 anulada, 6 (origen), 7 (año), 8 (no existe) y 9 (ya anulada) omitidas, «FALLA: 0»; sin `IDS`: mensaje y salida 1. Detección: con e04/e05 alteradas → 2 FALLA (incluida «la version no cambio»); con n01/n11a en 400 → 5 OMITIDOS; con p01 en 400 → 12 OMITIDOS. Copia borrada |

## 5. Errores y cómo se resolvieron
### 5.1 Salida Release bloqueada
Un `App.Api.exe` en ejecución (PID 56588, `src/App.Api/bin/Release`) bloquea la copia de las DLL. **No se detuvo.** Se compiló y se probó con `--artifacts-path`; con esa ruta `EscritorReporteExcelTests` no encuentra `docs/PLANTILLA_REPORTE_PROYECTOS.xlsx` (sube carpetas desde el ensamblado). Con la salida normal pasa (234/234). Para confirmar las 984 juntas: detén esa instancia y ejecuta `dotnet test --solution Profesiograma.slnx -c Release`.

### 5.2 Mis pruebas nuevas (1.ª corrida)
- Una tupla con una lista se comparaba por referencia: se separó la aserción.
- **A3:** la prueba esperaba `OPENJSON` (supuesto de la Fase A), pero **EF 10 traduce `Contains` a `IN (@dias1, …)` con un parámetro por valor** (relleno hasta 400 con 366 días). Es una traducción correcta y lejos del límite de 2100 parámetros de SQL Server: se mantuvo el plan A (no hizo falta el plan B) y la prueba ahora lo verifica.

### 5.3 Respuestas simuladas mal escritas
En PowerShell, `@(400, $a + 'b')` arma un arreglo de 3 elementos (la coma tiene más precedencia que `+`), así que 3 `.json` de simulación quedaron cortados y el script los marcó FALLA («sin error en version»). Se reescribieron; los scripts no cambiaron.

## 6. Desviaciones (reportadas)
1. **Servicio y puerto aparte:** `NovedadModificacionServicio` + `INovedadModificacion` en lugar de métodos nuevos en `NovedadEscrituraServicio` e `INovedadRepositorio`. Así los dobles existentes de la 23-1 (`NovedadRepositorioFalso`, `EscenarioNovedad`) no se tocaron; solo se agregaron 2 registros a la preparación del pipeline de `NovedadEndpointsTests`. `NovedadRepositorio` implementa los dos puertos.
2. **Dos lecturas:** fuera de la transacción y para la comprobación de adentro se usa una proyección **sin seguimiento**; la escritura vuelve a leer **con seguimiento** (`ConsultaParaModificar`). Evita que una entidad seguida desde fuera llegue vieja a la transacción.
3. **`PermisosNovedad`:** se extrajo `PuedeModificar` (sin cambio de comportamiento). **Observación (no es discrepancia con N3/N10):** el detalle (`Modificacion`) da el motivo en el orden origen → anulada → permiso, mientras que el 409/403 de la edición sigue H5 (origen → permiso → anulada). Un gestor sin permiso ve «La novedad está anulada.» como motivo en el detalle, y un 403 si intenta editarla. No se cambió.
4. **`limpiar.cmd`:** la lectura del detalle es informativa (sin `CONTROL`): un Id inexistente se informa como OMITIDA y no cuenta como FALLA. `IDS` es obligatoria (sin valor por defecto).
5. **`CODIGO_EKON_ADMIN`** = `CODIGO_EKON` por defecto en los dos modos (no hace falta detectar el modo del ERP).

## 7. Novedades de prueba
- **La 23-1 dejó los Id 5–14** (fechas de 2070): se anulan con `set IDS=5-14` → `limpiar.cmd` (vista previa) → `set CONFIRMAR=1` → `limpiar.cmd`.
- **`edicion.cmd` crea 4 por corrida y las anula al final** (lista sus códigos en `resultado-edicion.txt`).

## 8. Pendientes
- Verificación del usuario con `edicion.cmd` (e04/e05 es la prueba real de R1) y limpieza con `limpiar.cmd`.
- **63** (nuevo): permiso de Graph para leer la lista NOVEDADES ASISTENCIA (distinto del 7).
- **62** sin cambios.
- 23-3 (sincronización de PERMISO MEDICO), 23-4 (Graph) y 24 (frontend: ver la nota A4 en `FASE_5_Novedades.md` §7.5).
