# TAREA-7d-1 — Reporte: publicación de prueba en SDMAPPTEST2 (QA, http, sin inicio de sesión)

**Fecha:** 05/11/2026 · **Fase:** B (implementación del plan de la Fase A, con D1–D8 y J1–J7) · **Estado:** implementada; pendiente de la ejecución y la verificación del usuario (`backend/tests/manual/tarea7d1/LEEME.md`)

Diseño, decisiones y ajustes: `docs/fases/FASE_7_Entornos_Despliegue.md` §9.

## 1. Qué se hizo
- **La SPA desde la API (D2, J2, J3):**
  - `wwwroot` se sirve antes de la seguridad;
  - `/api/**` desconocido → 404 sin HTML;
  - cualquier otra ruta → `index.html`;
  - caché: `/assets/*` inmutable por un año; el resto, `no-cache`;
  - se quitó `MapGet("/")` (D8).
- **Salud (D6):** `/api/salud/vivo` y `/api/salud/listo` (`SELECT 1`, 200/503 sin detalle), anónimos, en todos los entornos.
- **Arranque seguro:**
  - en modo EntraId, la API no arranca si TenantId o ClientId no son GUID (E15);
  - el mensaje de la cadena de conexión menciona user-secrets en DEV y la variable del grupo en QA y PRO.
- **QA (D1, D5):**
  - `appsettings.QA.json`: IDs de la API-QA y `Logging` con EF en Warning;
  - `appsettings.Development.json` no se publica.
- **NLS en toda la solución (D7, J1):** las 1196 pruebas existentes pasaron **sin cambios**.
- **Frontend (D4, J4):**
  - `decidirArranque` muestra «Esta aplicación requiere HTTPS para iniciar sesión.» en http con IP;
  - un fallo de MSAL muestra un aviso, nunca una página en blanco;
  - `.env.qa` versionado, con solo IDs públicos.
- **Scripts (J5, J6):** `publicar-qa` (desde `git archive HEAD`, 13 líneas CONTROL), `humo-qa` (H1–H9 con `curl.exe` y `SIMULAR=1`) y `LEEME.md` con los pasos 1–9.

