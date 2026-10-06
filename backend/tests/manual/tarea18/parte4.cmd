@echo off
setlocal
rem UTF-8 en la consola para que las tildes de las respuestas se guarden bien.
chcp 65001 > nul
rem ==========================================================================
rem TAREA-18b - Parte 4: ESCRIBE en el proyecto C (Id 11). Ejecutar UNA sola vez.
rem   u4a) GET edicion: elige F >= hoy en la que un back quede recortado (como parte3). Si no hay: termina sin escribir.
rem   u4b) GET cabecera: actividad vigente en F y la otra (DEV.01 / DEV.02) como actividad nueva.
rem   u4c) Vista previa del cambio de actividad (nueva desde F) - debe ser 200 CAMBIO_ACTIVIDAD.
rem   u4d) Vista previa de acortar a F - debe ser 200 con el back recortado y diasAgregados (H15).
rem        Si u4c o u4d no son validas: termina SIN escribir.
rem   u4e) REGISTRAR el cambio de actividad - 200; la etapa CAMBIO_ACTIVIDAD lleva la actividad nueva (O2).
rem   u4f) REGISTRAR acortar a F - 200; se insertan los diasAgregados (H15).
rem   u4g) GET detalle y u4h) GET cabecera: etapas nuevas y la misma actividad vigente (O3).
rem   Despues: ejecutar parte4-descansos.sql en SSMS (solo lectura) para ver los DESCANSO del back despues de F.
rem Salida: resultado-parte4.txt (proteccion: si existe, no se ejecuta; se crea justo antes de u4e).
rem Modo simulacion: set SIMULAR=1 - no llama a curl; lee %SIMULACION%\NOMBRE.txt y usa BASE=https://localhost:1.
rem   Opcional: set HOY=yyyy-MM-dd para fijar hoy.
rem ==========================================================================
cd /d "%~dp0"
if not defined SIMULACION set "SIMULACION=simulacion"
if "%SIMULAR%"=="1" (set "BASE=https://localhost:1") else (set "BASE=https://localhost:7180")
if not defined ID_C set ID_C=11
set SALIDA=resultado-parte4.txt
set PREVIO=tmp\parte4-previo.txt

if exist "%SALIDA%" (
  echo Ya existe %SALIDA%: la parte 4 ya se ejecuto y el Id %ID_C% ya se modifico.
  echo Si de verdad quiere repetirla, borre %SALIDA% y vuelva a ejecutar.
  exit /b 1
)

if not "%SIMULAR%"=="1" call :verificar_api || exit /b 1
if not exist tmp mkdir tmp

rem Hasta el primer registro (u4e), la salida va a %PREVIO%.
set "SALIDA=%PREVIO%"
> "%SALIDA%" echo TAREA-18b parte 4 - %date% %time% - SIMULAR=%SIMULAR% - ID_C=%ID_C%

rem --- u4a) u4b) fecha F y actividades -------------------------------------
call :http u4a "GET edicion del Id %ID_C% - para elegir F" GET "proyectos/%ID_C%/edicion" gestor || exit /b 1
call :elegir_f u4a
>> "%SALIDA%" echo Fecha elegida: F=%F% back=%BACK% fin del back=%BACK_FIN% descanso=%BACK_DESCANSO% (hoy=%HOY_CALC%)
if not defined F (
  >> "%SALIDA%" echo CONTROL u4a - no hay F mayor o igual a hoy en la que un back quede recortado: NO se escribio nada.
  echo No hay una fecha F valida: NO se escribio nada. Revise %PREVIO%.
  exit /b 1
)

