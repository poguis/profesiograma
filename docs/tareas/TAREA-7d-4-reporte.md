# TAREA-7d-4 — Reporte: `ssoSilent` con el puente de redirección de MSAL 5

**Fecha:** 06/11/2026 · **Fase:** B (implementación del plan de la Fase A, con G1–G5 y J1–J4) · **Estado:** implementada; pendiente de la verificación del usuario (N1 en DEV; tercera publicación, H1–H14 y N2–N4 en QA)

Causa con las citas de MSAL 5.26.0 y la solución: `docs/fases/FASE_7_Entornos_Despliegue.md` §11.

## 1. Qué se hizo
- **Puente (G1, J1):** `frontend/redirect.html` + `src/redirect.ts` como segunda entrada de Vite. `redirect.ts` importa **solo** `@azure/msal-browser/redirect-bridge` y llama a `broadcastResponseToMainFrame()`, que envía la respuesta de Entra a la ventana principal por `BroadcastChannel`.
- **`redirectUri` silencioso (G2, G3):** `uriRedireccionSilenciosa(origen)` → `<origen>/redirect.html`.
  - Lo usan `ssoSilent` (`SesionEntra.tsx`) y `acquireTokenSilent` (`encabezadosEntra`, desde `msal.ts`).
  - `loginRedirect` y `logoutRedirect` no cambian.
- **Backend:** sin cambios de código. Una prueba confirma que `/redirect.html` es un archivo estático (no el fallback) con `no-cache`, `SAMEORIGIN` y `frame-ancestors 'self'`.
- **Despliegue:**
  - `publicar-qa` con encabezado neutro (pendiente 87) y C15;
  - `humo-qa` con H14;
  - LEEME con el registro de las 2 URIs (J4), H14 y N1–N4.
- **Documentación:**
  - FASE_7: §7.1 (nota en el Ajuste 2), §7.4 y §11 (J3);
  - 00_ESTADO: 86 «implementado», 87 cerrado;
  - HISTORIAL: H31.

## 2. Archivos tocados
| Archivo | Cambio |
|---|---|
| `frontend/redirect.html`, `frontend/src/redirect.ts` (nuevos) | Página del puente; solo el import del puente de MSAL |
| `frontend/vite.config.ts` | `build.rolldownOptions.input`: `main` (`index.html`) y `redirect` (`redirect.html`) |
| `frontend/src/auth/entra.ts` (+ `.test.ts`) | `uriRedireccionSilenciosa`; `ClienteTokens.acquireTokenSilent` acepta `redirectUri`; `encabezadosEntra(…, redirectUri?)`. 5 pruebas nuevas; el doble registra `redirecciones` (las pruebas existentes no cambian) |
| `frontend/src/auth/msal.ts`, `src/auth/SesionEntra.tsx` | `redirectUri` silencioso en `acquireTokenSilent` y en `ssoSilent` |
| `backend/tests/App.Api.Tests/SpaSaludTests.cs` | `redirect.html` en el `wwwroot` de prueba y 1 prueba nueva |
| `deploy/qa/publicar-qa.ps1` | Encabezado neutro, C15 e INFO de migraciones sin «TAREA-7d-1»; «Siguiente paso: LEEME.md sección 10…» |
| `deploy/qa/humo-qa.ps1`, `deploy/qa/simulacion/H14.*` (nuevos) | H14 |
| `deploy/qa/LEEME.md` | URIs de Entra (J4), C15, H14, N1–N4 |
| `docs/fases/FASE_7_Entornos_Despliegue.md`, `docs/00_ESTADO_ACTUAL.md` (24 739 B), `docs/HISTORIAL.md` | §7.1, §7.4 y §11; 86 y 87; H31 |

