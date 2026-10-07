@echo off
setlocal
rem TAREA-26a - ver LEEME.md. Simulacion (sin red): set SIMULAR=1 antes de ejecutar.
rem Sin "chcp 65001": con esa pagina de codigos, PowerShell 5.1 puede fallar al escribir tildes en la consola.
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0conectividad.ps1"
exit /b %ERRORLEVEL%
