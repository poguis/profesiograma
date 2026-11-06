# TAREA-7d-3 — Reporte: inicio de sesión real en QA por HTTPS (incluye la 7d-2)

**Fecha:** 06/11/2026 · **Fase:** B (implementación del plan de la Fase A, con E1–E8 y K1–K6) · **Estado:** implementada; pendiente de la ejecución y la verificación del usuario (`deploy/qa/LEEME.md` §10–§13)

Diseño, decisiones y ajustes: `docs/fases/FASE_7_Entornos_Despliegue.md` §10.

## 1. Qué se hizo
- **Encabezados de seguridad (K1, E2, E3):** `UseEncabezadosSeguridad()` es el primer middleware.
  - Pone `nosniff`, `strict-origin-when-cross-origin`, `X-Frame-Options: SAMEORIGIN` y `frame-ancestors 'self'` con `OnStarting`, así que llegan también al 500.
  - HSTS solo con `Seguridad:Hsts = true` (por defecto `false`).
- **Rutas:**
  - `/assets/no-existe.js` → 404 anónimo (antes daba 401);
  - `/assets/` → `index.html`;
  - `UseRouting` explícito después de los archivos.
- **Frontend (E1, K2):**
  - `VITE_URL_PUBLICA` y el estado `otroOrigen`: http 7082 o https con IP redirigen a `https://sdmapptest2.sedemi.local:7083`, conservando la ruta y la consulta, sin el fragmento;
  - una URL inválida da `configIncompleta`;
  - DEV no cambia.
- **`.env.qa`:** ClientId del SPA-QA y URL pública; el scope no cambia.
- **Despliegue (E5, E6, K3, K4):** `deploy/qa/` versionado, con su `.gitignore`.
  - `publicar-qa` agrega C13, C14 e INFO de migraciones.
  - `humo-qa` va por https (`QA_CA` → `--cacert` + `--ssl-no-revoke`; `-k` solo con `QA_INSEGURO=1`), con H10–H13.
  - LEEME con Actualización, certificado, L1–L6 y F1–F4.
- **E7: pospuesta por decisión del usuario** (06/11/2026). `vite` sigue en 8.3.1 (solo de desarrollo; `--omit=dev`: 0 vulnerabilidades). Pendiente 81, reabierto.

