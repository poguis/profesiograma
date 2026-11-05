# TAREA-23-1 — Novedades, backend: lectura, opciones y creación

**Fecha:** 2026-10-21 (Fase B; plan de la Fase A aprobado con los ajustes del prompt).
**Resultado:** implementada; **pendiente de la verificación del usuario** con `backend/tests/manual/tarea23-1/novedades.cmd`.
- `dotnet build Profesiograma.slnx -c Release`: 0 advertencias, 0 errores (en una carpeta de salida aparte, ver §5.1).
- `dotnet test --solution Profesiograma.slnx -c Release`: **947/947** = 872 anteriores + 75 nuevas; 0 ajustadas (ver §5.1 sobre la prueba de la plantilla).
- `dotnet ef migrations has-pending-model-changes … --configuration Release`: «No changes have been made to the model since the last migration.» **Sin migración.**
- `ToQueryString`: las 9 consultas EF nuevas tienen prueba (`NovedadSqlTests`).

**Lo que no se hizo:** PUT, anular y sincronización (23-2/23-3/23-4); frontend; ejecutar la API, clientes HTTP o el script; SQL; `dotnet ef database update`; git que modifique el repositorio; build o update de graphify.

## 1. Qué se hizo
- **Domain/Novedades:** `GeneradorCodigoNovedad` (R7: `NOV-{yyyyMMdd}-{6 hex en minúsculas}`) y `DiasNovedad` (todos los días calendario del rango, tope `MaxDias` = 366, `Limites` mínimo/máximo para la 23-2).
- **Application/Novedades:**
  - `NovedadContratos`: solicitudes, filtro, DTO, `TipoNovedadRef` (RN16 con `EsElegible`), datos internos, puertos `INovedadConsultas` e `INovedadRepositorio`, `ResultadoCrearNovedad`.
  - `PermisosNovedad`: **único punto de decisión**. J1 `PuedeRegistrarGeneral` (solo Admin); J2 `TryObtenerVisibilidad` + `VisiblePara` (expresión que Infrastructure traduce a SQL); N3 `Modificacion`; registrante solo para Admin.
  - `NovedadValidador`: 400 por campo; GENERAL sin permiso → 403 antes de validar lo demás; API de empleados (lectura fresca) y base solo si el resto es válido.
  - `NovedadEscrituraServicio.CrearAsync`: dentro de `ITransaccionAsignaciones`: duplicado (N7, R6) → código y Uid (R7, 3 intentos) → guardar.
  - `NovedadConsultaServicio` (listado y detalle) y `OpcionesFormularioNovedad`.
  - Registro en `AddApplication`.
- **Infrastructure:** `Consultas/NovedadConsultas` (listado con `COUNT` de días en la misma consulta, detalle + días, tipos, proyecto elegible, `Visibles`) y `Persistencia/Novedades/NovedadRepositorio` (duplicado, visibilidad del duplicado, código o Uid, alta puntual con cédula D15 y novedad con sus días). Registro en `AddPersistencia`.
- **Api:** `Endpoints/NovedadEndpoints` (`/api/novedades`, política Gestor) y `app.MapNovedadEndpoints()` en `Program.cs`.
- **Scripts:** `backend/tests/manual/tarea23-1/` (`novedades.cmd`, `novedades.ps1`, `LEEME.md`, `.gitignore`, `simulacion/` con 22 respuestas ficticias).
- **Documentación:** `docs/fases/FASE_5_Novedades.md` (nuevo); `docs/00_ESTADO_ACTUAL.md` (cabecera, documentos, hoja de ruta 23-1…23-4 y 24, pendiente 7 vinculado a la 23-4, pendiente 62 nuevo, fila de §8); `docs/fases/FASE_2_Modelo_Datos.md` §0 (C32–C36).

