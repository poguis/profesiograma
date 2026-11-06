@echo off
setlocal
rem TAREA-7d-1/7d-3 - prueba de humo de QA: solo GET anonimos con curl.exe, ver LEEME.md. Lo ejecuta el usuario.
rem Simulacion (sin red): set SIMULAR=1. URLs: QA_BASE (https, por defecto sdmapptest2:7083) y QA_HTTP. Certificado: QA_CA o QA_INSEGURO=1.
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0humo-qa.ps1"
exit /b %ERRORLEVEL%
