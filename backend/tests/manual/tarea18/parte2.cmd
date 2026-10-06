@echo off
setlocal
rem UTF-8 en la consola para que las tildes de las respuestas se guarden bien.
chcp 65001 > nul
rem ==========================================================================
rem TAREA-18 - Parte 2: edicion de cabecera del proyecto C (lee valores-parte1.txt).
rem Ejecutarla el MISMO dia que parte1.cmd o el siguiente (el caso d adelanta el inicio a H+3, que debe ser >= hoy).
rem Escriben solo o), p) y q). Ejecutar UNA sola vez.
rem   a) GET cabecera de C; b) GET cabecera del Id 10 (inicio no editable) y del Id 2 (SUSPENDIDO).
rem   c) inicio despues del inicio de P1 - 400 (C2).   d) adelantar el inicio a H+3 - 200 (actividad v1 movida, P1).
rem   e) atrasar el inicio al inicio de P1 (H+8) - 200, la actividad v1 pasa a H+8 (P1).
rem   f) inicio anterior a hoy - 400.   g) fin anterior a hoy - 400 (C4, P2).
rem   h) acortar el fin a H+11 - 200 con recorte (C4).   i) ampliar el fin a H+60 - 200, actividad extendida (C3, H14).
rem   j) almuerzo fuera de rango y horario inactivo - 400 (C5).   k) horario 2 + almuerzo 12:00-13:00 - 200.
rem   l) misma actividad DEV.01 - 400 (C6).   m) cuerpo vacio - 200 con "No hay cambios." (C9).
rem   n) REGISTRAR sin cambios - 400 general (C9, no escribe).
rem   o) REGISTRAR ampliar a H+60 + horario 2 + almuerzo - 200 version 2 EDICION_CABECERA.
rem   p) REGISTRAR solo actividad DEV.02 desde H+8 - 200 version 3 CAMBIO_ACTIVIDAD (si DEV.02 no esta en el ERP: OMITIDO).
rem   q) REGISTRAR acortar a H+11 - 200 version 4 (3 si p se omitio) EDICION_CABECERA.
rem   r) GET cabecera y detalle de C.   s) personal sin cambios: vista previa con advertencia y registro 400 (C9).
rem   t) crear sin principales (vista previa) - 400 (C10).   u) cambio-estado del Id 2 a ACTIVO - un solo error (C11).
rem Salida: resultado-parte2.txt (proteccion: si existe, no se ejecuta; se crea justo antes de n).
rem Modo simulacion: set SIMULAR=1 - no llama a curl; lee %SIMULACION%\NOMBRE.txt y usa BASE=https://localhost:1.
rem ==========================================================================
cd /d "%~dp0"
if not defined SIMULACION set "SIMULACION=simulacion"
if "%SIMULAR%"=="1" (set "BASE=https://localhost:1") else (set "BASE=https://localhost:7180")
set SALIDA=resultado-parte2.txt
set VALORES=valores-parte1.txt
set PREVIO=tmp\parte2-previo.txt

if exist "%SALIDA%" (
  echo Ya existe %SALIDA%: la parte 2 ya se ejecuto y el proyecto C ya se modifico.
  echo Si de verdad quiere repetirla, borre %SALIDA% y vuelva a ejecutar.
  exit /b 1
)

if not exist "%VALORES%" (
  echo Falta %VALORES%: ejecute primero parte1.cmd.
  exit /b 1
)
for /f "usebackq tokens=1,2 delims==" %%a in ("%VALORES%") do set %%a=%%b
for %%v in (ID_C HOY_P1 INICIO P1_INICIO FIN EMP_P1 EMP_BACK ID_EMP_P1) do if not defined %%v (
  echo %VALORES% no tiene %%v.
  exit /b 1
)

if not "%SIMULAR%"=="1" call :verificar_api || exit /b 1
if not exist tmp mkdir tmp

rem --- Fechas de los casos (relativas a H de la parte 1; AYER relativo al dia de hoy) ---
set AYER=
for /f "usebackq tokens=1,2 delims==" %%a in (`powershell -NoProfile -Command "$h=[datetime]::ParseExact($env:HOY_P1,'yyyy-MM-dd',$null); $hoy=[datetime]::Today; if ($env:HOY) { $hoy=[datetime]::ParseExact($env:HOY,'yyyy-MM-dd',$null) }; $f='yyyy-MM-dd'; 'AYER=' + $hoy.AddDays(-1).ToString($f); 'INICIO_TARDE=' + $h.AddDays(9).ToString($f); 'INICIO_ANTES=' + $h.AddDays(3).ToString($f); 'CORTO=' + $h.AddDays(11).ToString($f); 'LARGO=' + $h.AddDays(60).ToString($f); 'DESDE=' + $h.AddDays(8).ToString($f)"`) do set %%a=%%b
if not defined AYER (
  echo No se pudieron calcular las fechas.
  exit /b 1
)

