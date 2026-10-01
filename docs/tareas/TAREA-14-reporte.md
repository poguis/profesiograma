# TAREA-14 — Cambio de estado: SUSPENSIÓN y CIERRE (backend)

**Fecha:** 2026-10-01 (Fase A y Fase B)
**Resultado:** ✅ completada. Prueba HTTP S1–S8 del usuario: todas según lo esperado (sección 6).
- `dotnet build Profesiograma.slnx -c Release`: **0 advertencias, 0 errores**.
- `dotnet test --solution Profesiograma.slnx -c Release`: **308/308** (232 + 76 nuevas).
- `has-pending-model-changes`: sin cambios (no se tocó el modelo ni las migraciones).

No se ejecutaron `dotnet run` ni SQL manual. No se tocó `frontend/`. No se usaron comandos git que modifiquen el repositorio.

## 1. Decisiones aprobadas (Fase A)
| # | Decisión |
|---|---|
| Alcance | SUSPENSION y CIERRE. REACTIVACION → 400 "La reactivación todavía no está disponible." (TAREA-17) |
| 8.1 | Hallazgos H1–H6 según el texto del usuario; las diferencias con el original quedan como D1–D8 |
| 8.2 (a) | Se borran todos los días con `Fecha > F`, incluidos los DESCANSO MANUAL de backs posteriores a la fecha fin (P3). La prueba "F = FechaFin" verifica que el personal no se recorta y que esos días sí se eliminan |
| 8.3 | El back que cubre a un principal eliminado queda con la referencia en `null` y advertencia "El back {n} ({nombre}) quedará sin principal relacionado." |
| 8.4 | `DiasDescanso` del back recortado no cambia; queda como pendiente para la TAREA-16 |
| Riesgo | Si el recorte elimina al principal inicial, se registra como pendiente vinculado a H4 (TAREA-17) |
| Correcciones al enunciado | El archivo de origen es `ConfigurarProyecto_1.pa.yaml`. `ProyectoAsignacionDia` sí tiene columnas de auditoría |

## 2. Qué se hizo

### Domain (`App.Domain/Proyectos/Estados/`)
| Archivo | Contenido |
|---|---|
| `MaquinaEstadosProyecto.cs` | `MovimientoEstado`, `CodigosEstadoProyecto`, `Resolver(origen, destino)` por código (sin distinguir mayúsculas), `CodigoTipoMovimiento`, `EsConocido` |
| `RecorteProyecto.cs` | `PersonaCorte`, `DiaCorte`, `ActividadCorte`, `PlanRecorte`, `RecorteProyecto.Calcular`. Lógica pura de E3 (días, personal, backs sin principal, actividades H3, actividad vigente en F) |

### Application
| Archivo | Contenido |
|---|---|
| `Proyectos/Estados/CambioEstadoContratos.cs` | `CambioEstadoSolicitud`, `PersonalCambio`, `DatosCambioEstado`, `CambioEstadoAplicar` (con `Version`), `ICambioEstadoRepositorio`, `ConflictoConcurrenciaException` |
| `Proyectos/Estados/CambioEstadoDtos.cs` | DTO de la vista previa y del resultado; `ResultadoCambioEstado` |
| `Proyectos/Estados/CambioEstadoValidador.cs` | E1/E2 con los mensajes de la sección 3 de `FASE_5_Estados_Proyecto.md` |
| `Proyectos/Estados/CambioEstadoServicio.cs` | Visibilidad R1 → validar → `RecorteProyecto`. Vista previa con advertencias (E6 con `FechaNegocio`, backs sin principal). Aplicar en `ITransaccionAsignaciones`: relectura dentro del applock, 409 si cambió el estado o si la fecha dejó de ser válida, plan recalculado, versión = máx + 1, snapshot del personal resultante, `ConflictoConcurrenciaException` → 409 |
| `Proyectos/SnapshotPersonal.cs` | Formato común del snapshot (`ElementoSnapshot`). `CrearProyectoServicio` lo usa (refactor sin cambio de JSON) |
| `DependencyInjection.cs` | + validador y servicio |

### Infrastructure / Api
| Archivo | Contenido |
|---|---|
| `Persistencia/Proyectos/CambioEstadoRepositorio.cs` | Lecturas sin seguimiento; `ExecuteDelete` de días; dos `SaveChanges` con entidades con seguimiento (auditoría y `RowVer`); errores 2601/2627 → conflicto |
| `Persistencia/PersistenciaServiceCollectionExtensions.cs` | + `ICambioEstadoRepositorio` |
| `Api/Endpoints/ProyectoEndpoints.cs` | `POST /{id:int}/cambio-estado/previsualizar` y `POST /{id:int}/cambio-estado` |
| `Api/Profesiograma.Dev.http` | S1–S8 (sección 6) |