## 2. Archivos tocados
| Archivo | Cambio |
|---|---|
| `backend/src/App.Api/Configuracion/EncabezadosSeguridadExtensions.cs` (nuevo) | 4 encabezados con `OnStarting`; `UseHsts` solo con `Seguridad:Hsts` |
| `backend/src/App.Api/Program.cs` | `UseEncabezadosSeguridad()` primero; `UseRouting()` después de `UseSpaEstaticos()` |
| `backend/src/App.Api/Configuracion/SpaExtensions.cs` | `MapFallback("{**ruta:file}")` → 404 anónimo; comentario del orden |
| `backend/src/App.Api/appsettings.json` | `"Seguridad": { "Hsts": false }` |
| `backend/tests/App.Api.Tests/SpaSaludTests.cs` | 9 pruebas nuevas; el pipeline de pruebas agrega encabezados, el manejador de excepciones, un endpoint de error y una `IHttpResponseFeature` que ejecuta `OnStarting` |
| `frontend/src/auth/entra.ts` (+ `.test.ts`), `src/main.tsx`, `src/vite-env.d.ts` | `origenPublico`, `destinoPublico`, `otroOrigen`; redirección y enlace; el tipo de `VITE_URL_PUBLICA`; 8 pruebas |
| `frontend/.env.qa` | `VITE_ENTRA_CLIENT_ID`, `VITE_URL_PUBLICA` |
| `frontend/package.json`, `package-lock.json` | Sin cambios: el usuario revirtió la actualización de E7 |
| `deploy/qa/` (movido desde `backend/tests/manual/tarea7d1/`) | `publicar-qa.*`, `humo-qa.*`, `comun7d1.ps1`, `simulacion\` (H1, H4, H12a, H12b y H13 actualizados; sin H9), `LEEME.md`, `.gitignore` (nuevo) |
| `docs/fases/FASE_7_Entornos_Despliegue.md`, `docs/00_ESTADO_ACTUAL.md`, `docs/HISTORIAL.md` | §5, §6, §7.4, §9 (la ruta) y §10; estado (24 963 B); H29 |

## 3. Comandos y resultado
| Comando | Resultado |
|---|---|
| `dotnet build Profesiograma.slnx -c Release` | Correcto, 0 advertencias, 0 errores |
| `dotnet test --solution Profesiograma.slnx -c Release` | **1220/1220** (1211 + 9 nuevas); 0 ajustadas |
| `npm run build` / `npm run build -- --mode qa` / `npm run lint` / `npm test` | Correcto / correcto / 0 / **425/425** (417 + 8 nuevas), con `vite` 8.3.1; repetidos el 06/11/2026 tras el `npm ci` del lockfile revertido, con el mismo resultado |
| Grep de `dist` (modo `qa`) | `X-Dev-User` y `SelectorUsuarioDev`: 0. El ClientId del SPA-QA y la URL pública están presentes |
| `npm view vite time` | 8.3.3 (06/10), 8.3.4 (08/10), 8.3.5 (21/10, 16 días), 8.3.6 (05/11, 1 día) |
| `npm install --save-dev vite@8.3.5` | Se probó y el usuario lo revirtió (§4) |
| `npm ci` (tras revertir el lockfile) | El primer intento falló con EPERM (el `npm run dev` del usuario bloqueaba el binario de `rolldown`). **Resuelto:** el usuario lo detuvo y `npm ci` terminó bien. `npm ls vite`: 8.3.1 en todo el árbol; `npm audit --omit=dev`: 0 |
| Analizador de PowerShell sobre `deploy/qa/*.ps1` | 0 errores de sintaxis; solo ASCII. Los `.cmd` quedaron con CRLF |
| Migraciones | 7 (sin cambios) |
| `npm run build` (final) | `dist` quedó con el build normal |

## 4. Desviaciones, errores y decisiones
- **[Desviación] Asset inexistente → 401, no 404:** la prueba lo mostró. Sin endpoint, la política Fallback exigía autenticación (en QA, `/assets/no-existe.js` también habría dado 401).
  - **Corrección:** `MapFallback("{**ruta:file}") → 404` anónimo y `app.UseRouting()` explícito después de `UseSpaEstaticos()`. Si no, ese endpoint taparía los archivos reales.
  - Las pruebas de la SPA de la 7d-1 siguen pasando.
- **E7, pospuesta por decisión del usuario:** `npm install vite@8.3.5` también actualizó dependencias transitivas con menos de 14 días:
  - `rolldown` 1.2.12 → 1.2.16 (29/10, 8 días); `rolldown` genera el `dist` publicado;
  - `postcss` 8.5.28 → 8.5.32 (26/10, 11 días);
  - `@oxc-project/types` 0.152 → 0.156 (27/10, 10 días).

  El usuario revirtió `package.json` y `package-lock.json` (`vite` 8.3.1). La regla de los 14 días se aplica al árbol completo resuelto (nueva línea en `CLAUDE.md`, Frontend). Se reintenta desde el 13/11/2026 (pendiente 81).
- **Error propio corregido:** el pipeline de pruebas sin servidor no ejecuta `OnStarting`, y la primera ejecución dio los encabezados vacíos. Se agregó una `IHttpResponseFeature` de prueba que los ejecuta, como el servidor.
- **Error propio corregido:** al agregar C13 a `publicar-qa.ps1`, Python convirtió `\a` en un carácter de control. Se corrigió y revisé todos los archivos cambiados: 0 caracteres de control.
- **`humo-qa`:**
  - usa `D:\PROFESIOGRAMA_DEPLOY\QA\certificados\qa_7083.pem` por defecto si existe (`QA_CA` lo cambia);
  - H9 ya no consulta `/assets/` (lo cubre H12a).
- **[PENDIENTE DE CONFIRMAR] Schannel:** que el `curl.exe` de Windows acepte el PEM y que `--ssl-no-revoke` alcance. Si no, `QA_INSEGURO=1`.
- **[SUPUESTO] Iframe de `ssoSilent`:** el iframe puede no aceptar el certificado autofirmado. En ese caso L5 muestra el botón, sin bucles.
- **Archivos del usuario:** los `resultado-*` de la 7d-1 quedan en `backend/tests/manual/tarea7d1/` (no los moví ni los leí).
- **Corrección documental:** 00_ESTADO tenía la 7d-2 como tarea aparte. Quedó absorbida, y los pendientes 78–81 se cerraron (H29).

## 5. Verificación para el usuario
Orden completo en `deploy/qa/LEEME.md` §10 (Actualización), §11 (certificado), §12 (L1–L6) y §13 (F1–F4).

| # | Paso | Esperado |
|---|---|---|
| 0 | Commit (incluye `deploy/qa/` y las bajas de `backend/tests/manual/tarea7d1/`) | `git status` limpio |
| 1 | `deploy\qa\publicar-qa.cmd` | P1 y C1–C14 OK; INFO: 7 migraciones, la última `AgregarUpnUsuario` |
| 2 | `COUNT(*)` de `__EFMigrationsHistory` = 7 | Se saltan los pasos de la base |
| 3–7 | `app_offline.htm`, respaldo en `E:\`, `robocopy /MIR … /XD logs`, `version.txt`, borrar `app_offline.htm` | El sitio vuelve con el commit nuevo |
| 8 | `.pem` (§11) y `deploy\qa\humo-qa.cmd` | H1–H8 y H10–H13 OK; H9 INFO |
| 9 | L1–L6 | Admin y Gestor entran; sin asignación no hay acceso; cerrar sesión; F5 sin credenciales; 2 filas con oid y UPN |
| 10 | F1–F4 | Proyecto con 2 empleados reales, cronograma, Excel con el nombre y la cantidad de filas, novedad |

## 6. Pendientes
- La ejecución y la verificación de esta tarea, y su cierre documental.
- 75 (certificado de confianza, PRO, Graph, TLS, `X-Powered-By`, Hosting Bundle); 77 (configuración en tiempo de ejecución); 82 (bloque de 505 kB); 83 (CSP completa); 84 (K6); 85 (renovar el certificado antes del 06/11/2027).
