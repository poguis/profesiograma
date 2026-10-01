# TAREA-12 — Caso de uso "Crear proyecto": previsualizar + registrar (transacción) + cruces externos

**Fechas:** 2026-09-30 (Fase A e implementación) · 2026-10-01 (pruebas HTTP, corrección y cierre)
**Resultado:** completada.
- `dotnet build -c Release`: **0 advertencias, 0 errores**.
- `dotnet test`: **217/217 correctas**.
- `has-pending-model-changes`: sin cambios.
- **C1–C9 y C5b correctos** (tras corregir un 500 en C1; ver sección 6).

No se modificaron el modelo ni las migraciones, no se ejecutaron `dotnet run` (la API la reinició el usuario) ni SQL manual, no se tocó `frontend/` ni se usaron comandos git que modifiquen el repositorio.

**Proyecto creado en la base de desarrollo:** `PRY-20261001-4ede0f` (Id 4, CAMPO, 01–31/12/2026, DEV006/DEV007/DEV008, propietario gestor). Queda en `PROFESIOGRAMA_DEV`. Ningún otro caso guardó datos: C1 y C5 son previsualizaciones, C4 terminó en 409 y C6–C9 en 400/401.

## 1. Decisiones aprobadas (Fase A)
| # | Decisión |
|---|---|
| (a) | Almuerzo **obligatorio** (como la app original: "Debes seleccionar un horario de trabajo y almuerzo…") con los rangos de RN09: salida 11:00–14:00, regreso 12:00–15:00, regreso > salida |
| (b) | Campos que no corresponden al grupo → **400** (p. ej. "El grupo PLANTA no usa proyecto ERP."). **Para la TAREA-13:** el formulario debe limpiarlos al cambiar de grupo |
| (c) | `CRONOGRAMA_MAX_DIAS` no se aplica a la creación (es del reporte del cronograma) |
| (d) | Applock no obtenido en 15 s → `RegistroOcupadoException` → **503** |
| (e) | C5 en octubre (fechas de PRY-DEV-0001) + C5b de contraste |
| — | `PrincipalRelacionadoId` (FK sin navegación): segundo `SaveChanges` en la misma transacción. Modelo sin cambios |
| — | Refactor `FechaNegocio` (hoy en Ecuador) sin cambio de comportamiento |
| Ajuste 1 | Mes del resumen con `CultureInfo.GetCultureInfo("es-EC")` explícito (prueba con la cultura del hilo en `en-US`) |
| Ajuste 2 | El 409 solo expone del proyecto existente **código, nombre y estado** (`CruceDto`). La consulta no lee propietario ni otros datos (verificado en el SQL) |
| Ajuste 3 | Regla del applock documentada en `00_ESTADO_ACTUAL.md` §7.1 |

Otros hallazgos de la Fase A aplicados:
- `EsPrincipalInicial` = principal n.º 1, como el original (`EsPrincipalInicial: CountRows(colPrincipalesProyecto) = 0`).
- Los días de descanso del back se validan contra el parámetro `BACK_MAX_DIAS_DESCANSO`.
- Los máximos salen de `PROYECTO_MAX_PRINCIPALES` y `PROYECTO_MAX_BACKS`.

## 2. Qué se hizo

### Domain
| Archivo | Contenido |
|---|---|
| `Proyectos/GeneradorCodigoProyecto.cs` | RN01 + P6: `PRY-` + yyyyMMdd + `-` + últimos 6 caracteres de `uid.ToString("N")` en minúsculas |
| `Proyectos/NombreVisualProyecto.cs` | Nombre del proyecto ERP; si no hay, `"PLANTA - "` + descripción de la dimensión |

