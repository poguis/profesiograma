# TAREA-22c — 503 rápido cuando la base de datos no responde (pendiente 55, Z12)

**Fecha:** 2026-10-21 (Fase B; Fase A aprobada con las decisiones 1b, 2a, 3b, 4a y 5a y los ajustes 1–5).
**Resultado:** ✅ **cerrada** (22c y 22c-b verificadas por el usuario el 21/10/2026 con `backend/tests/manual/tarea22c/bd-caida.cmd` y con la base real; ver Anexo c).
- Backend: `dotnet build Profesiograma.slnx -c Release`: 0 advertencias, 0 errores.
- Backend: `dotnet test --solution Profesiograma.slnx -c Release`: **841/841** = 801 anteriores + 40 nuevas; 0 ajustadas.
- Backend: `has-pending-model-changes`: «No changes have been made to the model since the last migration.»
- Frontend: `npm run build` OK; `npm run lint` OK (código de salida 0, sin avisos); `npm test` **294/294** = 291 anteriores + 3 nuevas; 0 ajustadas.
- **Sin consultas EF nuevas ni modificadas** (no aplica `ToQueryString`): solo se agregaron un interceptor, un ajuste de la cadena y un manejador de excepciones.

**Lo que no se hizo:** ejecutar la API, curl o el script K; tocar los user-secrets o leer la cadena de conexión; SQL; migraciones; git que modifique el repositorio.

## 1. Qué se hizo

### 1.1 Paso 1: prueba aislada del interceptor (ajuste 1)
Antes de todo lo demás se escribió una prueba temporal (`SondaInterceptorTests`): un `DbConnectionInterceptor` que lanza su propia excepción desde `ConnectionFailedAsync`, con `Server=127.0.0.1,1;…;Connect Timeout=1` (loopback, datos ficticios).
- Resultado: **la excepción del interceptor reemplaza a la original**, con la `SqlException` como interna. Número **258**, en **2512 ms** con Connect Timeout = 1.
- Se siguió con el diseño A (sin el plan B de `Exception.Data`). La prueba temporal se borró y quedó como `FalloBaseDatosInterceptorTests.AlAbrir_…` con el interceptor real.
- Observación: el puerto cerrado de loopback no falla al instante; SqlClient reintenta hasta el Connect Timeout y termina con 258.

### 1.2 Application
- **`Comun/BaseDatosNoDisponibleException.cs` (nuevo):** `FaseFalloBaseDatos` (`AlAbrir`, `EnComando`, `EnConfirmacion`) y la excepción con `Fase`, `NumeroError`, `DuracionMs` y la original como interna. Su mensaje no incluye el del proveedor.

### 1.3 Infrastructure
- **`Persistencia/FalloBaseDatosInterceptor.cs` (nuevo):**
  - `ClasificadorFalloSql.EsNoDisponible(numero, fase)` (lógica pura): -1, 2, 53, 64, 233, 258, 4060, 10053, 10054, 10060, 10061, 11001 en cualquier fase; -2 **solo al abrir** (4a). No cuentan 547, 2601, 2627, 1205 (deadlock), 18456 (login fallido) ni los de sintaxis.
  - `FalloBaseDatosInterceptor` (`IDbConnectionInterceptor`, `IDbCommandInterceptor`, `IDbTransactionInterceptor`; instancia única sin estado): `ConnectionFailed` → `AlAbrir`; `CommandFailed` → `EnComando`; `TransactionFailed` con acción `Commit` → `EnConfirmacion` (otras acciones → `EnComando`). Si el número cuenta, lanza `BaseDatosNoDisponibleException`.
- **`Persistencia/CadenaConexion.cs` (nuevo):** `ConTiempoEspera(cadena, segundos)` (1b). `SqlConnectionStringBuilder.ShouldSerialize("Connect Timeout")` resuelve los alias (`Connect Timeout`, `Connection Timeout`, `Timeout`) y solo es true si el valor vino en la cadena, así que el valor por defecto del builder no da falsos positivos. Con valor explícito devuelve la cadena intacta; si no, fija el configurado. Un valor ≤ 0 lanza.
- **`DependencyInjection.cs`:** lee `BaseDatos:TiempoEsperaConexionSegundos` (5 por defecto) y aplica `ConTiempoEspera` a la cadena que usan EF y `SqlDatabaseDiagnostics`.
- **`PersistenciaServiceCollectionExtensions.cs`:** registra el interceptor junto al de auditoría y baja `RelationalEventId.ConnectionError` a Debug (nombra la base y el servidor; el 503 se registra en la API).
- **`Seguridad/UsuarioProvisionamiento.cs`:** el `catch (DbUpdateException)` del alta concurrente ya no atrapa la base caída (`when (ex.InnerException is not BaseDatosNoDisponibleException)`): no reintenta ni registra «Conflicto al provisionar».

