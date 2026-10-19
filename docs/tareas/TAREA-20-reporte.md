# TAREA-20 — Consulta del cronograma (backend)

**Fecha:** 2026-10-19 (Fase A: plan; Fase B: implementación)
**Resultado:** implementada; **pendiente de la verificación del usuario** (`backend/tests/manual/tarea20/cronograma.cmd`).
- `dotnet build Profesiograma.slnx -c Release`: 0 advertencias, 0 errores.
- `dotnet test --solution Profesiograma.slnx -c Release`: **740/740** (691 anteriores sin cambios + 49 nuevas).
- **Sin cambios de esquema ni migraciones.** Las consultas usan los índices existentes.

**Lo que no se hizo:**
- llamadas a la API o al ERP;
- SQL;
- `dotnet run`;
- comandos git que modifiquen el repositorio;
- lectura de `resultado-*.txt` o `crear-proyecto-*.json` del usuario.

D14–D16 (reporte Excel) no se implementan: son de la TAREA-22.

## 1. Decisiones aplicadas
Las D1–D13 del usuario (19/10/2026), detalladas en `docs/fases/FASE_5_Cronograma.md` §5:
- D1: novedad PROYECTO solo ese día y en ese proyecto.
- D2: `otrosProyectos`, sin datos del proyecto ajeno.
- D3: CHOQUE con cualquier estado del proyecto.
- D4: sin personas que solo tienen novedades.
- D5: más de 90 días → 400 en `hasta`.
- D6: sin fechas, el mes actual en Ecuador.
- D7: respuesta dispersa (sin días LIBRE).
- D8: colores de proyectos en el frontend.
- D9: gana la novedad de Id mayor; `novedadesExtra` cuenta las demás.
- D10: visibilidad CREADOR.
- D11: incluye PERMISOS_MEDICOS.
- D12: endpoint por proyecto sin fechas, desde su inicio y como máximo 90 días.
- D13: LINQ sobre las tablas.

**Agregado en la implementación:** estado **`OTRO_PROYECTO`**, para el día en que la persona solo trabaja en un proyecto que no viene en la respuesta. Sin él, D2 obligaba a mostrar ese día como LIBRE o PROYECTO sin asignación visible.

## 2. Qué se hizo
**Application (`Cronograma/`):**
- `CronogramaContratos.cs`: solicitudes (texto), DTO, filas de lectura e `ICronogramaConsultas`.
- `ArmadoCronograma.cs` (lógica pura): precedencia de la celda, actividad vigente por día, cargo por proyecto, leyenda, orden y `Horario(…)` con `cruzaMedianoche` (`ReglasAlmuerzo.EsNocturno`).
- `CronogramaConsultaServicio.cs`: rango (D5, D6, D12), validación, visibilidad R1 y llamadas a las consultas. Las consultas 1b a 4 se omiten si no hay días; la 1b se omite para el Admin en el cronograma global.
- `LectorParametros.LeerId` (Id opcional) y el registro en `DependencyInjection`.

**Infrastructure:**
- `Consultas/CronogramaConsultas.cs`: 7 consultas `internal static IQueryable` (0 máximo de días, 1 días, 1b otros proyectos, 2 novedades, 3 actividades, 4 cabeceras, 5 rango del proyecto).
- Registro en `PersistenciaServiceCollectionExtensions`.

**Api:**
- `Endpoints/CronogramaEndpoints.cs`: `GET /api/cronograma` y `GET /api/proyectos/{id:int}/cronograma`, con la política Gestor.
- `Program.cs`: `MapCronogramaEndpoints`.
- Los parámetros llegan como texto (`[AsParameters]` con `string?`), así que un valor ilegible no produce un error de enlace: el servicio responde 400 en el campo.

**Scripts:**
- `backend/tests/manual/tarea20/`: `cronograma.cmd`/`.ps1`, `LEEME.md`, `.gitignore` y `simulacion\c_*` (ficticios).
- `tarea26a/comun.ps1`: `Iniciar-Salida` escribe "MODO REAL: llamadas reales a la API (SIMULAR no esta en 1)." cuando no hay simulación.

**Documentación:**
- `docs/fases/FASE_5_Cronograma.md` (nuevo).
- `docs/00_ESTADO_ACTUAL.md`: encabezado, §1, §6, §7 (4 corregido; nuevos 51 a 54) y §8.
- Este reporte.

## 3. Archivos
| Archivo | Cambio |
|---|---|
| `backend/src/App.Application/Cronograma/CronogramaContratos.cs`, `ArmadoCronograma.cs`, `CronogramaConsultaServicio.cs` | Nuevos |
| `backend/src/App.Application/Comun/LectorParametros.cs` | `LeerId` |
| `backend/src/App.Application/DependencyInjection.cs` | Registro del servicio |
| `backend/src/App.Infrastructure/Consultas/CronogramaConsultas.cs` | Nuevo |
| `backend/src/App.Infrastructure/Persistencia/PersistenciaServiceCollectionExtensions.cs` | Registro de las consultas |
| `backend/src/App.Api/Endpoints/CronogramaEndpoints.cs` | Nuevo |
| `backend/src/App.Api/Program.cs` | `MapCronogramaEndpoints` |
| `backend/tests/App.Application.Tests/Cronograma/DoblesCronograma.cs`, `ArmadoCronogramaTests.cs`, `CronogramaConsultaServicioTests.cs` | Nuevos |
| `backend/tests/App.Infrastructure.Tests/Consultas/CronogramaSqlTests.cs` | Nuevo |
| `backend/tests/manual/tarea20/` | Nuevo |
| `backend/tests/manual/tarea26a/comun.ps1` | "MODO REAL" |
| `docs/fases/FASE_5_Cronograma.md` | Nuevo |
| `docs/00_ESTADO_ACTUAL.md` | Actualizado |

