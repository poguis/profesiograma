@echo off
setlocal
rem UTF-8 en la consola para que las tildes de las respuestas se guarden bien.
chcp 65001 > nul
rem ==========================================================================
rem TAREA-17 - Parte 1: P0 (crear el "Proyecto A") y P1 (GET edicion).
rem Ejecutar UNA sola vez: P0 crea un proyecto nuevo cada vez que se ejecuta.
rem Salida: resultado-parte1.txt (y los Id para parte2.cmd al final).
rem ==========================================================================
cd /d "%~dp0"
set BASE=https://localhost:7180
set SALIDA=resultado-parte1.txt

if exist "%SALIDA%" (
  echo Ya existe %SALIDA%: la parte 1 ya se ejecuto y P0 crearia OTRO proyecto.
  echo Si de verdad quiere repetirla, borre %SALIDA% y vuelva a ejecutar.
  exit /b 1
)

call :verificar_api || exit /b 1

echo TAREA-17 parte 1 - %date% %time%> "%SALIDA%"

rem --- P0 -------------------------------------------------------------------
echo.>> "%SALIDA%"
echo === P0: crear el proyecto A, gestor - esperado 201 ===>> "%SALIDA%"
echo === P0: crear el proyecto A, gestor - esperado 201 ===
curl -k -sS -i -X POST "%BASE%/api/proyectos" -H "X-Dev-User: gestor" -H "Content-Type: application/json" --data-binary "@p0-crear.json" > p0-respuesta.txt 2>&1
type p0-respuesta.txt>> "%SALIDA%"
echo.>> "%SALIDA%"

set ID_A=
for /f "usebackq delims=" %%i in (`powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('p0-respuesta.txt'); $i=$t.IndexOf('{'); if ($i -ge 0) { ($t.Substring($i) | ConvertFrom-Json).id }"`) do set ID_A=%%i
if not defined ID_A (
  echo P0 no devolvio un id. Revise %SALIDA%.
  exit /b 1
)
echo Proyecto A creado con id %ID_A%.

rem --- P1 -------------------------------------------------------------------
echo.>> "%SALIDA%"
echo === P1: GET edicion del proyecto A, id %ID_A% - esperado 200 ===>> "%SALIDA%"
echo === P1: GET edicion del proyecto A, id %ID_A% - esperado 200 ===
curl -k -sS -i "%BASE%/api/proyectos/%ID_A%/edicion" -H "X-Dev-User: gestor" > p1-respuesta.txt 2>&1
type p1-respuesta.txt>> "%SALIDA%"
echo.>> "%SALIDA%"

rem --- Valores para parte2.cmd ---------------------------------------------
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('p1-respuesta.txt'); $j=$t.Substring($t.IndexOf('{')) | ConvertFrom-Json; 'rem Copiar estas lineas al inicio de parte2.cmd:'; 'set ID_A=' + $j.id; foreach ($p in $j.personal) { if ($p.rol -eq 'PRINCIPAL') { $n = 'ID_P' + $p.numero } else { $n = 'ID_K' + $p.numero }; 'rem ' + $p.rol + ' ' + $p.numero + ' ' + $p.empleado.codigoEkon + ' ' + $p.clase; 'set ' + $n + '=' + $p.id }" > valores-parte2.txt
echo.>> "%SALIDA%"
echo === Valores para parte2.cmd ===>> "%SALIDA%"
type valores-parte2.txt>> "%SALIDA%"

echo.
echo === Valores para parte2.cmd ===
type valores-parte2.txt
echo.
echo Listo. Resultados en %SALIDA%. Complete ID_A, ID_P1, ID_P2 e ID_K1 en parte2.cmd con los valores de arriba.
exit /b 0

rem ==========================================================================
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