rem Hasta el primer registro (n), la salida va a %PREVIO%.
set "SALIDA=%PREVIO%"
> "%SALIDA%" echo TAREA-18 parte 2 - %date% %time% - SIMULAR=%SIMULAR%
>> "%SALIDA%" echo Valores: ID_C=%ID_C% H=%HOY_P1% INICIO=%INICIO% P1_INICIO=%P1_INICIO% FIN=%FIN% P1=%EMP_P1% back=%EMP_BACK%
>> "%SALIDA%" echo Fechas: AYER=%AYER% INICIO_TARDE=%INICIO_TARDE% INICIO_ANTES=%INICIO_ANTES% CORTO=%CORTO% LARGO=%LARGO% DESDE=%DESDE%
echo Valores: ID_C=%ID_C% INICIO=%INICIO% FIN=%FIN% CORTO=%CORTO% LARGO=%LARGO% DESDE=%DESDE%

rem --- a) y b) lecturas ----------------------------------------------------
call :http a "GET cabecera de C - esperado 200, todo editable" GET "proyectos/%ID_C%/cabecera" gestor || exit /b 1
call :control_cabecera a
call :http b1 "GET cabecera del Id 10 - esperado fechaInicioEditable false con motivo" GET "proyectos/10/cabecera" gestor || exit /b 1
call :control_cabecera b1
call :http b2 "GET cabecera del Id 2 - esperado puedeEditar false (SUSPENDIDO)" GET "proyectos/2/cabecera" gestor || exit /b 1
call :control_cabecera b2

rem --- c) a m) vistas previas ----------------------------------------------
call :vista c "Inicio despues del inicio de P1 (%INICIO_TARDE%) - esperado 400 fechaInicio (C2)" c-inicio-tarde.json || exit /b 1
call :vista d "Adelantar el inicio a %INICIO_ANTES% - esperado 200, actividad v1 MODIFICADA (P1)" d-adelantar.json || exit /b 1
call :vista e "Atrasar el inicio a %P1_INICIO% - esperado 200, actividad v1 desde %P1_INICIO% (P1)" e-atrasar.json || exit /b 1
call :vista f "Inicio anterior a hoy (%AYER%) - esperado 400 fechaInicio" f-inicio-pasado.json || exit /b 1
call :vista g "Fin anterior a hoy (%AYER%) - esperado 400 fechaFin (C4, P2)" g-fin-pasado.json || exit /b 1
call :vista h "Acortar el fin a %CORTO% - esperado 200 con recorte de P1, back y dias (C4)" h-acortar.json || exit /b 1
call :vista i "Ampliar el fin a %LARGO% - esperado 200, actividad extendida (C3, H14)" i-ampliar.json || exit /b 1
call :vista j1 "Salida a almuerzo 10:00 - esperado 400 salidaAlmuerzo (C5)" j1-almuerzo-malo.json || exit /b 1
call :vista j2 "Horario 3 (inactivo) - esperado 400 horarioCodigo (C5)" j2-horario-inactivo.json || exit /b 1
call :vista k "Horario 2 + almuerzo 12:00-13:00 - esperado 200 EDICION_CABECERA (C5)" k-horario-almuerzo.json || exit /b 1
call :vista l "Misma actividad DEV.01 desde %DESDE% - esperado 400 actividad.actividadId (C6)" l-misma-actividad.json || exit /b 1
call :vista m "Cuerpo vacio - esperado 200 con No hay cambios. (C9)" m-vacio.json || exit /b 1
call :vista pv "DEV.02 desde %DESDE% (vista previa) - esperado 200 CAMBIO_ACTIVIDAD; 400 si DEV.02 no esta en el ERP" p-actividad.json || exit /b 1
set P_DISPONIBLE=
for /f "usebackq delims=" %%c in (`powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\pv.txt'); if ($t.Split([char]10)[0] -match ' 200 ') { '1' } else { '0' }"`) do set P_DISPONIBLE=%%c

