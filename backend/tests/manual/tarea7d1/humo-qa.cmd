@echo off
setlocal
rem TAREA-7d-1 - prueba de humo de QA: solo GET anonimos con curl.exe, ver LEEME.md. Lo ejecuta el usuario.
rem Simulacion (sin red): set SIMULAR=1 antes de ejecutar. Otra URL: set QA_BASE=http://192.168.1.77:7082
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0humo-qa.ps1"
exit /b %ERRORLEVEL%
