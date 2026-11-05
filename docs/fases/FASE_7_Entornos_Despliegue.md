# FASE 7 — Entornos y despliegue

**Última actualización:** 2026-11-05 (Fase 7a, registro de avances del 29/10 al 04/11/2026). Pendientes relacionados en `docs/00_ESTADO_ACTUAL.md`: 7, 59, 63, 65 y 75.

Todo lo de este documento lo **ejecutó el usuario** (SSMS, RDP). Claude Code no se conecta a ninguna base ni a ningún servidor.

## 1. Entornos

| Entorno | Base de datos | Servidor web | Estado |
|---|---|---|---|
| DEV | `PROFESIOGRAMA_DEV` en `NIQUEL\SSDEV` | Equipo del desarrollador (`https://localhost:7180` + Vite) | En uso |
| QA | `PROFESIOGRAMA_QA` en `NIQUEL\SSDEV` | Sitio IIS en `SDMAPPTEST2` (§5) | Base lista; sitio pendiente (Fase 7d) |
| PRO | Servidor y SQL **por confirmar con TI** (pendiente 75). Nombre de base propuesto: `SIG_PROFESIOGRAMA`, porque en SEDEMI existe otra aplicación antigua «Profesiograma» | Por confirmar con TI | — |

## 2. Bases y logins

