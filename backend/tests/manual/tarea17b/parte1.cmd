@echo off
setlocal
rem UTF-8 en la consola para que las tildes de las respuestas se guarden bien.
chcp 65001 > nul
rem ==========================================================================
rem TAREA-17b - Parte 1: preparar el "proyecto B" para probar la reactivacion.
rem ESCRIBE: crea el proyecto B y lo suspende. Ejecutar UNA sola vez.
rem   Fechas relativas a hoy (H): inicio H-3, fin H+55, suspension F = H-2, reactivacion R = H-1.
rem   P1 = EMP_P1 TIPO_2 de H-3 a H+55; Back 1 = EMP_BACK JORNADA solo el dia F, 3 dias de descanso, relacionado con P1.
rem   Grupo CAMPO con actividad DEV.01 (para ver la actividad REACTIVACION, R8).
rem Empleados (codigo EKON):
rem   - parte1.cmd DEV003 DEV004      : P1 = DEV003, back = DEV004 (solo ese par);
rem   - empleados.txt (opcional)      : lineas EMP_P1=DEV003 y EMP_BACK=DEV004 (solo ese par);
rem   - sin indicar                   : seleccion automatica. Primero DEV007/DEV008 y luego los demas pares de
rem                                     DEV001-DEV008 sin DEV005 (reservado para los casos e/f de la parte 2).
rem   Cada par se prueba SOLO con la vista previa de la creacion (no guarda). Se usa el primero sin cruces.
rem   Los Id se leen con GET /api/empleados (solo lectura).
rem   0) Seleccion del par: si ninguno sirve se detiene SIN escribir nada.
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
set "DEF_P1=DEV007"
set "DEF_BACK=DEV008"
set "CANDIDATOS=DEV001 DEV002 DEV003 DEV004 DEV006 DEV007 DEV008"
set "RESERVADO=DEV005"
set COPIAR=
set SALIDA=resultado-parte1.txt
set VALORES=valores-parte1.txt
set PREVIO=tmp\parte1-previo.txt

if exist "%SALIDA%" (
  echo Ya existe %SALIDA%: la parte 1 ya se ejecuto y el proyecto B ya existe.
  echo Si de verdad quiere repetirla, borre %SALIDA% y %VALORES% y vuelva a ejecutar.
  exit /b 1
)

rem --- Empleados indicados: linea de comandos o empleados.txt ----------------
set EMP_P1=
set EMP_BACK=
if exist empleados.txt for /f "usebackq eol=# tokens=1,2 delims== " %%a in ("empleados.txt") do (
  if /i "%%a"=="EMP_P1" set "EMP_P1=%%b"
  if /i "%%a"=="EMP_BACK" set "EMP_BACK=%%b"
)
if not "%~1"=="" set "EMP_P1=%~1"
if not "%~2"=="" set "EMP_BACK=%~2"
set MODO=automatico
if defined EMP_P1 set MODO=indicado
if defined EMP_BACK set MODO=indicado
if "%MODO%"=="indicado" if not defined EMP_P1 goto :faltan_empleados
if "%MODO%"=="indicado" if not defined EMP_BACK goto :faltan_empleados
if /i "%EMP_P1%"=="%RESERVADO%" goto :reservado
if /i "%EMP_BACK%"=="%RESERVADO%" goto :reservado
if "%MODO%"=="indicado" if /i "%EMP_P1%"=="%EMP_BACK%" (
  echo EMP_P1 y EMP_BACK deben ser empleados distintos.
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

rem Hasta el registro, la salida va a %PREVIO%: si ningun par sirve no queda resultado-parte1.txt.
set "SALIDA=%PREVIO%"
> "%SALIDA%" echo TAREA-17b parte 1 - %date% %time% - SIMULAR=%SIMULAR% - empleados: %MODO%
>> "%SALIDA%" echo Fechas: H=%HOY_CALC% INICIO=%INICIO% F=%F% R=%R% FIN=%FIN%
echo Fechas: H=%HOY_CALC% INICIO=%INICIO% F=%F% R=%R% FIN=%FIN% - empleados: %MODO%

rem --- 0) Seleccion del par (solo vistas previas, no escribe) ---------------
call :resolver %RESERVADO%
set ELEGIDO=
if "%MODO%"=="indicado" (
  call :probar %EMP_P1% %EMP_BACK% primero
) else (
  call :probar %DEF_P1% %DEF_BACK% primero
  for %%p in (%CANDIDATOS%) do for %%b in (%CANDIDATOS%) do call :probar %%p %%b
)
if not defined ELEGIDO (
  >> "%SALIDA%" echo CONTROL P0a - ningun par de empleados sin cruces: no se creo nada.
  echo Ningun par de empleados sirve: NO se creo nada. Revise %PREVIO%.
  exit /b 1
)
>> "%SALIDA%" echo CONTROL P0a - par elegido: P1 %EMP_P1% Id %ID_EMP_P1%, back %EMP_BACK% Id %ID_EMP_BACK%
echo Par elegido: P1 %EMP_P1%, back %EMP_BACK%.

