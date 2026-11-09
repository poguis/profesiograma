@echo off
setlocal
rem TAREA-22c / 22c-b - 503 rapido con la base caida (ver LEEME.md). Lo ejecuta el usuario; Claude Code no lo ejecuta.
rem Levanta una instancia APARTE de la API (puerto 5181) con una cadena FICTICIA solo en este proceso.
echo ==============================================================================
echo  MODO REAL - base FICTICIA por variable de entorno; no toca PROFESIOGRAMA_DEV
echo ==============================================================================
echo.
cd /d "%~dp0"

set "CURL=%SystemRoot%\System32\curl.exe"
set "PUERTO=5181"
set "BASE=http://localhost:%PUERTO%"
set "TITULO=PROFESIOGRAMA-22C-%PUERTO%"
set "API=%~dp0..\..\..\src\App.Api"
set "TMPCOD=%TEMP%\tarea22c-codigo.txt"
set "CRITERIOS=resultado-criterios.txt"

netstat -ano | findstr /R /C:"TCP.*:%PUERTO% .*LISTENING" >nul
if not errorlevel 1 (
  echo El puerto %PUERTO% esta ocupado. Se aborta sin hacer nada.
  exit /b 1
)

echo Compilando la API en Release (no toca el bin\Debug de la API que ya esta corriendo)...
dotnet build "%API%" -c Release -v q -nologo
if errorlevel 1 (
  echo Fallo la compilacion. Se aborta.
  exit /b 1
)

rem Variables SOLO de este proceso y de sus hijos (setlocal). Nunca user-secrets.
set "ASPNETCORE_ENVIRONMENT=Development"
set "DatosPrueba__SembrarAlIniciar=false"
del "%CRITERIOS%" >nul 2>&1

rem Caso: nombre, servidor, BaseDatos__ForzarTcp, orden (ef = EF primero; diag = /api/health/db primero), criterio.
rem Primero los "despues" (con tcp:); al final los informativos, con IP nuevas (el sistema puede recordar fallos de Named Pipes).
call :caso 1-rechazo "127.0.0.1,1" true ef estricto
if errorlevel 1 exit /b 1
call :caso 2-no-enrutable "192.0.2.1" true ef estricto
if errorlevel 1 exit /b 1
call :caso 3-tcp-explicito "tcp:192.0.2.1" false ef estricto
if errorlevel 1 exit /b 1
call :caso 4-instancia "192.0.2.1\SSDEV" true ef instancia
if errorlevel 1 exit /b 1
call :caso 5-antes-no-enrutable "203.0.113.1" false ef info
if errorlevel 1 exit /b 1
call :caso 6-antes-instancia "203.0.113.2\SSDEV" false ef info
if errorlevel 1 exit /b 1
call :caso 7-diagnostico-primero "198.51.100.1" false diag info
if errorlevel 1 exit /b 1

del "%TMPCOD%" >nul 2>&1
echo.
echo ==== Criterios (tambien en %CRITERIOS%)
type "%CRITERIOS%"
echo.
echo Fin. Revise resultado-*-resumen.txt y los cuerpos resultado-*.txt (ver LEEME.md, "Que esperar").
exit /b 0

rem ---------------------------------------------------------------------------- un caso: %1 nombre, %2 servidor, %3 ForzarTcp, %4 orden, %5 criterio
:caso
set "CASO=%~1"
set "CRITERIO=%~5"
set "ConnectionStrings__Profesiograma=Server=%~2;Database=BD_FICTICIA_22C;User Id=usuario_ficticio;Password=clave_ficticia;TrustServerCertificate=true"
set "BaseDatos__ForzarTcp=%~3"
for %%v in (ef1 ef2 diag) do (
  set "MS_%%v=0"
  set "COD_%%v="
)
echo.
echo ---- Caso %CASO%: servidor FICTICIO %~2, ForzarTcp=%~3, primero %~4
del "resultado-%CASO%-*.txt" >nul 2>&1

start "%TITULO%" /min cmd /c "dotnet run --project "%API%" -c Release --no-build --no-launch-profile --urls %BASE% > "%~dp0resultado-%CASO%-api.txt" 2>&1"

set /a INTENTOS=0
:esperar
set /a INTENTOS+=1
"%CURL%" -s -o nul -w "%%{http_code}" "%BASE%/" > "%TMPCOD%" 2>nul
set "C="
set /p C=<"%TMPCOD%"
if "%C%"=="200" goto listo
if %INTENTOS% GEQ 120 (
  echo La API no respondio en 120 s. Revise resultado-%CASO%-api.txt.
  call :cerrar
  exit /b 1
)
timeout /t 1 /nobreak >nul
goto esperar