**PROFESIOGRAMA_QA** (creada por el usuario el 29/10/2026):
- collation `Modern_Spanish_CI_AS`, compatibilidad 150;
- recuperación SIMPLE, RCSI desactivado (OFF);
- dueño `sa`; archivos en `D:\DATA\` del servidor.

**Login `profesiograma_test`:**
- `CHECK_POLICY ON`, `CHECK_EXPIRATION OFF`, base por defecto `PROFESIOGRAMA_QA`;
- en la base, roles `db_datareader` + `db_datawriter`;
- comprobado con `EXECUTE AS`: no es `db_owner` y no puede crear tablas.

| Entorno | Login de la aplicación | Login de migraciones |
|---|---|---|
| DEV | `profesiograma_dev` (`db_owner` de su base; pendiente 47) | El mismo (`dotnet ef database update` o un script) |
| QA | `profesiograma_test`: lectura y escritura (`db_datareader` + `db_datawriter`) | `sa` (lo ejecuta el usuario) |
| PRO | `profesiograma_prod_app`: lectura y escritura (`db_datareader` + `db_datawriter`) | `profesiograma_prod_migrator`: `db_ddladmin` + `db_datareader` + `db_datawriter`, **sin** `db_owner` |

**Por qué basta con lectura y escritura** (consulta de solo lectura del código, 29/10/2026):
- La API **no aplica migraciones** al arrancar: no hay `Migrate()` ni `EnsureCreated()` (`App.Api/Program.cs`).
- El sembrador de datos de prueba solo corre en Development **y** con `DatosPrueba:SembrarAlIniciar = true` (`DatosPruebaExtensions.cs:14`; `appsettings.json` trae `false`).
- `/api/health/db` devuelve **404 fuera de Development** (`HealthEndpoints.cs:15`); sus consultas de permisos (`HAS_PERMS_BY_NAME`, `IS_ROLEMEMBER`, `sys.databases`) no fallan con un login mínimo.
- `sp_getapplock` (en `master`) solo requiere el rol `public`. La vista `dbo.vwProyectoResumen` se lee con un SELECT que cubre `db_datareader`: la vista es de dbo sobre tablas de dbo, así que alcanza el permiso sobre la vista.
- `ExecuteUpdate` y `ExecuteDelete` son DML (`db_datawriter`).
- No hay procedimientos ni funciones propios (EXECUTE), secuencias, Hangfire, `TRUNCATE`, `SqlBulkCopy` ni DBCC.

## 3. Migraciones

Procedimiento para QA y PRO (pendiente 65):
1. Generar el script `--idempotent` desde el **commit que se publica**.
2. Guardarlo **fuera del repo**, en `D:\PROFESIOGRAMA_DEPLOY\<ENTORNO>\`, y agregar al inicio `USE <base>;` y `GO`.
3. Revisarlo antes de ejecutar: buscar `DROP TABLE`, `DELETE FROM` y `USE`, y comprobar que la base sea la correcta.
4. Ejecutarlo **completo, sin selección** (en la TAREA-27, en DEV, una selección parcial dejó fuera el `BEGIN TRANSACTION`).
5. Al terminar, comprobar `@@TRANCOUNT = 0` y que EF no tenga migraciones pendientes.

**Estado de QA después de la primera aplicación:**
- 6 migraciones y 23 tablas (DEV tiene además `sysdiagrams`, creada por SSMS);
- 9 parámetros, 3 cargos semilla, 1 usuario y 0 proyectos.

## 4. Respaldos

| Base | Recuperación | Respaldos |
|---|---|---|
| DEV | SIMPLE desde el 29/10/2026 (antes FULL sin respaldos del log) | Manuales, antes de cada cambio de esquema |
| QA | SIMPLE | Manuales, antes de cada cambio de esquema y del «punto cero» |
| PRO | FULL | Respaldos del log a cargo de TI |

**Forma:** `COPY_ONLY` + `CHECKSUM`, verificado con `RESTORE VERIFYONLY`, en `D:\BACKUP` del servidor SQL.

| Archivo | Para qué |
|---|---|
| `PROFESIOGRAMA_DEV_20261029_antes_TAREA27.bak` | Estado de DEV antes de la migración `QuitarParametrosAlmuerzo` |
| `PROFESIOGRAMA_QA_20261030_punto_cero.bak` | Estado limpio de QA para repetir los ensayos de la Fase 3 |

**Restauración del punto cero:** `SINGLE_USER` → `RESTORE … WITH REPLACE` → `MULTI_USER`. El usuario la probó una vez con éxito. El script lo guarda el usuario, fuera del repo.

## 5. Servidor de QA: SDMAPPTEST2.sedemi.local

- **Sistema:** Windows Server 2019 en una máquina virtual.
- **IIS:** en ejecución, con el módulo ASP.NET Core V2 y el runtime .NET 10.0.10.
- **Sitios:** unos 40, todos en http y ninguno en https.
- **Certificados:** están vencidos; no hay ninguno utilizable (pendiente 75).
- **Convenciones:** los sitios van en `C:\inetpub\wwwroot\<Sistema>\`; la unidad `E:` es RESPALDOS.
- **Conectividad comprobada:**
  - `NIQUEL:1433`;
  - el ERP (puertos 7048 y 7055);
  - `login.microsoftonline.com` y `graph.microsoft.com`.
- **Red (05/11/2026):**
  - los sitios de SDMAPPTEST2 escuchan en `192.168.1.77` con puertos 70xx; el **7082 está libre** (comprobado el 05/11/2026) y el PC del usuario llega a los puertos 70xx;
  - `backstack.sedemi.com` resuelve a `192.168.1.117`: el ERP está en otro servidor;
  - `sdmapp.sedemi.local` resuelve a `192.168.1.52`: candidato a IIS de producción, por confirmar con TI.
- **Cuenta:** en QA se usa `administrador.SEDEMI` (decisión del usuario del 05/11/2026); la cuenta de administración se revisa al llegar a producción.
- Hay un sitio antiguo, **«Profesiograma_FrontEnd»** (otro proyecto, WebForms), que **no se toca**.

## 6. Diseño del sitio de QA (propuesta, pendiente de la Fase 7d)

- **Sitio y grupo de aplicaciones:** `SIG_Profesiograma`; el grupo sin código administrado.
- **Carpeta:** `C:\inetpub\wwwroot\SIG_Profesiograma\`.
- **Un solo sitio para la API y el SPA**, con un mismo origen: no hace falta CORS (pendiente 59).
- **Enlaces:**
  - https en el puerto 443 con SNI, cuando haya certificado;
  - un enlace http **provisional**, solo para la prueba de humo: `http 192.168.1.77:7082`, según la convención de puertos 70xx del servidor.

## 7. Entra ID

Lo verificó el usuario el 29/10/2026 (pendiente 7):
- Los usuarios pueden registrar aplicaciones. El usuario no tiene roles de administrador.
- La licencia es Microsoft Entra ID gratis: los roles `Admin` y `Gestor` se asignan **por usuario**, no por grupo.
- La configuración de consentimiento no es visible (401).
- Los permisos de **aplicación** de Graph requieren a TI (Privileged Role Administrator).

**Estado: Entra ID en DEV verificado (05/11/2026)**, Fase 7b y TAREA-7b-2 (M1–M7). Operación y lo que falta para QA: §7.4.

### 7.1 Fase 7b — diseño final (implementado y verificado el 05/11/2026)

**Decisiones fijas:**
- single-tenant SEDEMI;
- registros «SIG-Profesiograma-API-DEV» y «SIG-Profesiograma-SPA-DEV»;
- scope `access_as_user`;
- App Roles `Admin` y `Gestor` por usuario;
- el SPA preautorizado;
- DevAuth solo en Development y seleccionable;
- departamentos en la base, sin Graph.

| # | Decisión del usuario (Fase A, 05/11/2026) |
|---|---|
| P1 | Sin rol `Admin` ni `Gestor`: 403 «Sin rol asignado» **antes** del alta; la fila `Usuario` no se crea |
| P2 | No se valida el dominio al ingresar (single-tenant + asignación requerida) |
| P3 | UPN distinto del correo: resuelto en la **TAREA-7b-2** (§7.3) |
| P5 | «Asignación requerida» solo en la API; el frontend traduce AADSTS501051 a «No tienes acceso…» |
| P6 | Tenant y client IDs (públicos) en user-secrets (backend) y en `frontend/.env.development.local` (ignorado) |
| Ajuste 1 | `AzureAd:Audience` **vacío**: Microsoft.Identity.Web acepta el GUID del ClientId (token v2) y `api://ClientId` (token v1); un token v2 con `aud = api://…` se rechaza (E2c) |
| Ajuste 2 | MSAL con `sessionStorage`. Sin cuenta, **un** `ssoSilent` (con `loginHint` si se guardó uno) y luego el botón «Iniciar sesión con la cuenta de SEDEMI». Nunca un redirect automático |
| Ajuste 3 | `frontend/.env.development.local.example` versionado, con marcadores |