### 1.4 Api
- **`Configuracion/BaseDatosNoDisponibleExceptionHandler.cs` (nuevo):** busca la excepción también envuelta (`InnerException`, `AggregateException.InnerExceptions`; EF envuelve los fallos de `SaveChanges` en `DbUpdateException`) y responde 503 ProblemDetails con el formato de los manejadores del ERP y del applock (ajuste 2):
  - Title fijo: «Base de datos no disponible».
  - GET/HEAD: «No se pudo consultar la base de datos. Intente nuevamente en un minuto.»
  - Escritura con fallo al abrir: «La base de datos no respondió y no se guardó ningún cambio. Intente nuevamente en un minuto.»
  - Escritura en comando o commit: «La base de datos dejó de responder durante el registro. Vuelva a cargar el proyecto para comprobar si el cambio se guardó antes de intentarlo de nuevo.»
  - Log: un Warning como máximo cada 30 s con el número SQL, la fase, la duración, el método, la ruta y los avisos omitidos desde el anterior; sin el mensaje del proveedor ni la cadena.
- **`ErroresApiExtensions.cs`:** registra el manejador después de los existentes (los 503 del ERP, de empleados y de «Registro ocupado» no cambian).
- **`appsettings.json`:** `"BaseDatos": { "TiempoEsperaConexionSegundos": 5 }`.
- **`Program.cs`:** solo el comentario de `AddErroresApi`.

### 1.5 Frontend (3b)
- **`features/proyectos/erroresDialogo.ts`:** `ErrorRespuesta.detalle?`; en `noDisponible`, el mensaje general es «título: detail» (solo el título si no hay detail). `red` y los demás tipos no cambian. Lo usan los diálogos de cambio de estado, cabecera, personal (`interpretarErrorPersonal` delega), reactivación y exportar.
- **`features/proyectos/edicionPersonal.ts`:** `ErrorRespuestaPersonal.detalle?` (para pasarlo a `interpretarErrorDialogo`).
- **Comentarios actualizados:** `reporteExcel.ts` (`ofreceReintento`) y `cronograma/api.ts` (`useCronograma`, P10).
- «Nuevo proyecto» ya mostraba el detail (`NuevoProyecto.tsx`, `errorEnvio`): sin cambios.

### 1.6 Script K y documentación
- `backend/tests/manual/tarea22c/bd-caida.cmd` y `LEEME.md` (§5). Guardado con CRLF (usa etiquetas y `goto`).
- `00_ESTADO_ACTUAL.md`: cabecera, hoja de ruta (22b ✅, 22c implementada), pendiente 55 resuelto, nuevos pendientes 60 y 61, regla 4 en §7.1, filas de la TAREA-22c en §8 y §9 y 22b ✅ en §9.
- `FASE_5_Cronograma.md`: P10 (§7), reintento del reporte (§8) y Z12.

## 2. Archivos
**Nuevos:**
- `backend/src/App.Application/Comun/BaseDatosNoDisponibleException.cs`
- `backend/src/App.Infrastructure/Persistencia/FalloBaseDatosInterceptor.cs`
- `backend/src/App.Infrastructure/Persistencia/CadenaConexion.cs`
- `backend/src/App.Api/Configuracion/BaseDatosNoDisponibleExceptionHandler.cs`
- `backend/tests/App.Infrastructure.Tests/Persistencia/FalloBaseDatosInterceptorTests.cs`
- `backend/tests/App.Infrastructure.Tests/Persistencia/CadenaConexionTests.cs`
- `backend/tests/App.Api.Tests/BaseDatosNoDisponibleTests.cs`
- `backend/tests/manual/tarea22c/bd-caida.cmd`, `backend/tests/manual/tarea22c/LEEME.md`
- `frontend/src/features/proyectos/erroresDialogo.test.ts`
- `docs/tareas/TAREA-22c-reporte.md`