## 2. Archivos
**Nuevos (backend):**
- `src/App.Domain/Novedades/GeneradorCodigoNovedad.cs`, `src/App.Domain/Novedades/DiasNovedad.cs`
- `src/App.Application/Novedades/NovedadContratos.cs`, `PermisosNovedad.cs`, `NovedadValidador.cs`, `NovedadEscrituraServicio.cs` (incluye `OpcionesFormularioNovedad`), `NovedadConsultaServicio.cs`
- `src/App.Infrastructure/Consultas/NovedadConsultas.cs`, `src/App.Infrastructure/Persistencia/Novedades/NovedadRepositorio.cs`
- `src/App.Api/Endpoints/NovedadEndpoints.cs`
- Pruebas: `tests/App.Domain.Tests/Novedades/NovedadDominioTests.cs`, `tests/App.Application.Tests/Novedades/NovedadDobles.cs`, `tests/App.Application.Tests/Novedades/NovedadTests.cs`, `tests/App.Infrastructure.Tests/Consultas/NovedadSqlTests.cs`, `tests/App.Api.Tests/NovedadEndpointsTests.cs`
- Scripts: `tests/manual/tarea23-1/*`

**Modificados (backend):** `src/App.Application/DependencyInjection.cs`, `src/App.Infrastructure/Persistencia/PersistenciaServiceCollectionExtensions.cs`, `src/App.Api/Program.cs`.

**Documentación:** `docs/fases/FASE_5_Novedades.md` (nuevo), `docs/00_ESTADO_ACTUAL.md`, `docs/fases/FASE_2_Modelo_Datos.md`, este reporte.

## 3. Pruebas nuevas (75)
| Proyecto | Pruebas |
|---|---|
| App.Domain.Tests (7) | código (formato, fecha recibida, colisión del sufijo); días (rango con fines de semana, 1 día, tope 366, fin < inicio), conteo, mínimo/máximo |
| App.Application.Tests (52) | RN16 por aplicación (11 casos, incluye `SeleccionableManual` y mayúsculas); aplicación faltante o inválida; GENERAL gestor → 403 sin leer nada ni abrir la transacción; GENERAL admin; sujeto que no corresponde o falta; proyecto elegible (propio, ajeno, inexistente, admin); empleado inexistente o inactivo → 400; API caída → 503 sin abrir la transacción; la API no se llama si hay errores de forma; alta puntual de una persona sin fila con la cédula de la lectura fresca (y sin buscar duplicado); fechas (4 casos 400, 366 días y pasadas permitidas); observación; días generados, código con la fecha de Ecuador y registrante; reintento de código y agotamiento; duplicado con y sin visibilidad (R6) y su criterio; J1; J2 (6 casos + proyecto eliminado); N3; detalle (registrante solo Admin, versión, permisos, 404); listado (4 valores ilegibles, filtro con visibilidad y sin anuladas); opciones (gestor y admin) |
| App.Infrastructure.Tests (11) | `ToQueryString`: listado gestor (visibilidad, filtros, `COUNT`, `OFFSET`, sin cédula ni `DetalleOrigenJson`) y admin; detalle; días; tipos; proyecto elegible; duplicado (PERSONA, PROYECTO, GENERAL); visibilidad del duplicado; código o Uid |
| App.Api.Tests (5) | GENERAL gestor 403 / admin 201 con Location; 400 por campo; cuerpo ilegible 400 en `fechaInicio`; detalle no visible 404, listado 400 y sin sesión 401; opciones del gestor |

## 4. Comandos y resultados
| Comando (desde `backend/`) | Resultado |
|---|---|
| `dotnet build Profesiograma.slnx -c Release` | Código sin errores; **6 errores de copia**: `src/App.Api/bin/Release` bloqueado por el proceso `App.Api.exe` (PID 21184, iniciado 12:36 desde `dotnet.exe`; §5.1) |
| `dotnet build Profesiograma.slnx -c Release --artifacts-path <scratchpad>\artefactos` | 0 advertencias, 0 errores |
| `dotnet test --solution Profesiograma.slnx -c Release --artifacts-path … --no-build` (1.ª) | 943/947: 3 de `NovedadEndpointsTests` (§5.2) y `EscritorReporteExcelTests` (§5.1) |
| Ídem (2.ª, tras corregir la prueba) | 946/947: solo `EscritorReporteExcelTests` (§5.1) |
| `dotnet test --project tests/App.Infrastructure.Tests -c Release` (salida normal) | **229/229**, incluida `EscritorReporteExcelTests` |
| `dotnet ef migrations has-pending-model-changes … --configuration Release` (con `ArtifactsPath`/`UseArtifactsOutput` solo en ese proceso) | sin cambios de modelo |
| Simulación del script en una COPIA en el scratchpad (`SIMULAR=1`, `CORRIDA=0`) | 23 `CONTROL … OK`, «Casos con FALLA: 0»; con dos respuestas alteradas en la copia: 2 FALLA detectadas. Copia borrada |

