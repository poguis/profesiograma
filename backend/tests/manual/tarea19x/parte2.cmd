@echo off
setlocal
rem UTF-8 en la consola para que las tildes de las respuestas se guarden bien.
chcp 65001 > nul
rem ==========================================================================
rem TAREA-19x - Parte 2: 409 con token VIEJO en cambio de estado, personal y cabecera del Id 12 y en la reactivacion
rem del Id 2. DISENADA PARA NO ESCRIBIR: cada registro lleva un token viejo (V - 1) Y un cuerpo que el servidor
rem rechazaria con 400. Esperado: 409 "El proyecto cambio; vuelve a cargarlo." (la comprobacion previa del token va
rem antes de cualquier 400). Si el control fallara, la respuesta seria 400 y tampoco se escribiria nada.
rem Se puede repetir.
rem   qa) GET cabecera del Id 12: V.          qb) GET reactivacion del Id 2: VR.
rem   q1) cambio-estado Id 12: SUSPENDIDO con fecha 01/01/2000 (fuera de rango) y token V-1 - esperado 409.
rem   q2) cambio-estado Id 12: el mismo cuerpo SIN token - esperado 400 versionProyecto.
rem   q3) personal Id 12: un principal sin empleado (invalido) y token V-1 - esperado 409.
rem   q4) cabecera Id 12: regreso de almuerzo actual (sin cambios) y token V-1 - esperado 409.
rem   q5) reactivacion Id 2: fechas 01/01/2000 (invalidas) y token VR-1 - esperado 409.
rem Salida: resultado-parte2.txt (cada ejecucion la reemplaza).
rem Modo simulacion: set SIMULAR=1 - no llama a curl; lee %SIMULACION%\NOMBRE.txt y usa BASE=https://localhost:1.
rem ==========================================================================
cd /d "%~dp0"
if not defined SIMULACION set "SIMULACION=simulacion"
if "%SIMULAR%"=="1" (set "BASE=https://localhost:1") else (set "BASE=https://localhost:7180")
if not defined ID_D set ID_D=12
if not defined ID_S set ID_S=2
set SALIDA=resultado-parte2.txt

if not "%SIMULAR%"=="1" call :verificar_api || exit /b 1
if not exist tmp mkdir tmp

> "%SALIDA%" echo TAREA-19x parte 2 - %date% %time% - SIMULAR=%SIMULAR% - ID_D=%ID_D% - ID_S=%ID_S%

rem --- tokens ---------------------------------------------------------------
call :http qa "GET cabecera del Id %ID_D% - token V" GET "proyectos/%ID_D%/cabecera" gestor || exit /b 1
call :http qb "GET reactivacion del Id %ID_S% - token VR" GET "proyectos/%ID_S%/reactivacion" gestor || exit /b 1
set V=
set VR=
set ACTUAL=
for /f "usebackq tokens=1,2 delims==" %%a in (`powershell -NoProfile -Command "foreach ($n in 'qa','qb') { $t=[IO.File]::ReadAllText('tmp\' + $n + '.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { continue }; $j=$t.Substring($i) | ConvertFrom-Json; if ($n -eq 'qa') { 'V=' + $j.versionProyecto; 'ACTUAL=' + $j.regresoAlmuerzo } else { 'VR=' + $j.versionProyecto } }"`) do set %%a=%%b
>> "%SALIDA%" echo Tokens: V=%V% (Id %ID_D%), VR=%VR% (Id %ID_S%); regreso actual del Id %ID_D%=%ACTUAL%
if not defined V goto :sin_datos
if not defined VR goto :sin_datos
if not defined ACTUAL goto :sin_datos
if "%V%"=="0" goto :sin_datos
if "%VR%"=="0" goto :sin_datos

