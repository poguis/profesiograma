# FASE 7 — Entornos y despliegue

**Última actualización:** 2026-11-06 (cierre de la TAREA-7d-4: §7.4 y §11). Pendientes relacionados en `docs/00_ESTADO_ACTUAL.md`: 7, 59, 63, 65 y 75.

Todo lo de este documento lo **ejecutó el usuario** (SSMS, RDP). Claude Code no se conecta a ninguna base ni a ningún servidor.

## 1. Entornos

| Entorno | Base de datos | Servidor web | Estado |
|---|---|---|---|
| DEV | `PROFESIOGRAMA_DEV` en `NIQUEL\SSDEV` | Equipo del desarrollador (`https://localhost:7180` + Vite) | En uso |
| QA | `PROFESIOGRAMA_QA` en `NIQUEL\SSDEV` | Sitio IIS `SIG_Profesiograma` (id 42) en `SDMAPPTEST2` (§5, §9), entorno `QA` | **En uso por http provisional** (`http://192.168.1.77:7082`, desde el 06/11/2026, sin inicio de sesión); HTTPS según el pendiente 75 |
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
| `PROFESIOGRAMA_QA_20261030_punto_cero.bak` | Estado limpio de QA (6 migraciones). **Reemplazado por el punto cero v2** |
| `PROFESIOGRAMA_QA_20261105_antes_7d1.bak` | TAREA-7d-1: QA antes de `AgregarUpnUsuario` (6,08 MB, VERIFYONLY válido) |
| `PROFESIOGRAMA_QA_20261105_punto_cero_v2.bak` | TAREA-7d-1: estado limpio de QA con las 7 migraciones (incluye `AgregarUpnUsuario`), para repetir los ensayos de la Fase 3 (VERIFYONLY válido) |

**Configuración de IIS (SDMAPPTEST2):** `appcmd add backup "antes_SIG_Profesiograma_20261106"` antes de crear el grupo y el sitio. Se restaura con `appcmd restore backup "antes_SIG_Profesiograma_20261106"`; esa restauración revierte **toda** la configuración de IIS, también la de los otros sitios.

**Restauración del punto cero:** `SINGLE_USER` → `RESTORE … WITH REPLACE` → `MULTI_USER`. El usuario la probó una vez con éxito. El script lo guarda el usuario, fuera del repo.

## 5. Servidor de QA: SDMAPPTEST2.sedemi.local