**Modificados:**
- `backend/src/App.Infrastructure/DependencyInjection.cs`
- `backend/src/App.Infrastructure/Persistencia/PersistenciaServiceCollectionExtensions.cs`
- `backend/src/App.Infrastructure/Seguridad/UsuarioProvisionamiento.cs`
- `backend/src/App.Api/Configuracion/ErroresApiExtensions.cs`, `backend/src/App.Api/Program.cs` (comentario), `backend/src/App.Api/appsettings.json`
- `frontend/src/features/proyectos/erroresDialogo.ts`, `edicionPersonal.ts`, `reporteExcel.ts` (comentario), `frontend/src/features/cronograma/api.ts` (comentario)
- `docs/00_ESTADO_ACTUAL.md`, `docs/fases/FASE_5_Cronograma.md`

## 3. Pruebas nuevas (40 backend + 3 frontend)
| Archivo | Pruebas |
|---|---|
| `FalloBaseDatosInterceptorTests` | 1 de conexión real por loopback (excepción reemplazada, `SqlException` interna, fase `AlAbrir`, mensaje sin el servidor); 12 números que cuentan en las 3 fases; -2 solo al abrir; 7 que no cuentan (547, 2601, 2627, 1205, 18456, 102, 0) |
| `CadenaConexionTests` | sin valor → 5 (y el resto de la cadena intacto); 5 alias o valores explícitos que ganan (incluido `Connect Timeout=15`, igual al valor por defecto); 2 valores inválidos |
| `BaseDatosNoDisponibleTests` (pipeline en memoria) | GET al abrir y en comando; POST/PUT/PATCH/DELETE al abrir; POST en comando envuelta en `DbUpdateException`; commit dentro de `AggregateException` e `InvalidOperationException`; excepción no clasificada → 500; «Registro ocupado» y «Servicio de empleados no disponible» sin cambios; log con un solo aviso para 3 fallos, con número y ruta, sin texto de la excepción interna (también comprueba que `ExceptionHandlerMiddleware` no registra la excepción manejada) |
| `erroresDialogo.test.ts` | 503 con detail; 503 sin detail; `red` y `conflicto` sin detail |

## 4. Comandos y resultados
| Comando | Resultado |
|---|---|
| `dotnet test --project tests/App.Infrastructure.Tests -c Release -- --filter-class "*SondaInterceptorTests"` (prueba temporal) | 1/1; 258 en 2512 ms (salida a un archivo del scratchpad, ya borrado) |
| `dotnet build Profesiograma.slnx -c Release` | 0 advertencias, 0 errores |
| `dotnet test --solution Profesiograma.slnx -c Release` (1.ª vez) | 840/841: `Cache-Control` vacío (§6) |
| `dotnet test --solution Profesiograma.slnx -c Release` | **841/841** |
| `dotnet ef migrations has-pending-model-changes … --configuration Release` | sin cambios de modelo |
| `npm run build` | OK |
| `npm run lint` | OK (salida 0) |
| `npm test` | **294/294** |

## 5. Verificación manual (la ejecuta el usuario)
`backend/tests/manual/tarea22c/bd-caida.cmd` (ver su `LEEME.md`):
- Banner «MODO REAL - base FICTICIA por variable de entorno; no toca PROFESIOGRAMA_DEV»; aborta si el puerto 5181 está ocupado.
- Compila en Release y levanta una instancia aparte (`--no-launch-profile --urls http://localhost:5181`, Development) en una ventana con título `PROFESIOGRAMA-22C-5181`; la cierra por ese título (`taskkill /FI "WINDOWTITLE eq …"`), nunca por nombre de proceso.
- Solo en ese proceso: `ConnectionStrings__Profesiograma` ficticia (`BD_FICTICIA_22C`, `usuario_ficticio`, `clave_ficticia`) y `DatosPrueba__SembrarAlIniciar=false`. Las variables de entorno ganan a los user-secrets (`WebApplication.CreateBuilder` estándar).
- Casos `127.0.0.1,1` y `192.0.2.1`. Peticiones: `GET /` (200), `GET /api/proyectos` (503 y tiempo ≈ 5 s), la misma repetida (casi inmediata, bloqueo del pool), `POST /api/proyectos/previsualizar` (503 «no se guardó ningún cambio»), `GET /api/health/db` (503 «No se pudo conectar a SQL Server»). Busca en el log los datos de la cadena ficticia.
- Salidas `resultado-*.txt`.
- **No cubre** las escrituras en comando o commit (el middleware falla antes al abrir): las cubren las pruebas en memoria.