powershell -NoProfile -Command "$d=Join-Path (Get-Location) 'tmp'; $v=[int]$env:V - 1; $vr=[int]$env:VR - 1; [IO.File]::WriteAllText((Join-Path $d 'q1.json'), (@{ estadoDestino = 'SUSPENDIDO'; fecha = '2000-01-01'; versionProyecto = $v } | ConvertTo-Json)); [IO.File]::WriteAllText((Join-Path $d 'q2.json'), (@{ estadoDestino = 'SUSPENDIDO'; fecha = '2000-01-01' } | ConvertTo-Json)); [IO.File]::WriteAllText((Join-Path $d 'q3.json'), (@{ principales = @(@{ clave = 'p1'; id = $null; empleadoId = $null; jornada = $null; fechaInicio = $null; fechaFin = $null; cargo = $null }); backs = @(); versionProyecto = $v } | ConvertTo-Json -Depth 4)); [IO.File]::WriteAllText((Join-Path $d 'q4.json'), (@{ regresoAlmuerzo = $env:ACTUAL; versionProyecto = $v } | ConvertTo-Json)); [IO.File]::WriteAllText((Join-Path $d 'q5.json'), (@{ fecha = '2000-01-01'; fechaFin = '2000-01-02'; principales = @(); backs = @(); versionProyecto = $vr } | ConvertTo-Json))"

rem --- registros que no pueden escribir -------------------------------------
call :http q1 "cambio-estado Id %ID_D%: fecha fuera de rango + token viejo - esperado 409" POST "proyectos/%ID_D%/cambio-estado" gestor q1.json || exit /b 1
call :http q2 "cambio-estado Id %ID_D%: mismo cuerpo SIN token - esperado 400 versionProyecto" POST "proyectos/%ID_D%/cambio-estado" gestor q2.json || exit /b 1
call :http q3 "personal Id %ID_D%: principal sin empleado + token viejo - esperado 409" POST "proyectos/%ID_D%/personal" gestor q3.json || exit /b 1
call :http q4 "cabecera Id %ID_D%: sin cambios + token viejo - esperado 409" POST "proyectos/%ID_D%/cabecera" gestor q4.json || exit /b 1
call :http q5 "reactivacion Id %ID_S%: fechas invalidas + token viejo - esperado 409" POST "proyectos/%ID_S%/reactivacion" gestor q5.json || exit /b 1

powershell -NoProfile -Command "$esperado=@{ q1='409'; q2='400'; q3='409'; q4='409'; q5='409' }; foreach ($n in 'q1','q2','q3','q4','q5') { $t=[IO.File]::ReadAllText('tmp\' + $n + '.txt'); $s=$t.Split([char]10)[0].Trim(); $i=$t.IndexOf('{'); $det=''; if ($i -ge 0) { $j=$t.Substring($i) | ConvertFrom-Json; $det=$j.title; if ($j.errors) { $det += ' ' + (@($j.errors.PSObject.Properties | ForEach-Object { $_.Name + ': ' + ($_.Value -join ' / ') }) -join ' | ') } }; 'CONTROL ' + $n + ' - ' + $s + ' - ' + $det + ' - ' + $(if ($s -match (' ' + $esperado[$n] + ' ')) { 'OK (esperado ' + $esperado[$n] + ')' } else { 'DISTINTO (esperado ' + $esperado[$n] + ')' }) }">> "%SALIDA%"

echo.
echo Listo. Resultados en %SALIDA%. Revise las lineas CONTROL.
exit /b 0

:sin_datos
>> "%SALIDA%" echo CONTROL - faltan tokens o el regreso actual (o un token es 0): no se envio ningun registro.
echo Faltan datos: no se envio ningun registro. Revise %SALIDA%.
exit /b 1

rem ==========================================================================
rem :http NOMBRE "descripcion" METODO "ruta despues de /api/" usuario [cuerpo.json] -> tmp\NOMBRE.txt (y copia en la salida)
rem Con SIMULAR=1 NUNCA llama a curl: copia %SIMULACION%\NOMBRE.txt.
:http
>> "%SALIDA%" echo.
>> "%SALIDA%" echo === %~1: %~2 ===
echo === %~1: %~2 ===
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
type "tmp\%~1.txt">> "%SALIDA%"
>> "%SALIDA%" echo.
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
