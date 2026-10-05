@echo off
setlocal
rem UTF-8 en la consola para que las tildes de las respuestas se guarden bien.
chcp 65001 > nul
rem ==========================================================================
rem TAREA-17b - Parte 2: reactivacion del proyecto B (lee valores-parte1.txt).
rem Solo g) escribe (reactivar). Ejecutar UNA sola vez.
rem Empleados: los que eligio parte1.cmd (EMP_P1, EMP_BACK e Id en valores-parte1.txt); DEV005 para e) y f).
rem   a) GET reactivacion de B - 200, puedeReactivar, propuesta EMP_P1, fechaMinima F+1.
rem   b) GET reactivacion del Id 2 (SUSPENDIDO; solo lectura).
rem   c) Vista previa con R = F - 400 fecha (H9).
rem   d) Vista previa con R = H-1 - 200: advertencia de dias transcurridos (R3), Back 1 EMP_BACK HISTORICO sin
rem      descanso (pendiente 23), actividad DEV.01 de R a FIN (R8), principal nuevo numero 2 desde R.
rem   i) cambio-estado/previsualizar a ACTIVO - 400 "La reactivacion se registra con la opcion Reactivar."
rem   O5) GET detalle del Id 5 (admin): si DEV005 no trabaja en R, e) y f) quedan OMITIDO (sin datos).
rem   e) Vista previa con back nuevo DEV005 en R - 200 con cruce EXTERNO.
rem   f) Registro de e) - 409 con cruces (no escribe).
rem   g) REACTIVAR con R = H-1 - 200 { id B, ACTIVO, version 3 }.
rem   h) GET detalle, GET reactivacion, GET edicion de B y POST personal/previsualizar sin cambios (O6, H12).
rem   j) Repetir g) - 400 proyecto "Solo se puede reactivar un proyecto SUSPENDIDO (estado actual: ACTIVO)."
rem Salida: resultado-parte2.txt (proteccion: si existe, no se ejecuta).
rem Modo simulacion: set SIMULAR=1 - no llama a curl; lee %SIMULACION%\NOMBRE.txt y usa BASE=https://localhost:1.
rem ==========================================================================
cd /d "%~dp0"
if not defined SIMULACION set "SIMULACION=simulacion"
if "%SIMULAR%"=="1" (set "BASE=https://localhost:1") else (set "BASE=https://localhost:7180")
set SALIDA=resultado-parte2.txt
set VALORES=valores-parte1.txt
set PREVIO=tmp\parte2-previo.txt

if exist "%SALIDA%" (
  echo Ya existe %SALIDA%: la parte 2 ya se ejecuto y el proyecto B ya se reactivo.
  echo Si de verdad quiere repetirla, borre %SALIDA% y vuelva a ejecutar.
  exit /b 1
)

if not exist "%VALORES%" (
  echo Falta %VALORES%: ejecute primero parte1.cmd.
  exit /b 1
)
for /f "usebackq tokens=1,2 delims==" %%a in ("%VALORES%") do set %%a=%%b
for %%v in (ID_B F R FIN EMP_P1 EMP_BACK ID_EMP_P1) do if not defined %%v (
  echo %VALORES% no tiene %%v.
  exit /b 1
)

if not "%SIMULAR%"=="1" call :verificar_api || exit /b 1
if not exist tmp mkdir tmp

rem Hasta el registro g), la salida va a %PREVIO%.
set "SALIDA=%PREVIO%"
> "%SALIDA%" echo TAREA-17b parte 2 - %date% %time% - SIMULAR=%SIMULAR%
>> "%SALIDA%" echo Valores: ID_B=%ID_B% F=%F% R=%R% FIN=%FIN% EMP_P1=%EMP_P1% Id %ID_EMP_P1% EMP_BACK=%EMP_BACK% Id %ID_EMP_BACK% DEV005 Id %ID_DEV005%
echo Valores: ID_B=%ID_B% F=%F% R=%R% FIN=%FIN% P1=%EMP_P1% back=%EMP_BACK%

rem --- a) GET reactivacion de B --------------------------------------------
call :http a "GET reactivacion de B - esperado 200, puedeReactivar true, propuesta %EMP_P1%" GET "proyectos/%ID_B%/reactivacion" gestor || exit /b 1
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\a.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { 'CONTROL a - sin cuerpo JSON'; exit }; $j=$t.Substring($i) | ConvertFrom-Json; $p=$j.principalPropuesto; 'CONTROL a - puedeReactivar ' + $j.puedeReactivar + ', fechaMinima ' + $j.fechaMinima + ', propuesta ' + $p.empleado.codigoEkon + ' ' + $p.jornada + ' activo ' + $p.empleado.activo + ', advertencias ' + @($j.advertencias).Count + ' (esperado True, F+1, ' + $env:EMP_P1 + ' TIPO_2 activo True, 0)'">> "%SALIDA%"