## 2. Archivos tocados
| Archivo | Cambio |
|---|---|
| `backend/src/App.Api/Program.cs` | `UseSpaEstaticos` antes de la seguridad, `MapSaludEndpoints`, `MapSpa`; sin `MapGet("/")` |
| `backend/src/App.Api/Configuracion/SpaExtensions.cs` (nuevo) | `StaticFileOptions` comunes, fallback de `/api` (404) y `MapFallbackToFile` |
| `backend/src/App.Api/Endpoints/SaludEndpoints.cs` (nuevo) | `vivo` y `listo` |
| `backend/src/App.Application/Diagnostics/IDatabaseDiagnostics.cs`, `App.Infrastructure/Diagnostics/SqlDatabaseDiagnostics.cs` | `EstaDisponibleAsync` (`SELECT 1`; false ante `SqlException`) |
| `backend/src/App.Api/Seguridad/AutenticacionExtensions.cs` | `ValidarAzureAd` (E15); el mensaje no incluye los valores |
| `backend/src/App.Infrastructure/DependencyInjection.cs` | Mensaje de la cadena de conexión |
| `backend/src/App.Api/appsettings.QA.json` (nuevo), `App.Api.csproj` | Configuración de QA; `appsettings.Development.json` con `CopyToPublishDirectory="Never"` |
| `backend/Directory.Build.props` | `RuntimeHostConfigurationOption System.Globalization.UseNls = true` |
| `backend/tests/App.Api.Tests/SpaSaludTests.cs` (nuevo), `EntraIdAutenticacionTests.cs` | 9 pruebas de la SPA y la salud; E15 (3 casos) y E15b |
| `backend/tests/App.Application.Tests/Comun/GlobalizacionTests.cs` (nuevo) | `UseNls` y `SA Pacific Standard Time` |
| `frontend/src/auth/entra.ts` (+ `.test.ts`), `msal.ts`, `src/main.tsx` | `decidirArranque` (5 casos), `faltantesEntra`, arranque sin página en blanco |
| `frontend/.env.qa` (nuevo), `.gitignore` (raíz) | IDs públicos de QA; excepción `!frontend/.env.qa` |
| `backend/tests/manual/tarea7d1/` (nuevo) | `publicar-qa.cmd/.ps1`, `humo-qa.cmd/.ps1`, `comun7d1.ps1`, `simulacion\` (ficticia), `LEEME.md` |
| `docs/fases/FASE_7_Entornos_Despliegue.md`, `docs/00_ESTADO_ACTUAL.md`, `docs/HISTORIAL.md` | §1, §4, §5, §6, §7.4 y §9; estado (24 938 B); H27 |

## 3. Comandos y resultado
| Comando | Resultado |
|---|---|
| `dotnet build Profesiograma.slnx -c Release` | Correcto, 0 advertencias, 0 errores |
| `dotnet test --solution Profesiograma.slnx -c Release` | **1211/1211** (1196 + 15 nuevas); 0 ajustadas. Antes de agregar las nuevas, con NLS: 1196/1196 |
| `npm run build` / `npm run lint` / `npm test` | Correcto (se mantiene el aviso de más de 500 kB) / 0 / **417/417** (412 + 5 nuevas) |
| `npm run build -- --mode qa` | Correcto. `dist` contiene el scope de la API-QA y el texto «requiere HTTPS»; Grep de `X-Dev-User` y `SelectorUsuarioDev`: 0 |
| Analizador de PowerShell sobre los 3 `.ps1` | 0 errores de sintaxis; solo ASCII |
| `npm run build` (final) | `dist` quedó con el build normal (modo `production`) |

## 4. Desviaciones, errores y decisiones
- **[SUPUESTO] ClientId de la API-QA:** D5 lo trae como `<CLIENT_ID_API_QA>`. Usé `a0b57ea8-e21b-4bd1-a9c4-f39fc7a0b2eb`, el GUID del scope (el URI por defecto es `api://<ClientId>`, FASE_7 §7.2). **Confírmalo.** Si no coincide, corrige `appsettings.QA.json` antes de publicar. H1–H9 no dependen de él, pero los tokens de QA sí.
- **Los scripts nuevos están en una carpeta ignorada:** el `.gitignore` raíz ignora `backend/tests/manual/`, y los scripts anteriores se agregaron forzados. Para versionarlos: `git add -f backend/tests/manual/tarea7d1`. Lo indica el LEEME.
- **No simulé `humo-qa`:** esta tarea prohíbe ejecutar los scripts (`.claude/rules/pruebas-manuales.md` permitiría simularlo en una copia). Solo revisé la sintaxis con el analizador de PowerShell.
- **`no-cache` se aplica a todo lo que no está en `/assets`** (`index.html` y `favicon.svg`), no solo a `index.html`.
- **[SUPUESTO] 405 → 404:** con el fallback `/api/{**ruta}`, un método no admitido en una ruta existente de `/api` respondería 404 en lugar de 405. Ninguna prueba ni el frontend dependen del 405.
- **`/` en DEV:** ya no devuelve el JSON; da 404, porque en DEV no hay `wwwroot` (la SPA la sirve Vite).
- **`main.tsx`:**
  - oxlint advirtió `only-export-components` con un componente local; se reemplazó por una función `aviso()`;
  - se agregó `faltantesEntra()` en `msal.ts` para decidir antes de crear MSAL.
- **Error propio corregido:** `AppContext.GetData` devuelve el texto `"true"`, no un `bool`. La primera versión de la prueba falló y ahora compara el texto.
- **`Logging` de QA:** EF en Warning. Con la base (`Default: Information`), cada SQL quedaría en el log.
- **C7 de `publicar-qa`:** compara `ProductVersion` con `10.0.13*`. El `FileVersion` de los paquetes de .NET tiene otro formato.
- **Pendiente nuevo:** el 77 (D4 antes de PRO). El 75 suma el Hosting Bundle.

## 5. Verificación para el usuario
El orden completo y el resultado esperado de cada paso están en `backend/tests/manual/tarea7d1/LEEME.md`.

| # | Paso | Esperado |
|---|---|---|
| 0 | Commit de esta tarea (y `git add -f` de `tarea7d1`, si se versiona) | `git status` limpio |
| 1 | `publicar-qa.cmd` | CONTROL P1 y C1–C12 OK; carpeta `<yyyyMMdd_HHmm>_<hash7>` |
| 2–4 | Respaldo, `migraciones-QA.sql` (F5 sin selección) y punto cero v2 | `@@TRANCOUNT = 0`; 7 migraciones; `Upn` de largo 512; los dos `VERIFYONLY` válidos |
| 5–7 | Grupo de aplicaciones y variable (appcmd desde CMD), sitio, NTFS, enlace y copia | Grupo y sitio `Started`; sin `appsettings.Development.json` en el sitio |
| 8 | `humo-qa.cmd`, V1 y V2 | H1–H8 OK; H9 INFO; V1 «Esta aplicación requiere HTTPS para iniciar sesión.»; V2 sin errores sin explicar |
| 9 | Si algo falla | Tabla de 500.19, 500.30, 500.31 y 503 del LEEME |

## 6. Pendientes
- La ejecución y la verificación de esta tarea, y su cierre documental.
- 7d-2: endurecimiento (`Server` y `X-Powered-By`, `web.config` propio).
- SPA-QA y HTTPS (pendiente 75); Hosting Bundle (75); configuración del frontend en tiempo de ejecución (77).