- **Sistema:** Windows Server 2019 en una máquina virtual.
- **IIS:** en ejecución, con el módulo ASP.NET Core V2 y el runtime .NET 10.0.10. La aplicación (paquetes 10.0.12, dependiente del framework) **arranca con 10.0.10** (comprobado el 06/11/2026). Igual se pidió a TI actualizar el Hosting Bundle (pendiente 75).
- **Encabezado de la consola:** Windows Server 2019, `10.0.17763`. Sirve para comprobar que un comando se ejecuta en el servidor y no en el PC.
- **Cabeceras de todo el servidor:** `X-Powered-By: Sedemi` (configurada por TI a nivel de servidor); `Server` ausente.
- **Globalización:** Windows Server 2019 (17763) no trae ICU; la solución usa NLS en todos los entornos (§9, D7).
- **Sitios:** unos 40, todos en http y ninguno en https.
- **Certificados:** los del servidor están vencidos. Para QA hay uno **autofirmado** (TAREA-7d-3, creado por el usuario el 06/11/2026):
  - creado con `New-SelfSignedCertificate`: `CN=sdmapptest2.sedemi.local`, con el SAN `sdmapptest2.sedemi.local`, RSA 2048 / SHA256, clave no exportable;
  - huella `DD51425E584DF82B312B747DB5BC29FF65F3C6D5`, **vence el 06/11/2027**;
  - `.cer` público en `C:\DEPLOY\SIG_Profesiograma\sig_profesiograma_qa_7083.cer`; en el PC del usuario, `D:\PROFESIOGRAMA_DEPLOY\QA\certificados\` (fuera del repo).
  - Para PRO, o para un nombre `.sedemi.com` de QA, se pide a TI un certificado de confianza (pendiente 75).
- **DNS:** `sdmapptest2.sedemi.local` resuelve a `192.168.1.77`.
- **Experimento en Edge (06/11/2026):** tras aceptar la advertencia del certificado, `window.isSecureContext = true` y MSAL funciona. Los probadores no instalan el `.cer`. Con `curl`, sin `-k` → 000 (no confiable); con `-k` → 200.
- **TLS:** «Deshabilitar las versiones TLS heredadas: Not Set», configuración de todo el servidor; le corresponde a TI (pendiente 75).
- **Convenciones:** los sitios van en `C:\inetpub\wwwroot\<Sistema>\`; la unidad `E:` es RESPALDOS.
- **Conectividad comprobada:**
  - `NIQUEL:1433`;
  - el ERP (puertos 7048 y 7055);
  - `login.microsoftonline.com` y `graph.microsoft.com`.
- **Red (05/11/2026):**
  - los sitios de SDMAPPTEST2 escuchan en `192.168.1.77` con puertos 70xx; el **7082 lo usa `SIG_Profesiograma`** desde el 06/11/2026 (estaba libre y accesible sin regla de firewall) y el PC del usuario llega a los puertos 70xx;
  - `backstack.sedemi.com` resuelve a `192.168.1.117`: el ERP está en otro servidor;
  - `sdmapp.sedemi.local` resuelve a `192.168.1.52`: candidato a IIS de producción, por confirmar con TI.
- **Cuenta:** en QA se usa `administrador.SEDEMI` (decisión del usuario del 05/11/2026); la cuenta de administración se revisa al llegar a producción.
- Hay un sitio antiguo, **«Profesiograma_FrontEnd»** (otro proyecto, WebForms), que **no se toca**.

## 6. Sitio de QA (implementado el 06/11/2026; procedimiento en §9)

- **Estado:** sitio `SIG_Profesiograma`, id 42, `http/192.168.1.77:7082:`. La ruta física es `C:\inetpub\wwwroot\SIG_Profesiograma`, con NTFS RX para `IIS AppPool\SIG_Profesiograma`. El paquete está en `C:\DEPLOY\SIG_Profesiograma\<carpeta>\`.
- **Cadena de conexión:** variable `ConnectionStrings__Profesiograma` del grupo, con la forma `Server=NIQUEL,1433;Database=PROFESIOGRAMA_QA;User ID=profesiograma_test;Password=…;Encrypt=True;TrustServerCertificate=True;Application Name=SIG_Profesiograma_QA`. Va por puerto fijo, sin SQL Browser. La contraseña solo está en el servidor.
- **Sitio y grupo de aplicaciones:** `SIG_Profesiograma`; el grupo sin código administrado, canalización integrada, `ApplicationPoolIdentity` y «Cargar perfil de usuario» = True.
- **La SPA la sirve ASP.NET Core** desde `wwwroot` (D2), sin URL Rewrite ni aplicación `/api` aparte.
- **Carpeta:** `C:\inetpub\wwwroot\SIG_Profesiograma\`.
- **Un solo sitio para la API y el SPA**, con un mismo origen: no hace falta CORS (pendiente 59).
- **Enlaces reales** (convención de puertos 70xx del servidor):
  - `https/192.168.1.77:7083:`, **sin SNI**, con el certificado autofirmado: la **URL pública** es `https://sdmapptest2.sedemi.local:7083` (TAREA-7d-3);
  - `http/192.168.1.77:7082:`: el navegador redirige a la URL pública (E1); `humo-qa` lo usa en H13.

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
| Ajuste 2 | MSAL con `sessionStorage`. Sin cuenta, **un** `ssoSilent` (con `loginHint` si se guardó uno) y luego el botón «Iniciar sesión con la cuenta de SEDEMI». Nunca un redirect automático. **Nota (TAREA-7d-4):** `ssoSilent` nunca funcionó hasta la 7d-4, tampoco en DEV; M5 no cubría una pestaña nueva. En MSAL 5 requiere el puente `/redirect.html` (§11) |
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

