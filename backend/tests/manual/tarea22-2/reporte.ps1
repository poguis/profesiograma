# ==========================================================================
# TAREA-22-2 - GET /api/proyectos/{id}/reporte?desde&hasta: descarga Reporte.xlsx (NO escribe en la API). Uso: reporte.cmd ID DESDE HASTA.
#   set DEV_USER=gestor   (por defecto admin)
#   set API_BASE=https://localhost:7180
# Imprime SOLO: codigo HTTP, X-Reporte-Filas, el nombre de Content-Disposition, Cache-Control y el tamano del archivo.
# NUNCA el contenido. curl con -D (encabezados a un archivo temporal que se borra) y -o Reporte.xlsx: con -i los encabezados
# quedarian DENTRO del .xlsx y lo danarian. curl con ruta completa (en este equipo NoDefaultCurrentDirectoryInExePath=1).
# Salida: resultado-reporte.txt. Simulacion: SIMULAR=1 -> muestra simulacion\reporte_encabezados.txt (FICTICIO); NO descarga.
# ==========================================================================
param([string]$Id, [string]$Desde, [string]$Hasta)
. (Join-Path $PSScriptRoot '..\tarea26a\comun.ps1')
if (-not $env:SIMULACION) { $script:CarpetaSimulacion = Join-Path $PSScriptRoot 'simulacion' }

$base = Base-Url $(if ($env:API_BASE) { $env:API_BASE } else { 'https://localhost:7180' })
$usuario = if ($env:DEV_USER) { $env:DEV_USER } else { 'admin' }
if (-not $Id) { $Id = '14' }

$consulta = @()
if ($Desde) { $consulta += 'desde=' + [Uri]::EscapeDataString($Desde) }
if ($Hasta) { $consulta += 'hasta=' + [Uri]::EscapeDataString($Hasta) }
$url = $base.TrimEnd('/') + "/api/proyectos/$Id/reporte" + $(if ($consulta.Count -gt 0) { '?' + ($consulta -join '&') } else { '' })

function Mostrar-Encabezados([string[]]$Lineas) {
    $estado = ($Lineas | Where-Object { $_ -match '^HTTP/' } | Select-Object -Last 1)
    Escribir ('Estado: ' + $(if ($estado) { $estado.Trim() } else { '(sin respuesta)' }))
    foreach ($nombre in 'X-Reporte-Filas', 'Cache-Control', 'Content-Type') {
        $linea = $Lineas | Where-Object { $_ -match "^$([regex]::Escape($nombre)):" } | Select-Object -Last 1
        Escribir ("{0}: {1}" -f $nombre, $(if ($linea) { ($linea -split ':', 2)[1].Trim() } else { '(no viene)' }))
    }
    $disposicion = $Lineas | Where-Object { $_ -match '^Content-Disposition:' } | Select-Object -Last 1
    $archivo = if ($disposicion -match 'filename=("?)([^";]+)\1') { $Matches[2] } else { '(no viene)' }
    Escribir "Nombre (Content-Disposition): $archivo"
}

Iniciar-Salida (Join-Path $PSScriptRoot 'resultado-reporte.txt')
try {
    Escribir "== TAREA-22-2 reporte: proyecto $Id, desde '$Desde', hasta '$Hasta', usuario $usuario =="
    if ($script:Simular) {
        $simulado = Join-Path $script:CarpetaSimulacion 'reporte_encabezados.txt'
        if (-not (Test-Path $simulado)) { Escribir "No existe $simulado"; exit 1 }
        Mostrar-Encabezados (Get-Content $simulado)
        Escribir 'Archivo: no se descarga en simulacion.'
        exit 0
    }

    $destino = Join-Path $PSScriptRoot 'Reporte.xlsx'
    $encabezados = Join-Path ([IO.Path]::GetTempPath()) ("tarea22-2-encabezados-" + [Guid]::NewGuid().ToString('N') + '.txt')
    try {
        & "$env:SystemRoot\System32\curl.exe" -s -k -D $encabezados -o $destino -H "X-Dev-User: $usuario" $url
        if (Test-Path $encabezados) { Mostrar-Encabezados (Get-Content $encabezados) } else { Escribir 'Sin respuesta (curl no pudo conectar).' }
    }
    finally {
        if (Test-Path $encabezados) { Remove-Item $encabezados -Force }
    }
    if (Test-Path $destino) { Escribir ("Archivo: {0} ({1} bytes)" -f $destino, (Get-Item $destino).Length) }
}
finally {
    Cerrar-Salida
}
