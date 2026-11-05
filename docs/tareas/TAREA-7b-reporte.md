# TAREA-7b — Reporte: Entra ID en DEV (backend y MSAL)

**Fecha:** 05/11/2026 · **Fase:** B (implementación del plan de la Fase A aprobado) · **Estado:** implementada; pendiente de los registros del portal y de la verificación del usuario (M1–M7)

Diseño, claves, tabla 401/403, decisiones P1–P6, ajustes 1–3 y lo que se crea en el portal: `docs/fases/FASE_7_Entornos_Despliegue.md` §7.1–§7.2.

## 1. Qué se hizo
- **Backend:**
  - todas las políticas exigen el scope `access_as_user` (`RequireScope` de Microsoft.Identity.Web);
  - DevAuth emite `scp`;
  - 403 «Sin rol asignado» antes del alta (P1);
  - `AzureAd:Audience` vacío (ajuste 1).
- **Frontend:** MSAL detrás de `obtenerEncabezadosAutenticacion()`:
  - `sessionStorage` y un solo `ssoSilent`, seguido del botón «Iniciar sesión con la cuenta de SEDEMI» (ajuste 2);
  - «No tienes acceso a PROFESIOGRAMA.» ante AADSTS501051 o el 403 «Sin rol asignado»;
  - «Cerrar sesión» en la barra.
- **DevAuth sin cambios para el usuario:** el selector y `X-Dev-User` siguen con `VITE_AUTH_MODE=dev`. El build no los incluye (comprobado en `dist/`).
- **Configuración:** `frontend/.env.development.local.example` (ajuste 3); los valores reales van en user-secrets y en `.env.development.local` (P6).
- Ninguna llamada a Entra ID: las pruebas usan tokens firmados con una clave RSA local y MSAL simulado.

## 2. Archivos tocados
| Archivo | Cambio |
|---|---|
| `backend/src/App.Api/Seguridad/AutenticacionExtensions.cs` | Políticas con `Base()` = autenticado + scope; `AddRequiredScopeAuthorization()`; DefaultPolicy |
| `backend/src/App.Api/Seguridad/ClaimsPrincipalExtensions.cs` | `ClaimNames.Scope`, `ScopesApi.AccederComoUsuario` |
| `backend/src/App.Api/Seguridad/DevAuth/DevAuthHandler.cs` | Claim `scp` |
| `backend/src/App.Api/Seguridad/UsuarioActualMiddleware.cs` | 403 «Sin rol asignado» antes del alta |
| `backend/src/App.Api/appsettings.json` | `AzureAd:Audience` = `""` |
| `backend/tests/App.Api.Tests/EntraIdAutenticacionTests.cs` (nuevo) | E1–E14, E2a/E2b/E2c y lectura de los claims |
| `frontend/src/auth/entra.ts` (+ `.test.ts`), `msal.ts`, `SesionEntra.tsx` (nuevos) | Lógica pura, instancia de MSAL, la puerta y «Cerrar sesión» |
| `frontend/src/auth/index.ts`, `src/main.tsx`, `src/app/App.tsx`, `src/components/DisenoPrincipal.tsx`, `src/pages/Inicio.tsx`, `src/vite-env.d.ts` | Bearer con MSAL, inicialización, puerta, botón, texto de ayuda, tipos |
| `frontend/package.json` (+ lock) | `@azure/msal-browser` 5.26.0 y `@azure/msal-react` 5.7.3, exactas |
| `frontend/.env.development.local.example` (nuevo), `.gitignore` (raíz) | Ejemplo y su excepción `!frontend/.env.development.local.example` |
| `docs/fases/FASE_7_Entornos_Despliegue.md`, `docs/00_ESTADO_ACTUAL.md`, `docs/HISTORIAL.md` | §7.1–§7.2; estado; H24 |

## 3. Comandos y resultado
| Comando | Resultado |
|---|---|
| `dotnet build Profesiograma.slnx -c Release` | Correcto, 0 errores |
| `dotnet test --solution Profesiograma.slnx -c Release` | **1180/1180** (1163 + 17 nuevas); 0 ajustadas |
| `npm run build` / `npm run lint` / `npm test` | Correcto (con un aviso nuevo de tamaño, §4) / 0 / **411/411** (404 + 7 nuevas); 0 ajustadas |
| Grep de `X-Dev-User` y `SelectorUsuarioDev` en `frontend/dist` | 0 coincidencias |
| `git check-ignore -v` | `.env.development.local` ignorado por `frontend/.gitignore:13` (`*.local`); el `.example` queda excluido de la regla por la negación |
| `npm view` | `msal-react` 5.8.0 y `msal-browser` 5.28.0 publicadas el 05/11/2026; `msal-react` 5.7.3 pide `react ^19.2.1` y `msal-browser ^5.26.0` |

