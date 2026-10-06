# TAREA-19x — Token de concurrencia desde el cliente (pendiente 32)

**Fecha:** 2026-10-06 (Fase A y Fase B)
**Resultado:** ✅ completada. Prueba manual del usuario del 06/10/2026 (`parte1.cmd` y `parte2.cmd`) y verificación visual W1–W3: todo OK (secciones 6 y 7). Dato de prueba: Id 12 en versión 6 tras la parte 1; las W1–W3 agregaron versiones (versión ≥ 6).

**Backend**
- `dotnet build Profesiograma.slnx -c Release --no-incremental`: 0 advertencias y 0 errores.
- `dotnet test`: **510/510**. Son 486 anteriores más 24 nuevas. Ajuste aprobado P5: 41 llamadas de 39 pruebas existentes ahora llevan el token; ninguna aserción cambió.
- `has-pending-model-changes`: sin cambios.

**Frontend**
- `npm run build` y `npm run lint`: sin errores ni advertencias.
- `npm test`: **123/123** (117 anteriores + 6 nuevas; las fixtures llevan `versionProyecto: 1`).

**Lo que no se hizo:** `dotnet run`, `npm run dev`, `dotnet ef database update`, SQL que modifique datos, ningún cliente HTTP contra la API, ni comandos git que modifiquen el repositorio. Los scripts manuales se probaron solo en modo simulación (sección 6.3).

## 1. Problema y decisiones
**Problema (V15 de la TAREA-19a).** Con dos pestañas sobre el Id 12, la B registraba sobre los datos de la A sin recibir 409, porque el cliente no indicaba sobre qué versión decidió. La relectura dentro del applock solo detecta carreras dentro de la misma petición.

| # | Decisión aprobada |
|---|---|
| Token | Última versión de etapa (`versionProyecto`, entero; 0 si no hay etapas). Toda escritura de un proyecto crea una etapa (sección 2). El `RowVer` no sirve: la actualización de personal no modifica la fila `Proyecto` |
| P1 | Comprobación previa fuera del applock (token ≠ actual → 409 antes de cualquier 400, "No hay cambios" o cruces) y comprobación definitiva dentro del applock, antes de calcular la versión nueva |
| P2 | Token viejo → 409 "El proyecto cambió; vuelve a cargarlo." en los 4 registros. La relectura actual del cambio de estado conserva "El proyecto cambió de estado; vuelve a cargarlo." |
| P3 | Sin token → 400 `versionProyecto` "Falta la versión del proyecto; vuelve a cargarlo.", antes de comprobar la visibilidad, sin leer la base ni abrir la transacción |
| P4 | El GET del detalle no devuelve el token |
| P5 | Ajuste de pruebas: solo se agrega el token a las solicitudes de registro (`ConVersion`) y `versionProyecto: 1` a las fixtures del frontend |
| P6 | `parte2.cmd` aprobado (diseñado para no escribir) |
| Orden de lectura | Los GET y las vistas previas leen la versión **antes** que los datos, para que el error caiga del lado seguro |

## 2. Verificación: ¿toda escritura crea una etapa?
| Escritura | Etapa | ¿Toca la fila `Proyecto` (RowVer)? |
|---|---|---|
| Creación (`ProyectoRepositorio`) | v1 CREACION | Sí |
| Suspensión y cierre (`CambioEstadoRepositorio`) | máx + 1 | Sí |
| Actualización de personal (`EdicionPersonalRepositorio`) | máx + 1 ACTUALIZACION_PERSONAL | **No** |
| Reactivación (`EdicionPersonalRepositorio` con `Reactivacion`) | máx + 1 REACTIVACION | Sí |
| Cabecera y actividad (`EdicionCabeceraRepositorio`) | máx + 1 EDICION_CABECERA o CAMBIO_ACTIVIDAD | Sí |

Las otras escrituras no tocan proyectos: `DatosPruebaSembrador` (solo en desarrollo, con etapas) y `UsuarioProvisionamiento`. Desde la C9, registrar sin cambios no escribe. Esta condición queda como regla 3 en `00_ESTADO_ACTUAL.md` §7.1, con la nota para la Fase 3.

