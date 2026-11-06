# Despliegue de QA en SDMAPPTEST2 — primera publicación y actualizaciones

Todo lo ejecuta **el usuario**: los scripts en su PC, RDP en SDMAPPTEST2 y SSMS contra `NIQUEL\SSDEV`. Claude Code solo
escribió estos archivos. Diseño y decisiones: `docs/fases/FASE_7_Entornos_Despliegue.md` §9 (TAREA-7d-1) y §10 (TAREA-7d-3).

- **URL pública:** `https://sdmapptest2.sedemi.local:7083`, con inicio de sesión de Entra ID (SPA-QA) y un certificado
  autofirmado que vence el **06/11/2027**.
- `http://192.168.1.77:7082` sigue activo: el navegador redirige a la URL pública (E1).
- **Primera publicación** (pasos 1–9, hecha el 06/11/2026): solo para un servidor nuevo o para rehacer el sitio.
- **Actualizaciones** (la de la 7d-3 y las siguientes): **§10**.

**Antes de empezar:**
- `publicar-qa` usa `git archive HEAD`: lo que no está en el commit no se publica.
- Esta carpeta (`deploy/qa/`) se versiona normalmente; su `.gitignore` excluye `resultado-*` y `*.lock`.

| Archivo | Qué hace |
|---|---|
| `publicar-qa.cmd` (+ `.ps1`) | Genera la publicación desde HEAD, fuera del repo: CONTROL P1, C1–C15 e INFO de migraciones |
| `humo-qa.cmd` (+ `.ps1`) | H1–H14, solo GET anónimos con `curl.exe`. `set SIMULAR=1` para ensayarlo sin red |
| `comun7d1.ps1` | Candado, archivo de estado y salida (`resultado-*.txt`) |
| `simulacion\` | Respuestas FICTICIAS de H1–H14 |

**Advertencias:**
- **Nunca pegue tokens** en un chat ni en un ticket; para inspeccionarlos use `jwt.ms` en su propio navegador.
- Restaure el **punto cero v2** antes de los ensayos de la Fase 3: las pruebas L y F escriben en QA.
- Use **Edge o Chrome**.
- El certificado de QA **vence el 06/11/2027**: renovarlo antes, o pedir a TI uno de confianza (pendiente 75).

## 1. Generar la publicación (PC del usuario)
```bat
deploy\qa\publicar-qa.cmd
```
Requisitos: `git status` limpio, Node/npm y el SDK de .NET (los de DEV) y los user-secrets de DEV (`dotnet ef` los usa
para crear el contexto; **no** se conecta a la base).

**Esperado:**
- todas las líneas `CONTROL P1, C1–C15 … OK` y «Casos con FALLA: 0»; `CONTROL INFO` con la cantidad de migraciones y la última;
- carpeta `D:\PROFESIOGRAMA_DEPLOY\QA\<yyyyMMdd_HHmm>_<hash7>\` con `app\`, `migraciones-QA.sql` y `version.txt`;
- el estado (etapa, commit y destino) queda en `resultado-estado-publicar-qa.txt`.

Si alguna CONTROL da FALLA, no siga: la publicación no es válida.

## 2. Respaldo de QA (SSMS como `sa`, antes de la migración)
```sql
BACKUP DATABASE PROFESIOGRAMA_QA TO DISK = N'D:\BACKUP\PROFESIOGRAMA_QA_<yyyyMMdd>_antes_7d1.bak' WITH COPY_ONLY, CHECKSUM, INIT;
RESTORE VERIFYONLY FROM DISK = N'D:\BACKUP\PROFESIOGRAMA_QA_<yyyyMMdd>_antes_7d1.bak' WITH CHECKSUM;
```
**Esperado:** «BACKUP DATABASE successfully processed…» y «The backup set on file 1 is valid.»

## 3. Migración (SSMS como `sa`)
1. Abra `migraciones-QA.sql` de la carpeta del paso 1 y revíselo: empieza con `USE PROFESIOGRAMA_QA;` y `GO`, sin `DROP TABLE`.
2. Ejecútelo **completo**: F5 **sin** selección.
3. Compruebe:
```sql
SELECT @@TRANCOUNT AS Transacciones;                                            -- 0
SELECT COUNT(*) AS Migraciones FROM PROFESIOGRAMA_QA.dbo.__EFMigrationsHistory; -- 7
SELECT TOP 1 MigrationId FROM PROFESIOGRAMA_QA.dbo.__EFMigrationsHistory ORDER BY MigrationId DESC; -- 20261105140125_AgregarUpnUsuario
SELECT COL_LENGTH('PROFESIOGRAMA_QA.dbo.Usuario', 'Upn') AS LargoUpn;           -- 512 (nvarchar(256))
```
Las 6 migraciones anteriores se saltan (script idempotente); solo se aplica `AgregarUpnUsuario`.

## 4. Punto cero v2 (después de la migración, antes del humo)
El punto cero anterior (`PROFESIOGRAMA_QA_20261030_punto_cero.bak`) no tiene `AgregarUpnUsuario`.
```sql
BACKUP DATABASE PROFESIOGRAMA_QA TO DISK = N'D:\BACKUP\PROFESIOGRAMA_QA_<yyyyMMdd>_punto_cero_v2.bak' WITH COPY_ONLY, CHECKSUM, INIT;
RESTORE VERIFYONLY FROM DISK = N'D:\BACKUP\PROFESIOGRAMA_QA_<yyyyMMdd>_punto_cero_v2.bak' WITH CHECKSUM;
```
**Esperado:** igual que en el paso 2.

## 5. Grupo de aplicaciones y cadena de conexión (RDP en SDMAPPTEST2, **CMD como administrador**, nunca PowerShell)
**Copie y pegue** los comandos de este documento; no los escriba a mano (el incidente (a) de la 7d-1 fue una ruta mal
escrita). Compruebe que la consola es la del **servidor**: el encabezado de CMD dice `Microsoft Windows [Versión 10.0.17763…]`.

**5.0 Respaldo de la configuración de IIS** (antes de crear nada):
```bat
cd /d %windir%\system32\inetsrv
appcmd add backup "antes_SIG_Profesiograma_<yyyyMMdd>"
```
Se restaura con `appcmd restore backup "antes_SIG_Profesiograma_<yyyyMMdd>"`. Ojo: revierte **toda** la configuración de IIS,
también la de los otros sitios.

**5.1 Probar el login antes** (SSMS en su PC; Autenticación de SQL Server; servidor `NIQUEL,1433`, por puerto fijo y sin SQL
Browser; login `profesiograma_test`):
```sql
SELECT @@SERVERNAME AS Servidor, DB_NAME() AS Base;   -- NIQUEL\SSDEV, PROFESIOGRAMA_QA
```

**5.2 Grupo de aplicaciones:**
```bat
appcmd add apppool /name:SIG_Profesiograma /managedRuntimeVersion:"" /managedPipelineMode:Integrated
appcmd set apppool SIG_Profesiograma /processModel.identityType:ApplicationPoolIdentity /processModel.loadUserProfile:true
```

**5.3 Variable `ConnectionStrings__Profesiograma` (D3), con el Editor de configuración** (método principal):
1. Administrador de IIS → el **nodo del servidor** (no el sitio) → **Editor de configuración**.
2. Sección: `system.applicationHost/applicationPools`.
3. `(Colección)` → `…` → seleccione el grupo `SIG_Profesiograma`.
4. En sus propiedades, `environmentVariables` → `…` → **Agregar**:
   - `name` = `ConnectionStrings__Profesiograma`;
   - `value` = la cadena.
5. Cierre las ventanas de la colección y pulse **Aplicar**, en el panel derecho. Sin «Aplicar» no se guarda.

Plantilla de la cadena (escriba la contraseña solo en el servidor; **nunca** en un chat, ticket ni archivo):
```text
Server=NIQUEL,1433;Database=PROFESIOGRAMA_QA;User ID=profesiograma_test;Password=<CONTRASEÑA>;Encrypt=True;TrustServerCertificate=True;Application Name=SIG_Profesiograma_QA
```
- Sobre la plantilla:
  - la API antepone `tcp:` sola (`BaseDatos:ForzarTcp`);
  - `Application Name` permite ver la sesión en SQL (paso 8).
- **La contraseña no debe contener** `;` `'` `"` `{` `}` `=`. Rompen la cadena o la línea de comandos.
- **Alternativa con appcmd** (solo si el Editor no está disponible): la contraseña queda en el historial de la consola mientras
  esté abierta, y en la auditoría de la línea de comandos de Windows si TI la activó. Cierre la ventana al terminar.
  ```bat
  appcmd set config -section:system.applicationHost/applicationPools /+"[name='SIG_Profesiograma'].environmentVariables.[name='ConnectionStrings__Profesiograma',value='<CADENA DE QA>']" /commit:apphost
  ```