### Documentación
- `docs/fases/FASE_5_Estados_Proyecto.md` (nuevo).
- `docs/00_ESTADO_ACTUAL.md`:
  - §1: documentos de Fase 5;
  - §6: hoja de ruta 14–26;
  - §7: pendientes 22–24;
  - §8: fila de la TAREA-14.

## 3. SQL generado (pruebas de traducción)
Estado inicial de las tres consultas principales:
```sql
-- Cabecera (gestor)
SELECT [p].[Id], [p].[Codigo], [e].[Codigo] AS [EstadoCodigo], [p].[FechaInicio], [p].[FechaFin]
FROM [dbo].[Proyecto] AS [p] INNER JOIN [dbo].[EstadoProyecto] AS [e] ON [p].[EstadoProyectoId] = [e].[Id]
WHERE [p].[Id] = @proyectoId AND [p].[Eliminado] = CAST(0 AS bit) AND [p].[PropietarioUsuarioId] = @propietario

-- Personal (sin cédula ni correo)
SELECT [p].[Id], … [e].[CodigoEkon], [e].[NombreCompleto], [j].[Codigo] AS [JornadaCodigo], …
FROM [dbo].[ProyectoPersonal] AS [p] INNER JOIN [dbo].[Empleado] AS [e] … LEFT JOIN [dbo].[Jornada] AS [j] …
WHERE [p].[ProyectoId] = @proyectoId ORDER BY [p].[RolAsignacionId], [p].[Numero]

-- Filtro del DELETE masivo (ExecuteDelete) con 2 personas eliminadas
WHERE [p].[ProyectoId] = @proyectoId AND ([p].[Fecha] > @fecha OR [p].[ProyectoPersonalId] IN (@personalEliminado1, @personalEliminado2))
```
- Con una sola persona eliminada, EF genera `= @personalEliminado1`; sin ninguna, omite la condición.
- `ExecuteDeleteAsync` no tiene `ToQueryString()`: se prueba el `IQueryable` del filtro, que EF convierte en `DELETE FROM [p] FROM … WHERE …`.

## 4. Pruebas nuevas (76)
| Proyecto | Pruebas | Contenido |
|---|---|---|
| App.Domain.Tests | 34 | Máquina de estados (las **16 combinaciones**, mayúsculas, códigos desconocidos, tipo de movimiento) y `RecorteProyecto`. Casos de recorte:<br>• F = FechaFin, con descansos de back posteriores al fin (8.2);<br>• F = FechaInicio;<br>• back con descanso posterior más allá de F;<br>• back recortado;<br>• principal que empieza después de F, con su back sin principal;<br>• descanso AUTO que cruza F;<br>• actividades antes, durante y después;<br>• fecha fuera de rango |
| App.Application.Tests | 30 | Validador (mensajes exactos, fecha inclusiva, errores acumulados). Servicio con dobles:<br>• vista previa completa;<br>• advertencia de fecha pasada y de hoy (`TimeProvider` fijo, zona Ecuador);<br>• REACTIVACION 400;<br>• 404;<br>• visibilidad gestor/admin;<br>• sin fecha;<br>• **versión = máx + 1**;<br>• cierre desde SUSPENDIDO;<br>• **409 por cambio de estado dentro de la transacción**;<br>• 409 por fecha fin cambiada;<br>• 409 por concurrencia al guardar;<br>• eliminado dentro de la transacción.<br>Además, **`SnapshotCreacionTests`**: el JSON de la etapa v1 es idéntico al anterior al refactor (la prueba se escribió y pasó **antes** del refactor y sigue pasando después) |
| App.Infrastructure.Tests | 12 | Traducción de todas las consultas nuevas: cabecera con y sin propietario, personal, días posteriores (incluido `DateOnly.MaxValue`), actividades, filtro del DELETE con 0/1/2 personas, Id de catálogo por código, cargas con seguimiento y última versión |

## 5. Comandos ejecutados y resultado
| Comando | Resultado |
|---|---|
| `App.Application.Tests.exe -class …SnapshotCreacionTests` (antes del refactor) | 1/1: fija el JSON vigente |
| `dotnet build Profesiograma.slnx -c Release [--no-incremental]` | 0 advertencias, 0 errores |
| `App.*.Tests.exe -namespace …Estados` / `-class …CambioEstadoSqlTests -showliveoutput` | 34 + 29 + 12, todas correctas; SQL de la sección 3 |
| `dotnet test --solution Profesiograma.slnx -c Release` | **308/308** (Domain 65, Application 152, Infrastructure 91) |
| `dotnet ef migrations has-pending-model-changes … --configuration Release` | "No changes have been made to the model since the last migration." |

