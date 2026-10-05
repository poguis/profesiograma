@echo off
setlocal
rem UTF-8 en la consola para que las tildes de las respuestas se guarden bien.
chcp 65001 > nul
rem ==========================================================================
rem TAREA-17 - Parte 4: limpieza del 06/10 del proyecto 9 (dias guardados por P13 antes de M4).
rem ESCRIBE UNA VEZ: registro SIN cambios (P1 TIPO_2 igual; el Back 2 ya es historico y no se envia).
rem Al registrar se borran los dias desde el corte y se regeneran con M4: el 06/10 queda solo PRINCIPAL.
rem   0) GET edicion: lee ID_P1 y el corte. Solo sigue si el corte es 2026-10-05 o 2026-10-06.
rem   1) R4: POST personal con parte3-p2.json - esperado 200 { id 9, version 3 }.
rem   2) GET edicion y GET detalle despues del registro.
rem   3) Vista previa de parte3 (d) con su linea CONTROL (debe quedar vacia).
rem Proteccion: no se ejecuta si ya existe resultado-parte4.txt.
rem Salida: resultado-parte4.txt
rem ==========================================================================
cd /d "%~dp0"
set BASE=https://localhost:7180
set ID_A=9
set SALIDA=resultado-parte4.txt
set CUERPO=parte3-p2.json

if exist "%SALIDA%" (
  echo Ya existe %SALIDA%: la parte 4 ya se ejecuto y el registro ya se hizo.
  echo Si de verdad quiere repetirla, borre %SALIDA% y vuelva a ejecutar.
  exit /b 1
)

call :verificar_api || exit /b 1
if not exist tmp mkdir tmp

rem --- 0) GET edicion: valores y condiciones, todavia sin escribir nada ------
curl -k -sS -i "%BASE%/api/proyectos/%ID_A%/edicion" -H "X-Dev-User: gestor" > tmp\r4-edicion-antes.txt 2>&1

set ID_P1=
set CORTE=
set P1_IGUAL=
set OTROS_VIGENTES=
for /f "usebackq tokens=1,2 delims==" %%a in (`powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\r4-edicion-antes.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { exit 1 }; $j=$t.Substring($i) | ConvertFrom-Json; $p1=$j.personal | Where-Object { $_.rol -eq 'PRINCIPAL' -and $_.numero -eq 1 }; $igual=($p1.clase -eq 'VIGENTE' -and $p1.empleado.id -eq 6 -and $p1.jornada -eq 'TIPO_2' -and $p1.fechaInicio -eq '2026-09-21' -and $p1.fechaFin -eq '2026-11-29'); 'ID_P1=' + $p1.id; 'CORTE=' + $j.corte; 'P1_IGUAL=' + $igual; 'OTROS_VIGENTES=' + @($j.personal | Where-Object { $_.clase -eq 'VIGENTE' -and $_.id -ne $p1.id }).Count"`) do set %%a=%%b

if not defined ID_P1 goto :sin_lectura
if not defined CORTE goto :sin_lectura
if "%CORTE%"=="2026-10-05" goto :corte_ok
if "%CORTE%"=="2026-10-06" goto :corte_ok
powershell -NoProfile -Command "[Console]::OutputEncoding=[Text.Encoding]::UTF8; 'El 06/10 ya qued' + [char]0xF3 + ' antes del corte; no hace falta limpiar.'"
echo Corte leido: %CORTE%. No se registro nada ni se creo %SALIDA%.
exit /b 0

:corte_ok
if not "%P1_IGUAL%"=="True" goto :no_igual
if not "%OTROS_VIGENTES%"=="0" goto :no_igual

rem --- desde aqui se escribe -------------------------------------------------
rem La redireccion va al inicio: "9>" o "0>>" al final de la linea se leen como redireccion del flujo 9 o 0.
> "%SALIDA%" echo TAREA-17 parte 4 - %date% %time% - ID_A=%ID_A%
>> "%SALIDA%" echo Valores leidos: ID_P1=%ID_P1% CORTE=%CORTE% P1_IGUAL=%P1_IGUAL% OTROS_VIGENTES=%OTROS_VIGENTES%
echo Valores leidos: ID_P1=%ID_P1% CORTE=%CORTE% P1_IGUAL=%P1_IGUAL% OTROS_VIGENTES=%OTROS_VIGENTES%
>> "%SALIDA%" echo.
>> "%SALIDA%" echo === P4a: GET edicion ANTES del registro - esperado 200 ===
type tmp\r4-edicion-antes.txt>> "%SALIDA%"
>> "%SALIDA%" echo.