## 6. Errores y cómo se resolvieron
- **`Cache-Control` vacío en la prueba en memoria:** `ExceptionHandlerMiddleware` pone `no-cache,no-store` en `Response.OnStarting`, que `DefaultHttpContext` no dispara. Se quitó esa aserción (sin cambiar código de producción) y se comprueba en el script K con Kestrel real (los cuerpos `resultado-*.txt` incluyen los encabezados).
- **La salida de la prueba temporal no se veía** con `--output Detailed` ni `--show-live-output`: se escribió a un archivo del scratchpad y luego se borró.

## 7. Contradicciones y observaciones
- **Plan de la Fase A, punto K:** suponía que `127.0.0.1,1` rechaza «al instante». En la prueba, SqlClient reintenta hasta el Connect Timeout (258 tras ~2,5 s con 1 s). El caso del script sigue siendo útil, pero ambos casos deberían tardar ≈ 5 s.
- `/api/health/db` usa el mismo pool que EF (la misma cadena): si se consulta después de un fallo, también falla al instante. En el script va al final.
- `POST …/previsualizar` no escribe, pero en la fase `EnComando` recibiría el texto cauteloso de escritura (la regla es por método, como se aprobó).
- El detail «no se guardó ningún cambio» se apoya en que ningún servicio vuelve a la base después del commit de `ITransaccionAsignaciones` (comprobado: los 6 servicios devuelven el resultado de `EjecutarAsync`). Quedó como parte de la regla 4 de §7.1.

- **`.gitignore` con cambios sin confirmar (previos a esta tarea, no los hizo Claude Code):** ignora `docs/tareas/`, `backend/tests/manual/` y `backend/src/App.Infrastructure/Persistencia/Migraciones/`. Los archivos ya versionados siguen igual, pero los NUEVOS de esas carpetas (este reporte, `tarea22c/bd-caida.cmd` y `LEEME.md`) no aparecen en `git status`: para versionarlos hay que ajustar el `.gitignore` o usar `git add -f`. Ignorar `Migraciones/` haría que una migración nueva no se versione.

## 8. Pendientes (ver también el Anexo b)
- Verificación manual con `bd-caida.cmd` (§5).
- **60** [PENDIENTE DE DECISIÓN DEL USUARIO]: en Development el sembrador impide arrancar con la base caída.
- **61** [PENDIENTE]: probar el timeout de 5 s desde casa con Sophos.
- Riesgo conocido (decisión 2a): tras volver la base, el bloqueo del pool de SqlClient puede mantener los 503 hasta ~60 s.

## Anexo b — TAREA-22c-b: 27 s con un host no enrutable (21/10/2026)

### b.1 Evidencia (verificación del usuario con `bd-caida.cmd`; la dio el usuario en el prompt, Claude Code no leyó los `resultado-*.txt`)
- `127.0.0.1,1`: primera llamada por EF 6,18 s; `/api/health/db` 5,02 s; segunda llamada 0,06 s. OK.
- `192.0.2.1` (sin puerto ni prefijo): primera llamada por EF **27,29 s** (503 correcto, pero lento); `/api/health/db` 5,02 s; segunda llamada y POST 0,02–0,06 s.
- Log del handler: «error SQL 64 (AlAbrir, 26087 ms)»: un solo intento de 26 s.
- El mensaje interno de la `SqlException` menciona el proveedor de canalizaciones con nombre (error 40): hubo paso de TCP a Named Pipes.