## 3. Qué se hizo
**Backend**
- **`Proyectos/VersionProyecto.cs`** (nuevo): `Clave`, `MensajeFalta` y `ErroresFalta()`, con la documentación del orden de las comprobaciones.
- **Solicitudes:** `int? VersionProyecto = null` como último parámetro de `CambioEstadoSolicitud`, `ActualizarPersonalSolicitud`, `ReactivarProyectoSolicitud` y `EditarCabeceraSolicitud`. Es opcional en el contrato porque la vista previa usa el mismo cuerpo.
- **Respuestas:** `int VersionProyecto` en `CabeceraDto`, `EdicionPersonalDto`, `ReactivacionDto`, `CambioEstadoPrevisualizacionDto`, `PrevisualizacionPersonalDto`, `PrevisualizacionReactivacionDto` y `PrevisualizacionCabeceraDto`.
- **Servicios** (`CambioEstadoServicio`, `EdicionPersonalServicio`, `ReactivacionServicio`, `EdicionCabeceraServicio`):
  - GET y vista previa: `ObtenerUltimaVersionEtapaAsync` antes de leer los datos;
  - registro: (1) sin token → 400; (2) cálculo de fuera, con 404 primero; (3) comprobación previa → 409 antes de los 400, C9 y cruces; (4) dentro del applock, tras las relecturas existentes, la comprobación definitiva → 409. La versión nueva es la actual + 1.
  - `EstadoCambio.Cambiado` nuevo para el cambio de estado.
- **Api:** `ProyectoEndpoints` mapea `EstadoCambio.Cambiado` → 409 `ProyectoCambiado()`. Personal, reactivación y cabecera ya mapeaban `EstadoEdicion.Cambiado`.
- **Infrastructure:** sin cambios. Se reutiliza `ConsultaUltimaVersion`, que ya tiene prueba ToQueryString en `CambioEstadoSqlTests`. No hay consultas EF nuevas.

**Frontend**
- **`tipos.ts`:**
  - `versionProyecto` en `PrevisualizacionCambioEstado`, `CabeceraEdicion` y `PrevisualizacionCabecera`;
  - `versionProyecto?` en `SolicitudCambioEstado` y `SolicitudEditarCabecera`;
  - contratos para la 19b/19c (sin pantallas): `EdicionPersonal`, `PersonaEdicion`, `SolicitudActualizarPersonal`, `PrevisualizacionPersonal`, `PersonalActualizado`, `Reactivacion`, `SolicitudReactivar`, `PrevisualizacionReactivacion`, `ProyectoReactivado` y sus tipos auxiliares.
- **Funciones de registro:** `aSolicitudRegistroCambio` (`cambioEstado.ts`) y `aSolicitudRegistroCabecera` (`edicionCabecera.ts`) agregan el token de la vista previa vigente. Los diálogos las usan al registrar. Las funciones de vista previa no cambian.
- **409:** verificado en el código; los dos diálogos ya ofrecían "Recargar datos del proyecto".
- **Cabecera:** `clavesRecarga` pasa a `['proyecto', 'versionProyecto']`. En el cambio de estado, todo 400 ya ofrecía recarga.

