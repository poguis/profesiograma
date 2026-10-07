# TAREA-26b — Sincronización de empleados (EvolutionEmployee)

> **DESCARTADA (07/10/2026, reemplazada por la 26d).** El usuario eligió la opción C: la API es la única fuente de los empleados, sin sincronización masiva; `Empleado` solo contiene a las personas asignadas. La 26b nunca se ejecutó contra la base. En la TAREA-26d-1 se retiró su código (endpoint `/api/admin/empleados/sincronizar`, plan masivo, repositorio, applock `profesiograma:empleados`, scripts `tarea26b`); se conservaron el cliente HTTP con DTO de campos permitidos, sus pruebas de datos sensibles y la opción de timeout. Este reporte queda como historial.

**Fecha:** 2026-10-07 (Fase B; diseño en la Fase A de la TAREA-26)
**Resultado:** implementada. Falta la verificación del usuario (S1–S10, sección 6).
- `dotnet build Profesiograma.slnx -c Release`: **0 advertencias, 0 errores**.
- `dotnet test --solution Profesiograma.slnx -c Release`: **578/578** (549 anteriores sin cambios + 29 nuevas).
- `dotnet ef migrations has-pending-model-changes … --configuration Release`: "No changes have been made to the model since the last migration." **Sin migraciones.**
- `sincronizar.cmd` probado **solo con `SIMULAR=1`**.

**Lo que no se hizo:**
- ninguna llamada a una API, ni a la nuestra ni a `backstack.sedemi.com`;
- `dotnet run`, `npm run dev`, `user-secrets`;
- SQL;
- comandos git que modifiquen el repositorio.

No se leyeron los `resultado-*.txt` ni los `cuerpo-empleados-*.json` reales de `tarea26a`. Las pruebas existentes no cambiaron.

## 1. Decisiones aprobadas
| # | Decisión |
|---|---|
| Contrato | El confirmado por el sondeo (TAREA-26a), con el cuerpo fijo `parameter` vacío y `estado = "A"`. `isSuccess = false` o `result` nulo → no disponible |
| P7 | Los que ya no vienen → `EstadoErp = "I"`, nunca se borran |
| P8 | Se excluyen los `CodigoEkon` con prefijo "DEV" (el sondeo confirmó que ningún código real lo tiene) |
| P9 | No se escribe nada si llegan menos del 50 % de los activos actuales no DEV; con 0 activos, la primera sincronización procede |
| P10 | La cédula no se mapea |
| P11 | `FechaSincronizacion` de los sin cambios con `ExecuteUpdate`, sin auditoría |
| P12 | Historial solo en el log (sin tabla) |
| P13 | Modo Simulado → 400 "La sincronización de empleados requiere el modo Http del ERP." |
| P14 | Solo endpoint y script (sin pantalla) |
| P16 | Sin SQL para `UsuarioDepartamento` |

Contrato, mapeo, sensibles y reglas S1–S15: `docs/fases/FASE_4_Sincronizacion_Empleados.md`.

## 2. Qué se hizo
- **Application (`Empleados/Sincronizacion/`):**
  - `EmpleadoErp` (solo campos permitidos), `IFuenteEmpleadosErp` (`Habilitada` + `ObtenerActivosAsync`), `DatosEmpleado`, `EmpleadoGuardado`, `ISincronizacionEmpleadosRepositorio`, `ITransaccionEmpleados` y `SincronizacionEnCursoException`;
  - **`PlanSincronizacionEmpleados.Calcular`**, lógica pura: normaliza y recorta; excluye los DEV; rechaza con motivo; clasifica en nuevos, actualizados (cambio de datos o reactivado) y sin cambios; inactiva a los que ya no vienen; calcula los cargos nuevos y la protección del 50 %;
  - **`SincronizacionEmpleadosServicio`**: modo Simulado → `NoDisponible`; lectura del ERP fuera de la transacción; dentro del bloqueo, lectura de la caché, plan, conteo de inactivados con asignaciones y aplicación; confirma solo si se realizó; `SincronizacionEnCursoException` → `EnCurso`. La fecha se trunca a segundos (DATETIME2(0)) y "hoy" es la fecha de Ecuador (`FechaNegocio`).