call :http u4b "GET cabecera del Id %ID_C% - actividad vigente en F" GET "proyectos/%ID_C%/cabecera" gestor || exit /b 1
set ACT_ACTUAL=
set ACT_NUEVA=
for /f "usebackq tokens=1,2 delims==" %%a in (`powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\u4b.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { exit }; $c=$t.Substring($i) | ConvertFrom-Json; $f=[datetime]$env:F; $v=@($c.actividades | Where-Object { [datetime]$_.fechaInicio -le $f -and [datetime]$_.fechaFin -ge $f } | Sort-Object version -Descending); if ($v.Count -gt 0) { 'ACT_ACTUAL=' + $v[0].codigo; 'ACT_NUEVA=' + $(if ($v[0].codigo -eq 'DEV.01') { 'DEV.02' } else { 'DEV.01' }) }"`) do set %%a=%%b
>> "%SALIDA%" echo Actividad vigente en F: %ACT_ACTUAL%; actividad nueva: %ACT_NUEVA%
if not defined ACT_NUEVA (
  >> "%SALIDA%" echo CONTROL u4b - no hay actividad vigente en F: NO se escribio nada.
  echo No hay actividad vigente en F: NO se escribio nada. Revise %PREVIO%.
  exit /b 1
)

powershell -NoProfile -Command "[IO.File]::WriteAllText((Join-Path (Get-Location) 'tmp\u4-actividad.json'), (@{ actividad = @{ actividadId = $env:ACT_NUEVA; desde = $env:F } } | ConvertTo-Json)); [IO.File]::WriteAllText((Join-Path (Get-Location) 'tmp\u4-fin.json'), (@{ fechaFin = $env:F } | ConvertTo-Json))"

rem --- u4c) u4d) validacion con vistas previas (no escriben) ---------------
call :http u4c "Vista previa: %ACT_NUEVA% desde %F% - esperado 200 CAMBIO_ACTIVIDAD" POST "proyectos/%ID_C%/cabecera/previsualizar" gestor u4-actividad.json || exit /b 1
call :http u4d "Vista previa: acortar a %F% - esperado 200 con %BACK% recortado y diasAgregados" POST "proyectos/%ID_C%/cabecera/previsualizar" gestor u4-fin.json || exit /b 1
set VALIDO=
for /f "usebackq delims=" %%c in (`powershell -NoProfile -Command "$ok=$true; foreach ($n in 'u4c','u4d') { $t=[IO.File]::ReadAllText('tmp\' + $n + '.txt'); if ($t.Split([char]10)[0] -notmatch ' 200 ') { $ok=$false; continue }; $j=$t.Substring($t.IndexOf('{')) | ConvertFrom-Json; if ($n -eq 'u4c' -and $j.tipoEtapa -ne 'CAMBIO_ACTIVIDAD') { $ok=$false }; if ($n -eq 'u4d' -and (@($j.personalRecortado | Where-Object { $_.empleado.codigoEkon -eq $env:BACK }).Count -eq 0 -or @($j.diasAgregados).Count -eq 0)) { $ok=$false } }; if ($ok) { '1' } else { '0' }"`) do set VALIDO=%%c
>> "%SALIDA%" echo CONTROL u4c/u4d - vistas previas validas: %VALIDO% (1 = se registra)
if not "%VALIDO%"=="1" (
  echo Las vistas previas no son validas: NO se escribio nada. Revise %PREVIO%.
  exit /b 1
)

rem --- desde aqui se escribe -------------------------------------------------
copy /y "%PREVIO%" "resultado-parte4.txt" > nul
set "SALIDA=resultado-parte4.txt"

call :http u4e "REGISTRAR %ACT_NUEVA% desde %F% - esperado 200 (etapa CAMBIO_ACTIVIDAD con %ACT_NUEVA%)" POST "proyectos/%ID_C%/cabecera" gestor u4-actividad.json || exit /b 1
call :control_registro u4e
call :http u4f "REGISTRAR acortar a %F% - esperado 200 (H15: diasAgregados insertados)" POST "proyectos/%ID_C%/cabecera" gestor u4-fin.json || exit /b 1
call :control_registro u4f