rem --- b) GET reactivacion del Id 2 (solo lectura) -------------------------
call :http b "GET reactivacion del Id 2 (SUSPENDIDO) - esperado 200" GET "proyectos/2/reactivacion" gestor || exit /b 1

rem --- c) R = F ------------------------------------------------------------
call :preparar r-c.json || exit /b 1
call :http c "Vista previa con R = F - esperado 400 fecha (H9)" POST "proyectos/%ID_B%/reactivacion/previsualizar" gestor r-c.json || exit /b 1
call :control_errores c

rem --- d) R = H-1 ----------------------------------------------------------
call :preparar r-d.json || exit /b 1
call :http d "Vista previa con R = %R% - esperado 200 con advertencia R3, Back 1 historico y actividad nueva" POST "proyectos/%ID_B%/reactivacion/previsualizar" gestor r-d.json || exit /b 1
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\d.txt'); $s=$t.Split([char]10)[0].Trim(); $i=$t.IndexOf('{'); if ($i -lt 0) { 'CONTROL d - ' + $s + ' - sin cuerpo JSON'; exit }; $j=$t.Substring($i) | ConvertFrom-Json; $r=[datetime]$env:R; $k=$j.personal | Where-Object { $_.empleado.codigoEkon -eq $env:EMP_BACK }; $n=$j.personal | Where-Object { $_.clave -eq 'p1' }; $pt=@($j.tramos | Where-Object { $_.codigoEkon -eq $env:EMP_P1 -and $_.rol -eq 'PRINCIPAL' -and [datetime]$_.inicio -ge $r } | Sort-Object inicio); $a=$j.actividad; 'CONTROL d - ' + $s + '; corte ' + $j.corte + '; advertencias: ' + (@($j.advertencias) -join ' | ') + '; ' + $env:EMP_BACK + ' ' + $k.clase + ', tramos ' + $env:EMP_BACK + ' desde R: ' + @($j.tramos | Where-Object { $_.codigoEkon -eq $env:EMP_BACK -and [datetime]$_.fin -ge $r }).Count + '; nuevo ' + $n.clase + ' numero ' + $n.numero + ', primer PRINCIPAL desde ' + $pt[0].inicio + '; actividad ' + $a.codigo + ' ' + $a.fechaInicio + '..' + $a.fechaFin + '; cruces ' + @($j.cruces).Count + ' (esperado 200; R; Se generaran dias ya transcurridos.; HISTORICO, 0; NUEVO 2, R; DEV.01 R..FIN; 0)'">> "%SALIDA%"

rem --- i) cambio-estado a ACTIVO (vista previa, no escribe) ----------------
call :preparar i-cambio.json || exit /b 1
call :http i "cambio-estado/previsualizar a ACTIVO - esperado 400 estadoDestino con el mensaje nuevo" POST "proyectos/%ID_B%/cambio-estado/previsualizar" gestor i-cambio.json || exit /b 1
call :control_errores i

rem --- O5) DEV005 en el Id 5 ----------------------------------------------
call :http P5d "GET detalle del Id 5 (admin) - para decidir e) y f)" GET "proyectos/5" admin || exit /b 1
set DEV005_EN_R=
for /f "usebackq delims=" %%c in (`powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\P5d.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { '0'; exit }; $j=$t.Substring($i) | ConvertFrom-Json; $r=[datetime]$env:R; $x=@($j.personal | Where-Object { $_.empleado.codigoEkon -eq 'DEV005' -and $_.rol -eq 'PRINCIPAL' -and [datetime]$_.fechaInicio -le $r -and [datetime]$_.fechaFin -ge $r }); if ($j.estado.codigo -eq 'ACTIVO' -and $x.Count -gt 0) { '1' } else { '0' }"`) do set DEV005_EN_R=%%c
if not defined ID_DEV005 set DEV005_EN_R=0
>> "%SALIDA%" echo CONTROL O5 - Id de DEV005: %ID_DEV005% - DEV005 asignado como PRINCIPAL en el Id 5 ACTIVO en R=%R%: %DEV005_EN_R% (1 = se ejecutan e y f)
if not "%DEV005_EN_R%"=="1" goto :omitir_ef

