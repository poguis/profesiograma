# TAREA-7d-1 — Publicación de prueba en SDMAPPTEST2 (QA, http 192.168.1.77:7082, sin inicio de sesión)

Todo lo ejecuta **el usuario**: los scripts en su PC, RDP en SDMAPPTEST2 y SSMS contra `NIQUEL\SSDEV`. Claude Code solo
escribió estos archivos. Diseño y decisiones (D1–D8, J1–J7): `docs/fases/FASE_7_Entornos_Despliegue.md` §9.

**Antes de empezar:** el commit que se publica debe contener los cambios de la TAREA-7d-1 (`appsettings.QA.json`,
`frontend/.env.qa`, etc.). `publicar-qa` usa `git archive HEAD`: lo que no está en el commit no se publica.
Los archivos nuevos de esta carpeta están bajo `backend/tests/manual/`, que el `.gitignore` raíz ignora: para versionarlos,
`git add -f backend/tests/manual/tarea7d1`.

| Archivo | Qué hace |
|---|---|
| `publicar-qa.cmd` (+ `.ps1`) | Paso 1: genera la publicación desde HEAD, fuera del repo, con 13 líneas CONTROL |
| `humo-qa.cmd` (+ `.ps1`) | Paso 8: H1–H9, solo GET anónimos con `curl.exe`. `set SIMULAR=1` para ensayarlo sin red |
| `comun7d1.ps1` | Candado, archivo de estado y salida (`resultado-*.txt`) |
| `simulacion\` | Respuestas FICTICIAS de H1–H9 |

## 1. Generar la publicación (PC del usuario)
```bat
backend\tests\manual\tarea7d1\publicar-qa.cmd
```
Requisitos: `git status` limpio, Node/npm y el SDK de .NET (los de DEV) y los user-secrets de DEV (`dotnet ef` los usa
para crear el contexto; **no** se conecta a la base).

**Esperado:**
- todas las líneas `CONTROL P1, C1–C12 … OK` y «Casos con FALLA: 0»;
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
backend\tests\manual\tarea7d1\humo-qa.cmd
```
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
| H9 | Cabeceras `Server` y `X-Powered-By`; `GET /assets/` | Solo INFORMA (el endurecimiento va en la 7d-2). En la 7d-1: `Server` ausente; `X-Powered-By: Sedemi` (del servidor, de TI); `/assets/` → 200 (fallback a index.html, no es un listado) |

| # | Verificación visual | Esperado |
|---|---|---|
| V1 | Abrir `http://192.168.1.77:7082` en el navegador | «Esta aplicación requiere HTTPS para iniciar sesión.», nunca una página en blanco |
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
