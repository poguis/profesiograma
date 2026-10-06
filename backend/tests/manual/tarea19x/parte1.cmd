@echo off
setlocal
rem UTF-8 en la consola para que las tildes de las respuestas se guarden bien.
chcp 65001 > nul
rem ==========================================================================
rem TAREA-19x - Parte 1: token de concurrencia versionProyecto en la cabecera. ESCRIBE en el Id 12 (proyecto D de la
rem TAREA-19a). Ejecutar UNA sola vez.
rem   a) GET cabecera: V = versionProyecto, regreso de almuerzo actual y opciones. Elige R1 y R2 (posteriores a la salida,
rem      distintas entre si y de la actual). Si el Id 12 no esta ACTIVO o no hay R1/R2: termina SIN escribir.
rem   b) Vista previa con regreso R1 - esperado 200 y versionProyecto = V.
rem   c) Registrar SIN token y SIN cambios (regreso actual) - esperado 400 versionProyecto. Si el control fallara, el
rem      servidor responderia 400 "No hay cambios para registrar.": tampoco escribe.
rem      Si b) o c) no dan lo esperado: termina SIN escribir.
rem   d) REGISTRAR regreso R1 con el token V - esperado 200, version V+1 (escribe).
rem   e) REGISTRAR regreso R2 con el MISMO token V (ya viejo) - esperado 409 "El proyecto cambio; vuelve a cargarlo."
rem      (si el control fallara, escribiria la version V+2).
rem   f) GET cabecera - CONTROL: versionProyecto = V+1 y regreso R1.
rem Salida: resultado-parte1.txt (proteccion: si existe, no se ejecuta; se crea justo antes de d).
rem Modo simulacion: set SIMULAR=1 - no llama a curl; lee %SIMULACION%\NOMBRE.txt y usa BASE=https://localhost:1.
rem ==========================================================================
cd /d "%~dp0"
if not defined SIMULACION set "SIMULACION=simulacion"
if "%SIMULAR%"=="1" (set "BASE=https://localhost:1") else (set "BASE=https://localhost:7180")
if not defined ID_D set ID_D=12
set SALIDA=resultado-parte1.txt
set PREVIO=tmp\parte1-previo.txt

if exist "%SALIDA%" (
  echo Ya existe %SALIDA%: la parte 1 ya se ejecuto y el Id %ID_D% ya se modifico.
  echo Si de verdad quiere repetirla, borre %SALIDA% y vuelva a ejecutar.
  exit /b 1
)

if not "%SIMULAR%"=="1" call :verificar_api || exit /b 1
if not exist tmp mkdir tmp

rem Hasta el primer registro que puede escribir (d), la salida va a %PREVIO%.
set "SALIDA=%PREVIO%"
> "%SALIDA%" echo TAREA-19x parte 1 - %date% %time% - SIMULAR=%SIMULAR% - ID_D=%ID_D%

rem --- a) cabecera, token y regresos ----------------------------------------
call :http pa "GET cabecera del Id %ID_D% - token V y opciones de regreso" GET "proyectos/%ID_D%/cabecera" gestor || exit /b 1
set V=
set ESTADO=
set PUEDE=
set ACTUAL=
set R1=
set R2=
for /f "usebackq tokens=1,2 delims==" %%a in (`powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\pa.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { exit }; $c=$t.Substring($i) | ConvertFrom-Json; 'V=' + $c.versionProyecto; 'ESTADO=' + $c.estado; 'PUEDE=' + $c.puedeEditar; 'ACTUAL=' + $c.regresoAlmuerzo; $o=@($c.opcionesAlmuerzo.regreso | Where-Object { $_ -gt $c.salidaAlmuerzo -and $_ -ne $c.regresoAlmuerzo }); if ($o.Count -ge 2) { 'R1=' + $o[0]; 'R2=' + $o[1] }"`) do set %%a=%%b
>> "%SALIDA%" echo Datos: V=%V% estado=%ESTADO% puedeEditar=%PUEDE% regreso actual=%ACTUAL% R1=%R1% R2=%R2%
if not "%ESTADO%"=="ACTIVO" (
  >> "%SALIDA%" echo CONTROL a - el Id %ID_D% no esta ACTIVO: NO se escribio nada.
  echo El Id %ID_D% no esta ACTIVO: NO se escribio nada. Revise %PREVIO%.
  exit /b 1
)
if not defined V goto :sin_datos
if not defined R1 goto :sin_datos
if not defined R2 goto :sin_datos
if not defined ACTUAL goto :sin_datos