**Al terminar cualquier prueba con Entra en DEV, se vuelve a DevAuth** (TAREA-7d-4: el 06/11/2026 se encontró DEV en modo Entra). Para comprobarlo:
- `cd frontend` y `dir /b /a .env*`: debe aparecer `.env.development.local.entra` y **no** `.env.development.local`.
- Desde `backend/`: `dotnet user-secrets list --project src/App.Api | findstr Autenticacion`, **sin resultados** (sin mostrar los otros valores).

**Lección:** nunca pegar tokens en chats ni en tickets. Para inspeccionarlos se usa `jwt.ms`.

**Registros de QA (06/11/2026):**
- **API-QA** (SIG-Profesiograma-API-QA): ClientId `a0b57ea8-e21b-4bd1-a9c4-f39fc7a0b2eb`, confirmado en el portal; IDs en §9, D5. Tiene los roles Admin y Gestor, el claim `email`, tokens v2, asignación requerida (el usuario como Admin y el pasante como Gestor) y autoriza al SPA-QA.
- **SPA-QA** (SIG-Profesiograma-SPA-QA): ClientId `4312b905-f25b-4b58-8e0a-ef896a717761`. Plataforma SPA con la URI `https://sdmapptest2.sedemi.local:7083` (sin barra final), sin flujos implícitos ni de cliente público. Permiso delegado `access_as_user` de la API-QA; sin `User.Read` ni secretos.
- Inicio de sesión en QA: TAREA-7d-3 (§10).
- **URIs silenciosas (TAREA-7d-4, registradas por el usuario el 06/11/2026):** `https://sdmapptest2.sedemi.local:7083/redirect.html` (SPA-QA) y `http://localhost:5173/redirect.html` (SPA-DEV), además de los orígenes. `ssoSilent` nunca funcionó hasta la 7d-4, tampoco en DEV: M5 no cubría una pestaña nueva. Desde la 7d-4, la prueba de la pestaña nueva (N1) forma parte de la verificación estándar del inicio de sesión (§11).
- **Falta para PRO:** sus registros, con la URI de PRO (pendiente 75).

## 8. Pendientes con TI

Ver el pendiente 75 de `docs/00_ESTADO_ACTUAL.md`, que reúne:
- el certificado y el DNS;
- los servidores de PRO (IIS: candidato SDMAPP, `192.168.1.52`; SQL por identificar);
- el nombre de la base de PRO;
- la cuenta de administración del servidor de PRO;
- los consentimientos de Graph.

## 9. Despliegue en QA (TAREA-7d-1, preparada el 05/11/2026 y ejecutada el 06/11/2026)

Publicación de prueba en `SDMAPPTEST2`, `http 192.168.1.77:7082`, con una prueba de humo **sin inicio de sesión**: Entra ID no admite una URI de redirección http fuera de localhost, y el SPA-QA espera el certificado y el DNS (pendiente 75). La ejecuta el usuario con `deploy/qa/LEEME.md` (hasta la TAREA-7d-3, `backend/tests/manual/tarea7d1/LEEME.md`).

