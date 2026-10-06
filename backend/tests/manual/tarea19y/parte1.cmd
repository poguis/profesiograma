@echo off
setlocal
rem UTF-8 en la consola para que las tildes de las respuestas se guarden bien.
chcp 65001 > nul
rem ==========================================================================
rem TAREA-19y - Parte 1: principal opcional con PROYECTO_EXIGE_PRINCIPAL = 0 (valor de la migracion; si la migracion
rem no esta aplicada, el parametro falta y tambien vale 0). ESCRIBE: crea el proyecto E solo con backs y registra una
rem actualizacion de personal en E. Ejecutar UNA sola vez.
rem   Fechas relativas a hoy (H): E de H+20 a H+40; back 1 de H+20 a H+25, back 2 de H+30 a H+35 (2 dias de descanso).
rem   Grupo CAMPO con actividad DEV.01. Sin principales: los backs van "Sin relacion" (principalRelacionado null).
rem Empleados de los backs (codigo EKON):
rem   - parte1.cmd DEV007 DEV008      : back 1 = DEV007, back 2 = DEV008 (solo ese par);
rem   - empleados.txt (opcional)      : lineas EMP_K1=DEV007 y EMP_K2=DEV008 (solo ese par);
rem   - sin indicar                   : seleccion automatica. Primero DEV007/DEV008 y luego los demas pares de DEV001-DEV008.
rem   Cada par se prueba SOLO con la vista previa de la creacion (no guarda). Se usa el primero sin cruces que trae la
rem   advertencia "El proyecto no tendra principal...". Los Id se leen con GET /api/empleados (solo lectura).
rem   a) Seleccion del par: si ninguno sirve se detiene SIN escribir nada.
rem   b) Vista previa SIN nadie - esperado 400 personal "Se requiere al menos 1 persona (principal o back)." (no escribe).
rem   c) CREAR E solo con backs - esperado 201 (escribe).
rem   d) GET detalle de E (2 backs, ningun principal) y GET listado con el codigo de E (responsable null).
rem   e) GET edicion de E; vista previa quitando a todos - esperado 400 personal (no escribe); vista previa acortando el
rem      back 2 un dia - esperado 200 con versionProyecto; REGISTRAR ese cambio con el token - esperado 200 version 2.
rem   f) GET detalle de E: back 2 acortado, sin principales.
rem   g) GET reactivacion del Id 2; vista previa solo con un back que empieza en R - esperado 200 sin errores (no
rem      escribe); vista previa con el back empezando en R+1 - esperado 400 backs (no escribe).
rem Salidas: resultado-parte1.txt (proteccion: si existe, no se ejecuta; se crea justo antes de c) y valores-parte1.txt.
rem Modo simulacion: set SIMULAR=1 - no llama a curl; lee %SIMULACION%\NOMBRE.txt y usa BASE=https://localhost:1.
rem   Opcional: set HOY=yyyy-MM-dd para fijar H.
rem ==========================================================================
cd /d "%~dp0"
if not defined SIMULACION set "SIMULACION=simulacion"
if "%SIMULAR%"=="1" (set "BASE=https://localhost:1") else (set "BASE=https://localhost:7180")
if not defined ID_S set ID_S=2
set "DEF_K1=DEV007"
set "DEF_K2=DEV008"
set "CANDIDATOS=DEV001 DEV002 DEV003 DEV004 DEV005 DEV006 DEV007 DEV008"
set COPIAR=
set SALIDA=resultado-parte1.txt
set VALORES=valores-parte1.txt
set PREVIO=tmp\parte1-previo.txt

if exist "%SALIDA%" (
  echo Ya existe %SALIDA%: la parte 1 ya se ejecuto y el proyecto E ya existe.
  echo Si de verdad quiere repetirla, borre %SALIDA% y %VALORES% y vuelva a ejecutar.
  exit /b 1
)