- **Infrastructure:**
  - `FuenteEmpleadosErpHttp`:
    - `HttpClient` tipado con `TimeoutEmpleadosSegundos` (60);
    - POST con el cuerpo fijo;
    - deserializa a `EmpleadoJson`, solo con campos permitidos; `nombrecompleto` se lee sin distinguir mayúsculas y los códigos admiten texto o número;
    - log Debug con operación, código y ms, sin cuerpo ni URL;
    - un JSON inválido se informa sin la excepción interna.
  - `FuenteEmpleadosDeshabilitada` para el modo Simulado.
  - `SincronizacionEmpleadosRepositorio` y `TransaccionEmpleados` (applock `profesiograma:empleados`, `LockTimeout = 0`).
  - `ServiciosExternosOpciones.TimeoutEmpleadosSegundos` (validado entre 1 y 600).
- **Api:** `POST /api/admin/empleados/sincronizar` (`Politica.Admin`), en `EmpleadoEndpoints`:

  | Caso | Respuesta |
  |---|---|
  | Sincronización realizada | 200 con el DTO |
  | Modo Simulado | 400 |
  | Otra sincronización en curso | 409 |
  | Respuesta incompleta (P9) | 502 |
  | ERP no disponible | 503 (manejador existente) |

  Todos los casos de error se responden con ProblemDetails, y el log lleva solo conteos.
- **Manual (`backend/tests/manual/tarea26b/`):**
  - `sincronizar.cmd`/`.ps1`: reutiliza `tarea26a/comun.ps1`, al que se le agregó un ajuste mínimo para aceptar una ruta absoluta de salida. Llama como `admin` (o `SINCRONIZAR_USUARIO=gestor` para probar el 403) e imprime solo los conteos y los rechazos agrupados por motivo, sin códigos;
  - `conteos.sql`: solo lectura;
  - `.gitignore`.

## 3. Archivos
| Archivo | Cambio |
|---|---|
| `backend/src/App.Application/Empleados/Sincronizacion/` (nuevo) | `SincronizacionEmpleadosContratos.cs`, `PlanSincronizacionEmpleados.cs`, `SincronizacionEmpleadosServicio.cs` |
| `backend/src/App.Application/DependencyInjection.cs` | `SincronizacionEmpleadosServicio` |
| `backend/src/App.Infrastructure/Erp/FuenteEmpleadosErpHttp.cs` (nuevo) | Cliente HTTP y fuente deshabilitada |
| `backend/src/App.Infrastructure/Erp/ServiciosExternosOpciones.cs`, `ErpServiceCollectionExtensions.cs` | `TimeoutEmpleadosSegundos`; registro según el modo |
| `backend/src/App.Infrastructure/Persistencia/Empleados/SincronizacionEmpleadosRepositorio.cs` (nuevo) | Repositorio y transacción |
| `backend/src/App.Infrastructure/Persistencia/PersistenciaServiceCollectionExtensions.cs` | Registro |
| `backend/src/App.Api/Endpoints/EmpleadoEndpoints.cs` | Endpoint de administración |
| `backend/tests/App.Application.Tests/Empleados/Sincronizacion/` (nuevo) | `PlanSincronizacionEmpleadosTests` (10), `SincronizacionEmpleadosServicioTests` (6) |
| `backend/tests/App.Infrastructure.Tests/Erp/FuenteEmpleadosErpHttpTests.cs` (nuevo) | 9 pruebas |
| `backend/tests/App.Infrastructure.Tests/Consultas/SincronizacionEmpleadosSqlTests.cs` (nuevo) | 4 pruebas `ToQueryString` |
| `backend/tests/manual/tarea26b/` (nuevo) | `sincronizar.cmd`, `sincronizar.ps1`, `conteos.sql`, `.gitignore` |
| `backend/tests/manual/tarea26a/comun.ps1` | `Iniciar-Salida` acepta una ruta absoluta (reutilización desde `tarea26b`) |
| `docs/fases/FASE_4_Sincronizacion_Empleados.md` (nuevo) | Contrato, mapeo, sensibles y reglas |
| `docs/fases/FASE_2_Modelo_Datos.md` | Contrato verificado (fila 10); C31 ampliado |
| `docs/00_ESTADO_ACTUAL.md`, `docs/tareas/TAREA-26a-reporte.md` | Cierre de la 26a y 26b (ver la parte 1 de la tarea) |
| `docs/tareas/TAREA-26b-reporte.md` (nuevo) | Este reporte |