rem --- e) cruce externo (vista previa) -------------------------------------
call :preparar r-e.json || exit /b 1
call :http e "Vista previa con back nuevo DEV005 en R - esperado 200 con cruce EXTERNO" POST "proyectos/%ID_B%/reactivacion/previsualizar" gestor r-e.json || exit /b 1
set EXTERNOS=
for /f "usebackq delims=" %%c in (`powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\e.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { '0'; exit }; $j=$t.Substring($i) | ConvertFrom-Json; @($j.cruces | Where-Object { $_.origen -eq 'EXTERNO' }).Count"`) do set EXTERNOS=%%c
if "%EXTERNOS%"=="0" (
  >> "%SALIDA%" echo CONTROL e - OMITIDO sin datos: R es dia de descanso de DEV005 en el Id 5; f tambien se omite
  goto :despues_ef
)
>> "%SALIDA%" echo CONTROL e - cruces EXTERNO: %EXTERNOS% (esperado 1 o mas)

rem --- f) registro con cruces (409, no escribe) ----------------------------
call :http f "REGISTRAR con el cruce de e - esperado 409 con cruces, no escribe" POST "proyectos/%ID_B%/reactivacion" gestor r-e.json || exit /b 1
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\f.txt'); $s=$t.Split([char]10)[0].Trim(); $i=$t.IndexOf('{'); if ($i -lt 0) { 'CONTROL f - ' + $s; exit }; $j=$t.Substring($i) | ConvertFrom-Json; 'CONTROL f - ' + $s + ' - ' + $j.title + ' - cruces ' + @($j.cruces).Count + ' (esperado 409 con cruces)'">> "%SALIDA%"
goto :despues_ef

:omitir_ef
>> "%SALIDA%" echo.
>> "%SALIDA%" echo === e: OMITIDO sin datos - DEV005 no tiene asignacion PRINCIPAL vigente en R en el Id 5 ===
>> "%SALIDA%" echo === f: OMITIDO sin datos ===
echo e y f: OMITIDO sin datos.

:despues_ef
rem --- desde aqui se escribe -------------------------------------------------
copy /y "%PREVIO%" "resultado-parte2.txt" > nul
set "SALIDA=resultado-parte2.txt"

rem --- g) Reactivar --------------------------------------------------------
call :http g "REACTIVAR B con R = %R% - esperado 200 id B, ACTIVO, version 3" POST "proyectos/%ID_B%/reactivacion" gestor r-d.json || exit /b 1
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\g.txt'); $s=$t.Split([char]10)[0].Trim(); $i=$t.IndexOf('{'); if ($i -lt 0) { 'CONTROL g - ' + $s + ' - sin cuerpo JSON'; exit }; $j=$t.Substring($i) | ConvertFrom-Json; 'CONTROL g - ' + $s + ' - id ' + $j.id + ', estado ' + $j.estado + ', version ' + $j.version + ' (esperado 200, ' + $env:ID_B + ', ACTIVO, 3)'">> "%SALIDA%"

rem --- h) Comprobaciones ---------------------------------------------------
call :http h1 "GET detalle de B despues de reactivar" GET "proyectos/%ID_B%" gestor || exit /b 1
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\h1.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { 'CONTROL h1 - sin cuerpo JSON'; exit }; $j=$t.Substring($i) | ConvertFrom-Json; $e=$j.etapas | Sort-Object version | Select-Object -Last 1; $a=$j.actividadVigente; 'CONTROL h1 - estado ' + $j.estado.codigo + ', fechaFin ' + $j.fechaFin + '; ultima etapa v' + $e.version + ' ' + $e.tipoMovimiento + ' ' + $e.estado + ' ' + $e.fechaInicio + '..' + $e.fechaFin + ' corte ' + $e.fechaCorte + ' actividad ' + $e.actividadCodigo + '; actividad vigente v' + $a.version + ' ' + $a.tipoMovimiento + ' ' + $a.actividadCodigo + ' ' + $a.fechaInicio + '..' + $a.fechaFin + '; principales: ' + (@($j.personal | Where-Object { $_.rol -eq 'PRINCIPAL' } | ForEach-Object { $_.numero.ToString() + ' ' + $_.empleado.codigoEkon + ' ' + $_.fechaInicio + '..' + $_.fechaFin + ' inicial ' + $_.esPrincipalInicial }) -join '; ') + ' (esperado ACTIVO, FIN; v3 REACTIVACION ACTIVO R..FIN corte R DEV.01; v2 REACTIVACION DEV.01 R..FIN; P1 y P2 iniciales)'">> "%SALIDA%"

