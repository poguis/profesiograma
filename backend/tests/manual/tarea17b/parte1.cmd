@echo off
setlocal
rem UTF-8 en la consola para que las tildes de las respuestas se guarden bien.
chcp 65001 > nul
rem ==========================================================================
rem TAREA-17b - Parte 1: preparar el "proyecto B" para probar la reactivacion.
rem ESCRIBE: crea el proyecto B y lo suspende. Ejecutar UNA sola vez.
rem   Fechas relativas a hoy (H): inicio H-3, fin H+55, suspension F = H-2, reactivacion R = H-1.
rem   P1 = DEV007 TIPO_2 de H-3 a H+55; Back 1 = DEV008 JORNADA solo el dia F, 3 dias de descanso, relacionado con P1.
rem   Grupo CAMPO con actividad DEV.01 (para ver la actividad REACTIVACION, R8).
rem   0) Vista previa de la creacion: si hay cruces se detiene SIN escribir nada.
rem   1) Crear el proyecto B - esperado 201.
rem   2) Suspenderlo en F - esperado 200 SUSPENDIDO version 2.
rem   3) GET detalle de B.
rem Salidas: resultado-parte1.txt (proteccion: si existe, no se ejecuta) y valores-parte1.txt (lo lee parte2.cmd).
rem Modo simulacion: set SIMULAR=1 - no llama a curl; lee las respuestas de %SIMULACION%\NOMBRE.txt
rem   y usa BASE=https://localhost:1. Opcional: set HOY=yyyy-MM-dd para fijar H.
rem ==========================================================================
cd /d "%~dp0"
if not defined SIMULACION set "SIMULACION=simulacion"
if "%SIMULAR%"=="1" (set "BASE=https://localhost:1") else (set "BASE=https://localhost:7180")
rem Empleados (Id): P1 y back. Ver docs/tareas/TAREA-17b-reporte.md (seccion 6) antes de cambiarlos.
set EMP_P1=7
set EMP_BACK=8
set SALIDA=resultado-parte1.txt
set VALORES=valores-parte1.txt
set PREVIO=tmp\parte1-previo.txt

if exist "%SALIDA%" (
  echo Ya existe %SALIDA%: la parte 1 ya se ejecuto y el proyecto B ya existe.
  echo Si de verdad quiere repetirla, borre %SALIDA% y %VALORES% y vuelva a ejecutar.
  exit /b 1
)

if not "%SIMULAR%"=="1" call :verificar_api || exit /b 1
if not exist tmp mkdir tmp

rem --- Fechas relativas a hoy ----------------------------------------------
set HOY_CALC=
for /f "usebackq tokens=1,2 delims==" %%a in (`powershell -NoProfile -Command "$h=[datetime]::Today; if ($env:HOY) { $h=[datetime]::ParseExact($env:HOY,'yyyy-MM-dd',$null) }; $f='yyyy-MM-dd'; 'HOY_CALC=' + $h.ToString($f); 'INICIO=' + $h.AddDays(-3).ToString($f); 'F=' + $h.AddDays(-2).ToString($f); 'R=' + $h.AddDays(-1).ToString($f); 'FIN=' + $h.AddDays(55).ToString($f)"`) do set %%a=%%b
if not defined HOY_CALC (
  echo No se pudieron calcular las fechas.
  exit /b 1
)

rem Hasta el registro, la salida va a %PREVIO%: si la vista previa tiene cruces no queda resultado-parte1.txt.
set "SALIDA=%PREVIO%"
> "%SALIDA%" echo TAREA-17b parte 1 - %date% %time% - SIMULAR=%SIMULAR%
>> "%SALIDA%" echo Fechas: H=%HOY_CALC% INICIO=%INICIO% F=%F% R=%R% FIN=%FIN% EMP_P1=%EMP_P1% EMP_BACK=%EMP_BACK%
echo Fechas: H=%HOY_CALC% INICIO=%INICIO% F=%F% R=%R% FIN=%FIN%