**Backend** (`App.Api/Seguridad/`):
- Todas las políticas (por defecto, Fallback, Admin y Gestor) exigen un usuario autenticado y `RequireScope("access_as_user")`. Así un token de aplicación, con roles pero sin `scp`, recibe 403.
- DevAuth emite `scp=access_as_user`.
- `UsuarioActualMiddleware` rechaza al usuario sin rol antes del alta.
- Identificador: `oid`. UPN y correo: ver §7.3 (TAREA-7b-2).

**Frontend** (`src/auth/`):
- `entra.ts`: lógica pura.
- `msal.ts`: instancia única, `initialize` y `handleRedirectPromise` en `main.tsx`.
- `SesionEntra.tsx`: la puerta de la aplicación y «Cerrar sesión».
- `obtenerEncabezadosAutenticacion()` sigue siendo el único punto: `X-Dev-User` en dev, `Authorization: Bearer` con MSAL en Entra.
- `VITE_AUTH_MODE`: `dev` vale solo con `npm run dev`; cualquier otro valor, y siempre el build, usan Entra.
- MSAL: `@azure/msal-browser` 5.26.0 y `@azure/msal-react` 5.7.3.

**Respuestas:**

| Caso | Respuesta |
|---|---|
| Sin token, vencido, `aud` o `iss` ajeno, firma inválida, `X-Dev-User` en modo EntraId | 401 sin cuerpo; el frontend muestra «Iniciar sesión» |
| Token sin `scp` (de aplicación) | 403 |
| Sin rol `Admin` ni `Gestor` | 403 «Sin rol asignado» / «Pida a TI que le asigne el rol Gestor o Admin en Entra ID.»; el frontend muestra «No tienes acceso a PROFESIOGRAMA.» |
| AADSTS501051 (sin asignación en la API) | Sin llamada a la API; el frontend muestra «No tienes acceso a PROFESIOGRAMA.» |
| Gestor en un endpoint Admin | 403 (sin cambios) |
| Usuario inactivo / correo de otra identidad / token sin correo | 403 con los textos de `FASE_5_Usuarios_Departamentos.md` §1 y §6 (sin cambios) |