## 4. Archivos
| Capa | Archivo | Cambio |
|---|---|---|
| Application | `Proyectos/VersionProyecto.cs` (nuevo) | Constantes y errores del token |
| Application | `Estados/CambioEstadoContratos.cs`, `Estados/CambioEstadoDtos.cs`, `Estados/CambioEstadoServicio.cs` | Token en solicitud y vista previa; `Cambiado`; comprobaciones |
| Application | `Personal/EdicionPersonalContratos.cs`, `Personal/EdicionPersonalDtos.cs`, `Personal/EdicionPersonalServicio.cs` | Token en solicitud, GET y vista previa; comprobaciones |
| Application | `Reactivacion/ReactivacionContratos.cs`, `Reactivacion/ReactivacionDtos.cs`, `Reactivacion/ReactivacionServicio.cs` | Ídem |
| Application | `Cabecera/EdicionCabeceraContratos.cs`, `Cabecera/EdicionCabeceraDtos.cs`, `Cabecera/EdicionCabeceraServicio.cs` | Ídem |
| Api | `Endpoints/ProyectoEndpoints.cs` | 409 de `EstadoCambio.Cambiado`; comentario |
| Tests | `Proyectos/VersionProyectoTests.cs` (nuevo) | 24 pruebas (sección 5) |
| Tests | `Estados/DoblesEstados.cs`, `Cabecera/DoblesCabecera.cs`, `Personal/DoblesPersonal.cs` | `Versiones` (cola) y `Orden` en los repositorios falsos; ayudantes `ConVersion` |
| Tests | `ActividadVigenteTests.cs`, `Cabecera/EdicionCabeceraServicioTests.cs`, `Cabecera/EdicionCabeceraH15Tests.cs`, `Estados/CambioEstadoServicioTests.cs`, `Personal/EdicionPersonalC9Tests.cs`, `Personal/EdicionPersonalServicioTests.cs`, `Reactivacion/ReactivacionServicioTests.cs` | `.ConVersion(…)` en las solicitudes de registro (P5) |
| Manual | `backend/tests/manual/tarea19x/parte1.cmd`, `parte2.cmd` (nuevos) | Prueba manual (sección 6) |
| Frontend | `features/proyectos/tipos.ts` | Token y contratos de personal y reactivación |
| Frontend | `cambioEstado.ts`, `components/DialogoCambioEstado.tsx` | `aSolicitudRegistroCambio` |
| Frontend | `edicionCabecera.ts`, `components/DialogoEditarCabecera.tsx` | `aSolicitudRegistroCabecera`; `clavesRecarga` |
| Frontend | `cambioEstado.test.ts`, `edicionCabecera.test.ts` | Fixtures con `versionProyecto: 1` y 3 pruebas nuevas cada uno |
| Docs | `FASE_5_Estados_Proyecto.md` §9, `FASE_5_Edicion_Cronograma.md` §9, `FASE_5_Edicion_Cabecera.md` §8 | Contrato `versionProyecto` |
| Docs | `00_ESTADO_ACTUAL.md` | §6 (19x, 19y), §7 (32 resuelto, 33 nuevo), §7.1 (regla 3), §8 y §9 |

## 5. Pruebas
### 5.1 Pruebas existentes ajustadas (P5): solo la solicitud, ninguna aserción
Cambiaron 41 llamadas de 39 pruebas: `RegistrarAsync(…, s.ConVersion(repo), …)`, o `ConVersion(1)` en los casos de 404 sin repositorio a mano.

| Archivo | Pruebas |
|---|---|
| `ActividadVigenteTests.cs` | `EtapaSuspension_UsaLaRegla_…`, `EtapaActualizacionPersonal_…` |
| `Cabecera/EdicionCabeceraServicioTests.cs` | `Registrar_SoloActividad_…`, `Registrar_P3_…`, `Registrar_AcortarYHorario_…`, `Relectura_EstadoCambiado_409`, `Relectura_RowVerOFechasCambiadas_409` (2 llamadas), `ConflictoAlGuardar_409_YRevierte`, `NoActivo_400Proyecto_YNoVisible404`, `C9_SinCambios_…` |
| `Cabecera/EdicionCabeceraH15Tests.cs` | `Registro_DescansosAgregadosEnElCambio_…` (2 llamadas) |
| `Estados/CambioEstadoServicioTests.cs` | `Reactivacion_400_SinAbrirTransaccion`, `NoVisibleOInexistente_404`, `Aplicar_Suspension_…`, `Aplicar_Cierre_DesdeSuspendido`, `Aplicar_EstadoCambio…_409_YRevierte`, `Aplicar_FechaFinCambio…_409`, `Aplicar_ConcurrenciaAlGuardar_409_YRevierte`, `Aplicar_EliminadoDentroDeLaTransaccion_404` |
| `Personal/EdicionPersonalC9Tests.cs` | `SinCambios_Registro400General_SinAbrirTransaccion` |
| `Personal/EdicionPersonalServicioTests.cs` | `P6_…`, `P7_…`, `P11_NoVisible_404`, `Relaciones_PrincipalClaveYPrincipalId`, `P13_…`, `P14_…`, `DentroDeLaTransaccion_EstadoCambio_409`, `DentroDeLaTransaccion_AparecenCrucesExternos_409ConCruces`, `ConflictoAlGuardar_409_YRevierte` |
| `Reactivacion/ReactivacionServicioTests.cs` | `SinActividad_…`, `CrucesExternos_…`, `Registrar_EtapaReactivacion_…`, `H4_…`, `Registrar_Relaciones_…`, `Registrar_Snapshot…`, `Relectura_EstadoCambiado_409…`, `Relectura_FechaFinCambiada_409…`, `ConflictoDeConcurrencia_409`, `NoVisible_404` |

**Cómo se verificó que solo cambió la solicitud:**
- Con el código nuevo y sin el ajuste fallaban exactamente esas 39 pruebas, todas por la falta del token.
- Con el ajuste pasaron las 486, sin modificar ninguna aserción.
- A los dobles se les agregó la cola `Versiones` y la lista `Orden`. Con la cola vacía se comportan como antes.