## 4. Pruebas nuevas (29)
| Archivo | Casos |
|---|---|
| `PlanSincronizacionEmpleadosTests` (10) | Mapeo y normalización (espacios, vacíos → null, cargo en mayúsculas); sin cambios / actualizados / reactivado si estaba "I"; P7 inactivación una sola vez; P8 DEV no se crean, no se tocan ni se inactivan; rechazos con motivo (vacío, > 20, repetido, nombre vacío, `codEmpresa` > 10), y un rechazado existente no se inactiva; estado distinto de "A" → rechazado e inactivado; recorte de textos (200); P9 (4 de 10 bloquea, 5 de 10 procede, vacío bloquea, 0 activos procede); cargos nuevos sin repetir; **idempotencia** (aplicar en memoria y recalcular → 0 cambios) |
| `SincronizacionEmpleadosServicioTests` (6) | Simulado → 400 sin leer el ERP ni abrir la transacción; ERP no disponible → se propaga sin transacción; realizada con conteos, fecha truncada a segundos, hoy en Ecuador y confirmación; P9 bloqueada sin aplicar y con reversión; en curso → `EnCurso`; detalle de rechazados máximo 50 |
| `FuenteEmpleadosErpHttpTests` (9) | Mapeo solo de los campos permitidos, `codPersona`/`codPuesto` como número, URL, POST y cuerpo fijo exacto; **datos sensibles:** un JSON ficticio con los 18 campos sensibles (valores "SENSIBLE-…") → ningún valor sensible en el resultado serializado, y ni `EmpleadoErp`, ni `DatosEmpleado`, ni el DTO interno tienen esas propiedades; `isSuccess = false`, `result` nulo, código con tipo no admitido y JSON inválido → no disponible, sin excepción interna ni fragmentos en el mensaje o el log; HTTP 500 → log Debug sin cuerpo ni URL; error de red; fuente deshabilitada y timeout por defecto 60 |
| `SincronizacionEmpleadosSqlTests` (4) | `ToQueryString`: empleados guardados (sin `[Cedula]`), cargos, por Id (`IN`, para actualizar, inactivar y `ExecuteUpdate`), con asignaciones activas (`DISTINCT`, JOIN a `Proyecto`, ACTIVO, no eliminado, fin ≥ hoy) |

## 5. Comandos
| Comando | Resultado |
|---|---|
| `dotnet build Profesiograma.slnx -c Release` | 0/0 (una advertencia CS8602 intermedia corregida con `OfType<EmpleadoJson>()`) |
| `dotnet test --solution Profesiograma.slnx -c Release` | 578/578 (dos intentos previos fallidos por errores de las pruebas nuevas, ver abajo) |
| `dotnet ef migrations has-pending-model-changes … --configuration Release` | Sin cambios |
| `sincronizar.cmd` con `SIMULAR=1`: respuestas simuladas 200, 409 y 403 (en el scratchpad) | Conteos y rechazos por motivo (200); `title` del ProblemDetails (409); solo el código (403). La salida se borró después |

**Errores durante el desarrollo y cómo se resolvieron:**
1. **Application no referencia el paquete de logging.** El registro del resultado (P12) se movió al endpoint, en la capa Api, para no agregar una dependencia.
2. **CS0118 en las pruebas.** El ayudante `Erp(...)` chocaba con el espacio de nombres `App.Application.Tests.Erp`; se renombró a `Recibido(...)`.
3. **Ayudante de prueba con códigos DEV.** Calculaba los datos de un guardado `DEV…` con el propio plan, que los excluye; ahora calcula con un código válido (los datos no dependen del código).