### b.2 Causa
- Con un Data Source sin protocolo, SqlClient prueba TCP y luego Named Pipes. La fase de Named Pipes (SMB) contra un host que no responde no respeta el Connect Timeout (~21 s extra). Prueba de esta fase (conexión directa, Connect Timeout = 5): `tcp:192.0.2.1` → error 258 en 5013 ms; `tcp:192.0.2.1\SSDEV` → error -1 en 4962 ms.
- El diagnóstico tardó 5 s porque EF y `SqlDatabaseDiagnostics` usan **pools distintos**: EF 10 cambia `Application Name` (el binario `Microsoft.EntityFrameworkCore.SqlServer` 10.0.12 tiene `_defaultApplicationName` y llama a `set_ApplicationName`; el valor exacto no se verificó) y, según la documentación de `SqlConnection.ConnectionString` («Pooling»), solo comparten pool las cadenas idénticas. Que su intento no pasara por Named Pipes se atribuye a que el sistema recordó el fallo anterior hacia la misma IP [SUPUESTO; lo comprueba el caso 7 del script].

### b.3 Cambio
- `CadenaConexion.ConTcp(cadena)`: antepone `tcp:` al Data Source salvo si está vacío, ya tiene prefijo (`tcp:`, `np:`, `lpc:`, `admin:`, sin distinguir mayúsculas) o es local (`.`, `(local)`, `(localdb)`, con o sin instancia). La documentación de Microsoft.Data.SqlClient admite `tcp:<host>\<instancia>` (el nombre de instancia se resuelve a un puerto TCP).
- `CadenaConexion.Ajustar(cadena, segundos, forzarTcp)` = `ConTcp(ConTiempoEspera(...))`; `DependencyInjection.CadenaConexionEfectiva(configuration)` lee `BaseDatos:TiempoEsperaConexionSegundos` y `BaseDatos:ForzarTcp` (sin la clave, true) y la usan EF y el diagnóstico. `appsettings.json`: `"ForzarTcp": true`.
- `CoreEventId.QueryIterationFailed` (10100) a Debug en `ConfigureWarnings` (junto a `ConnectionError`). Ningún `catch` del código atrapa errores de consulta: un error real termina en 500 y lo registra `ExceptionHandlerMiddleware` (prueba nueva), y el SQL queda en `CommandError`.
- Script K: `:caso` recibe servidor, `BaseDatos__ForzarTcp` (variable solo del proceso), orden (EF o diagnóstico primero) y criterio; 7 casos e impresión OK/FALLA en `resultado-criterios.txt` (ver `LEEME.md`).

### b.4 Contradicción con la decisión 2a
- La decisión excluía las cadenas con `Network Library`/`Network`/`Net`. En .NET, Microsoft.Data.SqlClient **rechaza** esa palabra clave al leer la cadena (`NotSupportedException`, comprobado en una prueba temporal y ahora en `NetworkLibrary_SqlClientLaRechazaEnNet`). Una cadena así no puede usarse (tampoco antes de esta tarea), así que `ConTcp` no necesita excluirla aparte. La documentación lista la palabra clave para todas las versiones, sin aclarar que en .NET no se admite.

### b.5 Archivos
- **Modificados:** `backend/src/App.Infrastructure/Persistencia/CadenaConexion.cs`, `backend/src/App.Infrastructure/DependencyInjection.cs`, `backend/src/App.Infrastructure/Persistencia/PersistenciaServiceCollectionExtensions.cs`, `backend/src/App.Api/appsettings.json`, `backend/tests/App.Infrastructure.Tests/Persistencia/CadenaConexionTests.cs` (pruebas nuevas al final; las existentes sin cambios), `backend/tests/App.Api.Tests/BaseDatosNoDisponibleTests.cs` (1 prueba nueva), `backend/tests/manual/tarea22c/bd-caida.cmd`, `backend/tests/manual/tarea22c/LEEME.md`, `docs/00_ESTADO_ACTUAL.md`, este reporte.
- **Nuevos:** `backend/tests/App.Api.Tests/CadenaConexionConfiguracionTests.cs`, `backend/tests/App.Infrastructure.Tests/Persistencia/PersistenciaLogTests.cs`.

