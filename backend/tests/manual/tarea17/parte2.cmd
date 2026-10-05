@echo off
setlocal
rem UTF-8 en la consola para que las tildes de las respuestas se guarden bien.
chcp 65001 > nul
rem ==========================================================================
rem TAREA-17 - Parte 2: P2 a P15, en el orden de la tabla del reporte.
rem Completar con los valores que imprime parte1.cmd (valores-parte2.txt):
set ID_A=9
set ID_P1=14
set ID_P2=15
set ID_K1=16
rem Solo P13 escribe (va despues de todas las vistas previas); P14 va justo despues.
rem Ejecutar UNA sola vez: P13 modifica el proyecto A.
rem Salida: resultado-parte2.txt
rem ==========================================================================
cd /d "%~dp0"
set BASE=https://localhost:7180
set SALIDA=resultado-parte2.txt

for %%v in (ID_A ID_P1 ID_P2 ID_K1) do if not defined %%v (
  echo Falta completar %%v al inicio de parte2.cmd. Use los valores de valores-parte2.txt.
  exit /b 1
)

if exist "%SALIDA%" (
  echo Ya existe %SALIDA%: la parte 2 ya se ejecuto y P13 ya modifico el proyecto A.
  echo Si de verdad quiere repetirla, borre %SALIDA% y vuelva a ejecutar.
  exit /b 1
)

call :verificar_api || exit /b 1
if not exist tmp mkdir tmp

echo TAREA-17 parte 2 - %date% %time% - ID_A=%ID_A% ID_P1=%ID_P1% ID_P2=%ID_P2% ID_K1=%ID_K1%> "%SALIDA%"

rem --- Vistas previas y casos que no escriben -------------------------------
call :post P2  "Cambiar la jornada de P1 a TIPO_2, vista previa - esperado 200 sin cruces" p2.json "proyectos/%ID_A%/personal/previsualizar" || exit /b 1
call :post P3  "Acortar P1 al 01/10, vista previa - esperado 200" p3.json "proyectos/%ID_A%/personal/previsualizar" || exit /b 1
call :post P3b "P1 con fin 30/09 - esperado 400 principales[0].fechaFin" p3b.json "proyectos/%ID_A%/personal/previsualizar" || exit /b 1
call :post P4  "Omitir K1, vista previa - esperado 200 con K1 ELIMINADO" p4.json "proyectos/%ID_A%/personal/previsualizar" || exit /b 1
call :post P5  "Back nuevo DEV008 28/09-04/10, M1 - esperado 200 con advertencia" p5.json "proyectos/%ID_A%/personal/previsualizar" || exit /b 1
call :post P6a "Cruce historico propio, DEV006 29-30/09, vista previa - esperado 200 con 2 cruces HISTORICO" p6.json "proyectos/%ID_A%/personal/previsualizar" || exit /b 1
call :post P6b "Cruce historico propio, registro - esperado 409 con cruces" p6.json "proyectos/%ID_A%/personal" || exit /b 1
call :post P7a "Cruce externo, DEV005 05-06/10, vista previa - esperado 200 con 2 cruces EXTERNO" p7.json "proyectos/%ID_A%/personal/previsualizar" || exit /b 1
call :post P7b "Cruce externo, registro - esperado 409 con cruces" p7.json "proyectos/%ID_A%/personal" || exit /b 1
call :post P8  "Enviar el historico P2 - esperado 400 principales[1].id" p8.json "proyectos/%ID_A%/personal/previsualizar" || exit /b 1
call :post P9  "Cambiar el empleado de P1 - esperado 400 principales[0].empleadoId" p9.json "proyectos/%ID_A%/personal/previsualizar" || exit /b 1
call :post P10 "Proyecto SUSPENDIDO, Id 2, vista previa - esperado 400 proyecto" p10.json "proyectos/2/personal/previsualizar" || exit /b 1
call :get  P10b "GET edicion del Id 2 - esperado 200 con puedeEditar false" "proyectos/2/edicion" gestor
call :get  P11 "Gestor sobre el Id 5 del admin - esperado 404" "proyectos/5/edicion" gestor
call :get  P12 "Sin identidad, anonimo - esperado 401" "proyectos/%ID_A%/edicion" anonimo

rem --- Unico caso que escribe y su comprobacion -----------------------------
call :post P13 "REGISTRAR: P1 TIPO_2 + omitir K1 + back nuevo DEV008 - esperado 200 version 2" p13.json "proyectos/%ID_A%/personal" || exit /b 1
call :post P14 "Reenviar con el id de K1 ya eliminado - esperado 409 El proyecto cambio" p14.json "proyectos/%ID_A%/personal" || exit /b 1
call :get  P13b "Detalle del proyecto A despues del registro" "proyectos/%ID_A%" gestor

rem --- P15 ------------------------------------------------------------------
echo.>> "%SALIDA%"
echo === P15: proyecto ACTIVO con fin anterior al corte - no hay datos asi; cubierto por la prueba P15_ProyectoQueYaTermino_400 ===>> "%SALIDA%"
echo === P15: no ejecutable por HTTP, cubierto por prueba unitaria ===

echo.
echo Listo. Resultados en %SALIDA%.
exit /b 0

rem ==========================================================================
rem :post caso "descripcion" archivo.json "ruta despues de /api/"
:post
call :preparar "%~3" || exit /b 1
echo.>> "%SALIDA%"
echo === %~1: %~2 ===>> "%SALIDA%"
echo === %~1: %~2 ===
curl -k -sS -i -X POST "%BASE%/api/%~4" -H "X-Dev-User: gestor" -H "Content-Type: application/json" --data-binary "@tmp\%~3">> "%SALIDA%" 2>&1
echo.>> "%SALIDA%"
exit /b 0

rem :get caso "descripcion" "ruta despues de /api/" usuario
:get
echo.>> "%SALIDA%"
echo === %~1: %~2 ===>> "%SALIDA%"
echo === %~1: %~2 ===
curl -k -sS -i "%BASE%/api/%~3" -H "X-Dev-User: %~4">> "%SALIDA%" 2>&1
echo.>> "%SALIDA%"
exit /b 0

rem :preparar archivo.json -> tmp\archivo.json con los marcadores reemplazados (UTF-8 sin BOM)
:preparar
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('%~1'); $t=$t.Replace('__ID_P1__','%ID_P1%').Replace('__ID_P2__','%ID_P2%').Replace('__ID_K1__','%ID_K1%'); [IO.File]::WriteAllText((Join-Path (Get-Location) 'tmp\%~1'), $t)"
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