**Procedimiento** (el resultado esperado de cada paso está en el LEEME):
1. `publicar-qa.cmd` en el PC del usuario.
2. Respaldo de QA.
3. Migración: `migraciones-QA.sql`, completo, F5 sin selección, con `@@TRANCOUNT = 0` al final.
4. Punto cero v2 (§4).
5. Grupo de aplicaciones y su variable con appcmd, desde CMD (nunca desde PowerShell).
6. Sitio, permisos NTFS, enlace y firewall.
7. Copia con `app_offline.htm` y respaldo de la versión anterior en `E:\`.
8. `humo-qa.cmd` (H1–H9) y las verificaciones visuales V1 y V2.
9. Diagnóstico (500.19, 500.30, 500.31, 503) y reversión.

**Decisiones del usuario (05/11/2026):**

| # | Decisión |
|---|---|
| D1 | Entorno `QA`: `appsettings.QA.json` (TenantId y ClientId de la API-QA, y `Logging` con EF en Warning) y `-p:EnvironmentName=QA` (lo escribe en `web.config`) |
| D2 | La SPA la sirve ASP.NET Core desde `wwwroot`, con fallback a `index.html`. `publicar-qa` copia `dist` a `wwwroot` |
| D3 | Cadena de conexión de QA en la variable `ConnectionStrings__Profesiograma` del grupo de aplicaciones (appcmd desde CMD); la escribe el usuario en el servidor |
| D4 | `vite build --mode qa` con `frontend/.env.qa` versionado (solo IDs públicos), sin `VITE_ENTRA_CLIENT_ID` hasta que exista el SPA-QA. Antes de PRO se evalúa una configuración en tiempo de ejecución |
| D5 | API-QA (SIG-Profesiograma-API-QA): tenant `3cec9264-5c1c-429e-8877-c23d701139ac`, scope `api://a0b57ea8-e21b-4bd1-a9c4-f39fc7a0b2eb/access_as_user` |
| D6 | `/api/salud/vivo` (sin base) y `/api/salud/listo` (`SELECT 1`, 200/503 sin detalle), anónimos, con `IDatabaseDiagnostics.EstaDisponibleAsync`. `/api/health/db` sigue siendo solo de Development |
| D7 | NLS en toda la solución (`System.Globalization.UseNls` en `backend/Directory.Build.props`). La zona de Ecuador usa el identificador de Windows `SA Pacific Standard Time`, que no requiere ICU |
| D8 | Se quitó `MapGet("/")`: la raíz sirve `index.html` |

**Ajustes:**

| # | Ajuste |
|---|---|
| J1 | `RuntimeHostConfigurationOption` (ítem) y una prueba con `AppContext.GetData`. Las 1196 pruebas existentes pasaron sin cambios con NLS |
| J2 | Las mismas `StaticFileOptions` en `UseStaticFiles` y en `MapFallbackToFile`: `/assets/*` → `public, max-age=31536000, immutable`; el resto (index.html) → `no-cache` |
| J3 | `MapFallback("/api/{**ruta}")` → 404 sin HTML, con `AllowAnonymous` |
| J4 | `decidirArranque` (frontend): DevAuth → `requiereHttps` (contexto no seguro) → `configIncompleta` → `app`. Un fallo de MSAL muestra «No se pudo iniciar la sesión con Entra ID.», nunca una página en blanco |
| J5 | `publicar-qa` exige un árbol limpio y compila desde `git archive HEAD`, en una carpeta temporal que borra siempre. Resultado en `D:\PROFESIOGRAMA_DEPLOY\QA\<yyyyMMdd_HHmm>_<hash7>\` |
| J6 | H9 solo informa `Server`, `X-Powered-By` y el listado de `/assets/`. Sin `web.config` propio: el endurecimiento va en la 7d-2 |
| J7 | Punto cero v2 después de la migración y antes del humo |

**Otras reglas de esta tarea:**
- En modo EntraId, la API no arranca si `AzureAd:TenantId` o `AzureAd:ClientId` no son GUID (E15).
- `appsettings.Development.json` no se publica (`CopyToPublishDirectory="Never"`).
- La publicación es **dependiente del framework** (sin RID): `App.Api.exe` y `web.config` en proceso.
- La configuración de QA queda en tres lugares:
  - `appsettings.json`, la base: modo EntraId, ERP Http, `BaseDatos`, `Usuarios`;
  - `appsettings.QA.json`: los IDs de la API-QA y `Logging`;
  - la variable del grupo: la cadena de conexión.
- Sin `NovedadesExternas:Modo`, la fuente queda «no configurada».
- **Logs bajo IIS:**
  - van al Visor de eventos (proveedor EventLog, Warning o superior; un error de arranque aparece como 500.30 con el origen «IIS AspNetCore Module V2»);
  - el stdout de ANCM se activa solo durante un diagnóstico.

**Ejecución (05–06/11/2026, la hizo el usuario):** el detalle está en `docs/tareas/TAREA-7d-1-reporte.md` §7.
- **Publicación:** commit `37e5dc4`; `publicar-qa` con P1 y C1–C12 OK (162 archivos, 94,93 MB).
- **Base QA:** respaldo previo, 7 migraciones (`Usuario.Upn` y `UX_Usuario_Upn`), `@@TRANCOUNT = 0` y punto cero v2 (§4).
- **IIS:**
  - respaldo de la configuración;
  - grupo y variable (escrita con el Editor de configuración);
  - sitio id 42;
  - sin regla de firewall: el 7082 ya era accesible.
- **Runtime:** arranca con 10.0.10.
- **Humo:**
  - H1–H8 OK;
  - H9 INFO: `X-Powered-By: Sedemi` es del servidor; `GET /assets/` → 200, porque el fallback devuelve `index.html` y no es un listado;
  - V1 OK; V2: consola sin errores en rojo (verificado por el usuario el 06/11/2026);
  - en SQL, la sesión de `profesiograma_test` llega desde SDMAPPTEST2 con `program_name = SIG_Profesiograma_QA` y `encrypt_option = TRUE`.

**Incidentes y lo que cambió en el procedimiento** (`LEEME.md`):

| Incidente | Corrección | Cambio en el LEEME |
|---|---|---|
| El sitio se creó con una ruta sin `wwwroot` (500) | `appcmd set vdir` | Paso 6: copiar y pegar los comandos; verificar con `appcmd list vdir` |
| Una regla de firewall se creó en el PC del usuario | Se eliminó | Paso 6: primero `curl.exe` desde el PC; la regla solo en el servidor (`10.0.17763` en el encabezado de la consola) |
| Los `.cmd` quedaron con LF | `git checkout` | `.gitattributes` con `*.cmd`/`*.bat eol=crlf` |
| La cadena de conexión con appcmd (historial, auditoría de la línea de comandos, comillas) | Se usó el Editor de configuración | Paso 5: el Editor de configuración es el método principal; `Server=NIQUEL,1433`, `Application Name`; probar antes el login en SSMS |

## 10. Inicio de sesión real en QA por HTTPS (TAREA-7d-3, 06/11/2026; incluye la 7d-2)

**Objetivo:** iniciar sesión con Admin y Gestor en `https://sdmapptest2.sedemi.local:7083`, con una segunda publicación (la primera actualización real). Procedimiento: `deploy/qa/LEEME.md` §10–§13. No hay migraciones nuevas (QA sigue en 7).