Errores durante la tarea: ninguno de compilación ni de pruebas.

Ajuste de diseño respecto de la Fase A: la versión máx + 1 la calcula el **servicio** (con `ObtenerUltimaVersionEtapaAsync` dentro de la transacción) y no el repositorio. Así se puede probar con dobles. El repositorio recibe la versión en `CambioEstadoAplicar`.

## 6. Prueba HTTP S1–S8 (usuario, API reiniciada, 2026-10-01 17:44–17:48)
Usuario `gestor`. Solo **S2** y **S4b** escribieron, sobre el **Id 7** (PRY-20261001-454325: 01/03–30/04/2027, P1 DEV004 TIPO_3, actividad DEV.01).
- El Id 8 recibió solo peticiones que terminan en 400.
- El Id 5 se usó solo para el 404.

| # | Petición | Esperado | Obtenido | Resultado |
|---|---|---|---|---|
| S1 | Previsualizar Id 7 → SUSPENDIDO, 15/03/2027 | 200 SUSPENSION; DEV004 PRINCIPAL 34 (16/03–30/04) y DESCANSO 12 (20/03–25/04); recorte 30/04 → 15/03; DEV.01 RECORTADA; sin advertencias; no guarda | 200 SUSPENSION ACTIVO→SUSPENDIDO; PRINCIPAL 34 (16/03–30/04), DESCANSO 12 (20/03–25/04); personal 30/04→15/03; DEV.01 RECORTADA; `advertencias: []`. S1b: `GET /7` seguía ACTIVO con fin 30/04 | OK |
| S2 / S2b | Registrar + `GET /api/proyectos/7` | 200 `{7, "SUSPENDIDO", 2}`; detalle SUSPENDIDO, fin 15/03, etapa v2 | 200 `{7, SUSPENDIDO, 2}`. Detalle: SUSPENDIDO, fin 15/03, DEV004 hasta 15/03, etapa v2 SUSPENSION con corte 15/03 y actividad DEV.01, actividad vigente hasta 15/03 | OK |
| S3 | Id 7 → ACTIVO | 400 reactivación | 400 `estadoDestino`: "La reactivación todavía no está disponible." | OK |
| S4 / S4b | Previsualizar y registrar Id 7 → TERMINADO, 10/03 | CIERRE; PRINCIPAL 3 (11–15/03), DESCANSO 2 (13–14/03); 200 `{7, "TERMINADO", 3}` | 200 CIERRE SUSPENDIDO→TERMINADO; PRINCIPAL 3 (11–15/03), DESCANSO 2 (13–14/03). S4b 200 `{7, TERMINADO, 3}` | OK |
| S5 | Id 7 → SUSPENDIDO | 400 no permitido | 400 "…(TERMINADO → SUSPENDIDO)." | OK |
| S6 | Gestor sobre Id 5 (admin) | 404 | 404 sin cuerpo | OK |
| S7 | `anonimo` | 401 | 401 | OK |
| S8a–e | Id 8: 01/05 / 31/03 / INACTIVO / ACTIVO / XYZ | 400 con el mensaje exacto | 400: fecha fin (30/04/2027), fecha inicio (01/04/2027), ACTIVO→INACTIVO no permitido, "ya está en estado ACTIVO", "'XYZ' no existe" | OK |

**Estado final verificado** con `GET` de solo lectura después de las pruebas:
- **Id 7: TERMINADO**, fin 10/03/2027.
  - Etapas: v1 CREACION/ACTIVO (corte 01/03), v2 SUSPENSION/SUSPENDIDO (corte 15/03), v3 CIERRE/TERMINADO (corte 10/03), todas con DEV.01.
  - DEV004 del 01/03 al 10/03; actividad DEV.01 v1 hasta el 10/03.
  - Queda en `PROFESIOGRAMA_DEV` como dato de prueba.
- **Id 8:** sigue ACTIVO, fin 30/04/2027, 1 etapa (disponible para la TAREA-15).

Los conteos de S1 y S4 salen del ciclo TIPO_3 (5/2) desde el 01/03. La advertencia E6 no se puede ver con proyectos de 2027 (pendiente 24).

## 7. Pendientes
- Proyecto de prueba Id 7 (PRY-20261001-454325) TERMINADO, versión 3; Id 8 ACTIVO para la TAREA-15.
- 22: principal inicial eliminado por el recorte (H4, TAREA-17).
- 23: `DiasDescanso` del back recortado (TAREA-16).
- 24: E6 solo cubierta por pruebas.
- TAREA-15: el frontend propondrá la `FechaFin` actual como fecha por defecto del cierre (H1).
