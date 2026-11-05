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

## 5. Grupo de aplicaciones (RDP en SDMAPPTEST2, **CMD como administrador**, nunca PowerShell)
```bat
cd /d %windir%\system32\inetsrv
appcmd add apppool /name:SIG_Profesiograma /managedRuntimeVersion:"" /managedPipelineMode:Integrated
appcmd set apppool SIG_Profesiograma /processModel.identityType:ApplicationPoolIdentity /processModel.loadUserProfile:true
```
Variable de la cadena de conexión (D3). Escríbala usted en el servidor; **nunca** en un chat, ticket ni archivo:
```bat
appcmd set config -section:system.applicationHost/applicationPools /+"[name='SIG_Profesiograma'].environmentVariables.[name='ConnectionStrings__Profesiograma',value='<CADENA DE QA>']" /commit:apphost
```
- `<CADENA DE QA>`: la misma forma que la de DEV, con `Database=PROFESIOGRAMA_QA` y el login `profesiograma_test`. La API
  antepone `tcp:` sola (`BaseDatos:ForzarTcp`).
- Si la contraseña tiene `'` o `"`, use el Editor de configuración de IIS (sección `system.applicationHost/applicationPools`
  → el grupo → `environmentVariables`) en lugar de appcmd.
- Cierre la ventana de CMD al terminar (el historial queda solo en memoria).

**Esperado:** `appcmd list apppool SIG_Profesiograma` → `(MgdVersion:,MgdMode:Integrated,state:Started)`.
Para comprobar el nombre de la variable sin mostrar su valor: Editor de configuración → `environmentVariables` (Count = 1).

## 6. Sitio, permisos, enlace y firewall (CMD como administrador)
```bat
mkdir C:\inetpub\wwwroot\SIG_Profesiograma
icacls C:\inetpub\wwwroot\SIG_Profesiograma /grant "IIS AppPool\SIG_Profesiograma:(OI)(CI)RX"
cd /d %windir%\system32\inetsrv
appcmd add site /name:SIG_Profesiograma /physicalPath:C:\inetpub\wwwroot\SIG_Profesiograma /bindings:http/192.168.1.77:7082:
appcmd set app "SIG_Profesiograma/" /applicationPool:SIG_Profesiograma
netsh advfirewall firewall show rule name=all | findstr 7082
```
- Si la última línea no muestra nada **y** el paso 8 no llega al puerto:
  `netsh advfirewall firewall add rule name="SIG_Profesiograma 7082" dir=in action=allow protocol=TCP localport=7082`.
- **No toque** el sitio «Profesiograma_FrontEnd» (otro sistema).

**Esperado:** `appcmd list site SIG_Profesiograma` → `bindings:http/192.168.1.77:7082:,state:Started`.

## 7. Copia de archivos
1. Copie por RDP la carpeta del paso 1 al servidor, por ejemplo a `C:\DEPLOY\SIG_Profesiograma\<carpeta>\` [SUPUESTO: ruta
   libre; elija otra si TI tiene una convención].
2. Si ya hay una versión publicada:
   - respáldela: `robocopy C:\inetpub\wwwroot\SIG_Profesiograma E:\SIG_Profesiograma\<yyyyMMdd_HHmm> /E`;
   - cree `C:\inetpub\wwwroot\SIG_Profesiograma\app_offline.htm` con el texto «PROFESIOGRAMA en mantenimiento.»;
     IIS detiene la aplicación y libera las DLL.
3. Copie:
   `robocopy C:\DEPLOY\SIG_Profesiograma\<carpeta>\app C:\inetpub\wwwroot\SIG_Profesiograma /MIR /XF app_offline.htm /XD logs`.
4. Borre `app_offline.htm`.

**Esperado:** en el sitio están `App.Api.dll`, `web.config`, `appsettings.json`, `appsettings.QA.json` y `wwwroot\index.html`,
y **no** está `appsettings.Development.json`.

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
| H9 | Cabeceras `Server` y `X-Powered-By`; `GET /assets/` | Solo INFORMA (el endurecimiento va en la 7d-2) |

| # | Verificación visual | Esperado |
|---|---|---|
| V1 | Abrir `http://192.168.1.77:7082` en el navegador | «Esta aplicación requiere HTTPS para iniciar sesión.», nunca una página en blanco |
| V2 | F12 → Consola, recargar | Sin errores sin explicar (anótelos si los hay) |

Salida: `resultado-humo-qa.txt`. H5 = 503: la API no llega a la base (firewall hacia `NIQUEL:1433`, cadena o login; pendiente 61).

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
