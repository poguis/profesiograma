@echo off
setlocal
rem UTF-8 en la consola para que las tildes de las respuestas se guarden bien.
chcp 65001 > nul
rem ==========================================================================
rem TAREA-18b - Parte 3: SOLO GET y vistas previas sobre el proyecto C (Id 11). NADA escribe; se puede repetir.
rem   a) GET cabecera y b) GET detalle del Id 11: CONTROL O3 = la misma actividad vigente (codigo y version).
rem   c) GET listado con el codigo del Id 11: el listado no expone la actividad; ver parte3-vista.sql (requiere la
rem      migracion ActividadVigenteVista aplicada).
rem   d) GET edicion del Id 11: elige F >= hoy en la que un back quede recortado (F = max(hoy, inicio del back,
rem      inicio del proyecto), con F < fin del back y F < fin del proyecto). Si no hay, e) queda OMITIDO (sin datos).
rem   e) Vista previa de acortar el Id 11 a F (H15): back recortado y diasAgregados DESCANSO de F+1 a F+descanso.
rem Salida: resultado-parte3.txt (cada ejecucion la reemplaza).
rem Modo simulacion: set SIMULAR=1 - no llama a curl; lee %SIMULACION%\NOMBRE.txt y usa BASE=https://localhost:1.
rem   Opcional: set HOY=yyyy-MM-dd para fijar hoy.
rem ==========================================================================
cd /d "%~dp0"
if not defined SIMULACION set "SIMULACION=simulacion"
if "%SIMULAR%"=="1" (set "BASE=https://localhost:1") else (set "BASE=https://localhost:7180")
if not defined ID_C set ID_C=11
set SALIDA=resultado-parte3.txt

if not "%SIMULAR%"=="1" call :verificar_api || exit /b 1
if not exist tmp mkdir tmp

> "%SALIDA%" echo TAREA-18b parte 3 - %date% %time% - SIMULAR=%SIMULAR% - ID_C=%ID_C%

rem --- a) b) O3 ------------------------------------------------------------
call :http t3a "GET cabecera del Id %ID_C%" GET "proyectos/%ID_C%/cabecera" gestor || exit /b 1
call :http t3b "GET detalle del Id %ID_C%" GET "proyectos/%ID_C%" gestor || exit /b 1
powershell -NoProfile -Command "$a=[IO.File]::ReadAllText('tmp\t3a.txt'); $b=[IO.File]::ReadAllText('tmp\t3b.txt'); if ($a.IndexOf('{') -lt 0 -or $b.IndexOf('{') -lt 0) { 'CONTROL O3 - sin cuerpo JSON'; exit }; $c=$a.Substring($a.IndexOf('{')) | ConvertFrom-Json; $d=$b.Substring($b.IndexOf('{')) | ConvertFrom-Json; $x=if ($c.actividadVigente) { $c.actividadVigente.codigo + ' v' + $c.actividadVigente.version } else { 'ninguna' }; $y=if ($d.actividadVigente) { $d.actividadVigente.actividadCodigo + ' v' + $d.actividadVigente.version } else { 'ninguna' }; 'CONTROL O3 - cabecera: ' + $x + '; detalle: ' + $y + ' - ' + $(if ($x -eq $y) { 'IGUALES (esperado)' } else { 'DISTINTAS' }) + '; proyecto ' + $c.fechaInicio + '..' + $c.fechaFin + '; actividades: ' + (@($c.actividades | ForEach-Object { 'v' + $_.version + ' ' + $_.codigo + ' ' + $_.fechaInicio + '..' + $_.fechaFin }) -join '; ')">> "%SALIDA%"

rem --- c) listado ----------------------------------------------------------
set CODIGO_C=
for /f "usebackq delims=" %%c in (`powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\t3a.txt'); $i=$t.IndexOf('{'); if ($i -ge 0) { ($t.Substring($i) | ConvertFrom-Json).codigo }"`) do set CODIGO_C=%%c
call :http t3c "GET listado con texto %CODIGO_C%" GET "proyectos?texto=%CODIGO_C%" gestor || exit /b 1
>> "%SALIDA%" echo CONTROL O3 listado - el listado (ProyectoResumenDto) no expone la actividad: verificar con parte3-vista.sql en SSMS. Requiere la migracion ActividadVigenteVista aplicada.