rem --- desde aqui se escribe -------------------------------------------------
copy /y "%PREVIO%" "resultado-parte1.txt" > nul
set "SALIDA=resultado-parte1.txt"
>> "%SALIDA%" echo.
>> "%SALIDA%" echo === P0a: vista previa del par elegido ===
type "tmp\P0a_%EMP_P1%_%EMP_BACK%.txt">> "%SALIDA%"
>> "%SALIDA%" echo.

rem --- 1) Crear B ----------------------------------------------------------
call :preparar p0-crear.json || exit /b 1
call :http P0 "CREAR el proyecto B con P1 %EMP_P1% y back %EMP_BACK% - esperado 201" POST "proyectos" gestor p0-crear.json || exit /b 1
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
set "ID_DEV005_VALOR="
if not defined NO_%RESERVADO% call set "ID_DEV005_VALOR=%%ID_%RESERVADO%%%"
> "%VALORES%" echo ID_B=%ID_B%
>> "%VALORES%" echo HOY_P1=%HOY_CALC%
>> "%VALORES%" echo INICIO=%INICIO%
>> "%VALORES%" echo F=%F%
>> "%VALORES%" echo R=%R%
>> "%VALORES%" echo FIN=%FIN%
>> "%VALORES%" echo EMP_P1=%EMP_P1%
>> "%VALORES%" echo EMP_BACK=%EMP_BACK%
>> "%VALORES%" echo ID_EMP_P1=%ID_EMP_P1%
>> "%VALORES%" echo ID_EMP_BACK=%ID_EMP_BACK%
>> "%VALORES%" echo ID_DEV005=%ID_DEV005_VALOR%

echo.
echo Listo. Proyecto B = Id %ID_B%. Resultados en %SALIDA%; valores para la parte 2 en %VALORES%.
exit /b 0

:faltan_empleados
echo Indique los dos empleados: parte1.cmd EMP_P1 EMP_BACK, o EMP_P1= y EMP_BACK= en empleados.txt.
exit /b 1

:reservado
echo %RESERVADO% se reserva para los casos e/f de la parte 2: elija otro empleado.
exit /b 1

rem ==========================================================================
rem :probar P1 BACK [primero] -> vista previa de la creacion con ese par; si no tiene cruces, ELEGIDO=1.
rem En el recorrido automatico se omiten el par por defecto (ya probado), los pares repetidos y los ya elegidos.
:probar
if defined ELEGIDO exit /b 0
if /i "%~1"=="%~2" exit /b 0
if "%~3"=="" if /i "%~1"=="%DEF_P1%" if /i "%~2"=="%DEF_BACK%" exit /b 0
call :resolver %~1
call :resolver %~2
if defined NO_%~1 goto :probar_sin_empleado
if defined NO_%~2 goto :probar_sin_empleado
call set "PR_ID_P1=%%ID_%~1%%"
call set "PR_ID_BACK=%%ID_%~2%%"
set "ID_EMP_P1=%PR_ID_P1%"
set "ID_EMP_BACK=%PR_ID_BACK%"
call :preparar p0-crear.json || exit /b 1
set COPIAR=0
call :http P0a_%~1_%~2 "Vista previa de la creacion con P1 %~1 y back %~2" POST "proyectos/previsualizar" gestor p0-crear.json
set COPIAR=
set "N_CRUCES="
set "DET_CRUCES="
for /f "usebackq tokens=1,* delims=|" %%a in (`powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\P0a_%~1_%~2.txt'); $s=$t.Split([char]10)[0].Trim(); $i=$t.IndexOf('{'); if ($i -lt 0) { 'X|' + $s; exit }; $j=$t.Substring($i) | ConvertFrom-Json; if ($null -eq $j.cruces) { 'X|' + $s + ' ' + $j.title; exit }; $c=@($j.cruces); if ($c.Count -eq 0) { '0|' } else { $c.Count.ToString() + '|' + ((@($c | Select-Object -First 6 | ForEach-Object { $_.codigoEkon + ' ' + $_.fecha + ' ' + $_.origen + ' ' + $_.proyectoCodigo })) -join '; ') + $(if ($c.Count -gt 6) { '; ...' } else { '' }) }"`) do (
  set "N_CRUCES=%%a"
  set "DET_CRUCES=%%b"
)
if "%N_CRUCES%"=="0" goto :probar_elegido
>> "%SALIDA%" echo DESCARTADO P1 %~1 / back %~2 - cruces: %N_CRUCES% - %DET_CRUCES%
echo Descartado P1 %~1 / back %~2 - cruces: %N_CRUCES% - %DET_CRUCES%
exit /b 0
:probar_elegido
set ELEGIDO=1
set "EMP_P1=%~1"
set "EMP_BACK=%~2"
>> "%SALIDA%" echo ELEGIDO P1 %~1 / back %~2 - sin cruces
exit /b 0
:probar_sin_empleado
>> "%SALIDA%" echo DESCARTADO P1 %~1 / back %~2 - un empleado no existe o no esta activo
echo Descartado P1 %~1 / back %~2 - un empleado no existe o no esta activo
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
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('%~1'); foreach ($v in 'INICIO','FIN','F','R','ID_EMP_P1','ID_EMP_BACK') { $t=$t.Replace('__' + $v + '__', [Environment]::GetEnvironmentVariable($v)) }; [IO.File]::WriteAllText((Join-Path (Get-Location) 'tmp\%~1'), $t)"
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