**Decisiones del usuario (06/11/2026):**

| # | Decisión |
|---|---|
| E1 | `VITE_URL_PUBLICA` (en `.env.qa`; DEV no la define) y el estado `otroOrigen` en `decidirArranque`, antes de `requiereHttps`. Redirige con `location.replace(origen público + ruta + consulta)`, **sin el fragmento**; si el navegador no redirige, muestra un enlace |
| E2 | `X-Frame-Options: SAMEORIGIN` y `Content-Security-Policy: frame-ancestors 'self'` (solo esa directiva). Nunca `DENY` ni `'none'`: `ssoSilent` carga la propia página en un iframe oculto del mismo origen |
| E3 | HSTS solo con `Seguridad:Hsts = true` (`appsettings.json`: `false`; QA no lo define). Con el autofirmado impediría aceptar la advertencia; se activa en PRO |
| E4 | La CSP completa queda para después (Griffel usa estilos en línea; MSAL, `login.microsoftonline.com`): probarla con `Report-Only` en DEV (pendiente) |
| E5 | `deploy/qa/` versionado (con su `.gitignore`: `resultado-*`, `*.lock`), con `publicar-qa`, `humo-qa`, `comun7d1.ps1`, `simulacion\` y el LEEME. `backend/tests/manual/` sigue ignorada |
| E6 | `humo-qa` por https: `QA_CA` (PEM) → `--cacert` + `--ssl-no-revoke`; `-k` solo con `QA_INSEGURO=1` y un aviso; sin ninguno de los dos, los casos HTTPS quedan OMITIDO |
| E7 | Pospuesta por decisión del usuario (dependencias transitivas con < 14 días); reintentar desde el 13/11/2026 (pendiente 81). `vite` sigue en 8.3.1 |
| E8 | Las 7 carpetas de `backend/tests/manual/` sin versionar no se tocan (K6) |

**Ajustes:**

| # | Ajuste |
|---|---|
| K1 | `UseEncabezadosSeguridad()` es el **primer** middleware: pone los 4 encabezados con `Response.OnStarting`, y así llegan también al 500 que escribe `UseExceptionHandler` después de limpiar la respuesta. Probado en `/`, en un asset, en vivo, en un 401, en un 404 y en un 500 |
| K2 | Se compara `new URL(urlPublica).origin` con `location.origin`. Una URL no `https:` o inválida → `configIncompleta` (nunca redirige). Una barra final no causa un bucle |
| K3 | El `.cer` y el `.pem` viven fuera del repo (`D:\PROFESIOGRAMA_DEPLOY\QA\certificados\`); conversión con `certutil -encode` |
| K4 | `publicar-qa`: C13 (el `dist` lleva el ClientId del SPA-QA y la URL pública), C14 (`.env.qa` sin `VITE_ENTRA_*` vacías) e INFO de migraciones |
| K5 | Pruebas funcionales F1–F4 en el LEEME (escriben en QA; se borran al restaurar el punto cero v2) |
| K6 | Pendiente: decidir qué hacer con las carpetas tarea22c, 23-1, 23-2, 23-3, 25, 25-3 y 62 de `backend/tests/manual/`, que no tienen ningún archivo versionado |

**Cambio de rutas (desviación de la Fase A):**
- Un asset inexistente (`/assets/no-existe.js`) respondía **401**: sin endpoint, la política Fallback exigía autenticación.
- Se agregó `MapFallback("{**ruta:file}") → 404` anónimo y `app.UseRouting()` explícito **después** de `UseSpaEstaticos`. Si no, ese endpoint taparía los archivos reales.
- `/assets/` (sin extensión) sigue devolviendo `index.html`.

**Primer ingreso en QA:**
- Admin y Gestor se crean al primer ingreso, con oid, UPN y correo (búsqueda oid → UPN → correo de la 7b-2); la semilla SISTEMA nunca coincide.
- El Gestor sin departamentos ve a todos los empleados y crea proyectos sin departamento; asignarle departamentos es opcional (`FASE_5_Usuarios_Departamentos.md` §5).
- No hace falta ningún dato previo.

**Ejecución y verificación (06/11/2026, la hizo el usuario):** el detalle está en `docs/tareas/TAREA-7d-3-reporte.md` §7.
- **Actualización** (la primera real): commit `1570c05`; P1 y C1–C14 OK; con 7 migraciones se omitieron los pasos de la base.
  - Respaldo en `E:\SIG_Profesiograma\20261106_antes_1570c05`.
  - `robocopy /MIR` purgó 68 assets del build anterior.
  - `version.txt` = `1570c05`.
- **Certificado:** el `.pem` está en el PC del usuario, con la huella igual a la del servidor. **Schannel acepta `--cacert` (PEM) con `--ssl-no-revoke`.**
- **Humo:** H1–H8 y H10–H13 OK; H9 INFO.
- **Inicio de sesión:**
  - L1 (Admin) y L2 (Gestor) OK; L4 OK; L3 no probado (cubierto en DEV);
  - L6: 2 filas con oid y `Upn`, una con el correo distinto del UPN.
- **Funcionales:** F1–F4 OK (`PRY-20261106-501802`, cronograma, Excel, novedad). Los datos quedan en QA hasta restaurar el punto cero v2.
- **L5:** F5 mantiene la sesión, pero una pestaña nueva muestra el botón.
  - Entra devuelve `#code=…` al iframe y la ventana principal no lo recoge.
  - [SUPUESTO] La SPA completa arranca en el iframe y consume el fragmento.
  - Se evalúa en la **TAREA-7d-4**: un `redirectUri` en blanco (`/redirect.html`) solo para las llamadas silenciosas, registrado en el SPA-QA y el SPA-DEV.
  - `sessionStorage` se mantiene.