### Application (`Proyectos/Crear/` + `Comun/FechaNegocio.cs`)
| Archivo | Contenido |
|---|---|
| `CrearProyectoSolicitud.cs` | Cuerpo FASE_5 §7.1 (horas del almuerzo como texto `HH:mm`) |
| `CrearProyectoContratos.cs` | `IDatosReferenciaProyecto`, `IConsultaCrucesExternos`, `IProyectoRepositorio`, `ITransaccionAsignaciones`, `RegistroOcupadoException`, `ProyectoValidado`, `PersonalValidado`, `NuevoProyecto` |
| `CrearProyectoDtos.cs` | `TramoDto`, `DiasPersonaDto`, `CruceDto`, `ResumenCruceDto`, `PrevisualizacionDto`, `ProyectoCreadoDto`, `ResultadoCrearProyecto` |
| `CrearProyectoValidador.cs` | Todas las reglas, con mensajes en español por campo (claves `principales[i].campo`). Datos ERP **por Id** (compañía, proyecto, actividad, dimensión, horario). P5. `MinimoPrincipales = 0` con un comentario que explica cómo exigir 1 o más. Numeración 1..n según el orden recibido |
| `CalculadorCruces.cs` | Cruces internos (motor) y externos (cruce en memoria con los días ≠ DESCANSO del nuevo) + resumen persona/rol/proyecto/mes ("5, 6, 7"), orden determinista |
| `CrearProyectoServicio.cs` | Validar → motor → cruces → previsualizar (no guarda) o registrar. Si hay cruces → 409. Si no: transacción → **cruces externos otra vez dentro** → código (3 intentos) → guardar. Snapshot JSON sin cédula ni correo |
| `Comun/FechaNegocio.cs` | `Hoy(TimeProvider)` en Ecuador. `ProyectoConsultaServicio` lo usa (refactor) |
| `DependencyInjection.cs` | + validador y servicio |

### Infrastructure
| Archivo | Contenido |
|---|---|
| `Consultas/DatosReferenciaProyecto.cs` | Grupo, jornadas, límites (`Parametro`), empleados activos y departamentos del usuario |
| `Consultas/ConsultaCrucesExternos.cs` | Cruces con otros proyectos (SQL en la sección 3) |
| `Persistencia/Proyectos/ProyectoRepositorio.cs` | Upsert de `Compania` (Nombre = tradeName ?? name, NombreComercial = name, Ruc) → `Proyecto` (ACTIVO, propietario, horario del ERP, almuerzo, departamento) → `ProyectoPersonal` → etapa v1 → actividad v1 (si hay) → **SaveChanges 1**; luego `PrincipalRelacionadoId` + `ProyectoAsignacionDia` (`DiasFinales` con FK compuesta) → **SaveChanges 2**. Descripciones del ERP recortadas al largo de columna |
| `Persistencia/Proyectos/TransaccionAsignaciones.cs` | `BeginTransaction(ReadCommitted)` + `sp_getapplock` con parámetro de salida (`@Resource = 'profesiograma:asignaciones'`, `Exclusive`, dueño `Transaction`, espera 15 000 ms). Código < 0 → `RegistroOcupadoException`. Confirma o revierte según el resultado; si hay excepción, `DisposeAsync` revierte |
| `Persistencia/PersistenciaServiceCollectionExtensions.cs` | + los 4 servicios |
| **Refactor sin cambio de comportamiento** | `ProyectoConsultas`, `EmpleadoConsultas`, `CatalogoConsultas`, `DatosReferenciaProyecto` y `ProyectoRepositorio`: cada consulta EF se expuso como `internal static IQueryable<…>` para las pruebas de traducción (sección 7) |

### Api
| Archivo | Contenido |
|---|---|
| `Endpoints/ProyectoEndpoints.cs` | `POST /api/proyectos/previsualizar` → 200 / 400. `POST /api/proyectos` → **201** `{ id, codigo }` + `Location` / 400 / **409** ProblemDetails "El proyecto tiene cruces de asignación." con extensiones `cruces` y `resumen` |
| `Configuracion/RegistroOcupadoExceptionHandler.cs` | → 503 "Registro ocupado" |
| `Program.cs` | + `AddExceptionHandler<RegistroOcupadoExceptionHandler>()` |
| `Profesiograma.Dev.http` | + C1–C9 y C5b |

### Documentación
- `CLAUDE.md`: nueva sección **"Reglas de pruebas"** (prueba de traducción con `ToQueryString()` obligatoria).
- `00_ESTADO_ACTUAL.md`: §7.1 "Reglas técnicas obligatorias" (applock y pruebas de traducción), fila TAREA-12 en el avance y pendientes 14–17.

