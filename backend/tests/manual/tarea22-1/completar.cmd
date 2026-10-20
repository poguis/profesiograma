@echo off
setlocal
rem ==========================================================================
rem TAREA-22-1 - M3/M4/M6/M7/M8: POST /api/empleados/completar-cedulas (ver LEEME.md). ESCRIBE en Empleado.Cedula.
rem   set DEV_USER=gestor     (por defecto admin; con gestor se espera 403)
rem   set API_BASE=https://localhost:7180
rem Imprime SOLO el codigo HTTP y el JSON de conteos (la respuesta no tiene cedulas ni codigos).
rem Simulacion: set SIMULAR=1 -> lee simulacion\completar_<usuario>.json (FICTICIO); nunca llama a curl.
rem curl con ruta completa: en este equipo NoDefaultCurrentDirectoryInExePath=1 (no depender del orden de busqueda).
rem ==========================================================================
cd /d "%~dp0"
if "%DEV_USER%"=="" set DEV_USER=admin
if "%API_BASE%"=="" set API_BASE=https://localhost:7180

if "%SIMULAR%"=="1" (
    echo MODO SIMULACION ^(SIMULAR=1^): sin llamadas de red; respuesta leida de simulacion\completar_%DEV_USER%.json
    if not exist "simulacion\completar_%DEV_USER%.json" (
        echo No existe simulacion\completar_%DEV_USER%.json
        exit /b 1
    )
    type "simulacion\completar_%DEV_USER%.json"
    echo.
    exit /b 0
)

echo MODO REAL: POST real a %API_BASE%/api/empleados/completar-cedulas como %DEV_USER% ^(SIMULAR no esta en 1^).
"%SystemRoot%\System32\curl.exe" -s -k -X POST -H "X-Dev-User: %DEV_USER%" -H "Content-Length: 0" ^
    -w "\nHTTP %%{http_code}\n" "%API_BASE%/api/empleados/completar-cedulas"
exit /b %ERRORLEVEL%