## 4. Pruebas nuevas (49)
**`ArmadoCronogramaTests` (20):**
- precedencia PERSONA > GENERAL > PROYECTO; novedad sobre CHOQUE; CHOQUE sobre PROYECTO; PROYECTO sobre DESCANSO (trabajo y descanso del mismo proyecto el mismo día);
- back + principal en dos proyectos = CHOQUE;
- días libres omitidos; GENERAL en un día sin asignación;
- D1 (novedad de otro proyecto; de su proyecto en un día sin asignación en él; aplica también al descanso);
- D2 (CHOQUE con un proyecto no visible sin exponerlo; OTRO_PROYECTO);
- D4; D9 (Id mayor y `novedadesExtra` = 2);
- actividad vigente por día (mayor versión);
- orden por nombre, cargo del último día y leyenda sin repetir;
- respuesta vacía;
- `cruzaMedianoche` (22–06, 17–04, 15:30–00:00, diurno y sin horas).

**`CronogramaConsultaServicioTests` (20):**
- D6 (mes actual en Ecuador con el reloj de las 22:00 del 18/10; solo `hasta`);
- 400 por fecha ilegible, Id ilegible o 0 y `hasta < desde`;
- D5: límite 90 válido y 91 inválido, parámetro de 7 días y parámetro ausente → 90;
- rango sin días (200 vacío, sin consultar otros proyectos);
- gestor: solo sus proyectos y `otrosProyectos` del ajeno, sin mostrarlo; admin: todo, sin la consulta 1b;
- los filtros llegan a la consulta;
- proyecto suspendido a mitad del rango;
- por proyecto: no visible o inexistente → null (404); D12 (inicio a fin, proyecto largo → 90 días, `desde` después del fin); cuenta los demás proyectos; fecha ilegible → 400.

**`CronogramaSqlTests` (9):** ToQueryString de las 7 consultas:
- días con todos los filtros, sin `[Cedula]`, y Admin por proyecto;
- otros proyectos con `GROUP BY`/`COUNT(*)`, sin `[Codigo]` ni `[NombreVisual]`, y su variante por proyecto;
- novedades no anuladas, actividades, cabeceras y rango.

## 5. Comandos y resultados
| Comando | Resultado |
|---|---|
| `dotnet build Profesiograma.slnx -c Release` | 0 advertencias, 0 errores |
| `dotnet test --solution Profesiograma.slnx -c Release` | 740/740 |
| `cronograma.cmd` con `SIMULAR=1` en una **copia** en el scratchpad | 8 casos, 0 no esperados; salida 0; "PERSONA FICTICIA" / "PROYECTO FICTICIO" en la salida: **0**; "ERROR INTERNO": 0 (sin red) |
| Variante sin personas | Caso `codigoEkon` omitido con su mensaje; 0 no esperados |
| `Iniciar-Salida` sin `SIMULAR` (script de prueba que **solo** abre y cierra el archivo, sin solicitudes) | Primera línea "MODO REAL: …"; con `SIMULAR=1`, "MODO SIMULACION …" |

**Errores durante la implementación y su corrección:**
1. **Endpoint por proyecto:** CS4010/CS0029, porque el operador ternario mezclaba dos tipos de `Results`. Se reescribió con un `if` explícito.
2. **Dos pruebas mal planteadas** (se corrigieron antes de ejecutarlas):
   - un caso de "91 días" que en realidad tenía 90;
   - una aserción sobre la consulta 1b sin días, en la que la consulta no se ejecuta.

## 6. Verificación del usuario (pendiente)
1. Con la API corriendo, ejecute `backend/tests/manual/tarea20/cronograma.cmd`. La primera línea debe decir **MODO REAL**; si dice SIMULACION, ejecute `set SIMULAR=`.
2. Opcional: `PROYECTO_ID`, `PROYECTO_NO_VISIBLE`, `CODIGO_EKON` y `DEV_USER` (ver `LEEME.md`).
3. Pegue `resultado-cronograma.txt`: solo conteos, códigos EKON, Id, estados y siglas.

## 7. Pendientes
- **51 (D15):** CEDULA en el reporte.
- **52 (D16):** almuerzo nulo en el reporte.
- **53:** PROYECTO para PLANTA/OFICINAS.
- **54:** salida de los turnos nocturnos en Infor.
- **D14:** origen de CARGO INFOR (TAREA-22).
- **4:** corregido; falta solo el Office Script.
- **TAREA-21:** la vista asigna los colores de los proyectos (D8) y hace la búsqueda por nombre en el cliente.
- **`PROYECTO_VISIBILIDAD`:** sigue sin uso (D10); se retoma en la TAREA-25.