rem --- d) e) H15 -----------------------------------------------------------
call :http t3d "GET edicion del Id %ID_C% - para elegir F" GET "proyectos/%ID_C%/edicion" gestor || exit /b 1
call :elegir_f t3d
>> "%SALIDA%" echo Fecha elegida: F=%F% back=%BACK% fin del back=%BACK_FIN% descanso=%BACK_DESCANSO% (hoy=%HOY_CALC%)
if not defined F (
  >> "%SALIDA%" echo.
  >> "%SALIDA%" echo === t3e: OMITIDO sin datos - no hay F mayor o igual a hoy en la que un back quede recortado ===
  echo t3e: OMITIDO sin datos.
  goto :fin
)
powershell -NoProfile -Command "[IO.File]::WriteAllText((Join-Path (Get-Location) 'tmp\t3e.json'), (@{ fechaFin = $env:F } | ConvertTo-Json))"
call :http t3e "Vista previa de acortar el Id %ID_C% a %F% - esperado %BACK% recortado y diasAgregados DESCANSO desde el dia siguiente (H15)" POST "proyectos/%ID_C%/cabecera/previsualizar" gestor t3e.json || exit /b 1
call :control_h15 t3e

:fin
echo.
echo Listo. Resultados en %SALIDA%. Revise las lineas CONTROL.
exit /b 0

rem ==========================================================================
rem :elegir_f NOMBRE -> F, BACK, BACK_FIN, BACK_DESCANSO, HOY_CALC (F vacio si no hay)
:elegir_f
set F=
set BACK=
set BACK_FIN=
set BACK_DESCANSO=
set HOY_CALC=
for /f "usebackq tokens=1,2 delims==" %%a in (`powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\%~1.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { exit }; $j=$t.Substring($i) | ConvertFrom-Json; $hoy=[datetime]::Today; if ($env:HOY) { $hoy=[datetime]::ParseExact($env:HOY,'yyyy-MM-dd',$null) }; 'HOY_CALC=' + $hoy.ToString('yyyy-MM-dd'); $ini=[datetime]$j.fechaInicio; $fin=[datetime]$j.fechaFin; foreach ($b in @($j.personal | Where-Object { $_.rol -eq 'BACK' })) { $bi=[datetime]$b.fechaInicio; $bf=[datetime]$b.fechaFin; $f=@($hoy, $bi, $ini | Sort-Object)[-1]; if ($f -lt $bf -and $f -lt $fin) { 'F=' + $f.ToString('yyyy-MM-dd'); 'BACK=' + $b.empleado.codigoEkon; 'BACK_FIN=' + $bf.ToString('yyyy-MM-dd'); 'BACK_DESCANSO=' + $b.diasDescanso; break } }"`) do set %%a=%%b
exit /b 0

rem :control_h15 NOMBRE -> recortados, dias eliminados, diasAgregados y advertencias de la vista previa
:control_h15
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\%~1.txt'); $s=$t.Split([char]10)[0].Trim(); $i=$t.IndexOf('{'); if ($i -lt 0) { 'CONTROL %~1 - ' + $s + ' - sin cuerpo JSON'; exit }; $j=$t.Substring($i) | ConvertFrom-Json; if ($j.errors) { 'CONTROL %~1 - ' + $s + ' - ' + (@($j.errors.PSObject.Properties | ForEach-Object { $_.Name + ': ' + ($_.Value -join ' / ') }) -join ' | '); exit }; $f=[datetime]$env:F; 'CONTROL %~1 - ' + $s + '; recortados: ' + (@($j.personalRecortado | ForEach-Object { $_.empleado.codigoEkon + ' ' + $_.fechaFinAnterior + '->' + $_.fechaFinNueva }) -join ', ') + '; diasAgregados: ' + (@($j.diasAgregados | ForEach-Object { $_.empleado.codigoEkon + ' ' + $_.rol + ' ' + $_.desde + '..' + $_.hasta + ' (' + $_.cantidad + ')' }) -join ', ') + ' (esperado ' + $env:BACK + ' DESCANSO ' + $f.AddDays(1).ToString('yyyy-MM-dd') + '..' + $f.AddDays([int]$env:BACK_DESCANSO).ToString('yyyy-MM-dd') + '); advertencias: ' + (@($j.advertencias) -join ' | ')">> "%SALIDA%"
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