**Claves de configuración (DEV):**

| Dónde | Clave | Valor |
|---|---|---|
| user-secrets (`src/App.Api`) | `Autenticacion:Modo` | `EntraId` para probar; `dotnet user-secrets remove Autenticacion:Modo` vuelve a DevAuth |
| user-secrets | `AzureAd:TenantId`, `AzureAd:ClientId` | Tenant de SEDEMI; Client ID de la API |
| `appsettings.json` | `AzureAd:Audience` | `""` (ajuste 1); `Instance` = `https://login.microsoftonline.com/` |
| `frontend/.env.development.local` | `VITE_AUTH_MODE`, `VITE_ENTRA_TENANT_ID`, `VITE_ENTRA_CLIENT_ID`, `VITE_ENTRA_API_SCOPE` | `entra`; tenant; Client ID del SPA; `api://<ClientId de la API>/access_as_user` |

No hay secretos: el SPA es un cliente público (PKCE) y la API solo valida tokens. Vite debe correr en el puerto **5173**, porque la URI de redirección es exacta.

### 7.2 Lo que el usuario crea en el portal (Entra ID → Registros de aplicaciones)

**SIG-Profesiograma-API-DEV**
1. Tipos de cuenta: «Solo las cuentas de este directorio organizativo». Sin URI de redirección.
2. **Exponer una API:**
   - URI del identificador de aplicación: `api://<ClientId>` (el valor por defecto).
   - Scope `access_as_user`:
     - quién da el consentimiento: «Administradores y usuarios»;
     - nombre (admin y usuario): «Acceder a PROFESIOGRAMA DEV como el usuario»;
     - descripción: «Permite al SPA llamar a la API con la identidad del usuario»;
     - estado: Habilitado.
3. **Roles de aplicación**, con tipo de miembro «Usuarios/grupos» y Habilitado:
   - «Administrador», valor `Admin`;
   - «Gestor», valor `Gestor`.
4. **Manifiesto:** `requestedAccessTokenVersion` (o `accessTokenAcceptedVersion`) = `2`.
5. **Aplicaciones cliente autorizadas:** el Client ID del SPA, con `access_as_user` (después de crear el SPA).
6. **Aplicaciones empresariales → la API:**
   - «¿Asignación requerida?» = **Sí**;
   - Usuarios y grupos: tu cuenta con el rol Administrador y, si hay una segunda cuenta, esa con el rol Gestor.

**SIG-Profesiograma-SPA-DEV**
1. Inquilino único.
2. **Plataforma «Aplicación de página única»:**
   - URI de redirección `http://localhost:5173`;
   - sin concesión implícita;
   - «Permitir flujos de clientes públicos» = No.
3. **Permisos de API:** «Mis API» → la API → `access_as_user` (delegado). Quitar `User.Read`.
4. **Certificados y secretos:** ninguno.

**Datos para devolver** (son públicos):
- Id. de directorio (tenant);
- Client ID de la API;
- Client ID del SPA;
- el Application ID URI, si no es el de por defecto.

**Riesgos:**
- Si el tenant bloquea el consentimiento de usuario, el primer ingreso pedirá la aprobación de un administrador para `openid`, `profile` y `offline_access`, y hará falta TI (pendiente 75).
- Si el portal no deja asignar roles al dueño del registro, también hará falta TI.
- Un cambio de rol rige con el siguiente token (hasta unos 60–90 minutos, o al cerrar e iniciar sesión).