rem --- desde aqui se escribe (n no escribe, pero usa el endpoint de registro) ---
copy /y "%PREVIO%" "resultado-parte2.txt" > nul
set "SALIDA=resultado-parte2.txt"

call :registro n "REGISTRAR sin cambios - esperado 400 general No hay cambios para registrar. (C9)" m-vacio.json || exit /b 1
call :registro o "REGISTRAR ampliar a %LARGO% + horario 2 + almuerzo - esperado 200 version 2" o-registro.json || exit /b 1
if "%P_DISPONIBLE%"=="1" (
  call :registro p "REGISTRAR solo actividad DEV.02 desde %DESDE% - esperado 200 version 3 CAMBIO_ACTIVIDAD" p-actividad.json || exit /b 1
) else (
  >> "%SALIDA%" echo.
  >> "%SALIDA%" echo === p: OMITIDO - DEV.02 no esta en el ERP simulado; q sera la version 3 ===
  echo p: OMITIDO, DEV.02 no esta en el ERP simulado.
)
call :registro q "REGISTRAR acortar a %CORTO% - esperado 200 version 4, o 3 si p se omitio" h-acortar.json || exit /b 1

rem --- r) comprobaciones ---------------------------------------------------
call :http r1 "GET cabecera de C despues de los registros" GET "proyectos/%ID_C%/cabecera" gestor || exit /b 1
call :control_cabecera r1
call :http r2 "GET detalle de C despues de los registros" GET "proyectos/%ID_C%" gestor || exit /b 1
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\r2.txt'); $i=$t.IndexOf('{'); if ($i -lt 0) { 'CONTROL r2 - sin cuerpo JSON'; exit }; $j=$t.Substring($i) | ConvertFrom-Json; 'CONTROL r2 - ' + $j.fechaInicio + '..' + $j.fechaFin + ', horario ' + $j.horario.codigo + ', almuerzo ' + $j.almuerzo.salida + '-' + $j.almuerzo.regreso + '; etapas: ' + (@($j.etapas | Sort-Object version | ForEach-Object { 'v' + $_.version + ' ' + $_.tipoMovimiento + ' ' + $_.fechaInicio + '..' + $_.fechaFin + ' corte ' + $_.fechaCorte + ' ' + $_.actividadCodigo }) -join '; ') + '; personal: ' + (@($j.personal | ForEach-Object { $_.rol + ' ' + $_.numero + ' ' + $_.empleado.codigoEkon + ' ' + $_.fechaInicio + '..' + $_.fechaFin }) -join '; ')">> "%SALIDA%"

rem --- s) personal sin cambios (C9) ----------------------------------------
call :http s0 "GET edicion de C - para armar el cuerpo sin cambios" GET "proyectos/%ID_C%/edicion" gestor || exit /b 1
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\s0.txt'); $j=$t.Substring($t.IndexOf('{')) | ConvertFrom-Json; $v=@($j.personal | Where-Object { $_.clase -eq 'VIGENTE' }); $p=@($v | Where-Object { $_.rol -eq 'PRINCIPAL' } | ForEach-Object { [ordered]@{ clave = 'p' + $_.id; id = $_.id; empleadoId = $_.empleado.id; jornada = $_.jornada; fechaInicio = $_.fechaInicio; fechaFin = $_.fechaFin } }); $ids=@($v | Where-Object { $_.rol -eq 'PRINCIPAL' } | ForEach-Object { $_.id }); $b=@($v | Where-Object { $_.rol -eq 'BACK' } | ForEach-Object { $x=[ordered]@{ clave = 'k' + $_.id; id = $_.id; empleadoId = $_.empleado.id; tipoRegistro = $_.tipoRegistro; fechaInicio = $_.fechaInicio; fechaFin = $_.fechaFin; diasDescanso = $_.diasDescanso; observacion = $_.observacion }; if ($_.principalRelacionadoId) { if ($ids -contains $_.principalRelacionadoId) { $x.principalClave = 'p' + $_.principalRelacionadoId } else { $x.principalId = $_.principalRelacionadoId } }; $x }); $cuerpo=[ordered]@{ principales = $p; backs = $b }; [IO.File]::WriteAllText((Join-Path (Get-Location) 'tmp\s-personal.json'), ($cuerpo | ConvertTo-Json -Depth 5))"
>> "%SALIDA%" echo Cuerpo de s1/s2 (armado del GET edicion):
type tmp\s-personal.json>> "%SALIDA%"
>> "%SALIDA%" echo.
call :http s1 "POST personal/previsualizar sin cambios - esperado 200 con No hay cambios. (C9)" POST "proyectos/%ID_C%/personal/previsualizar" gestor s-personal.json || exit /b 1
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\s1.txt'); $s=$t.Split([char]10)[0].Trim(); $i=$t.IndexOf('{'); if ($i -lt 0) { 'CONTROL s1 - ' + $s; exit }; $j=$t.Substring($i) | ConvertFrom-Json; 'CONTROL s1 - ' + $s + ' - advertencias: ' + (@($j.advertencias) -join ' | ') + ' (esperado 200, No hay cambios.)'">> "%SALIDA%"
call :http s2 "POST personal sin cambios - esperado 400 general (C9, no escribe)" POST "proyectos/%ID_C%/personal" gestor s-personal.json || exit /b 1
call :control_errores s2

rem --- t) y u) -------------------------------------------------------------
call :preparar t-crear-sin-principales.json || exit /b 1
call :http t "Crear sin principales (vista previa) - esperado 400 principales (C10)" POST "proyectos/previsualizar" gestor t-crear-sin-principales.json || exit /b 1
call :control_errores t
call :preparar u-c11.json || exit /b 1
call :http u "cambio-estado/previsualizar del Id 2 a ACTIVO con fecha fuera de rango - esperado 400 solo estadoDestino (C11)" POST "proyectos/2/cambio-estado/previsualizar" gestor u-c11.json || exit /b 1
call :control_errores u

