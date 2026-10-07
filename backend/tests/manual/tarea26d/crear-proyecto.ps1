# ==========================================================================
# TAREA-26d-1 - V3: crear un proyecto con empleados REALES por codigoEkon (NUESTRA API, X-Dev-User: gestor).
#   1) POST /api/proyectos/previsualizar con crear-proyecto-1.json (copia de crear-proyecto.ejemplo.json completada
#      con datos ERP reales y codigoEkon reales). No escribe.
#   2) Solo con confirmacion explicita (escribir REGISTRAR, o set REGISTRAR=SI): POST /api/proyectos. ESCRIBE (proyecto
#      nuevo y alta puntual de los empleados en la tabla Empleado + su puesto en CargoInfor).
#   Muestra: codigo HTTP, tramos, cruces, advertencias, pares codigoEkon -> empleadoId de los tramos (negativo = persona
#   todavia sin fila: se dara de alta al registrar), errores 400 por campo y, al registrar, id y codigo del proyecto.
# Base: %API_BASE%. Salida: resultado-crear.txt. Simulacion: SIMULAR=1 (crear_previsualizar.json, crear_registrar.json;
#   en simulacion solo se "registra" con REGISTRAR=SI; sin preguntar).
# ==========================================================================
. (Join-Path $PSScriptRoot '..\tarea26a\comun.ps1')
if (-not $env:SIMULACION) { $script:CarpetaSimulacion = Join-Path $PSScriptRoot 'simulacion' }

$base = Base-Url $(if ($env:API_BASE) { $env:API_BASE } else { 'https://localhost:7180' })
$encabezados = @{ 'X-Dev-User' = 'gestor' }
if (-not $script:Simular) { Permitir-CertificadoLocal }

function Mostrar-Errores($Texto) {
    try {
        $p = Leer-Json $Texto
        Escribir ('   title: ' + (Texto-Linea $p['title']))
        if ($p['errors']) { foreach ($k in $p['errors'].Keys) { Escribir ("   {0}: {1}" -f $k, (Texto-Linea (@($p['errors'][$k]) -join ' '))) } }
    }
    catch { Escribir '   (respuesta sin ProblemDetails)' }
}

$archivo = Join-Path $PSScriptRoot 'crear-proyecto-1.json'
Iniciar-Salida (Join-Path $PSScriptRoot 'resultado-crear.txt')
try {
    Escribir '== V3 Crear proyecto con empleados reales (codigoEkon) =='
    if (-not (Test-Path $archivo)) {
        Escribir 'No existe crear-proyecto-1.json: copie crear-proyecto.ejemplo.json y complete los datos (ver LEEME.md).'
        exit 1
    }
    $cuerpo = [IO.File]::ReadAllText($archivo, [Text.Encoding]::UTF8)
    try { [void](Leer-Json $cuerpo) } catch { Escribir 'crear-proyecto-1.json no es JSON valido.'; exit 1 }

    $r = Solicitar -Metodo 'POST' -Base $base -Ruta 'api/proyectos/previsualizar' -Cuerpo $cuerpo -Encabezados $encabezados `
        -TimeoutSegundos 120 -NombreSimulacion 'crear_previsualizar'
    Escribir ('Vista previa: ' + (Describir-Respuesta $r))
    if ($r.Codigo -ne 200) {
        if ($r.Texto) { Mostrar-Errores $r.Texto }
        Escribir 'No se registra (la vista previa no fue 200).'
        exit 1
    }

    $p = Leer-Json $r.Texto
    Escribir ("   tramos = {0}; cruces = {1}; advertencias = {2}" -f @($p['tramos']).Count, @($p['cruces']).Count, @($p['advertencias']).Count)
    foreach ($a in @($p['advertencias'])) { Escribir ('   advertencia: ' + (Texto-Linea $a)) }
    foreach ($g in (@($p['tramos']) | Group-Object { "$($_['codigoEkon'])|$($_['empleadoId'])" })) {
        $partes = $g.Name -split '\|'
        Escribir ("   empleado {0} -> empleadoId {1}{2}" -f $partes[0], $partes[1], $(if ([int]$partes[1] -lt 0) { ' (sin fila: alta puntual al registrar)' } else { '' }))
    }
    if (@($p['cruces']).Count -gt 0) { Escribir 'Hay cruces: no se registra.'; exit 1 }

    $confirmado = $env:REGISTRAR -eq 'SI'
    if (-not $confirmado -and -not $script:Simular) {
        $respuesta = Read-Host 'Escriba REGISTRAR para guardar el proyecto en PROFESIOGRAMA_DEV (cualquier otra cosa cancela)'
        $confirmado = $respuesta -eq 'REGISTRAR'
    }
    if (-not $confirmado) { Escribir 'No se registro (sin confirmacion).'; exit 0 }

    $r = Solicitar -Metodo 'POST' -Base $base -Ruta 'api/proyectos' -Cuerpo $cuerpo -Encabezados $encabezados -TimeoutSegundos 120 `
        -NombreSimulacion 'crear_registrar'
    Escribir ('Registro: ' + (Describir-Respuesta $r))
    if ($r.Codigo -eq 201) {
        $c = Leer-Json $r.Texto
        Escribir ("   id = {0}; codigo = {1}" -f $c['id'], $c['codigo'])
    }
    elseif ($r.Texto) { Mostrar-Errores $r.Texto }
    Escribir 'Fin. Pegue en el chat resultado-crear.txt.'
}
finally { Cerrar-Salida }
