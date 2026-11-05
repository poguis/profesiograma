@echo off
setlocal
rem TAREA-7d-1 - genera la publicacion de QA desde el ultimo commit (git archive), ver LEEME.md. Lo ejecuta el usuario.
rem Sin "chcp 65001": con esa pagina de codigos, PowerShell 5.1 puede fallar al escribir tildes en la consola.
cd /d "%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publicar-qa.ps1"
exit /b %ERRORLEVEL%