### 7.3 TAREA-7b-2 — UPN distinto del correo (P3; implementada y verificada el 05/11/2026, migración aplicada en DEV)

En SEDEMI el UPN (`preferred_username`, p. ej. `sig4@…`) suele ser **distinto** del correo (p. ej. `carlos_pogo@…`). El Admin precarga por correo y los avisos de la Fase 7c irán al correo. En el registro de la API se agrega el claim opcional **`email`** al access token (Configuración del token → Agregar notificación opcional → Acceso → `email`).

**Modelo:**
- `Usuario.Upn`: `nvarchar(256)` NULL, en minúsculas, con índice único filtrado `UX_Usuario_Upn` (`[Upn] IS NOT NULL`);
- `Usuario.Email` sigue siendo el correo;
- migración `AgregarUpnUsuario`.

**Claims:**
- UPN = `preferred_username` (si falta, `upn`);
- correo = `email` (si falta, el UPN);
- «Token sin correo» solo si faltan los dos;
- `/me` y `UsuarioActual.Email` devuelven el **correo**;
- DevAuth emite también `email`.

**Primer ingreso:** las reglas son una función pura, `ReglasProvisionamiento.Decidir` (Application); Infrastructure solo busca y guarda.
- **Orden de búsqueda: oid → UPN → correo.** Al encontrar la fila se completan el oid y el UPN que falten; nunca se reemplazan.
- Si no existe, se crea con `Email` = correo y `Upn` = UPN. El rol ya se exigió antes (P1).

| Conflicto | Respuesta |
|---|---|
| Correo ligado a otro oid | 403 «El correo está asociado a otra identidad de Entra ID.» (sin cambios) |
| UPN ligado a otro oid, o el UPN que se completaría ya está en otra fila | 403 «El UPN está asociado a otra identidad de Entra ID.» |
| Q1: fila encontrada por correo con un `Upn` guardado distinto del del token | 403 «El correo está asociado a otro UPN de Entra ID.» |

**Decisiones del usuario (05/11/2026):**

| # | Decisión |
|---|---|
| Q1 | Sí: el conflicto anterior da 403 |
| Q2 | Sí: el filtro `texto` del listado de usuarios también busca en `Upn` |
| Q3 | Sí: los logs del alta registran solo el Id y los 8 primeros caracteres del oid; nunca el correo ni el UPN |
| Q4 | No: el correo **no** se actualiza en cada ingreso. **Tema de la Fase 7c** (avisos por correo): si una persona cambia su correo en Entra, `Usuario.Email` queda con el anterior. Decidir allí si se actualiza desde el claim `email` o lo corrige el Admin |

**Administración:** el listado (columna «Inicio de sesión (UPN)») y el detalle muestran el UPN de solo lectura; sin valor, «Se completa en el primer ingreso». El alta manual sigue pidiendo el correo.

**Fase 3 (sin implementar):** el UPN se precarga desde el nombre de inicio de sesión de los campos de persona de SharePoint (ver también `FASE_5_Usuarios_Departamentos.md` §7).

**Riesgos:**
- **Correo provisional:** si el token no trae `email` (claim opcional sin configurar, o cuenta sin el atributo `mail`), la fila se crea con `Email` = UPN. Un alta manual posterior con el correo real crearía una segunda fila para la misma persona, o daría «Ya existe» si coincidieran. Por eso hay que configurar el claim antes del primer ingreso real y comprobarlo en M3.
- **Filas DevAuth:** las de antes de la 7b-2 completan su `Upn` en el siguiente ingreso (DevAuth: UPN = correo de prueba).

### 7.4 Entra ID en DEV — estado verificado y operación (05/11/2026)

**IDs públicos de DEV** (no son secretos; los valores reales están en user-secrets y en `frontend/.env.development.local`):

