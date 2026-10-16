# ==========================================================================
# TAREA-26d-3 - Comparacion "guardado frente al ERP": GET /api/proyectos/{id}/personal/comparacion-erp (NO escribe).
#   set PROYECTO_IDS=15 17     (Id de proyectos, separados por espacio; por defecto 15)
#   set DEV_USER=gestor        (gestor por defecto; admin para ver los Id 3 y 5)
#   Muestra por proyecto: codigo HTTP, aviso general y, por persona, el codigo EKON y los TIPOS de aviso
#   (NO_ACTIVO, CAMBIO_NOMBRE, CAMBIO_CARGO, CAMBIO_DEPARTAMENTO). Nunca nombres, cargos ni departamentos.
# Salida: resultado-comparacion.txt. Simulacion: SIMULAR=1 (simulacion\comparacion_<id>.json + .codigo).
# ==========================================================================
. (Join-Path $PSScriptRoot '..\tarea26a\comun.ps1')
if (-not $env:SIMULACION) { $script:CarpetaSimulacion = Join-Path $PSScriptRoot 'simulacion' }

$base = Base-Url $(if ($env:API_BASE) { $env:API_BASE } else { 'https://localhost:7180' })
if (-not $script:Simular) { Permitir-CertificadoLocal }
$usuario = if ($env:DEV_USER) { $env:DEV_USER } else { 'gestor' }
$ids = @($(if ($env:PROYECTO_IDS) { $env:PROYECTO_IDS } else { '15' }) -split '[\s,;]+' | Where-Object { $_ -match '^\d+$' })

Iniciar-Salida (Join-Path $PSScriptRoot 'resultado-comparacion.txt')
try {
    Escribir "== TAREA-26d-3 Comparacion con el ERP (no escribe), usuario $usuario =="
    if ($ids.Count -eq 0) { Escribir 'Indique set PROYECTO_IDS=<id> [<id> ...] (ver LEEME.md).'; exit 1 }

    foreach ($id in $ids) {
        $r = Solicitar -Metodo 'GET' -Base $base -Ruta "api/proyectos/$id/personal/comparacion-erp" `
            -Encabezados @{ 'X-Dev-User' = $usuario } -TimeoutSegundos 120 -NombreSimulacion "comparacion_$id"
        Escribir ''
        Escribir "-- Proyecto $id`: $(Describir-Respuesta $r)"
        if ($r.Codigo -ne 200 -or -not $r.Texto) { continue }

        $c = Leer-Json $r.Texto
        $aviso = if ($c['avisoGeneral']) { Texto-Linea $c['avisoGeneral'] } else { '(ninguno)' }
        Escribir "   avisoGeneral: $aviso"
        Escribir "   comparadoUtc: $(if ($c['comparadoUtc']) { $c['comparadoUtc'] } else { '(no se comparo)' })"
        $personas = @($c['personas'])
        Escribir "   personas con avisos: $($personas.Count)"
        $conteo = @{}
        foreach ($p in $personas) {
            $tipos = @($p['avisos'] | ForEach-Object { $_['tipo'] })
            foreach ($t in $tipos) { $conteo[$t] = 1 + $(if ($conteo.ContainsKey($t)) { $conteo[$t] } else { 0 }) }
            Escribir ("     {0}: {1}" -f $p['codigoEkon'], ($tipos -join ', '))
        }
        foreach ($t in ($conteo.Keys | Sort-Object)) { Escribir ("   total {0}: {1}" -f $t, $conteo[$t]) }
    }
    Escribir ''
    Escribir 'Fin. Pegue en el chat resultado-comparacion.txt.'
}
finally { Cerrar-Salida }