## 11. `ssoSilent` con el puente de redirección de MSAL 5 (TAREA-7d-4, 06/11/2026)

**Síntoma (L5 de la 7d-3):** en una pestaña nueva se ve el botón. Entra devuelve `#code=…` al iframe oculto, pero la ventana principal no lo recoge.

**Causa** (MSAL `@azure/msal-browser` 5.26.0, `frontend/node_modules/@azure/msal-browser/src/`):
- La ventana principal crea un iframe oculto con `sandbox` (`interaction_handler/SilentHandler.ts:95-110`) y **espera la respuesta por `BroadcastChannel`**, sin leer la URL del iframe: `interaction_client/SilentIframeClient.ts:701-715` → `utils/BrowserUtils.ts:269-378` (`waitForBridgeResponse`). Tiempo de espera: 10 s (`config/Configuration.ts:33`).
- Solo `broadcastResponseToMainFrame()` del subpaquete `@azure/msal-browser/redirect-bridge` emite esa respuesta (`redirect_bridge/index.ts:32-156`).
- El iframe volvía a la SPA completa (`redirectUri` = el origen):
  - su `handleRedirectPromise` descarta la respuesta silenciosa porque no es de tipo Redirect, **sin limpiar el fragmento** (`interaction_client/RedirectClient.ts:646-666`);
  - su propio `ssoSilent` lo bloquea `blockReloadInHiddenIframes` (`utils/BrowserUtils.ts:404-415`);
  - **nadie emitía la respuesta**.