## 4. Desviaciones, errores y decisiones
- **Ajuste 1 (Audience):** Microsoft.Identity.Web acepta `api://ClientId` **solo en tokens v1**. Un token v2 con `aud = api://…` da 401. Por eso:
  - E2b usa un token v1;
  - se agregó E2c (v2 con `api://` → 401);
  - E2a (v2 con el GUID) da 200.

  En la práctica no afecta: con `requestedAccessTokenVersion = 2`, Entra emite siempre el GUID.
- **Versiones de MSAL:** no se usaron las últimas (5.8.0 / 5.28.0), publicadas hace horas. Se eligió el par compatible publicado el 21/10/2026 (5.7.3 / 5.26.0). `npm audit` da 0 vulnerabilidades.
- **InteractionRequired:** el plan de la Fase A preveía un `acquireTokenRedirect` automático. Para cumplir el ajuste 2 («nunca un redirect automático»), ahora se muestra «La sesión venció; vuelva a iniciar sesión.» con el botón.
- **P3 → TAREA-7b-2** (05/11/2026; ver `TAREA-7b-2-reporte.md`, cuyas M0–M7 reemplazan las de este reporte). Texto original: **P3 no implementado:** el respaldo por `email` cambia la firma de `IUsuarioProvisionamiento.ResolverAsync`, sus tres dobles de prueba, la clave de caché y la regla de conflicto. Se decide después de M3.
- **`.gitignore` raíz:** `**/.env*` ignoraba el `.example`; se agregó la excepción `!frontend/.env.development.local.example`.
- **Pista de inicio de sesión:** `loginHint` se guarda en `localStorage` (`profesiograma.loginHint`, solo el nombre de usuario) para el `ssoSilent` tras cerrar el navegador. «Cerrar sesión» la borra.
- **Pruebas sin red:** en las pruebas, los metadatos OpenID son estáticos y el validador de emisor de Microsoft.Identity.Web (que consulta login.microsoftonline.com) se reemplaza por `ValidIssuers` (los emisores v1 y v2 del tenant ficticio). La validación real del emisor se verifica en M2.
- **Aviso de build:** el chunk `clienteHttp` pasó a 505 kB (más de 500 kB) porque incluye MSAL. Se podría dividir en una tarea aparte.
- **Vite debe usar el puerto 5173:** la URI de redirección es exacta. No se agregó `strictPort`.

## 5. Verificación para el usuario
Requisitos: los registros (FASE_7 §7.2), los IDs en user-secrets (`Autenticacion:Modo=EntraId`, `AzureAd:TenantId`, `AzureAd:ClientId`) y en `frontend/.env.development.local` (copia del `.example`), y reiniciar la API y `npm run dev`.

| # | Caso | Esperado |
|---|---|---|
| M1 | Sin `.env.development.local` y sin `Autenticacion:Modo` en user-secrets (DevAuth) | Todo igual que antes: selector admin, gestor, gestor2 y anonimo |
| M2 | Modo Entra: abrir `http://localhost:5173` | Primero «Verificando la sesión…» y luego el botón «Iniciar sesión con la cuenta de SEDEMI». Al iniciar sesión, la barra muestra tu nombre e Inicio dice Admin. En F12 → Red, `Authorization: Bearer …` y ningún `X-Dev-User`. Si aparece «Se necesita aprobación del administrador», anótalo (consentimiento, pendiente 75) |
| M3 | Primer ingreso, en SSMS: `SELECT Id, CASE WHEN EntraObjectId IS NULL THEN 0 ELSE 1 END AS ConOid FROM dbo.Usuario ORDER BY Id DESC;` | Una fila nueva con `ConOid = 1`. En Entra, anota si el UPN coincide con el correo (P3) |
| M4 | Cuenta sin rol (o quitarte la asignación y volver a iniciar sesión) | «No tienes acceso a PROFESIOGRAMA.» con «Cerrar sesión»; ninguna fila nueva en `dbo.Usuario` |
| M5 | «Cerrar sesión» | Vuelve al botón «Iniciar sesión…». Al iniciar otra vez, un solo `ssoSilent` (nunca un bucle de redirecciones) |
| M6 | Con una segunda cuenta Gestor: darla de baja en `/admin/usuarios` | En la siguiente solicitud de esa cuenta: 403 «Usuario no habilitado» |
| M7 | Volver a DevAuth (`dotnet user-secrets remove Autenticacion:Modo` y renombrar el `.local`) | Igual que M1 |

## 6. Pendientes
- Registros del portal, M1–M7 y el cierre documental de la Fase 7b.
- P3 (respaldo por `email`), según el resultado de M3.
- Dividir el chunk de 505 kB (opcional).
