# ==========================================================================
# TAREA-22-2 - GET /api/proyectos/{id}/reporte/resumen?desde&hasta (NO escribe). Uso: resumen.cmd ID DESDE HASTA.
#   set DEV_USER=gestor   (por defecto admin)
#   set API_BASE=https://localhost:7180
# Imprime SOLO el codigo HTTP y el JSON de conteos (sin nombres ni cedulas; los 400 traen solo los mensajes de los campos).
# Salida: resultado-resumen.txt. Simulacion: SIMULAR=1 (simulacion\resumen.json + .codigo, FICTICIOS; nunca llama a la API).
# ==========================================================================
param([string]$Id, [string]$Desde, [string]$Hasta)
. (Join-Path $PSScriptRoot '..\tarea26a\comun.ps1')
if (-not $env:SIMULACION) { $script:CarpetaSimulacion = Join-Path $PSScriptRoot 'simulacion' }

$base = Base-Url $(if ($env:API_BASE) { $env:API_BASE } else { 'https://localhost:7180' })
if (-not $script:Simular) { Permitir-CertificadoLocal }
$usuario = if ($env:DEV_USER) { $env:DEV_USER } else { 'admin' }
if (-not $Id) { $Id = '14' }

$consulta = @()
if ($Desde) { $consulta += 'desde=' + [Uri]::EscapeDataString($Desde) }
if ($Hasta) { $consulta += 'hasta=' + [Uri]::EscapeDataString($Hasta) }
$ruta = "api/proyectos/$Id/reporte/resumen" + $(if ($consulta.Count -gt 0) { '?' + ($consulta -join '&') } else { '' })

Iniciar-Salida (Join-Path $PSScriptRoot 'resultado-resumen.txt')
try {
    Escribir "== TAREA-22-2 resumen: proyecto $Id, desde '$Desde', hasta '$Hasta', usuario $usuario =="
    $r = Solicitar -Metodo 'GET' -Base $base -Ruta $ruta -Encabezados @{ 'X-Dev-User' = $usuario } -TimeoutSegundos 120 -NombreSimulacion 'resumen'
    Escribir ('Respuesta: ' + (Describir-Respuesta $r))
    if ($r.Texto) { Escribir (Texto-Linea $r.Texto 500) }
}
finally {
    Cerrar-Salida
}