**5.4 Verificar sin mostrar el valor y reciclar:**
```bat
find /c "ConnectionStrings__Profesiograma" %windir%\system32\inetsrv\config\applicationHost.config
appcmd recycle apppool SIG_Profesiograma
appcmd list apppool SIG_Profesiograma
```
**Esperado:**
- `find /c` termina en `: 1`;
- el grupo queda `(MgdVersion:,MgdMode:Integrated,state:Started)`.

## 6. Sitio, permisos, enlace y firewall (CMD como administrador, en el servidor)
Copie y pegue:
```bat
mkdir C:\inetpub\wwwroot\SIG_Profesiograma
icacls C:\inetpub\wwwroot\SIG_Profesiograma /grant "IIS AppPool\SIG_Profesiograma:(OI)(CI)RX"
cd /d %windir%\system32\inetsrv
appcmd add site /name:SIG_Profesiograma /physicalPath:C:\inetpub\wwwroot\SIG_Profesiograma /bindings:http/192.168.1.77:7082:
appcmd set app "SIG_Profesiograma/" /applicationPool:SIG_Profesiograma
appcmd list vdir "SIG_Profesiograma/"
```
**Esperado:**
- `appcmd list vdir` muestra `physicalPath:C:\inetpub\wwwroot\SIG_Profesiograma`, **con** `wwwroot`. Si no, corrija con
  `appcmd set vdir "SIG_Profesiograma/" /physicalPath:C:\inetpub\wwwroot\SIG_Profesiograma`.
