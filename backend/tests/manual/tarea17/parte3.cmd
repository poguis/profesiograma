@echo off
setlocal
rem UTF-8 en la consola para que las tildes de las respuestas se guarden bien.
chcp 65001 > nul
rem ==========================================================================
rem TAREA-17 - Parte 3, despues de las correcciones de P8 y M4.
rem SOLO GET y vistas previas: NADA escribe. Se puede ejecutar varias veces.
rem   a) GET edicion del proyecto 9: lee ID_P1, ID_P2, corte y fechaFinMinima.
rem   b) P3 con FechaFin = fechaFinMinima: esperado 200, P1 MODIFICADO, sin dias suyos desde el corte.
rem   c) P8 de nuevo: esperado 400 con un solo error, principales[1].id.
rem   d) P1 TIPO_2 sin cambios: sin PRINCIPAL y DESCANSO del mismo empleado en la misma fecha.
rem Salida: resultado-parte3.txt (cada ejecucion la reemplaza).
rem ==========================================================================
cd /d "%~dp0"
set BASE=https://localhost:7180
set ID_A=9
set SALIDA=resultado-parte3.txt

call :verificar_api || exit /b 1
if not exist tmp mkdir tmp

rem La redireccion va al inicio: "9>" o "0>>" al final de la linea se leen como redireccion del flujo 9 o 0.
> "%SALIDA%" echo TAREA-17 parte 3 - %date% %time% - ID_A=%ID_A%

rem --- a) GET edicion ------------------------------------------------------
echo.>> "%SALIDA%"
echo === P1c: GET edicion del proyecto %ID_A% - esperado 200 ===>> "%SALIDA%"
echo === P1c: GET edicion del proyecto %ID_A% - esperado 200 ===
curl -k -sS -i "%BASE%/api/proyectos/%ID_A%/edicion" -H "X-Dev-User: gestor" > tmp\edicion-respuesta.txt 2>&1
type tmp\edicion-respuesta.txt>> "%SALIDA%"
echo.>> "%SALIDA%"

set ID_P1=
set ID_P2=
set CORTE=
set FIN_MINIMA=
set OTROS_VIGENTES=
for /f "usebackq tokens=1,2 delims==" %%a in (`powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\edicion-respuesta.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { exit 1 }; $j=$t.Substring($i) | ConvertFrom-Json; $p1=$j.personal | Where-Object { $_.rol -eq 'PRINCIPAL' -and $_.numero -eq 1 }; $p2=$j.personal | Where-Object { $_.rol -eq 'PRINCIPAL' -and $_.numero -eq 2 }; 'ID_P1=' + $p1.id; 'ID_P2=' + $p2.id; 'CORTE=' + $j.corte; 'FIN_MINIMA=' + $p1.permisos.fechaFinMinima; 'OTROS_VIGENTES=' + @($j.personal | Where-Object { $_.clase -eq 'VIGENTE' -and $_.id -ne $p1.id }).Count"`) do set %%a=%%b

if not defined ID_P1 (
  echo No se pudo leer el GET edicion del proyecto %ID_A%. Revise %SALIDA%.
  exit /b 1
)
if not defined FIN_MINIMA (
  echo P1 no tiene fechaFinMinima: no es VIGENTE. Revise %SALIDA%.
  exit /b 1
)

>> "%SALIDA%" echo Valores leidos: ID_P1=%ID_P1% ID_P2=%ID_P2% CORTE=%CORTE% FIN_MINIMA=%FIN_MINIMA% OTROS_VIGENTES=%OTROS_VIGENTES%
echo Valores leidos: ID_P1=%ID_P1% ID_P2=%ID_P2% CORTE=%CORTE% FIN_MINIMA=%FIN_MINIMA% OTROS_VIGENTES=%OTROS_VIGENTES%
if not "%OTROS_VIGENTES%"=="0" (
  echo AVISO: hay personas vigentes ademas de P1; los cuerpos solo envian P1 y la respuesta puede incluir errores "Falta ...".>> "%SALIDA%"
  echo AVISO: hay personas vigentes ademas de P1. Revise %SALIDA%.
)