echo.
echo Listo. Resultados en %SALIDA%. Revise las lineas CONTROL.
exit /b 0

rem ==========================================================================
rem :vista NOMBRE "descripcion" plantilla.json -> POST cabecera/previsualizar y linea CONTROL
:vista
call :preparar %~3 || exit /b 1
call :http %~1 "%~2" POST "proyectos/%ID_C%/cabecera/previsualizar" gestor %~3 || exit /b 1
call :control_vista %~1
exit /b 0

rem :registro NOMBRE "descripcion" plantilla.json -> POST cabecera y linea CONTROL
:registro
call :preparar %~3 || exit /b 1
call :http %~1 "%~2" POST "proyectos/%ID_C%/cabecera" gestor %~3 || exit /b 1
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\%~1.txt'); $s=$t.Split([char]10)[0].Trim(); $i=$t.IndexOf('{'); if ($i -lt 0) { 'CONTROL %~1 - ' + $s + ' - sin cuerpo JSON'; exit }; $j=$t.Substring($i) | ConvertFrom-Json; if ($j.errors) { 'CONTROL %~1 - ' + $s + ' - ' + (@($j.errors.PSObject.Properties | ForEach-Object { $_.Name + ': ' + ($_.Value -join ' / ') }) -join ' | ') } elseif ($j.version) { 'CONTROL %~1 - ' + $s + ' - id ' + $j.id + ', version ' + $j.version } else { 'CONTROL %~1 - ' + $s + ' - ' + $j.title }">> "%SALIDA%"
exit /b 0

rem :control_vista NOMBRE -> estado, tipo de etapa, cambios, recorte, actividades y advertencias (o los errores)
:control_vista
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\%~1.txt'); $s=$t.Split([char]10)[0].Trim(); $i=$t.IndexOf('{'); if ($i -lt 0) { 'CONTROL %~1 - ' + $s + ' - sin cuerpo JSON'; exit }; $j=$t.Substring($i) | ConvertFrom-Json; if ($j.errors) { 'CONTROL %~1 - ' + $s + ' - ' + (@($j.errors.PSObject.Properties | ForEach-Object { $_.Name + ': ' + ($_.Value -join ' / ') }) -join ' | '); exit }; 'CONTROL %~1 - ' + $s + '; ' + $j.tipoEtapa + ' ' + $j.fechaInicioNueva + '..' + $j.fechaFinNueva + '; cambios: ' + (@($j.cambios | ForEach-Object { $_.campo + ' ' + $_.anterior + ' -> ' + $_.nuevo }) -join ', ') + '; recortados: ' + (@($j.personalRecortado | ForEach-Object { $_.empleado.codigoEkon + ' ' + $_.fechaFinAnterior + '->' + $_.fechaFinNueva }) -join ', ') + '; dias eliminados: ' + (@($j.diasEliminados | Measure-Object -Property cantidad -Sum).Sum) + '; actividades: ' + (@($j.actividades | ForEach-Object { $_.accion + ' v' + $_.version + ' ' + $_.codigo + ' ' + $_.fechaInicio + '..' + $_.fechaFin }) -join ', ') + '; advertencias: ' + (@($j.advertencias) -join ' | ')">> "%SALIDA%"
exit /b 0

rem :control_cabecera NOMBRE -> datos y permisos del GET cabecera
:control_cabecera
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\%~1.txt'); $s=$t.Split([char]10)[0].Trim(); $i=$t.IndexOf('{'); if ($i -lt 0) { 'CONTROL %~1 - ' + $s + ' - sin cuerpo JSON'; exit }; $j=$t.Substring($i) | ConvertFrom-Json; $p=$j.permisos; 'CONTROL %~1 - ' + $s + '; ' + $j.codigo + ' ' + $j.estado + ' puedeEditar ' + $j.puedeEditar + ' ' + $j.motivo + '; ' + $j.fechaInicio + '..' + $j.fechaFin + '; horario ' + $j.horario.codigo + '; almuerzo ' + $j.salidaAlmuerzo + '-' + $j.regresoAlmuerzo + '; inicioEditable ' + $p.fechaInicioEditable + ' ' + $p.motivoFechaInicio + '; fechaFinMinima ' + $p.fechaFinMinima + '; actividadEditable ' + $p.actividadEditable + '; actividades: ' + (@($j.actividades | ForEach-Object { 'v' + $_.version + ' ' + $_.tipoMovimiento + ' ' + $_.codigo + ' ' + $_.fechaInicio + '..' + $_.fechaFin }) -join '; ')">> "%SALIDA%"
exit /b 0

rem :control_errores NOMBRE -> linea CONTROL con el estado HTTP y los errores del ValidationProblem
:control_errores
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('tmp\%~1.txt'); $s=$t.Split([char]10)[0].Trim(); $i=$t.IndexOf('{'); if ($i -lt 0) { 'CONTROL %~1 - ' + $s + ' - sin cuerpo JSON'; exit }; $j=$t.Substring($i) | ConvertFrom-Json; $m=@($j.errors.PSObject.Properties | ForEach-Object { $_.Name + ': ' + ($_.Value -join ' / ') }); 'CONTROL %~1 - ' + $s + ' - ' + ($m -join ' | ')">> "%SALIDA%"
exit /b 0

rem :http NOMBRE "descripcion" METODO "ruta despues de /api/" usuario [cuerpo.json] -> tmp\NOMBRE.txt (y copia en la salida)
rem Con SIMULAR=1 NUNCA llama a curl: copia %SIMULACION%\NOMBRE.txt.
:http
>> "%SALIDA%" echo.
>> "%SALIDA%" echo === %~1: %~2 ===
echo === %~1: %~2 ===
if "%SIMULAR%"=="1" goto :http_simulado
if "%~6"=="" goto :http_sin_cuerpo
curl -k -sS -i -X %~3 "%BASE%/api/%~4" -H "X-Dev-User: %~5" -H "Content-Type: application/json" --data-binary "@tmp\%~6" > "tmp\%~1.txt" 2>&1
goto :http_fin
:http_sin_cuerpo
curl -k -sS -i -X %~3 "%BASE%/api/%~4" -H "X-Dev-User: %~5" > "tmp\%~1.txt" 2>&1
goto :http_fin
:http_simulado
if not exist "%SIMULACION%\%~1.txt" (
  echo SIMULAR: falta %SIMULACION%\%~1.txt
  exit /b 1
)
copy /y "%SIMULACION%\%~1.txt" "tmp\%~1.txt" > nul
:http_fin
type "tmp\%~1.txt">> "%SALIDA%"
>> "%SALIDA%" echo.
exit /b 0

rem :preparar plantilla.json -> tmp\plantilla.json con los marcadores reemplazados (UTF-8 sin BOM)
:preparar
powershell -NoProfile -Command "$t=[IO.File]::ReadAllText('%~1'); foreach ($v in 'INICIO','FIN','P1_INICIO','AYER','INICIO_TARDE','INICIO_ANTES','CORTO','LARGO','DESDE','ID_EMP_P1') { $t=$t.Replace('__' + $v + '__', [Environment]::GetEnvironmentVariable($v)) }; [IO.File]::WriteAllText((Join-Path (Get-Location) 'tmp\%~1'), $t)"
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