## 3. SQL real de la consulta de cruces externos (`ToQueryString`)
Obtenido con `ConsultaCrucesExternosSqlTests` (sin conexión), para los empleados 6 y 7 en diciembre de 2026:
```sql
DECLARE @empleadoIds1 int = 6;
DECLARE @empleadoIds2 int = 7;
DECLARE @desde date = '2026-12-01';
DECLARE @hasta date = '2026-12-31';

SELECT [p].[EmpleadoId], [p].[Fecha], [p].[RolAsignacionId], [p0].[Id], [p0].[Codigo], [p0].[NombreVisual], [e].[Codigo]
FROM [dbo].[ProyectoAsignacionDia] AS [p]
INNER JOIN [dbo].[Proyecto] AS [p0] ON [p].[ProyectoId] = [p0].[Id]
INNER JOIN [dbo].[EstadoProyecto] AS [e] ON [p0].[EstadoProyectoId] = [e].[Id]
WHERE [p].[EmpleadoId] IN (@empleadoIds1, @empleadoIds2) AND [p].[Fecha] >= @desde AND [p].[Fecha] <= @hasta AND [p].[RolAsignacionId] <> CAST(3 AS tinyint) AND [p0].[Eliminado] = CAST(0 AS bit) AND [e].[EsVigente] = CAST(1 AS bit)
```
- EF 10 usa parámetros escalares en el `IN`.
- La condición opcional `excluirProyectoId` desaparece cuando es `null`.
- La cubre `IX_ProyectoAsignacionDia_Cruces (EmpleadoId, Fecha) INCLUDE (ProyectoId, RolAsignacionId)`.
- Del proyecto existente solo se leen Id, código, nombre visual y código de estado.

## 4. Pruebas unitarias
| Proyecto | Total | Nuevas en esta tarea |
|---|---|---|
| App.Domain.Tests | 31 | 6: código RN01 (valor exacto con Uid fijo, formato, largo ≤ 30) y nombre visual (ERP / PLANTA - dimensión / error) |
| App.Application.Tests | 107 | 73: validador (**45** casos: 27 de cabecera y 18 de personal, con el mensaje exacto + máximos desde parámetros + P5 con 0/1/varios + back DESCANSO válido + sin usuario); caso de uso (registra con código `PRY-20261201-…`, 2 consultas de cruces fuera y dentro, 68 días, snapshot sin cédula ni correo, PLANTA sin actividad, código con fecha de Ecuador y no UTC, cruce interno → 409 sin transacción, cruce externo → 409 con código/nombre/estado, día de DESCANSO del nuevo no cruza, cruce que aparece **solo dentro** de la transacción → 409 + rollback, reintento de código 3 veces, 3 colisiones → error + rollback, inválido no calcula, previsualizar no guarda); resumen ("5, 6, 7", agrupación, mes es-EC con la cultura del hilo en en-US) |
| App.Infrastructure.Tests | 79 | 15 de **traducción a SQL** (sección 7) |
| **Total** | **217** | |

Resumen completo de `dotnet test --solution Profesiograma.slnx -c Release` (ejecución final):
```
Resumen de la serie de pruebas: Correcta!
  total: 217
  error: 0
  correcto: 217
  omitido: 0
```

## 5. Pruebas HTTP (API en Development, ERP Simulado)
Línea base antes de C1: gestor **2**, admin **3**.