rem --- b) P3 con fechaFinMinima -------------------------------------------
call :post P3c "P1 con fin = fechaFinMinima %FIN_MINIMA% - esperado 200, P1 MODIFICADO y sin dias suyos desde el corte" parte3-p3.json p3 || exit /b 1
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\p3-respuesta.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { 'CONTROL P3c - sin cuerpo JSON'; exit }; $j=$t.Substring($i) | ConvertFrom-Json; if (-not $j.tramos) { 'CONTROL P3c - no es una vista previa: ' + $j.title; exit }; $c=[datetime]$j.corte; $n=@($j.tramos | Where-Object { $_.codigoEkon -eq 'DEV006' -and [datetime]$_.fin -ge $c }).Count; $a=($j.personal | Where-Object { $_.id -eq %ID_P1% }).accion; 'CONTROL P3c - accion de P1: ' + $a + ' (esperado MODIFICADO); tramos de DEV006 desde el corte ' + $j.corte + ': ' + $n + ' (esperado 0)'">> "%SALIDA%"

rem --- c) P8 de nuevo ------------------------------------------------------
call :post P8c "Enviar el historico P2 - esperado 400 con UN solo error: principales[1].id" parte3-p8.json p8 || exit /b 1
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\p8-respuesta.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { 'CONTROL P8c - sin cuerpo JSON'; exit }; $j=$t.Substring($i) | ConvertFrom-Json; $k=@($j.errors.PSObject.Properties.Name); 'CONTROL P8c - claves de error: ' + ($k -join ', ') + ' (esperado solo principales[1].id); cantidad: ' + $k.Count">> "%SALIDA%"

rem --- d) P1 TIPO_2 sin cambios: control de superposicion ------------------
call :post P2c "P1 TIPO_2 sin cambios - esperado 200 sin PRINCIPAL y DESCANSO del mismo empleado el mismo dia" parte3-p2.json p2 || exit /b 1
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\p2-respuesta.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { 'CONTROL P2c - sin cuerpo JSON'; exit }; $j=$t.Substring($i) | ConvertFrom-Json; if (-not $j.tramos) { 'CONTROL P2c - no es una vista previa: ' + $j.title; exit }; $c=[datetime]$j.corte; $m=@{}; foreach ($x in $j.tramos) { $f=[datetime]$x.inicio; while ($f -le [datetime]$x.fin) { $k=$x.codigoEkon + ' ' + $f.ToString('yyyy-MM-dd'); if (-not $m.ContainsKey($k)) { $m[$k]=@() }; if ($m[$k] -notcontains $x.rol) { $m[$k]+=$x.rol }; $f=$f.AddDays(1) } }; $dobles=@($m.Keys | Where-Object { $m[$_].Count -gt 1 } | Sort-Object); $desde=@($dobles | Where-Object { [datetime]($_.Split(' ')[1]) -ge $c }); $antes=@($dobles | Where-Object { [datetime]($_.Split(' ')[1]) -lt $c }); 'CONTROL P2c - fechas desde el corte ' + $j.corte + ' con dos roles distintos del mismo empleado (debe quedar vacio): ' + ($desde -join ', '); 'INFO P2c - fechas ANTERIORES al corte con dos roles (dias guardados antes de la correccion M4, no se regeneran): ' + ($antes -join ', ')">> "%SALIDA%"

echo.
echo Listo. Resultados en %SALIDA%. Revise las lineas CONTROL al final de cada caso.
exit /b 0

rem ==========================================================================
rem :post caso "descripcion" plantilla.json nombre -> tmp\nombre-respuesta.txt (y copia en la salida)
:post
call :preparar "%~3" || exit /b 1
echo.>> "%SALIDA%"
echo === %~1: %~2 ===>> "%SALIDA%"
echo === %~1: %~2 ===
curl -k -sS -i -X POST "%BASE%/api/proyectos/%ID_A%/personal/previsualizar" -H "X-Dev-User: gestor" -H "Content-Type: application/json" --data-binary "@tmp\%~3" > "tmp\%~4-respuesta.txt" 2>&1
type "tmp\%~4-respuesta.txt">> "%SALIDA%"
echo.>> "%SALIDA%"
exit /b 0

rem :preparar plantilla.json -> tmp\plantilla.json con los marcadores reemplazados (UTF-8 sin BOM)
:preparar
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('%~1'); $t=$t.Replace('__ID_P1__','%ID_P1%').Replace('__ID_P2__','%ID_P2%').Replace('__FIN_MINIMA__','%FIN_MINIMA%'); [IO.File]::WriteAllText((Join-Path (Get-Location) 'tmp\%~1'), $t)"
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