**Cómo se garantizó que no hubo llamadas:**
- las pruebas usan un `HttpMessageHandler` falso y dobles;
- el script se ejecutó solo con `SIMULAR=1`, con lo que `Invocar-Http` lanza un error si se llama y la base se fuerza a `https://localhost:1`;
- los archivos del usuario en `tarea26a` mantienen su fecha.

## 6. Verificación del usuario — S1–S10
Requisitos:
- API reiniciada con este código y en modo Http (user-secrets de la 26a);
- usuario `admin` para sincronizar;
- pegue en el chat solo `resultado-sincronizar.txt` y los conteos de `conteos.sql`.

| # | Caso | Pasos | Esperado | ¿Escribe? | Resultado |
|---|---|---|---|---|---|
| S1 | Primera sincronización | `tarea26b\sincronizar.cmd` | 200. Recibidos ≈ 1633; nuevos ≈ 1633; actualizados 0; inactivados 0 (los DEV no se tocan); rechazados 0 (el sondeo no mostró códigos repetidos ni vacíos); cargos nuevos > 0. En el log de la API, una línea "Sincronización de empleados: …" con conteos | **Sí (Empleado, CargoInfor)** | |
| S2 | Idempotencia | `sincronizar.cmd` otra vez | 200. Nuevos 0, actualizados 0, inactivados 0, sin cambios ≈ 1633, cargos nuevos 0 | Sí (solo `FechaSincronizacion`) | |
| S3 | Conteos | `conteos.sql` en SSMS | ERP "A" ≈ 1633; DEV (prueba) "A" = 8; "UNIDAD SISTEMA INTEGRADO DE GESTION" = 85 en la consulta 3; `CargoInfor` con cargos sin código Infor; **`EmpleadosErpConCedula` = 0** | No | |
| S4 | Columnas sin datos sensibles | Consulta 6 de `conteos.sql` | Solo las columnas del modelo (sin sueldo, teléfono, dirección, nacimiento ni correo personal); `Cedula` vacía para los empleados del ERP (S3) | No | |
| S5 | Buscador con empleados reales | `gestor` → "Nuevo proyecto" → "Agregar principal" | Por defecto, empleados reales de "UNIDAD SISTEMA INTEGRADO DE GESTION" (≈ 85) más los DEV; con "Ver otros departamentos", el resto | No | |
| S6 | DEV siguen activos | Buscar "DEV00" en el buscador | DEV001–DEV008 aparecen | No | |
| S7 | Permisos | `set SINCRONIZAR_USUARIO=gestor` → `sincronizar.cmd` | 403; nada escrito | No | |
| S8 | ERP no disponible | Base inválida (`ServiciosExternos:ErpBase7048 = https://localhost:1`, ver `tarea26a\LEEME.md`) → reiniciar → `sincronizar.cmd` → restaurar | 503 "Servicio ERP no disponible"; `conteos.sql` sin cambios | No | |
| S9 | Modo Simulado | `user-secrets remove ServiciosExternos:Modo` → reiniciar → `sincronizar.cmd` → volver a Http | 400 "La sincronización de empleados requiere el modo Http del ERP." | No | |
| S10 | Datos de prueba existentes | Detalle y "Actualizar personal" de los Id 10–14 | Sin cambios; los DEV asignados siguen como antes | No | |

**Cubierto por pruebas** (no se puede reproducir con el ERP real sin alterarlo):
- respuesta parcial (P9, 502);
- código repetido o vacío;
- empleados que dejan de venir (inactivación);
- sincronización concurrente (409): se puede intentar con dos ejecuciones simultáneas de `sincronizar.cmd`, pero depende del tiempo.

## 7. Pendientes
- **Verificación del usuario S1–S10.** El pendiente 39 se da por resuelto después de ella.
- **Tarea programada** (Fase 7): Hangfire o el Programador de tareas de Windows (`FASE_4_Sincronizacion_Empleados.md` §5).
- **Pendientes 41–44** (horarios no laborales, RN09 con horarios que cruzan la medianoche, `codEmpresa`, campos para Perfiles).