### 5.2 Pruebas nuevas
**`VersionProyectoTests.cs` (24):** los 5 casos por servicio, más una prueba por servicio de los GET y vistas previas.

| Caso | Cambio de estado | Cabecera | Personal | Reactivación |
|---|---|---|---|---|
| (a) sin token → 400 `versionProyecto`, sin leer la base ni abrir la transacción | ✅ | ✅ | ✅ | ✅ |
| (b) token viejo → 409 sin transacción ni escritura | ✅ | ✅ | ✅ | ✅ |
| (c) token correcto → 200, versión actual + 1 | ✅ | ✅ | ✅ | ✅ |
| (d) versión distinta **dentro del applock** (cola 3 → 4) → 409, transacción revertida, sin escritura | ✅ | ✅ | ✅ | ✅ |
| (e) token viejo con una solicitud que daría 400 o "No hay cambios" → 409 (con el token vigente, el 400 de siempre) | ✅ fecha fuera de rango | ✅ V15: mismo horario | ✅ sin cambios | ✅ fecha fin inválida |
| GET y vista previa devuelven la versión, leída antes que los datos (`Orden` = version, datos) | vista previa | ✅ | ✅ | ✅ |

**Frontend (6):**
- `cambioEstado.test.ts`: el registro lleva el token de la vista y la vista previa no; 409 → recarga; 400 `versionProyecto` → recarga.
- `edicionCabecera.test.ts`: las mismas tres para la cabecera.

## 6. Prueba manual (usuario, 06/10/2026) — todo OK
Ejecutada por el usuario el 06/10/2026 con la API corriendo, desde `backend\tests\manual\tarea19x\` (salidas `resultado-parte1.txt` y `resultado-parte2.txt`, ignoradas por git):
1. `parte1.cmd`: escribe solo en el Id 12. Protección: si existe `resultado-parte1.txt`, no se ejecuta; el archivo se crea justo antes del primer registro que escribe (d).
2. `parte2.cmd`: diseñado para no escribir; se puede repetir. Usa el Id 12 y el Id 2.

### 6.1 `parte1.cmd` (Id 12)
| Paso | Solicitud | Esperado | ¿Escribe? | Resultado |
|---|---|---|---|---|
| a | GET cabecera | V = `versionProyecto`; R1 y R2 = regresos de almuerzo posteriores a la salida y distintos del actual. Si el Id 12 no está ACTIVO o faltan datos: termina sin escribir | No | OK: V = 5 |
| b | Vista previa con regreso R1 | 200 y `versionProyecto` = V | No | OK: 200, `versionProyecto` 5 |
| c | Registro **sin token** y sin cambios (regreso actual) | 400 `versionProyecto`. Si el control fallara, daría 400 "No hay cambios": tampoco escribe. Si b o c no dan lo esperado: termina sin escribir | No | OK: 400 `versionProyecto` (no escribió) |
| d | Registro regreso R1 con token V | 200, versión V+1 | **Sí (Id 12)** | OK: 200, versión 6 |
| e | Registro regreso R2 con el **mismo** token V | 409 "El proyecto cambió; vuelve a cargarlo." | No (si fallara: V+2) | OK: 409 "El proyecto cambió; vuelve a cargarlo." (no escribió) |
| f | GET cabecera | CONTROL: `versionProyecto` = V+1 y regreso R1 | No | OK: `versionProyecto` 6, regreso 14:00 |

### 6.2 `parte2.cmd` (Id 12 e Id 2; sin escritura posible)
Cada registro lleva un token viejo (V − 1) **y** un cuerpo que el servidor rechazaría con 400. Si la comprobación previa fallara, la respuesta sería 400, que tampoco escribe.

| Paso | Solicitud | Esperado | Resultado |
|---|---|---|---|
| qa / qb | GET cabecera del Id 12 (V) y GET reactivación del Id 2 (VR) | Tokens | OK |
| q1 | cambio-estado Id 12: SUSPENDIDO con fecha 01/01/2000 y token V−1 | 409 | OK: 409 (no escribió) |
| q2 | cambio-estado Id 12: el mismo cuerpo sin token | 400 `versionProyecto` | OK: 400 `versionProyecto` (no escribió) |
| q3 | personal Id 12: un principal sin empleado y token V−1 | 409 (sin `extensions`) | OK: 409 (no escribió) |
| q4 | cabecera Id 12: regreso actual (sin cambios) y token V−1 | 409 (antes del 400 de la C9) | OK: 409 (no escribió) |
| q5 | reactivación Id 2: fechas 01/01/2000 y token VR−1 | 409 | OK: 409 (no escribió; el Id 2 sigue SUSPENDIDO) |

### 6.3 Prueba de la lógica de los scripts (Claude, solo simulación)
**Cómo se ejecutó**
- Copias de los dos scripts en la carpeta temporal de la sesión, con `SIMULAR=1`, BASE `https://localhost:1` y un PATH sin System32 (solo PowerShell y una copia de `chcp.com`). Así `curl.exe` no se podía ejecutar.
- Las respuestas simuladas salieron de archivos generados con `sim19x.py`.
- **No hubo ninguna llamada a la API.**