## 3. Comandos y resultado
| Comando | Resultado |
|---|---|
| `dotnet build Profesiograma.slnx -c Release` | Correcto, 0 advertencias, 0 errores |
| `dotnet test --solution Profesiograma.slnx -c Release` | **1221/1221** (1220 + 1 nueva); 0 ajustadas |
| `npm run build` / `npm run build -- --mode qa` | Correcto / correcto. **Ya no aparece el aviso de más de 500 kB** (§4) |
| `npm run lint` / `npm test` | 0 / **430/430** (425 + 5 nuevas); 0 ajustadas |
| `dist/redirect.html` (modo `qa`) | 0,63 kB; `<script type="module" src="/assets/redirect-*.js">`; **sin `id="root"`** |
| Chunk del puente (J2) | `redirect-*.js` 1,2 kB + el chunk compartido `Configuration-*.js` 63 kB (código común de MSAL) + el polyfill de `modulepreload` 0,7 kB ≈ **65 kB** (menos de 100 kB) |
| Grep en el chunk del puente y en `Configuration-*.js` | `SesionEntra`, `clienteHttp`, `X-Dev-User`, `react-dom`, `useMsal`, `createRoot`, `fluentui`, el ClientId: **0** |
| Grep del bundle principal (modo `qa`) | El ClientId del SPA-QA y la URL pública siguen en `clienteHttp-*.js`/`main-*.js`; `X-Dev-User` y `SelectorUsuarioDev`: 0 |
| Analizador de PowerShell sobre `publicar-qa.ps1` y `humo-qa.ps1` | 0 errores de sintaxis; solo ASCII; 0 caracteres de control |
| Paquetes | Ninguno instalado ni actualizado; `npm ci` no se ejecutó |
| `npm run build` (final) | `dist` quedó con el build normal |

## 4. Desviaciones, errores y decisiones
- **Chunk compartido:** con dos entradas, Vite separó el código común de MSAL en `Configuration-*.js` (63 kB). El puente lo carga, pero no carga nada de la aplicación (comprobado con Grep).
  - **Efecto lateral:** `clienteHttp-*.js` bajó de unos 505 kB a 449 kB y **ya no aparece el aviso de más de 500 kB** del build.
  - El pendiente 82 queda en parte superado; no lo cambié (fuera del alcance) y se revisa al cierre.
- **`fileURLToPath(new URL(…, import.meta.url))`** para las entradas de `vite.config.ts`: `tsconfig.node.json` ya incluye los tipos de `node`.
- **`redirect.ts` captura el rechazo del puente:** si se abre `/redirect.html` a mano, sin respuesta en la URL, la página queda en blanco, sin errores sin capturar.
- **No simulé `humo-qa`** (la tarea prohíbe ejecutar los scripts). Solo revisé la sintaxis.
- **[PENDIENTE DE CONFIRMAR]** Que `npm run dev` sirva `/redirect.html` desde la raíz (Vite multipágina). N1 lo verifica.
- **Cookies de terceros:** si un navegador las bloquea en el iframe de Entra, `ssoSilent` falla y se ve el botón, sin bucles.

## 5. Verificación para el usuario
| # | Paso | Esperado |
|---|---|---|
| 0 | Las 2 URIs `/redirect.html` en el SPA-QA y el SPA-DEV (ya registradas el 06/11/2026) | LEEME §10 |
| N1 | DEV en modo Entra (`npm run dev`): iniciar sesión y abrir una pestaña nueva | Entra sola en menos de 10 s |
| 1–8 | Tercera publicación (LEEME §10): commit, `publicar-qa` (P1, C1–C15), sin pasos de base, `app_offline.htm`, respaldo en `E:\`, `robocopy`, `version.txt`, `humo-qa` | H1–H8 y H10–H14 OK; H9 INFO |
| N2 | QA: lo mismo que N1 | Entra sola |
| N3 | El botón y «Cerrar sesión», en DEV y en QA | Igual que antes |
| N4 | F12 → Consola en N1 y N2 | Sin `timed_out`, `redirect_bridge_timeout` ni `block_iframe_reload` |

## 6. Pendientes
- La verificación (N1–N4, H14) y el cierre documental; el pendiente 86 se cierra con N1/N2.
- 82: revisar si se cierra (ya no aparece el aviso de más de 500 kB).
- Siguen 75, 77, 81 (desde el 13/11), 83, 84 y 85.