| Dato | Valor |
|---|---|
| Tenant (Id. de directorio) | `3cec9264-5c1c-429e-8877-c23d701139ac` |
| Client ID de SIG-Profesiograma-API-DEV | `8e32bc38-c2bd-408c-aeb0-efe322f5c606` |
| Client ID de SIG-Profesiograma-SPA-DEV | `77fa0b93-436d-4983-b3e9-caccf894156c` |
| Scope | `api://8e32bc38-c2bd-408c-aeb0-efe322f5c606/access_as_user` |

**Configuración verificada en el portal:**
- **API:**
  - scope `access_as_user`;
  - roles `Admin` y `Gestor`;
  - manifiesto v2;
  - claim opcional `email` (Acceso), con el permiso `email` de Graph;
  - SPA preautorizado.
- **Aplicación empresarial de la API:**
  - asignación requerida = Sí;
  - visible = No;
  - asignación por usuario (la licencia no permite grupos).
- **SPA:**
  - plataforma SPA `http://localhost:5173`;
  - sin flujos implícitos ni de cliente público;
  - permiso `access_as_user`.
- Ningún permiso requiere consentimiento del administrador. El primer ingreso real no lo pidió (el riesgo de §7.2 no ocurrió).

**Token observado (v2):**
- `aud` = GUID de la API;
- `iss` = v2.0 del tenant;
- `azp` = SPA;
- `scp` = `access_as_user`;
- `roles`;
- `preferred_username` (UPN) y `email` (correo).

**Notas de la verificación:**
- **M4:** la cuenta sin asignación puede ver la pantalla de Microsoft (AADSTS501051 en el inicio de sesión) o la nuestra («No tienes acceso a PROFESIOGRAMA.»). Las dos son correctas.
- **Migración:** `AgregarUpnUsuario` se aplicó con `dotnet ef database update`, porque el script común deja el `ALTER` y el `CREATE INDEX` en el mismo lote. En QA se usa el script `--idempotent`, que envuelve cada sentencia en `EXEC`.

**Cambiar de modo en DEV:**

Para pasar a Entra ID:
1. Desde `backend/`: `dotnet user-secrets set Autenticacion:Modo EntraId --project src/App.Api`.
2. `cd frontend` y luego `ren .env.development.local.entra .env.development.local`.
3. Reiniciar la API y `npm run dev`.

Para volver a DevAuth:
1. Desde `backend/`: `dotnet user-secrets remove Autenticacion:Modo --project src/App.Api`.
2. `cd frontend` y luego `ren .env.development.local .env.development.local.entra`.
3. Reiniciar la API y `npm run dev`.

El `cd` va antes del `ren`, porque `ren` no acepta una ruta en el nombre nuevo. Git ignora los dos nombres del archivo: `.env.development.local` por `frontend/.gitignore` (`*.local`) y `.env.development.local.entra` por `.gitignore` (`**/.env*`). Si los modos no coinciden (API EntraId con el frontend en dev, o al revés), la aplicación muestra la ayuda correspondiente.

**Decisión del usuario (05/11/2026):** DEV queda por defecto en **DevAuth**, porque los scripts `.cmd` de `backend/tests/manual/` necesitan `X-Dev-User`. QA y PRO usan **siempre Entra ID**: DevAuth solo existe en Development (guarda E13).

**Lección:** nunca pegar tokens en chats ni en tickets. Para inspeccionarlos se usa `jwt.ms`.

**Falta para QA (y luego PRO):** los registros de QA, con sus URI de redirección HTTPS, cuando TI entregue el certificado y el DNS (pendiente 75). La prueba de humo de la Fase 7d-1 (`http 192.168.1.77:7082`) no usa inicio de sesión.

## 8. Pendientes con TI

Ver el pendiente 75 de `docs/00_ESTADO_ACTUAL.md`, que reúne:
- el certificado y el DNS;
- los servidores de PRO (IIS: candidato SDMAPP, `192.168.1.52`; SQL por identificar);
- el nombre de la base de PRO;
- la cuenta de administración del servidor de PRO;
- los consentimientos de Graph.