call :http h2 "GET reactivacion de B - esperado puedeReactivar false" GET "proyectos/%ID_B%/reactivacion" gestor || exit /b 1
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\h2.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { 'CONTROL h2 - sin cuerpo JSON'; exit }; $j=$t.Substring($i) | ConvertFrom-Json; 'CONTROL h2 - puedeReactivar ' + $j.puedeReactivar + ' - ' + $j.motivo">> "%SALIDA%"

call :http h3 "GET edicion de B - esperado Back 1 %EMP_BACK% HISTORICO (H12)" GET "proyectos/%ID_B%/edicion" gestor || exit /b 1
set ID_P2=
set CLASE_K1=
for /f "usebackq tokens=1,2 delims==" %%a in (`powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\h3.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { exit }; $j=$t.Substring($i) | ConvertFrom-Json; 'ID_P2=' + ($j.personal | Where-Object { $_.rol -eq 'PRINCIPAL' -and $_.numero -eq 2 }).id; 'CLASE_K1=' + ($j.personal | Where-Object { $_.empleado.codigoEkon -eq $env:EMP_BACK }).clase"`) do set %%a=%%b
>> "%SALIDA%" echo CONTROL h3 - Id del principal nuevo: %ID_P2%; clase de %EMP_BACK%: %CLASE_K1% (esperado HISTORICO)
if not defined ID_P2 (
  echo No se pudo leer el principal nuevo del GET edicion. Revise %SALIDA%.
  exit /b 1
)

call :preparar h-personal.json || exit /b 1
call :http h4 "POST personal/previsualizar sin cambios - esperado 200, %EMP_BACK% HISTORICO, sin dias de %EMP_BACK% desde el corte" POST "proyectos/%ID_B%/personal/previsualizar" gestor h-personal.json || exit /b 1
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\h4.txt'); $s=$t.Split([char]10)[0].Trim(); $i=$t.IndexOf('{'); if ($i -lt 0) { 'CONTROL h4 - ' + $s + ' - sin cuerpo JSON'; exit }; $j=$t.Substring($i) | ConvertFrom-Json; if (-not $j.tramos) { 'CONTROL h4 - ' + $s + ' - no es una vista previa: ' + $j.title + ' ' + ($j.errors | ConvertTo-Json -Compress); exit }; $c=[datetime]$j.corte; $k=$j.personal | Where-Object { $_.empleado.codigoEkon -eq $env:EMP_BACK }; 'CONTROL h4 - ' + $s + '; corte ' + $j.corte + '; ' + $env:EMP_BACK + ' ' + $k.clase + '; tramos de ' + $env:EMP_BACK + ' con fin desde el corte: ' + @($j.tramos | Where-Object { $_.codigoEkon -eq $env:EMP_BACK -and [datetime]$_.fin -ge $c }).Count + '; cruces ' + @($j.cruces).Count + ' (esperado 200, HISTORICO, 0, 0)'">> "%SALIDA%"

rem --- j) Repetir g) -------------------------------------------------------
call :http j "Repetir el registro g - esperado 400 proyecto, ya esta ACTIVO" POST "proyectos/%ID_B%/reactivacion" gestor r-d.json || exit /b 1
call :control_errores j

echo.
echo Listo. Resultados en %SALIDA%. Revise las lineas CONTROL.
exit /b 0

rem ==========================================================================
rem :control_errores NOMBRE -> linea CONTROL con el estado HTTP y los errores del ValidationProblem
:control_errores
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\%~1.txt'); $s=$t.Split([char]10)[0].Trim(); $i=$t.IndexOf('{'); if ($i -lt 0) { 'CONTROL %~1 - ' + $s + ' - sin cuerpo JSON'; exit }; $j=$t.Substring($i) | ConvertFrom-Json; $m=@($j.errors.PSObject.Properties | ForEach-Object { $_.Name + ': ' + ($_.Value -join ' / ') }); 'CONTROL %~1 - ' + $s + ' - ' + ($m -join ' | ')">> "%SALIDA%"
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
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('%~1'); foreach ($v in 'FIN','F','R','ID_EMP_P1','ID_DEV005','ID_P2') { $t=$t.Replace('__' + $v + '__', [Environment]::GetEnvironmentVariable($v)) }; [IO.File]::WriteAllText((Join-Path (Get-Location) 'tmp\%~1'), $t)"
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
