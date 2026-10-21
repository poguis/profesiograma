@echo off
setlocal
rem TAREA-22-2 - descarga del reporte Excel: reporte.cmd ID DESDE HASTA (p. ej. reporte.cmd 14 2026-10-01 2026-10-31).
rem Ver LEEME.md. Simulacion (sin red, sin descarga): set SIMULAR=1 antes de ejecutar. Usuario: set DEV_USER=gestor (por defecto admin).
rem Sin "chcp 65001": con esa pagina de codigos, PowerShell 5.1 puede fallar al escribir tildes en la consola.
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0reporte.ps1" "%~1" "%~2" "%~3"
exit /b %ERRORLEVEL%