powershell -NoProfile -Command "$d=Join-Path (Get-Location) 'tmp'; [IO.File]::WriteAllText((Join-Path $d 'pb.json'), (@{ regresoAlmuerzo = $env:R1 } | ConvertTo-Json)); [IO.File]::WriteAllText((Join-Path $d 'pc.json'), (@{ regresoAlmuerzo = $env:ACTUAL } | ConvertTo-Json)); [IO.File]::WriteAllText((Join-Path $d 'pd.json'), (@{ regresoAlmuerzo = $env:R1; versionProyecto = [int]$env:V } | ConvertTo-Json)); [IO.File]::WriteAllText((Join-Path $d 'pe.json'), (@{ regresoAlmuerzo = $env:R2; versionProyecto = [int]$env:V } | ConvertTo-Json))"

rem --- b) c) no escriben -----------------------------------------------------
call :http pb "Vista previa: regreso %R1% - esperado 200 y versionProyecto = %V%" POST "proyectos/%ID_D%/cabecera/previsualizar" gestor pb.json || exit /b 1
call :http pc "Registrar SIN token y sin cambios - esperado 400 versionProyecto (no escribe)" POST "proyectos/%ID_D%/cabecera" gestor pc.json || exit /b 1
set VALIDO=
for /f "usebackq delims=" %%c in (`powershell -NoProfile -Command "$ok=$true; $b=[IO.File]::ReadAllText('tmp\pb.txt'); if ($b.Split([char]10)[0] -notmatch ' 200 ') { $ok=$false } else { $j=$b.Substring($b.IndexOf('{')) | ConvertFrom-Json; if ([string]$j.versionProyecto -ne $env:V) { $ok=$false } }; $c=[IO.File]::ReadAllText('tmp\pc.txt'); if ($c.Split([char]10)[0] -notmatch ' 400 ') { $ok=$false } else { $k=$c.Substring($c.IndexOf('{')) | ConvertFrom-Json; if (-not $k.errors.versionProyecto) { $ok=$false } }; if ($ok) { '1' } else { '0' }"`) do set VALIDO=%%c
>> "%SALIDA%" echo CONTROL b/c - vista previa con versionProyecto=%V% y 400 versionProyecto sin token: %VALIDO% (1 = se continua)
if not "%VALIDO%"=="1" (
  echo b o c no dieron lo esperado: NO se escribio nada. Revise %PREVIO%.
  exit /b 1
)

rem --- desde aqui se escribe -------------------------------------------------
copy /y "%PREVIO%" "resultado-parte1.txt" > nul
set "SALIDA=resultado-parte1.txt"

call :http pd "REGISTRAR regreso %R1% con token %V% - esperado 200, version V+1" POST "proyectos/%ID_D%/cabecera" gestor pd.json || exit /b 1
call :http pe "REGISTRAR regreso %R2% con el MISMO token %V% - esperado 409 El proyecto cambio; vuelve a cargarlo." POST "proyectos/%ID_D%/cabecera" gestor pe.json || exit /b 1
call :http pf "GET cabecera del Id %ID_D% - control final" GET "proyectos/%ID_D%/cabecera" gestor || exit /b 1
powershell -NoProfile -Command "$s=@{}; foreach ($n in 'pd','pe') { $s[$n]=([IO.File]::ReadAllText('tmp\' + $n + '.txt')).Split([char]10)[0].Trim() }; $d=[IO.File]::ReadAllText('tmp\pd.txt'); $vd=if ($d.IndexOf('{') -ge 0) { ($d.Substring($d.IndexOf('{')) | ConvertFrom-Json).version } else { '?' }; $e=[IO.File]::ReadAllText('tmp\pe.txt'); $te=if ($e.IndexOf('{') -ge 0) { ($e.Substring($e.IndexOf('{')) | ConvertFrom-Json).title } else { '?' }; $f=[IO.File]::ReadAllText('tmp\pf.txt'); $c=$f.Substring($f.IndexOf('{')) | ConvertFrom-Json; 'CONTROL d - ' + $s.pd + ' - version ' + $vd + ' (esperado ' + ([int]$env:V + 1) + ')'; 'CONTROL e - ' + $s.pe + ' - ' + $te + ' (esperado 409)'; 'CONTROL f - versionProyecto ' + $c.versionProyecto + ' (esperado ' + ([int]$env:V + 1) + '); regreso ' + $c.regresoAlmuerzo + ' (esperado ' + $env:R1 + ')'">> "%SALIDA%"

echo.
echo Listo. Resultados en %SALIDA%. Revise las lineas CONTROL.
exit /b 0

:sin_datos
>> "%SALIDA%" echo CONTROL a - faltan datos (token, regreso actual o dos regresos alternativos): NO se escribio nada.
echo Faltan datos en la cabecera del Id %ID_D%: NO se escribio nada. Revise %PREVIO%.
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