rem --- 1) Registro sin cambios (unico paso que escribe) ----------------------
call :post R4 "REGISTRAR sin cambios: P1 TIPO_2 igual, sin backs - esperado 200 id 9, version 3" "proyectos/%ID_A%/personal" r4-registro || exit /b 1
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\r4-registro-respuesta.txt'); $s=$t.Split([char]10)[0].Trim(); $i=$t.IndexOf('{'); if ($i -lt 0) { 'CONTROL R4 - ' + $s + ' - sin cuerpo JSON'; exit }; $j=$t.Substring($i) | ConvertFrom-Json; 'CONTROL R4 - ' + $s + ' - id ' + $j.id + ', version ' + $j.version + ' (esperado 200, id 9, version 3)'">> "%SALIDA%"

rem --- 2) Comprobacion con GET --------------------------------------------------
call :get P4b "GET edicion DESPUES del registro - esperado 200, mismo personal" "proyectos/%ID_A%/edicion" r4-edicion-despues
call :get P4c "GET detalle DESPUES del registro - esperado etapa v3 ACTUALIZACION_PERSONAL" "proyectos/%ID_A%" r4-detalle
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\r4-detalle-respuesta.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { 'CONTROL P4c - sin cuerpo JSON'; exit }; $j=$t.Substring($i) | ConvertFrom-Json; $e=$j.etapas | Sort-Object version | Select-Object -Last 1; 'CONTROL P4c - ultima etapa: v' + $e.version + ' ' + $e.tipoMovimiento + ' ' + $e.estado + ', corte ' + $e.fechaCorte + ' (esperado v3 ACTUALIZACION_PERSONAL ACTIVO, corte %CORTE%); etapas: ' + @($j.etapas).Count + '; personal: ' + (@($j.personal | ForEach-Object { $_.rol + ' ' + $_.numero + ' ' + $_.empleado.codigoEkon }) -join ', ')">> "%SALIDA%"

rem --- 3) Vista previa de parte3 (d): control de superposicion -----------------
call :post P4d "Vista previa P1 TIPO_2 sin cambios, como parte3 d - esperado 200 y CONTROL vacio" "proyectos/%ID_A%/personal/previsualizar" r4-vista || exit /b 1
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\r4-vista-respuesta.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { 'CONTROL P4d - sin cuerpo JSON'; exit }; $j=$t.Substring($i) | ConvertFrom-Json; if (-not $j.tramos) { 'CONTROL P4d - no es una vista previa: ' + $j.title; exit }; $c=[datetime]$j.corte; $m=@{}; foreach ($x in $j.tramos) { $f=[datetime]$x.inicio; while ($f -le [datetime]$x.fin) { $k=$x.codigoEkon + ' ' + $f.ToString('yyyy-MM-dd'); if (-not $m.ContainsKey($k)) { $m[$k]=@() }; if ($m[$k] -notcontains $x.rol) { $m[$k]+=$x.rol }; $f=$f.AddDays(1) } }; $dobles=@($m.Keys | Where-Object { $m[$_].Count -gt 1 } | Sort-Object); $desde=@($dobles | Where-Object { [datetime]($_.Split(' ')[1]) -ge $c }); $antes=@($dobles | Where-Object { [datetime]($_.Split(' ')[1]) -lt $c }); 'CONTROL P4d - fechas desde el corte ' + $j.corte + ' con dos roles distintos del mismo empleado (debe quedar vacio): ' + ($desde -join ', '); 'INFO P4d - fechas ANTERIORES al corte con dos roles (debe quedar vacio): ' + ($antes -join ', ')">> "%SALIDA%"

echo.
echo Listo. Resultados en %SALIDA%. Revise las lineas CONTROL e INFO.
exit /b 0

:sin_lectura
echo No se pudo leer el GET edicion del proyecto %ID_A%. Respuesta en tmp\r4-edicion-antes.txt.
echo No se registro nada ni se creo %SALIDA%.
exit /b 1

:no_igual
echo El personal vigente no es el esperado: P1_IGUAL=%P1_IGUAL% OTROS_VIGENTES=%OTROS_VIGENTES%.
echo Se esperaba solo P1 DEV006 TIPO_2 del 2026-09-21 al 2026-11-29. Respuesta en tmp\r4-edicion-antes.txt.
echo No se registro nada ni se creo %SALIDA%.
exit /b 1

rem ==========================================================================
rem :post caso "descripcion" "ruta despues de /api/" nombre -> tmp\nombre-respuesta.txt (y copia en la salida)
:post
call :preparar || exit /b 1
>> "%SALIDA%" echo.
>> "%SALIDA%" echo === %~1: %~2 ===
echo === %~1: %~2 ===
curl -k -sS -i -X POST "%BASE%/api/%~3" -H "X-Dev-User: gestor" -H "Content-Type: application/json" --data-binary "@tmp\%CUERPO%" > "tmp\%~4-respuesta.txt" 2>&1
type "tmp\%~4-respuesta.txt">> "%SALIDA%"
>> "%SALIDA%" echo.
exit /b 0

rem :get caso "descripcion" "ruta despues de /api/" nombre -> tmp\nombre-respuesta.txt (y copia en la salida)
:get
>> "%SALIDA%" echo.
>> "%SALIDA%" echo === %~1: %~2 ===
echo === %~1: %~2 ===
curl -k -sS -i "%BASE%/api/%~3" -H "X-Dev-User: gestor" > "tmp\%~4-respuesta.txt" 2>&1
type "tmp\%~4-respuesta.txt">> "%SALIDA%"
>> "%SALIDA%" echo.
exit /b 0

rem :preparar -> tmp\parte3-p2.json con el marcador reemplazado (UTF-8 sin BOM)
:preparar
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('%CUERPO%'); $t=$t.Replace('__ID_P1__','%ID_P1%'); [IO.File]::WriteAllText((Join-Path (Get-Location) 'tmp\%CUERPO%'), $t)"
if errorlevel 1 (
  echo No se pudo preparar %CUERPO%.
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
