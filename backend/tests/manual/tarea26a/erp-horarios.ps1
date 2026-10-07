# ==========================================================================
# TAREA-26a - A5 / pendiente 13: horarios del ERP real a traves de NUESTRA API (GET /api/erp/horarios, X-Dev-User: gestor).
#   Muestra codigo, tipo (tipoHorario M/D), entrada, salida, minutos de jornada y trabajados, y descripcion
#   (datos de catalogo, no personales), mas un resumen por tipo para deducir el significado de M y D.
# Requisitos: API corriendo en modo Http (ver LEEME.md). Base: %API_BASE% (por defecto https://localhost:7180).
# Salida: resultado-horarios.txt. Simulacion: SIMULAR=1 (api_erp_horarios.json).
# ==========================================================================
. (Join-Path $PSScriptRoot 'comun.ps1')

$base = Base-Url $(if ($env:API_BASE) { $env:API_BASE } else { 'https://localhost:7180' })
$encabezados = @{ 'X-Dev-User' = 'gestor' }
if (-not $script:Simular) { Permitir-CertificadoLocal }

Iniciar-Salida 'resultado-horarios.txt'
Escribir '== A5 Horarios del ERP (pendiente 13: tipoHorario) =='
$r = Solicitar -Metodo 'GET' -Base $base -Ruta 'api/erp/horarios' -Encabezados $encabezados
Escribir ('GET /api/erp/horarios: ' + (Describir-Respuesta $r))
if ($r.Codigo -ne 200) { Escribir 'Sin datos (revise que la API este en modo Http y corriendo).'; exit 1 }

$horarios = Leer-Json $r.Texto
Escribir "Horarios activos: $($horarios.Count)"
Escribir ''
Escribir 'codigo | tipo | entrada | salida | min jornada | min trabajados | descripcion'
foreach ($h in ($horarios | Sort-Object { $_['tipo'] }, { $_['codigo'] })) {
    Escribir ("{0} | {1} | {2} | {3} | {4} | {5} | {6}" -f $h['codigo'], $h['tipo'], $h['horaEntrada'], $h['horaSalida'],
        $h['minutosJornada'], $h['minutosTrabajados'], $h['descripcion'])
}
Escribir ''
Escribir 'Resumen por tipo (cantidad | min jornada minimo-maximo | min trabajados minimo-maximo)'
foreach ($g in ($horarios | Group-Object { if ($_['tipo']) { $_['tipo'] } else { '(vacio)' } })) {
    $jornada = $g.Group | Where-Object { $null -ne $_['minutosJornada'] } | ForEach-Object { [int]$_['minutosJornada'] } | Measure-Object -Minimum -Maximum
    $trabajo = $g.Group | Where-Object { $null -ne $_['minutosTrabajados'] } | ForEach-Object { [int]$_['minutosTrabajados'] } | Measure-Object -Minimum -Maximum
    Escribir ("  {0}: {1} | {2}-{3} | {4}-{5}" -f $g.Name, $g.Count, $jornada.Minimum, $jornada.Maximum, $trabajo.Minimum, $trabajo.Maximum)
}
Escribir 'Fin. Pegue en el chat resultado-horarios.txt.'
Cerrar-Salida