rem --- Empleados indicados: linea de comandos o empleados.txt ----------------
set EMP_K1=
set EMP_K2=
if exist empleados.txt for /f "usebackq eol=# tokens=1,2 delims== " %%a in ("empleados.txt") do (
  if /i "%%a"=="EMP_K1" set "EMP_K1=%%b"
  if /i "%%a"=="EMP_K2" set "EMP_K2=%%b"
)
if not "%~1"=="" set "EMP_K1=%~1"
if not "%~2"=="" set "EMP_K2=%~2"
set MODO=automatico
if defined EMP_K1 set MODO=indicado
if defined EMP_K2 set MODO=indicado
if "%MODO%"=="indicado" if not defined EMP_K1 goto :faltan_empleados
if "%MODO%"=="indicado" if not defined EMP_K2 goto :faltan_empleados
if "%MODO%"=="indicado" if /i "%EMP_K1%"=="%EMP_K2%" (
  echo EMP_K1 y EMP_K2 deben ser empleados distintos.
  exit /b 1
)

if not "%SIMULAR%"=="1" call :verificar_api || exit /b 1
if not exist tmp mkdir tmp

rem --- Fechas relativas a hoy ----------------------------------------------
set HOY_CALC=
for /f "usebackq tokens=1,2 delims==" %%a in (`powershell -NoProfile -Command "$h=[datetime]::Today; if ($env:HOY) { $h=[datetime]::ParseExact($env:HOY,'yyyy-MM-dd',$null) }; $f='yyyy-MM-dd'; 'HOY_CALC=' + $h.ToString($f); 'INICIO=' + $h.AddDays(20).ToString($f); 'FIN=' + $h.AddDays(40).ToString($f); 'K1_INICIO=' + $h.AddDays(20).ToString($f); 'K1_FIN=' + $h.AddDays(25).ToString($f); 'K2_INICIO=' + $h.AddDays(30).ToString($f); 'K2_FIN=' + $h.AddDays(35).ToString($f)"`) do set %%a=%%b
if not defined HOY_CALC (
  echo No se pudieron calcular las fechas.
  exit /b 1
)

rem Hasta el registro, la salida va a %PREVIO%: si ningun par sirve no queda resultado-parte1.txt.
set "SALIDA=%PREVIO%"
> "%SALIDA%" echo TAREA-19y parte 1 - %date% %time% - SIMULAR=%SIMULAR% - empleados: %MODO%
>> "%SALIDA%" echo Fechas: H=%HOY_CALC% E=%INICIO%..%FIN% back1=%K1_INICIO%..%K1_FIN% back2=%K2_INICIO%..%K2_FIN%
echo Fechas: H=%HOY_CALC% E=%INICIO%..%FIN% - empleados: %MODO%

rem --- a) Seleccion del par (solo vistas previas, no escribe) ---------------
set ELEGIDO=
if "%MODO%"=="indicado" (
  call :probar %EMP_K1% %EMP_K2% primero
) else (
  call :probar %DEF_K1% %DEF_K2% primero
  for %%p in (%CANDIDATOS%) do for %%b in (%CANDIDATOS%) do call :probar %%p %%b
)
if not defined ELEGIDO (
  >> "%SALIDA%" echo CONTROL a - ningun par de empleados sin cruces y con la advertencia: no se creo nada.
  echo Ningun par de empleados sirve: NO se creo nada. Revise %PREVIO%.
  exit /b 1
)
>> "%SALIDA%" echo CONTROL a - par elegido: back 1 %EMP_K1% Id %ID_K1%, back 2 %EMP_K2% Id %ID_K2%
echo Par elegido: back 1 %EMP_K1%, back 2 %EMP_K2%.

rem --- b) Vista previa sin nadie (no escribe) -------------------------------
call :preparar e-vacio.json || exit /b 1
call :http b "Vista previa de la creacion SIN nadie - esperado 400 personal" POST "proyectos/previsualizar" gestor e-vacio.json || exit /b 1
call :control b 400 personal

rem --- desde aqui se escribe -------------------------------------------------
copy /y "%PREVIO%" "resultado-parte1.txt" > nul
set "SALIDA=resultado-parte1.txt"
>> "%SALIDA%" echo.
>> "%SALIDA%" echo === a: vista previa del par elegido ===
type "tmp\a_%EMP_K1%_%EMP_K2%.txt">> "%SALIDA%"
>> "%SALIDA%" echo.