| Escenario | Resultado |
|---|---|
| normal | parte1: CONTROL b/c = 1; d 200 v6; e 409; f v6 y regreso R1. parte2: q1–q5 "OK". Salida 0 |
| segunda ejecución | parte1: "Ya existe resultado-parte1.txt…", salida 1, no ejecuta nada |
| Id 12 no ACTIVO | parte1: "no esta ACTIVO: NO se escribio nada", sin `resultado-parte1.txt` |
| c responde 400 `general` (servidor sin control del token) | parte1: CONTROL b/c = 0, "NO se escribio nada", sin `resultado-parte1.txt` |
| q3 responde 400 | parte2: CONTROL q3 "DISTINTO (esperado 409)" |

**Cuerpos JSON generados:** se revisaron en `tmp\`. Por ejemplo, `pd` = `{"regresoAlmuerzo":"14:00","versionProyecto":5}` y `q3` lleva un principal con `empleadoId: null`.

## 7. Verificación visual (usuario, 06/10/2026) — W1–W3 OK
| # | Pasos | Esperado | ¿Escribe? | Resultado |
|---|---|---|---|---|
| W1 | Id 12, dos pestañas con "Editar datos generales" y el impacto listo, con cambios **distintos**: A cambia el almuerzo, B el horario. Registrar A y después B | B: 409 "El proyecto cambió; vuelve a cargarlo." y "Recargar datos del proyecto". Tras recargar, B ve los datos de A y puede volver a ver el impacto y registrar | Sí (Id 12: solo A) | OK: B recibió 409 y "Recargar datos del proyecto"; tras recargar vio los datos de A |
| W2 | Id 12: pestaña B con el impacto de "Suspender" listo; en la pestaña A editar la cabecera y registrar; luego confirmar en B | B: 409 y "Recargar datos del proyecto"; el proyecto **no** se suspende | Sí (Id 12: solo la cabecera de A) | OK: 409; el proyecto siguió ACTIVO |
| W3 | Flujo normal de los dos diálogos (por ejemplo, otro cambio de cabecera en el Id 12) | Registra como antes; el token viaja solo | Sí (Id 12) | OK: flujo normal de los dos diálogos |

## 8. Contradicciones y observaciones
1. **Mensajes de 409 del cambio de estado.** Ahora hay dos: el token viejo da "El proyecto cambió; vuelve a cargarlo." y la relectura de estado sigue dando "El proyecto cambió de estado; vuelve a cargarlo." (P2). Con token, la relectura casi nunca se alcanza, porque un cambio de estado crea una etapa y la comprobación previa responde antes.
2. **Personal y reactivación tienen dos 409.** Uno por cruces, con `extensions.cruces` y `extensions.resumen`, y otro por proyecto cambiado, sin `extensions`. El frontend de la 19b/19c los distingue por las extensiones.
3. **La relectura con `RowVer` de la cabecera (C8) se conserva.** No detecta una actualización de personal intermedia, porque esta no modifica la fila `Proyecto`. Ahora lo cubre el token.
4. **Visibilidad en la vista previa.** La vista previa lee la versión antes de comprobar la visibilidad. Para un proyecto no visible responde 404 igual, y el número leído no se devuelve.

## 9. Pendientes
- **Pendiente 33 → TAREA-19y** (antes de la 19b): principal opcional (ver `00_ESTADO_ACTUAL.md` §7).
- TAREA-19b (personal) y 19c (reactivación) usarán los contratos ya listos en `tipos.ts` y deben registrar con el token de su vista previa.