## 5. Errores y cómo se resolvieron
### 5.1 Salida Release bloqueada
Un `App.Api.exe` en ejecución (PID 21184, `src/App.Api/bin/Release`, padre `dotnet.exe`, iniciado a las 12:36; puede ser tu API o una instancia que quedó del script de la 22c) bloquea la copia de las DLL. **No se detuvo** (CLAUDE.md). Se compiló y se probó con `--artifacts-path` en el scratchpad (no toca `bin/`). Con esa salida, `EscritorReporteExcelTests` no encuentra `docs/PLANTILLA_REPORTE_PROYECTOS.xlsx` (sube carpetas desde el ensamblado); con la salida normal de App.Infrastructure.Tests pasa (229/229). Para confirmar el conteo con la salida normal: detén esa instancia y ejecuta `dotnet test --solution Profesiograma.slnx -c Release`.

### 5.2 Prueba de App.Api con cuerpo
Las 3 pruebas con POST daban 400 «cuerpo vacío»: un `DefaultHttpContext` declara que no hay cuerpo si no se le agrega `IHttpRequestBodyDetectionFeature` (como ya hace `SolicitudIlegibleTests`). Se agregó a la prueba; el código de producción no cambió.

## 6. Desviaciones (reportadas)
1. **`ITransaccionAsignaciones` en toda creación** (aprobada en el prompt): N7 sin carrera; en PERSONA cubre la alta puntual.
2. **Scripts: `.cmd` que llama a un `.ps1` con el cliente común de `tarea26a/comun.ps1`, no `curl.exe`.** Es el patrón vigente (TAREA-20, 22-1, 26d-3): da el modo `SIMULAR=1` sin red, lee JSON para encadenar casos (Id y código creados) y nunca depende de buscar ejecutables. Banner «MODO SIMULACION» / «MODO REAL» y línea `CONTROL <caso> OK|FALLA (esperado X, obtenido Y)` como se pidió.
3. **`sujeto`** se modela como `{ empleado: {…} }` o `{ proyecto: {…} }` (GENERAL: null) para no mezclar campos de los dos tipos.
4. **`tipoNovedad`** del POST es el **código** de `TipoNovedad` (p. ej. `VACACIONES`), sin distinguir mayúsculas.
5. La API de empleados y la verificación del proyecto se ejecutan **solo si el resto de la solicitud es válido** (un 400 de forma no gasta una llamada a la API); por eso un 400 puede no mostrar a la vez el error del empleado.

## 7. graphify (solo lectura; no se ejecutó build ni update)
- `graphify-out/` existe y está en `.gitignore` (`.gitignore:26: graphify-out/`). Ningún hook pidió ejecutar el build en esta tarea; el aviso de los hooks de Read/Grep («usa graphify query») se ignoró por instrucción del prompt.
- **No hay archivo de configuración con exclusiones** (`.graphifyignore`, `graphify.toml` y `graphify.json` no existen; `.graphify_detect.json` no menciona `exclude` ni `ignore`).
- El grafo **no** contiene `docs/origen/sharepoint/muestras`, `docs/origen/powerapps` ni ningún `resultado-*` o `.csv`; sí contiene 319 referencias a `backend/tests/manual` (scripts y LEEME) y 2 a `docs/origen` (texto de los documentos). Recomendación: agregar exclusiones explícitas (`docs/origen/`, `backend/tests/manual/`) antes del próximo build del grafo.

## 8. Novedades creadas en la base de desarrollo
Ninguna por Claude Code (no ejecuta la API). El script crea 4 por corrida (n01 PERSONA, n02 PROYECTO, n04 GENERAL, n11a PERSONA del admin) y las lista al final de `resultado-novedades.txt`; anótalas aquí tras la verificación (no se pueden anular hasta la 23-2).

## 9. Pendientes
- Verificación del usuario con `novedades.cmd` (con el ERP Simulado basta `CODIGO_EKON=DEV001`; con el real, un código activo).
- **62** (nuevo): `EMPLEADO_FAMILIAS_ASIGNABLES` sin uso.
- **7** vinculado a la 23-4 (Graph).
- 23-2 (edición y anulación; regla de `RowVer` en ediciones solo de días), 23-3 (sincronización), 24 (frontend: invalidar cronograma, reporte y listado).
- Exclusiones de graphify (§7).