- El [SUPUESTO] de la 7d-3 («la SPA consume el `#code`») quedó refutado en parte.

**Solución:**

| # | Decisión |
|---|---|
| G1 | `frontend/redirect.html` + `src/redirect.ts` como segunda entrada de Vite (`build.rolldownOptions.input`). `redirect.ts` importa **solo** `@azure/msal-browser/redirect-bridge` (J1). Build: `redirect.html` 0,63 kB; su chunk 1,2 kB, más el chunk compartido de MSAL (`Configuration-*.js`, 63 kB) |
| G2 | `redirectUri` silencioso por llamada, en `ssoSilent` (`SesionEntra.tsx`) y en `acquireTokenSilent` (`entra.ts` → `encabezadosEntra`, vía `msal.ts`). `loginRedirect` y `logoutRedirect` siguen con el origen |
| G3 | `uriRedireccionSilenciosa(origen)` = `new URL(origen).origin + '/redirect.html'`, con Vitest |
| G4 | Tiempo de espera por defecto (10 s) |
| G5 | Humo H14 (`/redirect.html`: 200, `text/html`, `no-cache`, `SAMEORIGIN`, sin `id="root"`) y C15 en `publicar-qa` |

**Servido por la API, sin cambios de código:**
- es un archivo con extensión, así que lo sirve `UseSpaEstaticos` (antes de `UseRouting`) y el fallback no lo tapa;
- lleva `no-cache` (no está en `/assets`), `SAMEORIGIN` y `frame-ancestors 'self'`;
- tiene una prueba en `SpaSaludTests`.

**Registro en Entra (J4):** las dos URIs de §7.4, exactas y sin barra final. Si falta una, Entra da AADSTS50011 dentro del iframe y se ve el botón, sin daño.

**Verificación:** N1 (DEV, pestaña nueva) y N2 (QA, después de la tercera publicación) en `deploy/qa/LEEME.md` §12.

**Verificación (06/11/2026, la hizo el usuario):** el detalle está en `docs/tareas/TAREA-7d-4-reporte.md` §7.
- **Registro previo:** las URIs `/redirect.html` no estaban registradas en el primer intento de N1, y se veía el botón (el mismo síntoma que el error original). Una vez registradas, N1 funcionó: confirma el orden de J4.
- **DEV:** N1, N3 y N4 OK; `npm run dev` sirve `/redirect.html`. DEV quedó otra vez en DevAuth (§7.4).
- **QA, tercera publicación** (`98f253a`):
  - P1 y C1–C15 OK; sin pasos de base;
  - respaldo en `E:\SIG_Profesiograma\20261106_antes_98f253a` (con `1570c05`);
  - copia con código 3;
  - humo H1–H8 y H10–H14 OK, H9 INFO;
  - N2, N3 y N4 OK.
- **`ssoSilent` resuelto.** El procedimiento de actualización quedó probado dos veces.