:listo
echo   API lista en %BASE% (intentos: %INTENTOS%).
call :pedir raiz GET / -
if /i "%~4"=="diag" call :pedir diag GET /api/health/db -
call :pedir ef1 GET /api/proyectos gestor
call :pedir ef2 GET /api/proyectos gestor
call :pedir prev POST /api/proyectos/previsualizar admin
if /i not "%~4"=="diag" call :pedir diag GET /api/health/db -

call :cerrar
timeout /t 3 /nobreak >nul
findstr /I /C:"BD_FICTICIA_22C" /C:"usuario_ficticio" /C:"clave_ficticia" "resultado-%CASO%-api.txt" >nul
if errorlevel 1 (
  echo   Log: OK, no aparece ningun dato de la cadena ficticia.
  echo log: OK sin datos de la cadena >> "resultado-%CASO%-resumen.txt"
) else (
  echo   ATENCION: el log de la API muestra datos de la cadena ficticia.
  echo log: ATENCION muestra datos de la cadena >> "resultado-%CASO%-resumen.txt"
)
call :evaluar
exit /b 0

rem ---------------------------------------------------------------------------- %1 nombre, %2 metodo, %3 ruta, %4 usuario o -
:pedir
set "SAL=resultado-%CASO%-%~1.txt"
set "USR="
if not "%~4"=="-" set USR=-H "X-Dev-User: %~4"
set "CUERPO="
if /i "%~2"=="POST" set CUERPO=-H "Content-Type: application/json" -d "{}"
"%CURL%" -s -i -X %~2 %USR% %CUERPO% -o "%SAL%" -w "%%{http_code} %%{time_total}" "%BASE%%~3" > "%TMPCOD%" 2>nul
set "R="
set /p R=<"%TMPCOD%"
set "COD="
set "T=0.000"
for /f "tokens=1,2" %%a in ("%R%") do (
  set "COD=%%a"
  set "T=%%b"
)
rem Milisegundos sin aritmetica decimal: "5.018234" -> 5*1000 + 018 (el "1" delante evita la base octal de set /a).
set "S=0"
set "F=000"
for /f "tokens=1,2 delims=." %%a in ("%T%") do (
  set "S=%%a"
  set "F=%%b"
)
set "F=%F%000"
set "F=%F:~0,3%"
set /a MS=S*1000+1%F%-1000
set "MS_%~1=%MS%"
set "COD_%~1=%COD%"
echo   %~1: %~2 %~3 -^> HTTP %COD% en %MS% ms
echo %~1: %~2 %~3 -^> HTTP %COD% en %MS% ms >> "resultado-%CASO%-resumen.txt"
exit /b 0

rem ---------------------------------------------------------------------------- criterio de aceptacion del caso
rem estricto: EF 1.a y diagnostico 503 en <= 7000 ms y EF 2.a <= 500 ms; instancia: EF 1.a 503 en <= 7000 ms; info: solo tiempos.
:evaluar
set "VEREDICTO=OK"
if /i "%CRITERIO%"=="info" (
  set "VEREDICTO=INFORMATIVO"
  goto evaluado
)
if not "%COD_ef1%"=="503" set "VEREDICTO=FALLA"
if %MS_ef1% GTR 7000 set "VEREDICTO=FALLA"
if /i "%CRITERIO%"=="instancia" goto evaluado
if not "%COD_diag%"=="503" set "VEREDICTO=FALLA"
if %MS_diag% GTR 7000 set "VEREDICTO=FALLA"
if %MS_ef2% GTR 500 set "VEREDICTO=FALLA"
:evaluado
echo   Criterio %CRITERIO%: %VEREDICTO% (EF 1.a %MS_ef1% ms, EF 2.a %MS_ef2% ms, diagnostico %MS_diag% ms)
echo %CASO%: %VEREDICTO% [%CRITERIO%] EF 1.a %MS_ef1% ms, EF 2.a %MS_ef2% ms, diagnostico %MS_diag% ms >> "%CRITERIOS%"
exit /b 0

rem ---------------------------------------------------------------------------- cierra SOLO la ventana de este script
:cerrar
taskkill /FI "WINDOWTITLE eq %TITULO%*" /T /F >nul 2>&1
exit /b 0
