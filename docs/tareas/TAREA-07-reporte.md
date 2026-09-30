# TAREA-07 — Endpoints de consulta de proyectos: listado y detalle (solo lectura)

**Fecha:** 2026-09-30
**Resultado:** completada. **Las 13 pruebas (P1–P13) pasan.** Sin cambios en el modelo EF, vistas ni tablas; sin migraciones.
No se ejecutaron `dotnet run` (la API la reinició el usuario), `migrations add`/`database update`, SQL manual ni comandos git que modifiquen el repositorio.
Todas las compilaciones fueron con `-c Release` y los comandos EF con `--configuration Release`, porque la API en ejecución bloquea `bin\Debug`.

## 1. Diseño implementado

| Endpoint | Política | Respuesta |
|---|---|---|
| `GET /api/proyectos` | `Politicas.Gestor` (Admin o Gestor) | 200 `{ items, pagina, tamano, total }` · 400 `ValidationProblem` |
| `GET /api/proyectos/{id:int}` | `Politicas.Gestor` | 200 detalle · 404 si no existe o no es visible |

- **Filtros:** `estado` (código), `grupo` (código), `texto` (contiene, en código o nombre), `desde`/`hasta` (`yyyy-MM-dd`), `pagina` (≥ 1, por defecto 1) y `tamano` (1–100, por defecto 20). Se reciben como **texto** y los valida el servicio, así los errores salen en español (C4).
- **Fechas:** se filtra por **solapamiento** con el periodo del proyecto: `FechaInicio <= hasta AND FechaFin >= desde`. Si solo llega uno de los dos, se aplica solo ese lado.
- **Orden:** `FechaInicio DESC`, luego `Codigo`.
- **Item del listado:** `id, codigo, nombre, grupo, estado, fechaInicio, fechaFin, responsable, backs[], horaEntrada, horaSalida`. `grupo` y `estado` son códigos, que es lo que trae la vista. La vista no incluye `HorarioDescripcion`, por eso el horario se da como `horaEntrada`/`horaSalida`.
- **Detalle:**
  - Cabecera: compañía, grupo y estado (código y nombre), fechas, bloque `erp` (ERP y dimensión), `departamento`, `horario`, `almuerzo` y `propietario`.
  - `personal`, ordenado por rol y número, con rol PRINCIPAL/BACK, empleado (**solo** Id, código EKON y nombre), cargo (`CargoAsignado`, o `Empleado.Puesto` si falta), jornada y fechas.
  - `etapas`, ordenadas por versión y **sin** `SnapshotPersonal`.
  - `actividadVigente`.
  - No incluye `ProyectoAsignacionDia`.

### Reglas
| Regla | Implementación |
|---|---|
| **R1** Visibilidad | Se decide **solo** en `ProyectoConsultaServicio.TryObtenerVisibilidad`. `Admin` ve todos (`PropietarioUsuarioId = null`); cualquier otro rol ve solo los suyos (`= IUsuarioActual.UsuarioId`). Infrastructure recibe el Id como filtro y no conoce roles |
| **R2** | El detalle filtra en SQL por `Id`, `Eliminado = 0` y el propietario. Si no encuentra nada devuelve `null`, y el endpoint responde **404** (nunca 403) |
| **R3** | El servicio valida: `pagina` debe ser un entero ≥ 1; `tamano`, un entero entre 1 y 100; `desde`/`hasta`, fechas en formato `yyyy-MM-dd`, con `desde <= hasta`. Si falla, responde 400 `ValidationProblem` con título "Parámetros de consulta no válidos." y mensajes en español. Un `estado` o `grupo` inexistente no es error: devuelve 0 resultados |
| **R4** | **Listado:** `db.Database.SqlQuery<FilaVwProyectoResumen>` sobre `dbo.vwProyectoResumen`. Es un tipo no mapeado, así que no se rastrea ni entra al modelo. EF lo envuelve como subconsulta, y `Where`, `CountAsync`, `OrderBy`, `Skip` y `Take` se ejecutan en SQL. En memoria solo se separa `BacksNombres` (`" \| "`) de los items de la página. **Detalle:** 4 consultas `AsNoTracking` con proyección (cabecera, personal, etapas y actividad, esta última con `TOP 1`) |