rem --- c) Crear E ----------------------------------------------------------
call :preparar e-crear.json || exit /b 1
call :http c "CREAR el proyecto E solo con backs %EMP_K1% y %EMP_K2% - esperado 201" POST "proyectos" gestor e-crear.json || exit /b 1
set ID_E=
set CODIGO_E=
for /f "usebackq tokens=1,2 delims==" %%a in (`powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\c.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { exit }; $j=$t.Substring($i) | ConvertFrom-Json; if ($j.id) { 'ID_E=' + $j.id; 'CODIGO_E=' + $j.codigo }"`) do set %%a=%%b
if not defined ID_E (
  >> "%SALIDA%" echo CONTROL c - no se creo el proyecto E. Se detiene.
  echo No se creo el proyecto E. Revise %SALIDA%.
  exit /b 1
)
>> "%SALIDA%" echo CONTROL c - proyecto E creado: Id %ID_E%, codigo %CODIGO_E%
> "%VALORES%" echo ID_E=%ID_E%
>> "%VALORES%" echo CODIGO_E=%CODIGO_E%
>> "%VALORES%" echo EMP_K1=%EMP_K1%
>> "%VALORES%" echo EMP_K2=%EMP_K2%
>> "%VALORES%" echo ID_K1=%ID_K1%
>> "%VALORES%" echo ID_K2=%ID_K2%
>> "%VALORES%" echo HOY_P1=%HOY_CALC%