rem --- u4g) u4h) comprobaciones --------------------------------------------
call :http u4g "GET detalle del Id %ID_C%" GET "proyectos/%ID_C%" gestor || exit /b 1
call :http u4h "GET cabecera del Id %ID_C%" GET "proyectos/%ID_C%/cabecera" gestor || exit /b 1
powershell -NoProfile -Command "$d=[IO.File]::ReadAllText('tmp\u4g.txt'); $c=[IO.File]::ReadAllText('tmp\u4h.txt'); $d=$d.Substring($d.IndexOf('{')) | ConvertFrom-Json; $c=$c.Substring($c.IndexOf('{')) | ConvertFrom-Json; $e=@($d.etapas | Sort-Object version | Select-Object -Last 2); $x=if ($c.actividadVigente) { $c.actividadVigente.codigo + ' v' + $c.actividadVigente.version } else { 'ninguna' }; $y=if ($d.actividadVigente) { $d.actividadVigente.actividadCodigo + ' v' + $d.actividadVigente.version } else { 'ninguna' }; 'CONTROL u4g - etapas nuevas: ' + (@($e | ForEach-Object { 'v' + $_.version + ' ' + $_.tipoMovimiento + ' ' + $_.fechaInicio + '..' + $_.fechaFin + ' corte ' + $_.fechaCorte + ' actividad ' + $_.actividadCodigo }) -join '; ') + ' (esperado CAMBIO_ACTIVIDAD con ' + $env:ACT_NUEVA + ', O2); fin del proyecto ' + $d.fechaFin + '; personal: ' + (@($d.personal | ForEach-Object { $_.rol + ' ' + $_.numero + ' ' + $_.empleado.codigoEkon + ' ' + $_.fechaInicio + '..' + $_.fechaFin }) -join '; '); 'CONTROL u4h O3 - cabecera: ' + $x + '; detalle: ' + $y + ' - ' + $(if ($x -eq $y) { 'IGUALES (esperado)' } else { 'DISTINTAS' })">> "%SALIDA%"
>> "%SALIDA%" echo Siguiente paso: ejecutar parte4-descansos.sql en SSMS (solo lectura). Esperado: DESCANSO de %BACK% despues de F=%F%.

echo.
echo Listo. Resultados en %SALIDA%. Revise las lineas CONTROL y ejecute parte4-descansos.sql en SSMS.
exit /b 0

rem ==========================================================================
rem :control_registro NOMBRE -> estado HTTP y version, o los errores
:control_registro
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\%~1.txt'); $s=$t.Split([char]10)[0].Trim(); $i=$t.IndexOf('{'); if ($i -lt 0) { 'CONTROL %~1 - ' + $s + ' - sin cuerpo JSON'; exit }; $j=$t.Substring($i) | ConvertFrom-Json; if ($j.errors) { 'CONTROL %~1 - ' + $s + ' - ' + (@($j.errors.PSObject.Properties | ForEach-Object { $_.Name + ': ' + ($_.Value -join ' / ') }) -join ' | ') } elseif ($j.version) { 'CONTROL %~1 - ' + $s + ' - id ' + $j.id + ', version ' + $j.version } else { 'CONTROL %~1 - ' + $s + ' - ' + $j.title }">> "%SALIDA%"
exit /b 0

rem :elegir_f NOMBRE -> F, BACK, BACK_FIN, BACK_DESCANSO, HOY_CALC (F vacio si no hay). Igual que parte3.cmd.
:elegir_f
set F=
set BACK=
set BACK_FIN=
set BACK_DESCANSO=
set HOY_CALC=
for /f "usebackq tokens=1,2 delims==" %%a in (`powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\%~1.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { exit }; $j=$t.Substring($i) | ConvertFrom-Json; $hoy=[datetime]::Today; if ($env:HOY) { $hoy=[datetime]::ParseExact($env:HOY,'yyyy-MM-dd',$null) }; 'HOY_CALC=' + $hoy.ToString('yyyy-MM-dd'); $ini=[datetime]$j.fechaInicio; $fin=[datetime]$j.fechaFin; foreach ($b in @($j.personal | Where-Object { $_.rol -eq 'BACK' })) { $bi=[datetime]$b.fechaInicio; $bf=[datetime]$b.fechaFin; $f=@($hoy, $bi, $ini | Sort-Object)[-1]; if ($f -lt $bf -and $f -lt $fin) { 'F=' + $f.ToString('yyyy-MM-dd'); 'BACK=' + $b.empleado.codigoEkon; 'BACK_FIN=' + $bf.ToString('yyyy-MM-dd'); 'BACK_DESCANSO=' + $b.diasDescanso; break } }"`) do set %%a=%%b
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