### Decisiones del usuario (Fase A)
| Id | Decisión | Aplicación |
|---|---|---|
| **C1** | Solapamiento con el periodo del proyecto (diseño aprobado) | **Diferencia documentada con la app original:** Fase 1 §2.4 indica que `ResumenProyectos` filtraba por la fecha de registro (`Creado`), con fecha fin igual a hoy por defecto. La nueva API filtra por el periodo del proyecto y no aplica rango por defecto |
| **C2** | Mismo criterio que la vista, con una sola definición | Primero la actividad que cubre hoy; si ninguna lo hace, la de mayor versión. Está en `ProyectoConsultas.OrdenarPorVigencia`, cuyo comentario remite al `OUTER APPLY act` de `vwProyectoResumen` (`VistasSql.VwProyectoResumen_V1`). **Coincide con la vista**: la vista calcula hoy con `SWITCHOFFSET(SYSDATETIMEOFFSET(), '-05:00')` y la API, con `TimeProvider` en la zona de Ecuador |
| **C3** | Constante `"SA Pacific Standard Time"` + `TimeProvider` | `ProyectoConsultaServicio.HoyEcuador()`. **Pendiente:** leerla de `Parametro.ZONA_HORARIA` |
| **C4** | Parámetros como texto y validación en español en el servicio | `ProyectoListadoSolicitud` (texto) → `ProyectoFiltro` (validado) |
| **C5** | Fechas relativas calculadas con la fecha de Ecuador del día de la prueba | Hoy en Ecuador = **2026-09-30**, el mismo día de la siembra, así que los rangos coinciden con los datos |
| Detalle | Incluir `erp` y `departamento` | Incluidos |
| Registro | `AddApplication()` en App.Application | Ver sección 2 |

## 2. Archivos tocados