rem --- 0) Vista previa de la creacion (no escribe) --------------------------
call :preparar p0-crear.json || exit /b 1
call :http P0a "Vista previa de la creacion de B - esperado 200 sin cruces" POST "proyectos/previsualizar" gestor p0-crear.json || exit /b 1
set CRUCES=
for /f "usebackq delims=" %%c in (`powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\P0a.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { 'X'; exit }; $j=$t.Substring($i) | ConvertFrom-Json; if ($null -eq $j.cruces) { 'X' } else { @($j.cruces).Count }"`) do set CRUCES=%%c
>> "%SALIDA%" echo CONTROL P0a - cruces de la vista previa: %CRUCES% (esperado 0)
if not "%CRUCES%"=="0" (
  echo La vista previa de la creacion no es valida o tiene cruces: NO se creo nada. Revise %PREVIO%.
  echo Con DEV007 y DEV008 hace falta H mayor o igual a 07/10/2026: DEV008 trabaja en el Id 9 hasta el 04/10.
  exit /b 1
)

rem --- desde aqui se escribe -------------------------------------------------
copy /y "%PREVIO%" "resultado-parte1.txt" > nul
set "SALIDA=resultado-parte1.txt"

rem --- 1) Crear B ----------------------------------------------------------
call :http P0 "CREAR el proyecto B - esperado 201" POST "proyectos" gestor p0-crear.json || exit /b 1
set ID_B=
for /f "usebackq delims=" %%c in (`powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\P0.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { exit }; $j=$t.Substring($i) | ConvertFrom-Json; if ($j.id) { $j.id }"`) do set ID_B=%%c
if not defined ID_B (
  >> "%SALIDA%" echo CONTROL P0 - no se creo el proyecto B. Se detiene.
  echo No se creo el proyecto B. Revise %SALIDA%.
  exit /b 1
)
>> "%SALIDA%" echo CONTROL P0 - proyecto B creado con Id %ID_B%

rem --- 2) Suspender B en F -------------------------------------------------
call :preparar s1-suspender.json || exit /b 1
call :http S1 "SUSPENDER B en F=%F% - esperado 200 SUSPENDIDO version 2 con advertencia de dias transcurridos" POST "proyectos/%ID_B%/cambio-estado" gestor s1-suspender.json || exit /b 1
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\S1.txt'); $s=$t.Split([char]10)[0].Trim(); $i=$t.IndexOf('{'); if ($i -lt 0) { 'CONTROL S1 - ' + $s + ' - sin cuerpo JSON'; exit }; $j=$t.Substring($i) | ConvertFrom-Json; 'CONTROL S1 - ' + $s + ' - id ' + $j.id + ', estado ' + $j.estado + ', version ' + $j.version + ' (esperado 200, SUSPENDIDO, 2)'">> "%SALIDA%"

rem --- 3) Detalle de B -----------------------------------------------------
call :http S1b "GET detalle de B despues de suspender" GET "proyectos/%ID_B%" gestor || exit /b 1
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\S1b.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { 'CONTROL S1b - sin cuerpo JSON'; exit }; $j=$t.Substring($i) | ConvertFrom-Json; 'CONTROL S1b - estado ' + $j.estado.codigo + ', fechaFin ' + $j.fechaFin + ' (esperado SUSPENDIDO, ' + $env:F + '); personal: ' + (@($j.personal | ForEach-Object { $_.rol + ' ' + $_.numero + ' ' + $_.empleado.codigoEkon + ' ' + $_.fechaInicio + '..' + $_.fechaFin + ' descanso ' + $_.diasDescanso + ' inicial ' + $_.esPrincipalInicial }) -join '; ')">> "%SALIDA%"

rem --- Valores para parte2.cmd ---------------------------------------------
> "%VALORES%" echo ID_B=%ID_B%
>> "%VALORES%" echo HOY_P1=%HOY_CALC%
>> "%VALORES%" echo INICIO=%INICIO%
>> "%VALORES%" echo F=%F%
>> "%VALORES%" echo R=%R%
>> "%VALORES%" echo FIN=%FIN%
>> "%VALORES%" echo EMP_P1=%EMP_P1%
>> "%VALORES%" echo EMP_BACK=%EMP_BACK%

echo.
echo Listo. Proyecto B = Id %ID_B%. Resultados en %SALIDA%; valores para la parte 2 en %VALORES%.
exit /b 0

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

rem :preparar plantilla.json -> tmp\plantilla.json con los marcadores reemplazados (UTF-8 sin BOM)
:preparar
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('%~1'); foreach ($v in 'INICIO','FIN','F','R','EMP_P1','EMP_BACK') { $t=$t.Replace('__' + $v + '__', [Environment]::GetEnvironmentVariable($v)) }; [IO.File]::WriteAllText((Join-Path (Get-Location) 'tmp\%~1'), $t)"
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