rem --- d) Detalle y listado de E -------------------------------------------
call :http d1 "GET detalle de E" GET "proyectos/%ID_E%" gestor || exit /b 1
call :http d2 "GET listado con el codigo de E - responsable null" GET "proyectos?texto=%CODIGO_E%" gestor || exit /b 1
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\d1.txt'); $j=$t.Substring($t.IndexOf('{')) | ConvertFrom-Json; 'CONTROL d1 - personal: ' + (@($j.personal | ForEach-Object { $_.rol + ' ' + $_.numero + ' ' + $_.empleado.codigoEkon + ' ' + $_.fechaInicio + '..' + $_.fechaFin + ' inicial ' + $_.esPrincipalInicial }) -join '; ') + ' (esperado 2 BACK, ningun PRINCIPAL)'; $l=[IO.File]::ReadAllText('tmp\d2.txt'); $k=$l.Substring($l.IndexOf('{')) | ConvertFrom-Json; $e=@($k.items | Where-Object { $_.id -eq [int]$env:ID_E }); if ($e.Count -eq 0) { 'CONTROL d2 - E no aparece en el listado' } else { 'CONTROL d2 - responsable: ' + $(if ($null -eq $e[0].responsable) { 'null (esperado null)' } else { $e[0].responsable + ' (esperado null)' }) + '; backs: ' + ($e[0].backs -join ', ') }">> "%SALIDA%"

rem --- e) Actualizacion de personal de E ------------------------------------
call :http e1 "GET edicion de E" GET "proyectos/%ID_E%/edicion" gestor || exit /b 1
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\e1.txt'); $j=$t.Substring($t.IndexOf('{')) | ConvertFrom-Json; $d=Join-Path (Get-Location) 'tmp'; $backs=@($j.personal | Where-Object { $_.rol -eq 'BACK' } | Sort-Object numero | ForEach-Object { [ordered]@{ clave = 'k' + $_.id; id = $_.id; empleadoId = $_.empleado.id; tipoRegistro = $_.tipoRegistro; fechaInicio = $_.fechaInicio; fechaFin = $_.fechaFin; diasDescanso = $_.diasDescanso; principalClave = $null; principalId = $null; observacion = $_.observacion } }); [IO.File]::WriteAllText((Join-Path $d 'e2.json'), (@{ principales = @(); backs = @() } | ConvertTo-Json)); if ($backs.Count -ge 2) { $backs[1].fechaFin = ([datetime]$backs[1].fechaFin).AddDays(-1).ToString('yyyy-MM-dd') }; [IO.File]::WriteAllText((Join-Path $d 'e3.json'), (@{ principales = @(); backs = $backs } | ConvertTo-Json -Depth 4))"
call :http e2 "Vista previa quitando a todos - esperado 400 personal (no escribe)" POST "proyectos/%ID_E%/personal/previsualizar" gestor e2.json || exit /b 1
call :control e2 400 personal
call :http e3 "Vista previa: back 2 termina un dia antes - esperado 200 con versionProyecto" POST "proyectos/%ID_E%/personal/previsualizar" gestor e3.json || exit /b 1
set V_E=
for /f "usebackq delims=" %%v in (`powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\e3.txt'); if ($t.Split([char]10)[0] -notmatch ' 200 ') { exit }; $j=$t.Substring($t.IndexOf('{')) | ConvertFrom-Json; if (@($j.cruces).Count -eq 0) { $j.versionProyecto }"`) do set V_E=%%v
>> "%SALIDA%" echo CONTROL e3 - token versionProyecto=%V_E% (vacio = la vista previa no es 200 sin cruces: no se registra)
if not defined V_E goto :sin_registro
powershell -NoProfile -Command "$d=Join-Path (Get-Location) 'tmp'; $j=[IO.File]::ReadAllText((Join-Path $d 'e3.json')) | ConvertFrom-Json; $j | Add-Member -NotePropertyName versionProyecto -NotePropertyValue ([int]$env:V_E); [IO.File]::WriteAllText((Join-Path $d 'e4.json'), ($j | ConvertTo-Json -Depth 4))"
call :http e4 "REGISTRAR la actualizacion de personal de E con el token %V_E% - esperado 200 version %V_E%+1" POST "proyectos/%ID_E%/personal" gestor e4.json || exit /b 1
call :control e4 200

rem --- f) Detalle de E despues -------------------------------------------------
call :http f "GET detalle de E despues de la actualizacion" GET "proyectos/%ID_E%" gestor || exit /b 1
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\f.txt'); $j=$t.Substring($t.IndexOf('{')) | ConvertFrom-Json; 'CONTROL f - personal: ' + (@($j.personal | ForEach-Object { $_.rol + ' ' + $_.numero + ' ' + $_.fechaInicio + '..' + $_.fechaFin }) -join '; ') + ' (esperado back 2 hasta ' + ([datetime]$env:K2_FIN).AddDays(-1).ToString('yyyy-MM-dd') + '); etapas: ' + (@($j.etapas | ForEach-Object { 'v' + $_.version + ' ' + $_.tipoMovimiento }) -join ', ')">> "%SALIDA%"
goto :reactivacion

:sin_registro
>> "%SALIDA%" echo CONTROL e4 - OMITIDO: la vista previa e3 no fue 200 sin cruces; no se registro la actualizacion.
echo e4 omitido: revise e3 en %SALIDA%.

rem --- g) Reactivacion del Id 2 (solo vistas previas) --------------------------
:reactivacion
call :http g1 "GET reactivacion del Id %ID_S%" GET "proyectos/%ID_S%/reactivacion" gestor || exit /b 1
set R=
for /f "usebackq delims=" %%r in (`powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\g1.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { exit }; $j=$t.Substring($i) | ConvertFrom-Json; if ($j.puedeReactivar) { $j.fechaMinima }"`) do set R=%%r
if not defined R (
  >> "%SALIDA%" echo CONTROL g1 - el Id %ID_S% no se puede reactivar: g2 y g3 OMITIDOS.
  goto :fin
)
powershell -NoProfile -Command "$d=Join-Path (Get-Location) 'tmp'; $r=[datetime]$env:R; $f='yyyy-MM-dd'; function Cuerpo($desde) { @{ fecha = $r.ToString($f); fechaFin = $r.AddDays(10).ToString($f); principales = @(); backs = @(@{ clave = 'k1'; id = $null; empleadoId = [int]$env:ID_K1; tipoRegistro = 'JORNADA'; fechaInicio = $desde.ToString($f); fechaFin = $r.AddDays(3).ToString($f); diasDescanso = 0; principalClave = $null; principalId = $null; observacion = $null }) } }; [IO.File]::WriteAllText((Join-Path $d 'g2.json'), ((Cuerpo $r) | ConvertTo-Json -Depth 4)); [IO.File]::WriteAllText((Join-Path $d 'g3.json'), ((Cuerpo $r.AddDays(1)) | ConvertTo-Json -Depth 4))"
call :http g2 "Vista previa de reactivacion del Id %ID_S% solo con un back desde R=%R% - esperado 200 (no escribe)" POST "proyectos/%ID_S%/reactivacion/previsualizar" gestor g2.json || exit /b 1
call :control g2 200
call :http g3 "Vista previa de reactivacion del Id %ID_S% con el back desde R+1 - esperado 400 backs (no escribe)" POST "proyectos/%ID_S%/reactivacion/previsualizar" gestor g3.json || exit /b 1
call :control g3 400 backs

:fin
echo.
echo Listo. Proyecto E = Id %ID_E%. Resultados en %SALIDA%; valores para la parte 2 en %VALORES%.
exit /b 0

:faltan_empleados
echo Indique los dos empleados: parte1.cmd EMP_K1 EMP_K2, o EMP_K1= y EMP_K2= en empleados.txt.
exit /b 1

rem ==========================================================================
rem :probar K1 K2 [primero] -> vista previa de la creacion solo con esos backs; ELEGIDO=1 si es 200, sin cruces y con
rem la advertencia de proyecto sin principal. En el recorrido automatico se omiten el par por defecto y los repetidos.
:probar
if defined ELEGIDO exit /b 0
if /i "%~1"=="%~2" exit /b 0
if "%~3"=="" if /i "%~1"=="%DEF_K1%" if /i "%~2"=="%DEF_K2%" exit /b 0
call :resolver %~1
call :resolver %~2
if defined NO_%~1 goto :probar_sin_empleado
if defined NO_%~2 goto :probar_sin_empleado
call set "ID_K1=%%ID_%~1%%"
call set "ID_K2=%%ID_%~2%%"
call :preparar e-crear.json || exit /b 1
set COPIAR=0
call :http a_%~1_%~2 "Vista previa de la creacion con los backs %~1 y %~2" POST "proyectos/previsualizar" gestor e-crear.json
set COPIAR=
set "RES="
for /f "usebackq delims=" %%a in (`powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\a_%~1_%~2.txt'); $s=$t.Split([char]10)[0].Trim(); $i=$t.IndexOf('{'); if ($i -lt 0) { $s; exit }; $j=$t.Substring($i) | ConvertFrom-Json; if ($null -eq $j.cruces) { $s + ' ' + $j.title; exit }; $c=@($j.cruces).Count; $adv=@($j.advertencias | Where-Object { $_ -like 'El proyecto no tendr*principal*' }).Count -gt 0; if ($c -eq 0 -and $adv) { 'OK' } else { 'cruces ' + $c + ', advertencia ' + $adv }"`) do set "RES=%%a"
if "%RES%"=="OK" goto :probar_elegido
>> "%SALIDA%" echo DESCARTADO backs %~1 / %~2 - %RES%
echo Descartado backs %~1 / %~2 - %RES%
exit /b 0
:probar_elegido
set ELEGIDO=1
set "EMP_K1=%~1"
set "EMP_K2=%~2"
>> "%SALIDA%" echo ELEGIDO backs %~1 / %~2 - sin cruces y con la advertencia de proyecto sin principal
exit /b 0
:probar_sin_empleado
>> "%SALIDA%" echo DESCARTADO backs %~1 / %~2 - un empleado no existe o no esta activo
echo Descartado backs %~1 / %~2 - un empleado no existe o no esta activo
exit /b 0

rem :resolver CODIGO -> ID_CODIGO con el Id del empleado activo (GET /api/empleados, solo lectura); vacio si no existe.
:resolver
if defined ID_%~1 exit /b 0
if defined NO_%~1 exit /b 0
set COPIAR=0
call :http E_%~1 "Empleado %~1" GET "empleados?texto=%~1&soloMisDepartamentos=false&tamano=100" gestor
set COPIAR=
for /f "usebackq delims=" %%i in (`powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\E_%~1.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { exit }; $j=$t.Substring($i) | ConvertFrom-Json; $e=@($j.items | Where-Object { $_.codigoEkon -eq '%~1' }); if ($e.Count -gt 0) { $e[0].id }"`) do set "ID_%~1=%%i"
if not defined ID_%~1 set "NO_%~1=1"
exit /b 0

rem :control NOMBRE ESTADO [clave de error esperada] -> linea CONTROL con el estado HTTP y los errores.
:control
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\%~1.txt'); $s=$t.Split([char]10)[0].Trim(); $i=$t.IndexOf('{'); $det=''; $j=$null; if ($i -ge 0) { $j=$t.Substring($i) | ConvertFrom-Json; if ($j.errors) { $det=(@($j.errors.PSObject.Properties | ForEach-Object { $_.Name + ': ' + ($_.Value -join ' / ') }) -join ' | ') } elseif ($j.version) { $det='version ' + $j.version } elseif ($null -ne $j.advertencias) { $det='advertencias: ' + (@($j.advertencias) -join ' | ') + '; cruces: ' + @($j.cruces).Count } }; $ok=($s -match ' %~2 ') -and ('%~3' -eq '' -or ($j -and $j.errors -and $j.errors.PSObject.Properties.Name -contains '%~3')); 'CONTROL %~1 - ' + $s + ' - ' + $det + ' - ' + $(if ($ok) { 'OK (esperado %~2 %~3)' } else { 'DISTINTO (esperado %~2 %~3)' })">> "%SALIDA%"
exit /b 0

rem :http NOMBRE "descripcion" METODO "ruta despues de /api/" usuario [cuerpo.json] -> tmp\NOMBRE.txt
rem Con COPIAR=0 la respuesta no se copia a la salida. Con SIMULAR=1 NUNCA llama a curl: copia %SIMULACION%\NOMBRE.txt.
:http
if not "%COPIAR%"=="0" (
  >> "%SALIDA%" echo.
  >> "%SALIDA%" echo === %~1: %~2 ===
  echo === %~1: %~2 ===
)
if "%SIMULAR%"=="1" goto :http_simulado
if "%~6"=="" goto :http_sin_cuerpo
curl -k -sS -i -X %~3 "%BASE%/api/%~4" -H "X-Dev-User: %~5" -H "Content-Type: application/json" --data-binary "@tmp\%~6" > "tmp\%~1.txt" 2>&1
goto :http_fin
:http_sin_cuerpo
curl -k -sS -i -X %~3 "%BASE%/api/%~4" -H "X-Dev-User: %~5" > "tmp\%~1.txt" 2>&1
goto :http_fin
:http_simulado
if not exist "%SIMULACION%\%~1.txt" (
  echo SIMULAR: falta %SIMULACION%\%~1.txt
  exit /b 1
)
copy /y "%SIMULACION%\%~1.txt" "tmp\%~1.txt" > nul
:http_fin
if "%COPIAR%"=="0" exit /b 0
type "tmp\%~1.txt">> "%SALIDA%"
>> "%SALIDA%" echo.
exit /b 0

rem :preparar plantilla.json -> tmp\plantilla.json con los marcadores reemplazados (UTF-8 sin BOM)
:preparar
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('%~1'); foreach ($v in 'INICIO','FIN','K1_INICIO','K1_FIN','K2_INICIO','K2_FIN','ID_K1','ID_K2') { $t=$t.Replace('__' + $v + '__', [Environment]::GetEnvironmentVariable($v)) }; [IO.File]::WriteAllText((Join-Path (Get-Location) 'tmp\%~1'), $t)"
if errorlevel 1 (
  echo No se pudo preparar %~1.
  exit /b 1
)
exit /b 0

:verificar_api
set CODIGO=
for /f "usebackq delims=" %%c in (`curl -k -s -o nul -w "%%{http_code}" "%BASE%/api/health/db"`) do set CODIGO=%%c
if not "%CODIGO%"=="200" (
  echo La API no responde en %BASE%/api/health/db - codigo HTTP: %CODIGO%.
  echo Inicie la API con el perfil https y vuelva a ejecutar este script.
  exit /b 1
)
echo API disponible en %BASE%.
exit /b 0