| Acción | Archivo |
|---|---|
| Modificado | `backend/src/App.Application/App.Application.csproj` (+ `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.12) |
| Nuevo | `backend/src/App.Application/DependencyInjection.cs`: `AddApplication()` registra `TimeProvider.System` (`TryAddSingleton`) y `ProyectoConsultaServicio` (Scoped) |
| Nuevo | `backend/src/App.Application/Comun/PaginaResultado.cs`, `Comun/ResultadoConsulta.cs` |
| Nuevo | `backend/src/App.Application/Proyectos/ProyectoDtos.cs`, `ProyectoFiltro.cs`, `IProyectoConsultas.cs`, `ProyectoConsultaServicio.cs` |
| Nuevo | `backend/src/App.Infrastructure/Consultas/ProyectoConsultas.cs` (incluye la clase interna `FilaVwProyectoResumen`) |
| Modificado | `backend/src/App.Infrastructure/Persistencia/PersistenciaServiceCollectionExtensions.cs` (+ `IProyectoConsultas → ProyectoConsultas`) |
| Nuevo | `backend/src/App.Api/Endpoints/ProyectoEndpoints.cs` (`MapProyectoEndpoints`) |
| Modificado | `backend/src/App.Api/Program.cs` (+ `using App.Application;`, `builder.Services.AddApplication();` antes de `AddInfrastructure`, `app.MapProyectoEndpoints();`) |
| Modificado | `backend/src/App.Api/Profesiograma.Dev.http` (+ P1–P13, con `{{$localDatetime "yyyy-MM-dd" -40 d}}` y las variables `@idPry0001`/`@idPry0003`) |
| Documentación | `docs/tareas/TAREA-07-reporte.md`, `docs/00_ESTADO_ACTUAL.md` (sección 8 nueva y pendiente 11) |

`ProyectoConsultaServicio` **no** se registra en Infrastructure. `AddPersistencia` sigue registrando `TimeProvider.System` con `TryAddSingleton`; al ser `TryAdd` en ambos lugares, queda una sola instancia.

## 3. Comandos ejecutados y resultado

| Comando | Resultado |
|---|---|
| `dotnet build Profesiograma.slnx -c Release` | **0 advertencias, 0 errores** |
| `dotnet ef migrations has-pending-model-changes --project src/App.Infrastructure --startup-project src/App.Api --context ProfesiogramaDbContext --configuration Release` | **"No changes have been made to the model since the last migration."** (exit 0) |
| Reinicio de la API | Lo hizo el usuario |
| `curl -k -sS -i -H "X-Dev-User: …" https://localhost:7180/api/proyectos…` (P1–P13) | Ver sección 4 |

## 4. Pruebas (2026-09-30, hoy en Ecuador = 2026-09-30; hoy−40 = 2026-08-21, hoy−35 = 2026-08-26, hoy−10 = 2026-09-20)

Id de los datos de prueba: PRY-DEV-0001 = 1, PRY-DEV-0002 = 2, PRY-DEV-0003 = 3.

| Prueba | Esperado | Obtenido | Resultado |
|---|---|---|---|
| P1 admin `GET /api/proyectos` | 200, total 3 | 200, total 3: 0001, 0002, 0003 (orden `FechaInicio DESC`) | OK |
| P2 gestor `GET /api/proyectos` | 200, total 2 (0001, 0002) | 200, total 2: 0001, 0002 | OK |
| P3 gestor `?estado=ACTIVO` | 200, total 1 (0001) | 200, total 1: 0001 | OK |
| P4 admin `?texto=0002` | 200, total 1 | 200, total 1: 0002 | OK |
| P5 admin `?desde=2026-08-21&hasta=2026-08-26` | 200, total 1 (0003) | 200, total 1: 0003 | OK |
| P6 gestor, mismo rango | 200, total 0 | 200, total 0, `items: []` | OK |
| P7 admin `?tamano=1&pagina=2` | 200, 1 item, total 3 | 200, pagina 2, tamano 1, total 3, 1 item (0002) | OK |
| P8 admin `?tamano=500` | 400 | 400 `ValidationProblem`: `tamano`: "El tamaño de página debe ser un número entero entre 1 y 100." | OK |
| P9 admin `?desde=2026-09-30&hasta=2026-09-20` | 400 | 400 `ValidationProblem`: `desde`: "La fecha 'desde' no puede ser posterior a la fecha 'hasta'." | OK |
| P10 admin `GET /api/proyectos/1` | 200; personal 3 (2 principales + 1 back), etapas y actividad vigente | 200. Personal: PRINCIPAL 1 DEV001 (TIPO_2), PRINCIPAL 2 DEV002 (TIPO_3), BACK 1 DEV003 (sin jornada, 2026-10-01..05). Etapas: 1 (v1 CREACION/ACTIVO). Actividad vigente: DEV.01 v1 (2026-09-20..11-19) | OK |
| P11 gestor `GET /api/proyectos/3` (de admin) | 404 | 404 | OK |
| P12 admin `GET /api/proyectos/999999` | 404 | 404 | OK |
| P13 anonimo `GET /api/proyectos` | 401 | 401 | OK |

**Controles adicionales:**
- El empleado solo expone las claves `id`, `codigoEkon` y `nombreCompleto`: ni cédula ni correo.
- Las etapas no incluyen `snapshotPersonal`.
- El item del listado (P1) incluye `responsable: "EMPLEADO PRUEBA 01"`, `backs: ["EMPLEADO PRUEBA 03"]` y el horario 07:00–18:00.
- Las respuestas 401 y 404 no traen cuerpo.

Las respuestas crudas quedaron en el scratchpad de la sesión, fuera del repositorio.

## 5. Errores y correcciones
Ninguno.

## 6. Observaciones
- El 400 de P8/P9 usa el formato estándar de `ValidationProblem` (`type` RFC 9110, `title`, `status`, `errors`, `traceId`). El `title` está en español; `type` es la URI estándar.
- Un `id` no numérico (p. ej. `/api/proyectos/abc`) no coincide con la restricción `{id:int}` y devuelve 404 del enrutador.
- `texto` usa `LIKE` con la collation `Modern_Spanish_CI_AS`, así que no distingue mayúsculas ni minúsculas.

## 7. Pendientes
- **Leer la zona de negocio de `Parametro.ZONA_HORARIA`** en lugar de la constante `"SA Pacific Standard Time"` (servicio y sembrador).
- **Fecha de siembra:** los datos de prueba se sembraron el 2026-09-30. En otro día, los rangos relativos de P5/P6 del `.http` (`$localDatetime`) dejarán de coincidir con los datos, porque las fechas del sembrador son fijas desde su creación.
- Los Id de `@idPry0001`/`@idPry0003` del `.http` asumen 1 y 3 (valores actuales). Si la base se reinicia, hay que verificarlos con P1.