| Caso | Esperado | Obtenido | Resultado |
|---|---|---|---|
| C1 gestor previsualizar (CAMPO DEV-ERP-001/DEV.01, 01–31/12, P1 DEV006 TIPO_2, P2 DEV007 TIPO_3, back DEV008 JORNADA 12–15/12 + 2 días) | 200, sin cruces, nada guardado | 200. 16 tramos (DEV006: 01–11, 16–26, 31 + DESCANSO AUTO 12–15, 27–30; DEV007: 5 bloques + 4 descansos; DEV008: BACK 12–15 + DESCANSO MANUAL 16–17). Días por persona 31/31/6. `cruces: []`. Listado **sin cambios (2/3)** | OK |
| C2 registrar el mismo | 201 | **201 Created**, `Location: /api/proyectos/4`, `{id: 4, codigo: "PRY-20261001-4ede0f"}` | OK |
| C2 detalle `GET /api/proyectos/4` | Personal, etapa v1, actividad | ACTIVO, CAMPO, compañía 9001 (del ERP), ERP DEV-ERP-001 "Activo", horario 1 07:00–18:00 (660/600), almuerzo 13:00–14:00, departamento SIG, propietario GESTOR. Personal: P1 DEV006 TIPO_2 11/4 **inicial**, P2 DEV007 TIPO_3 5/2, BACK 1 DEV008 JORNADA 2 días con observación. Etapa v1 CREACION/ACTIVO, corte 01/12, actividad DEV.01. Actividad vigente DEV.01 v1 | OK |
| C3 listados | gestor 3, admin 4 | gestor **3**, admin **4** | OK |
| C4 otro proyecto PLANTA con DEV006 (ESPECIAL) 05–10/12 | 409 con resumen | **409** "El proyecto tiene cruces de asignación." 6 cruces EXTERNO (05 al 10/12, PRINCIPAL) con solo `proyecto` "PROYECTO ERP DE PRUEBA", `proyectoCodigo` PRY-20261001-4ede0f y `estadoProyecto` ACTIVO. Resumen: `EMPLEADO PRUEBA 06 · PRINCIPAL · PRY-20261001-4ede0f · PROYECTO ERP DE PRUEBA · diciembre 2026 · "5, 6, 7, 8, 9, 10"`. Listado sigue en 3/4 | OK |
| C5 DEV001 (ESPECIAL) 01–04/10, previsualizar | Sin cruces | 200, tramos 01–03 y 04, **`cruces: []`** | OK |
| C5b mismo, 30/09–04/10 (contraste) | Cruce solo el 30/09 | 200, **1 cruce**: 30/09/2026 con PRY-DEV-0001 (ACTIVO); resumen "septiembre 2026 · 30" | OK |
| C6 CAMPO sin proyecto ERP | 400 | 400 `proyectoErpId`: "El grupo CAMPO requiere un proyecto ERP." | OK |
| C7 almuerzo 15:00–14:00 | 400 | 400 `salidaAlmuerzo`: "La salida a almuerzo debe estar entre 11:00 y 14:00." · `regresoAlmuerzo`: "El regreso de almuerzo debe ser posterior a la salida." | OK |
| C8 back con relacionado 5 | 400 | 400 `backs[0].principalRelacionado`: "El principal relacionado 5 no existe entre los principales enviados." | OK |
| C9 anonimo | 401 | 401 | OK |

**Explicación de C5/C5b con el detalle de PRY-DEV-0001:**
- En PRY-DEV-0001, DEV001 es principal 1 con TIPO_2 (11/4) desde el 20/09/2026.
- Su ciclo de 15 días es: trabajo del 20 al 30/09 y **DESCANSO del 01 al 04/10**.
- Por eso un nuevo proyecto que solo lo ocupe del 01 al 04/10 no cruza (los cruces ignoran DESCANSO), y en cuanto incluye el 30/09 aparece el cruce de ese único día.

## 6. Error encontrado y corrección
| Aspecto | Detalle |
|---|---|
| Síntoma | La primera ejecución de **C1 devolvió 500** (`An error occurred while processing your request.`). No se guardó nada (listado 2/3) |
| Diagnóstico | La consola de la API no estaba disponible. Una prueba temporal con `ToQueryString()` sobre las consultas de `DatosReferenciaProyecto` mostró que **`ObtenerDepartamentosDeUsuarioAsync` no era traducible**: `InvalidOperationException: The LINQ expression 'DbSet<UsuarioDepartamento>()…' could not be translated`. Causa: `Distinct()` + `OrderBy(d => d.Id)` después de proyectar a un `record` construido con constructor. Las otras tres consultas se traducían bien |
| Por qué no lo detectaron las pruebas | Las pruebas del caso de uso usan dobles de `IDatosReferenciaProyecto`. Solo existía la prueba de traducción de los cruces |
| Corrección | `Distinct` y `OrderBy` sobre un tipo anónimo (en SQL) y el `record` armado en memoria (`DatosReferenciaProyecto.ConsultaDepartamentosDeUsuario`). SQL resultante: `SELECT DISTINCT … ) AS [s] ORDER BY [s].[Id]` |
| Prevención | Prueba de regresión `DatosReferenciaProyectoSqlTests`; nueva regla en `CLAUDE.md` ("Reglas de pruebas") y en `00_ESTADO_ACTUAL.md` §7.1; pruebas de traducción para **todas** las consultas EF de Proyecto, Empleado, Catálogo y Crear (sección 7) |
| Verificación | La prueba temporal se eliminó. Build 0/0, 204/204. El usuario reinició la API y **C1–C9 pasaron** |