### b.6 Pruebas nuevas (31)
| Archivo | Pruebas |
|---|---|
| `CadenaConexionTests` | 9 que anteponen `tcp:` (`NIQUEL\SSDEV` con `Server` y `Data Source`, alias `Address`, mayúsculas mezcladas, espacios, `host,puerto`, IP, IP\instancia, `localhost`; conservan base, usuario y contraseña); 14 que quedan igual (locales, LocalDB, `np:`, `lpc:`, `tcp:`/`TCP:`/`Tcp:`, `admin:`/`ADMIN:`); sin Data Source; `Network Library` rechazada; composición (`Connect Timeout=30` explícito sigue ganando; sin valor, 5; sin `tcp:` con `forzarTcp=false`) |
| `CadenaConexionConfiguracionTests` | sin la clave = true (y 5 s); `ForzarTcp=false`; variable de entorno `…BaseDatos__ForzarTcp=false` lo desactiva |
| `BaseDatosNoDisponibleTests` | un 500 no clasificado sigue registrándose como Error |
| `PersistenciaLogTests` | con `AddPersistencia` real y loopback: 10100 y `ConnectionError` en Debug, ningún log de EF ≥ Warning |

### b.7 Comandos y resultados
| Comando | Resultado |
|---|---|
| `dotnet test --project tests/App.Infrastructure.Tests -c Release -- --filter-class "*SondaTcpTests"` (prueba temporal, borrada) | 3/3: `tcp:192.0.2.1` 5013 ms (258); `tcp:192.0.2.1\SSDEV` 4962 ms (-1); `Network Library` → `NotSupportedException` |
| `dotnet build Profesiograma.slnx -c Release` | 0 advertencias, 0 errores |
| `dotnet test --solution Profesiograma.slnx -c Release` | **872/872** = 841 + 31; 0 ajustadas |
| `dotnet ef migrations has-pending-model-changes … --configuration Release` | sin cambios de modelo |
| Cálculo de milisegundos del script (copia en el scratchpad, sin curl ni API, borrada) | `5.018234` → 5018; `27.290000` → 27290; `0.008912` → 8; vacío → 0 |

Sin consultas EF nuevas (no aplica `ToQueryString`). Frontend sin cambios.

### b.8 Pendientes
- Verificación manual con `bd-caida.cmd` (7 casos; criterios en `LEEME.md`). Caso 4: margen estrecho (≈ 5 s de conexión + ~1 s de la primera petición frente a 7 s).
- Pendiente 61 ampliado: probar con `ForzarTcp` desde casa y comprobar `net_transport`; si la base real falla con TCP, `ForzarTcp=false`.

## Anexo c — Verificación manual del usuario (21/10/2026)

Evidencia dada por el usuario (Claude Code no leyó los `resultado-*.txt`). `bd-caida.cmd`, 7 casos, con cadenas FICTICIAS:

| Caso | Servidor | ForzarTcp | Resultado |
|---|---|---|---|
| 1–4 | `127.0.0.1,1`, `192.0.2.1`, `tcp:192.0.2.1`, `192.0.2.1\SSDEV` | true / prefijo explícito | **OK**: EF 1.ª 6,3–6,7 s, EF 2.ª 58–67 ms, `/api/health/db` 4,8–5,0 s; caso 4: 6331 ms |
| 5 | `203.0.113.1` | false | 27 352 ms en EF 1.ª: reproduce el defecto (Named Pipes) |
| 6 | `203.0.113.2\SSDEV` | false | 6184 ms: la instancia con nombre no sufría el defecto |
| 7 | `198.51.100.1`, diagnóstico primero | false | diagnóstico 26 171 ms y EF 6604 ms: la primera conexión, por cualquier pool, paga el defecto (confirma la hipótesis del punto 1 de la Fase A de la 22c-b) |

- Log sin datos de la cadena ficticia en todos los casos.
- `Cache-Control` del 503: `no-cache,no-store` (lo que la prueba en memoria no podía comprobar, §6).
- Base **real** con `ForzarTcp=true` (por defecto): `GET /api/health/db` → 200 en 0,23 s; `GET /api/usuarios/me` (admin) → 200 en 0,25 s.

**Conclusión:** TAREA-22c y TAREA-22c-b **cerradas**. Siguen abiertos los pendientes 60 (sembrador con la base caída) y 61 (prueba desde casa con Sophos; `ForzarTcp` es de bajo riesgo según el caso 6 y la base real).
