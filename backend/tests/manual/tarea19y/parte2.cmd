@echo off
setlocal
rem UTF-8 en la consola para que las tildes de las respuestas se guarden bien.
chcp 65001 > nul
rem ==========================================================================
rem TAREA-19y - Parte 2: PROYECTO_EXIGE_PRINCIPAL = 1 (vuelve C10). NO ESCRIBE (solo GET y vistas previas); se puede
rem repetir. Requiere la migracion ParametroExigePrincipal aplicada y el bloque 1 de parametro-exige-principal.sql
rem ejecutado en SSMS (Valor = '1'). Al terminar, ejecute el bloque 2 (Valor = '0').
rem   h0) GET opciones-formulario: exigePrincipal debe ser true; si no, termina sin enviar nada mas.
rem   h1) Vista previa de la creacion solo con los backs de la parte 1 - esperado 400 principales
rem       "Se requiere al menos 1 principal(es).".
rem   h2) GET reactivacion del Id 2 y vista previa solo con un back desde R - esperado 400 principales.
rem Usa valores-parte1.txt (empleados de la parte 1); si no existe, set ID_K1= e ID_K2= con Id de empleados activos.
rem Salida: resultado-parte2.txt (cada ejecucion la reemplaza).
rem Modo simulacion: set SIMULAR=1 - no llama a curl; lee %SIMULACION%\NOMBRE.txt y usa BASE=https://localhost:1.
rem ==========================================================================
cd /d "%~dp0"
if not defined SIMULACION set "SIMULACION=simulacion"
if "%SIMULAR%"=="1" (set "BASE=https://localhost:1") else (set "BASE=https://localhost:7180")
if not defined ID_S set ID_S=2
set SALIDA=resultado-parte2.txt

if exist valores-parte1.txt for /f "usebackq tokens=1,2 delims==" %%a in ("valores-parte1.txt") do (
  if /i "%%a"=="ID_K1" if not defined ID_K1 set "ID_K1=%%b"
  if /i "%%a"=="ID_K2" if not defined ID_K2 set "ID_K2=%%b"
)
if not defined ID_K1 goto :sin_empleados
if not defined ID_K2 goto :sin_empleados

if not "%SIMULAR%"=="1" call :verificar_api || exit /b 1
if not exist tmp mkdir tmp

for /f "usebackq tokens=1,2 delims==" %%a in (`powershell -NoProfile -Command "$h=[datetime]::Today; if ($env:HOY) { $h=[datetime]::ParseExact($env:HOY,'yyyy-MM-dd',$null) }; $f='yyyy-MM-dd'; 'INICIO=' + $h.AddDays(20).ToString($f); 'FIN=' + $h.AddDays(40).ToString($f); 'K1_INICIO=' + $h.AddDays(20).ToString($f); 'K1_FIN=' + $h.AddDays(25).ToString($f); 'K2_INICIO=' + $h.AddDays(30).ToString($f); 'K2_FIN=' + $h.AddDays(35).ToString($f)"`) do set %%a=%%b

> "%SALIDA%" echo TAREA-19y parte 2 - %date% %time% - SIMULAR=%SIMULAR% - ID_K1=%ID_K1% ID_K2=%ID_K2% ID_S=%ID_S%

rem --- h0) parametro --------------------------------------------------------
call :http h0 "GET opciones-formulario - esperado exigePrincipal true" GET "proyectos/opciones-formulario" gestor || exit /b 1
set EXIGE=
for /f "usebackq delims=" %%e in (`powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\h0.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { exit }; ($t.Substring($i) | ConvertFrom-Json).exigePrincipal"`) do set EXIGE=%%e
>> "%SALIDA%" echo CONTROL h0 - exigePrincipal=%EXIGE% (esperado True)
if /i not "%EXIGE%"=="True" (
  >> "%SALIDA%" echo El parametro no esta en 1: ejecute el bloque 1 de parametro-exige-principal.sql. No se envio nada mas.
  echo PROYECTO_EXIGE_PRINCIPAL no esta en 1: ejecute el bloque 1 de parametro-exige-principal.sql en SSMS.
  exit /b 1
)

rem --- h1) creacion solo con backs ------------------------------------------
call :preparar e-crear.json || exit /b 1
call :http h1 "Vista previa de la creacion solo con backs - esperado 400 principales" POST "proyectos/previsualizar" gestor e-crear.json || exit /b 1
call :control h1 400 principales

rem --- h2) reactivacion del Id 2 solo con backs -----------------------------
call :http h2a "GET reactivacion del Id %ID_S%" GET "proyectos/%ID_S%/reactivacion" gestor || exit /b 1
set R=
for /f "usebackq delims=" %%r in (`powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\h2a.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { exit }; $j=$t.Substring($i) | ConvertFrom-Json; if ($j.puedeReactivar) { $j.fechaMinima }"`) do set R=%%r
if not defined R (
  >> "%SALIDA%" echo CONTROL h2 - el Id %ID_S% no se puede reactivar: OMITIDO.
  goto :fin
)
powershell -NoProfile -Command "$r=[datetime]$env:R; $f='yyyy-MM-dd'; $c=@{ fecha = $r.ToString($f); fechaFin = $r.AddDays(10).ToString($f); principales = @(); backs = @(@{ clave = 'k1'; id = $null; empleadoId = [int]$env:ID_K1; tipoRegistro = 'JORNADA'; fechaInicio = $r.ToString($f); fechaFin = $r.AddDays(3).ToString($f); diasDescanso = 0; principalClave = $null; principalId = $null; observacion = $null }) }; [IO.File]::WriteAllText((Join-Path (Get-Location) 'tmp\h2.json'), ($c | ConvertTo-Json -Depth 4))"
call :http h2 "Vista previa de reactivacion del Id %ID_S% solo con un back desde R=%R% - esperado 400 principales" POST "proyectos/%ID_S%/reactivacion/previsualizar" gestor h2.json || exit /b 1
call :control h2 400 principales

:fin
echo.
echo Listo. Resultados en %SALIDA%. Recuerde volver el parametro a 0 (bloque 2 de parametro-exige-principal.sql).
exit /b 0

:sin_empleados
echo Faltan ID_K1 e ID_K2: ejecute primero parte1.cmd (crea valores-parte1.txt) o defina set ID_K1= y set ID_K2=.
exit /b 1

rem ==========================================================================
rem :control NOMBRE ESTADO [clave de error esperada] -> linea CONTROL con el estado HTTP y los errores.
:control
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\%~1.txt'); $s=$t.Split([char]10)[0].Trim(); $i=$t.IndexOf('{'); $det=''; $j=$null; if ($i -ge 0) { $j=$t.Substring($i) | ConvertFrom-Json; if ($j.errors) { $det=(@($j.errors.PSObject.Properties | ForEach-Object { $_.Name + ': ' + ($_.Value -join ' / ') }) -join ' | ') } }; $ok=($s -match ' %~2 ') -and ('%~3' -eq '' -or ($j -and $j.errors -and $j.errors.PSObject.Properties.Name -contains '%~3')); 'CONTROL %~1 - ' + $s + ' - ' + $det + ' - ' + $(if ($ok) { 'OK (esperado %~2 %~3)' } else { 'DISTINTO (esperado %~2 %~3)' })">> "%SALIDA%"
exit /b 0

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