## 7. Revisión de consultas EF sin prueba de traducción (pedido adicional)
Revisión **de solo lectura**: ninguna de las tres clases pedidas tenía prueba de traducción, aunque ya habían funcionado contra la base en las TAREA-07 y TAREA-11.
- Se extrajeron sus consultas a métodos `internal static IQueryable<…>`, sin cambiar el LINQ ni el comportamiento.
- Se agregaron pruebas con una base común (`Consultas/BaseSql.cs`) que imprime el SQL y verifica fragmentos clave.

| Clase | Consultas cubiertas | Prueba |
|---|---|---|
| `ProyectoConsultas` | Listado (`SqlQuery` + todos los filtros; y sin filtros), página (`ORDER BY` + `OFFSET`), cabecera (con y sin propietario), personal (**sin `Cedula` ni `CorreoEmpresa`**), etapas (**sin `SnapshotPersonal`**), actividad vigente (`CASE`) | `ProyectoConsultasSqlTests` (7) |
| `EmpleadoConsultas` | Búsqueda con texto y departamentos (`IN` con 2; con 1, EF 10 genera `=`), página **sin cédula ni correo**, búsqueda sin filtros, departamentos del usuario | `EmpleadoConsultasSqlTests` (3) |
| `CatalogoConsultas` | Los 9 catálogos + parámetros | `CatalogoConsultasSqlTests` (1, con 10 consultas) |
| `DatosReferenciaProyecto` | Grupo, jornadas, límites (`[Clave] IN (`), empleados activos (sin cédula) + departamentos (regresión) | `CrearProyectoConsultasSqlTests` + `DatosReferenciaProyectoSqlTests` |
| `ProyectoRepositorio` | Existe código, compañía para el upsert | `CrearProyectoConsultasSqlTests` |
| `ConsultaCrucesExternos` | Cruces | `ConsultaCrucesExternosSqlTests` (ya existía en esta tarea) |

Ajuste durante la escritura de las pruebas: la primera versión de la prueba de empleados esperaba `IN (` con **un** departamento. EF 10 lo optimiza a `=`. La consulta era correcta y se ajustó la prueba (dos departamentos).

**No cubiertas** (fuera de la lista pedida; pendiente 15): consultas de `UsuarioProvisionamiento` y `DatosPruebaSembrador` (Paso 2).

**Nota:** el refactor de las clases de consulta se verificó con las pruebas de traducción y de unidad. **Después de ese refactor la API no se reinició**, así que los endpoints GET de proyectos, empleados y catálogos no se volvieron a probar con curl con el código refactorizado. Se recomienda un repaso rápido (P1 de la TAREA-07, E7 de la TAREA-11, `/api/catalogos`) en el próximo reinicio.

## 8. Comandos ejecutados y resultado
| Comando | Resultado |
|---|---|
| `dotnet build Profesiograma.slnx -c Release` | 0 advertencias, 0 errores (varias veces) |
| `dotnet test --solution Profesiograma.slnx -c Release` | 203 → 204 (regresión) → **217/217** |
| `dotnet ef migrations has-pending-model-changes … --configuration Release` | "No changes have been made to the model since the last migration." (antes y después del refactor) |
| `App.Infrastructure.Tests.exe -method … -showliveoutput` | SQL real de cruces y de departamentos |
| `curl -k -sS -i -X POST … /api/proyectos[/previsualizar]` y `GET` | Sección 5 |

## 9. Pendientes
- **TAREA-13:** pantalla "Nuevo proyecto". El formulario debe limpiar los campos que no correspondan al grupo al cambiarlo (pendiente 14).
- Repaso con curl de los GET tras el refactor de consultas, en el próximo reinicio de la API (sección 7).
- Pruebas de traducción para `UsuarioProvisionamiento` y `DatosPruebaSembrador` (pendiente 15).
- 400 genérico en inglés para un JSON mal formado (pendiente 16).
- Largos reales de los Id del ERP frente a las columnas (pendiente 17).
- `PRY-20261001-4ede0f` (Id 4) queda en `PROFESIOGRAMA_DEV`. Para retirarlo habría que borrarlo con SQL, lo que requiere aprobación. Se puede dejar como dato de prueba.