- `appcmd list site SIG_Profesiograma` → `bindings:http/192.168.1.77:7082:,state:Started`.

**Firewall** (en la 7d-1 no hizo falta). Primero, desde **su PC**:
```bat
curl.exe -s -o NUL -w "%{http_code}" http://192.168.1.77:7082/
```
- `403`, `404` u otro código → el puerto responde y **no** hace falta una regla.
- `000` → no llega: falta la regla.

La regla se crea **solo en el SERVIDOR** (compruebe `10.0.17763` en el encabezado de la consola), nunca en su PC:
`netsh advfirewall firewall add rule name="SIG_Profesiograma 7082" dir=in action=allow protocol=TCP localport=7082`.

**No toque** el sitio «Profesiograma_FrontEnd» (otro sistema).

## 7. Copia de archivos
1. **Transferencia:**
   - comprima la carpeta del paso 1 en un ZIP en su PC;
   - cópielo con el portapapeles del RDP (Ctrl+C en su PC, Ctrl+V en el servidor) a `C:\DEPLOY\SIG_Profesiograma\`;
   - en el servidor: clic derecho → **Extraer todo**.

   **Alternativa:** redirija la unidad D: en la conexión RDP («Recursos locales» → «Más…» → Unidades) y copie desde
   `\tsclient\D\PROFESIOGRAMA_DEPLOY\QA\<carpeta>`. **Desmarque** la redirección al terminar.
2. Si ya hay una versión publicada:
   - respáldela: `robocopy C:\inetpub\wwwroot\SIG_Profesiograma E:\SIG_Profesiograma\<yyyyMMdd_HHmm> /E`;
   - cree `C:\inetpub\wwwroot\SIG_Profesiograma\app_offline.htm` con el texto «PROFESIOGRAMA en mantenimiento.». IIS detiene
     la aplicación y libera las DLL.
3. Copie la aplicación:
   `robocopy C:\DEPLOY\SIG_Profesiograma\<carpeta>\app C:\inetpub\wwwroot\SIG_Profesiograma /MIR /XF app_offline.htm version.txt /XD logs`.
4. Copie `version.txt` a la raíz del sitio, para saber qué commit está publicado. No se sirve: solo se publica `wwwroot\`.
   `copy /y C:\DEPLOY\SIG_Profesiograma\<carpeta>\version.txt C:\inetpub\wwwroot\SIG_Profesiograma\`
5. Borre `app_offline.htm`.
6. **Retención:** conserve los últimos 3 paquetes en `C:\DEPLOY\SIG_Profesiograma\` y las copias de reversión en `E:\`.

**Esperado:**
- en el sitio están `App.Api.dll`, `web.config`, `appsettings.json`, `appsettings.QA.json`, `version.txt` y `wwwroot\index.html`;
- **no** está `appsettings.Development.json`.

## 8. Prueba de humo (PC del usuario) y verificaciones visuales
```bat
deploy\qa\humo-qa.cmd
```
Desde la TAREA-7d-3, `humo-qa` va por **https** y necesita el certificado (**§11**).

| Caso | Qué hace | Esperado |
|---|---|---|
| H1 | `GET /` | 200, `text/html`, `Cache-Control: no-cache`, index.html |
| H2 | `GET /proyectos/1/cronograma` | 200, `text/html` (index.html) |
| H3 | `GET` del primer `/assets/*.js` de index.html | 200, JavaScript, `public, max-age=31536000, immutable` |
| H4 | `GET /api/salud/vivo` | 200 `{"estado":"vivo"}` |
| H5 | `GET /api/salud/listo` | 200 `{"estado":"listo"}` (la base de QA responde con `profesiograma_test`) |
| H6 | `GET /api/usuarios/me` sin token | 401 |
| H7 | `GET /api/no-existe` | 404 sin HTML |
| H8 | `GET /api/usuarios/me` con `X-Dev-User: admin` | 401 (DevAuth no existe en QA) |
| H9 | Cabeceras `Server` y `X-Powered-By` | Solo INFORMA (`X-Powered-By: Sedemi` es del servidor; se consulta a TI) |
| H10 | Encabezados de seguridad en `/` y en `/api/salud/vivo` | `nosniff`, `strict-origin-when-cross-origin`, `SAMEORIGIN`, `frame-ancestors 'self'` |
| H11 | `Strict-Transport-Security` | Ausente (HSTS apagado en QA) |
| H12a / H12b | `GET /assets/` y `GET /assets/no-existe.js` | 200 index.html (no es un listado) y 404 |
| H13 | `GET http://192.168.1.77:7082/` | 200 index.html (la redirección a la URL pública la hace el navegador) |
| H14 | `GET /redirect.html` (puente de MSAL, TAREA-7d-4) | 200, `text/html`, `no-cache`, `SAMEORIGIN`, sin `id="root"` |

| # | Verificación visual | Esperado |
|---|---|---|
| V1 | Abrir `http://192.168.1.77:7082` en el navegador | Desde la 7d-3: redirige a `https://sdmapptest2.sedemi.local:7083` (en la 7d-1 mostraba «requiere HTTPS») |
| V2 | F12 → Consola, recargar | Sin errores sin explicar (anótelos si los hay) |

Salida: `resultado-humo-qa.txt`. H5 = 503: la API no llega a la base (firewall hacia `NIQUEL:1433`, cadena o login; pendiente 61).

**Opcional, sesión en SQL** (SSMS como `sa`, mientras navega o tras H5):
```sql
SELECT s.login_name, s.host_name, s.program_name, c.encrypt_option
FROM sys.dm_exec_sessions s JOIN sys.dm_exec_connections c ON c.session_id = s.session_id
WHERE s.program_name = N'SIG_Profesiograma_QA';
```
**Esperado (7d-1):** `profesiograma_test`, `SDMAPPTEST2`, `SIG_Profesiograma_QA` y `TRUE`.

## 9. Diagnóstico y reversión
| Código | Causa probable | Qué revisar |
|---|---|---|
| 500.19 | `web.config` ilegible o el módulo ASP.NET Core no instalado | Permiso RX del grupo en la carpeta; Hosting Bundle |
| 500.30 | La aplicación falla al iniciar (p. ej. falta `ConnectionStrings__Profesiograma`, o AzureAd sin GUID) | Visor de eventos → Registros de Windows → Aplicación (orígenes «IIS AspNetCore Module V2» y «.NET Runtime») |
| 500.31 | No encuentra el runtime de .NET 10 | Hosting Bundle 10.0.x del servidor (hoy 10.0.10; pendiente 75) |
| 503 en todo el sitio | El grupo de aplicaciones está detenido (protección contra errores rápidos) | `appcmd list apppool SIG_Profesiograma`; el Visor de eventos; iniciar el grupo |

**stdout temporal** (solo para diagnosticar):
1. Cree `C:\inetpub\wwwroot\SIG_Profesiograma\logs` y dé permiso de modificación al grupo:
   `icacls ...\logs /grant "IIS AppPool\SIG_Profesiograma:(OI)(CI)M"`.
2. En `web.config`, `stdoutLogEnabled="true"`.
3. Recicle el grupo y lea `logs\stdout_*.log`.
4. **Vuelva a `false`** al terminar y borre los logs: pueden contener datos.

**Revertir la aplicación:**
1. Cree `app_offline.htm`.
2. `robocopy E:\SIG_Profesiograma\<respaldo> C:\inetpub\wwwroot\SIG_Profesiograma /MIR /XF app_offline.htm`.
3. Borre `app_offline.htm`.

En la primera publicación no hay versión anterior: detenga el sitio (`appcmd stop site SIG_Profesiograma`).

**Revertir la base** (solo si hace falta; la columna `Upn` es NULL y no afecta a versiones anteriores):
`SINGLE_USER` → `RESTORE DATABASE PROFESIOGRAMA_QA FROM DISK = N'D:\BACKUP\PROFESIOGRAMA_QA_<yyyyMMdd>_antes_7d1.bak' WITH REPLACE` → `MULTI_USER`.

## 10. Actualización (segunda publicación y siguientes)
Sin cambios en IIS (el sitio, los enlaces 7082 y 7083, el grupo y la variable ya existen).

**Antes de publicar (y antes de probar en DEV): URIs de redirección en Entra** (TAREA-7d-4, J4). Desde la 7d-4, las
llamadas silenciosas (`ssoSilent` y la renovación de tokens) vuelven a `/redirect.html`, la página del puente de MSAL.
Cada SPA debe tener registradas, en la plataforma «Aplicación de página única», **las dos** URIs (exactas, sin barra final):

| Registro | URI de inicio y cierre de sesión (ya existía) | URI silenciosa (TAREA-7d-4) |
|---|---|---|
| SIG-Profesiograma-SPA-QA | `https://sdmapptest2.sedemi.local:7083` | `https://sdmapptest2.sedemi.local:7083/redirect.html` |
| SIG-Profesiograma-SPA-DEV | `http://localhost:5173` | `http://localhost:5173/redirect.html` |

- El usuario las registró el **06/11/2026**. En una publicación futura (otro SPA, otra URL o PRO), regístrelas antes.
- Si falta la URI silenciosa, Entra responde **AADSTS50011** dentro del iframe oculto (no se ve) y la aplicación muestra el
  botón «Iniciar sesión…», como antes de la 7d-4. Sin daño: el botón sigue funcionando.

| # | Paso | Esperado / qué anotar |
|---|---|---|
| 1 | Commit de los cambios, y `deploy\qa\publicar-qa.cmd` | CONTROL P1 y C1–C15 OK. Anote la carpeta y la línea `CONTROL INFO migraciones` (cantidad y última) |
| 2 | SSMS como `sa`: `SELECT COUNT(*) FROM PROFESIOGRAMA_QA.dbo.__EFMigrationsHistory;` | Si da **7** y la INFO del paso 1 también dice 7: **salte los pasos 2–4 de la base**. Si el commit tiene más: respaldo (paso 2), `migraciones-QA.sql` (paso 3) y, si hace falta, un punto cero nuevo |
| 3 | Copie el ZIP al servidor (paso 7, punto 1) y cree `C:\inetpub\wwwroot\SIG_Profesiograma\app_offline.htm` («PROFESIOGRAMA en mantenimiento.») | El sitio responde la página de mantenimiento |
| 4 | Respaldo del sitio: `robocopy C:\inetpub\wwwroot\SIG_Profesiograma E:\SIG_Profesiograma\<yyyyMMdd_HHmm> /E` | La carpeta de `E:\` tiene `App.Api.dll` y `version.txt` de la versión anterior |
| 5 | `robocopy C:\DEPLOY\SIG_Profesiograma\<carpeta>\app C:\inetpub\wwwroot\SIG_Profesiograma /MIR /XF app_offline.htm version.txt /XD logs` | Sin errores (código de salida de robocopy ≤ 7) |
| 6 | `copy /y C:\DEPLOY\SIG_Profesiograma\<carpeta>\version.txt C:\inetpub\wwwroot\SIG_Profesiograma\` | `version.txt` con el commit nuevo |
| 7 | Borre `app_offline.htm` | La aplicación vuelve a arrancar |
| 8 | `deploy\qa\humo-qa.cmd` (con `QA_CA`, §11) | H1–H8 y H10–H14 OK; H9 INFO; «Casos con FALLA: 0» |
| 9 | L1–L6 y N1–N4 (§12) | Según las tablas |
| 10 | F1–F4 (§13) | Según la tabla |

**Reversión:**
1. Cree `app_offline.htm`.
2. `robocopy E:\SIG_Profesiograma\<yyyyMMdd_HHmm> C:\inetpub\wwwroot\SIG_Profesiograma /MIR /XF app_offline.htm /XD logs`.
3. Borre `app_offline.htm`.
4. Repita `humo-qa`.

La base no se toca si no hubo migraciones.

## 11. Certificado de QA para `humo-qa` (K3, una sola vez por certificado)
El `.cer` es público, pero **no se versiona**: vive en `D:\PROFESIOGRAMA_DEPLOY\QA\certificados\`, fuera del repo.
1. Cópielo del servidor por el portapapeles del RDP:
   `C:\DEPLOY\SIG_Profesiograma\sig_profesiograma_qa_7083.cer` → `D:\PROFESIOGRAMA_DEPLOY\QA\certificados\`.
2. Conviértalo a PEM (en su PC, CMD):
   ```bat
   cd /d D:\PROFESIOGRAMA_DEPLOY\QA\certificados
   certutil -encode sig_profesiograma_qa_7083.cer qa_7083.pem
   ```
3. `humo-qa` usa `D:\PROFESIOGRAMA_DEPLOY\QA\certificados\qa_7083.pem` por defecto. Otra ruta: `set QA_CA=<ruta del .pem>`.
   - Va con `--cacert` y `--ssl-no-revoke`: el autofirmado no tiene lista de revocación.
   - [PENDIENTE DE CONFIRMAR que el `curl.exe` de Windows (Schannel) acepte el PEM.] Si no lo acepta, use
     `set QA_INSEGURO=1` (`-k`, sin verificar el certificado; el script muestra un aviso) y anótelo.
4. **Huella esperada:** `DD51425E584DF82B312B747DB5BC29FF65F3C6D5`. Compruébela con `certutil -dump qa_7083.pem` (línea
   «Cert Hash(sha1)»).

En el navegador **no** hace falta instalar el `.cer`: basta con aceptar la advertencia la primera vez («Avanzado» →
«Continuar a sdmapptest2.sedemi.local»).

## 12. Inicio de sesión (L1–L6)
Use `https://sdmapptest2.sedemi.local:7083`; para la segunda cuenta, una ventana InPrivate.

| # | Caso | Esperado | Si falla, anote |
|---|---|---|---|
| L1 | Admin: «Iniciar sesión con la cuenta de SEDEMI» | La barra muestra su nombre e Inicio dice Admin; existe `/admin/usuarios` | La hora y el código AADSTS de la pantalla de Microsoft |
| L2 | Pasante Gestor (InPrivate) | Entra sin el menú de administración; el buscador muestra a todos los empleados (sin departamentos) | La hora y el mensaje |
| L3 | Una cuenta **sin** asignación | Pantalla de Microsoft (AADSTS501051) o «No tienes acceso a PROFESIOGRAMA.»; sin una fila nueva en `dbo.Usuario` | La pantalla |
| L4 | «Cerrar sesión» | Vuelve al botón «Iniciar sesión…» | — |
| L5 | Con la sesión abierta: F5 y una pestaña nueva | Entra sin pedir credenciales (un solo `ssoSilent`, sin bucles). Si aparece el botón, anótelo: el iframe de `ssoSilent` puede no aceptar el certificado autofirmado | Si se ve el botón o un bucle |
| L6 | SSMS (como `sa`), sin mostrar valores: ver abajo | 2 filas (Admin y Gestor) con `ConOid = 1`, `ConUpn = 1` y `Activo = 1`; `CorreoDistinto = 1` si el UPN difiere del correo | El resultado de la consulta |

```sql
SELECT Id,
       CASE WHEN EntraObjectId IS NULL THEN 0 ELSE 1 END AS ConOid,
       CASE WHEN Upn IS NULL THEN 0 ELSE 1 END AS ConUpn,
       CASE WHEN Email <> Upn THEN 1 ELSE 0 END AS CorreoDistinto,
       Activo
FROM PROFESIOGRAMA_QA.dbo.Usuario WHERE EsSistema = 0 ORDER BY Id;
```

**Pestaña nueva (`ssoSilent`, TAREA-7d-4).** Hasta la 7d-4 `ssoSilent` **nunca funcionó**, tampoco en DEV: M5 de la 7b no
cubría una pestaña nueva. N1 forma parte, desde ahora, de la verificación estándar del inicio de sesión.

| # | Caso | Esperado | Si falla, anote |
|---|---|---|---|
| N1 | DEV en modo Entra (`npm run dev`): iniciar sesión y abrir una **pestaña nueva** en `http://localhost:5173` | Entra sola en menos de 10 s, sin el botón | Si aparece el botón; en F12 → Consola, los errores |
| N2 | QA, después de la tercera publicación: lo mismo en `https://sdmapptest2.sedemi.local:7083` | Entra sola | Igual que N1 |
| N3 | El botón «Iniciar sesión…» y «Cerrar sesión», en DEV y en QA | Igual que antes | — |
| N4 | F12 → Consola en N1 y N2 | La advertencia del sandbox del iframe puede seguir; **sin** `timed_out`, `redirect_bridge_timeout` ni `block_iframe_reload` | El texto del error |
- **Departamentos del Gestor (opcional):** el Admin entra en `/admin/usuarios` → el Gestor → Departamentos → Guardar.
  Filtra el buscador y fija el departamento de sus proyectos nuevos.
- **Cambio de rol en Entra:** rige con el siguiente token (cerrar e iniciar sesión).

## 13. Pruebas funcionales (F1–F4; escriben en QA)
Todo lo que creen se borra al **restaurar el punto cero v2** antes de la Fase 3. Si algo falla, anote la **hora exacta**
(hh:mm:ss) para buscarla en el Visor de eventos del servidor (Aplicación, orígenes «.NET Runtime» e «IIS AspNetCore Module V2»).

| # | Caso (como Gestor) | Esperado | Qué prueba |
|---|---|---|---|
| F1 | Crear un proyecto de prueba (nombre «PRUEBA QA 7d-3») con **2 empleados reales** elegidos en el buscador | «Proyecto registrado», con el código `PRY-…` | El ERP real desde el servidor (puertos 7048 y 7055) y la escritura en QA |
| F2 | Abrir el cronograma del proyecto | Los 2 empleados con sus días | Las consultas del cronograma |
| F3 | Descargar el reporte Excel | Archivo `Reporte_{código}_{yyyyMMdd}_{yyyyMMdd}.xlsx` y el aviso «Reporte generado» **con la cantidad de filas** | ClosedXML en el servidor y `Content-Disposition` / `X-Reporte-Filas` (mismo origen) |
| F4 | Crear una novedad (p. ej. una VACACIÓN de un empleado del proyecto) y abrirla | «Novedad NOV-… registrada» y su detalle | Novedades en QA |

No anote nombres ni cédulas: solo los códigos `PRY-…` y `NOV-…`, la hora y el mensaje.
