# ==========================================================================
# TAREA-7d-1 - Prueba de humo de QA SIN inicio de sesion (H1-H9). Lo ejecuta el USUARIO desde su PC.
# Solo GET anonimos con curl.exe (ruta completa: nunca un curl.cmd de la carpeta actual). Ningun token, ninguna escritura.
#   set QA_BASE=http://192.168.1.77:7082   (por defecto)
#   set SIMULAR=1                          (sin red: lee simulacion\<caso>.codigo, .json y .cabeceras)
# Salida: resultado-humo-qa.txt (codigos HTTP, tipos de contenido y cabeceras de cache; nunca datos personales).
# ==========================================================================
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'comun7d1.ps1')

$ArchivoCandado = Join-Path $PSScriptRoot 'resultado-humo-qa.lock'
$ArchivoEstado = Join-Path $PSScriptRoot 'resultado-estado-humo-qa.txt'
$ArchivoSalida = Join-Path $PSScriptRoot 'resultado-humo-qa.txt'
if (-not (Tomar-Candado $ArchivoCandado 'humo-qa.cmd')) { exit 2 }

$Base = $(if ($env:QA_BASE) { $env:QA_BASE } else { 'http://192.168.1.77:7082' }).TrimEnd('/')
$Simular = $env:SIMULAR -eq '1' -or $Base -eq 'https://localhost:1'
$Curl = Join-Path $env:SystemRoot 'System32\curl.exe'
$CarpetaSimulacion = Join-Path $PSScriptRoot 'simulacion'
$Temporal = Join-Path ([IO.Path]::GetTempPath()) ("humo-7d1-" + [Guid]::NewGuid().ToString('N'))
$Estado = @{ base = $Base; modo = $(if ($Simular) { 'SIMULACION' } else { 'REAL' }); inicio = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') }

# GET anonimo. Devuelve Codigo (int), Cuerpo (texto) y Cabeceras (hashtable, nombres en minusculas).
function Obtener([string]$Caso, [string]$Ruta, [hashtable]$Encabezados = @{}) {
    $cabeceras = @{}
    if ($Simular) {
        $codigo = [int]([IO.File]::ReadAllText((Join-Path $CarpetaSimulacion "$Caso.codigo")).Trim())
        $archivoCuerpo = Join-Path $CarpetaSimulacion "$Caso.json"
        $cuerpo = if (Test-Path $archivoCuerpo) { [IO.File]::ReadAllText($archivoCuerpo) } else { '' }
        $archivoCabeceras = Join-Path $CarpetaSimulacion "$Caso.cabeceras"
        $lineas = if (Test-Path $archivoCabeceras) { [IO.File]::ReadAllLines($archivoCabeceras) } else { @() }
    } else {
        $archivoCuerpo = Join-Path $Temporal "$Caso.cuerpo"
        $archivoCabeceras = Join-Path $Temporal "$Caso.cabeceras"
        $argumentos = @('-s', '-o', $archivoCuerpo, '-D', $archivoCabeceras, '-w', '%{http_code}', '--max-time', '30')
        foreach ($e in $Encabezados.GetEnumerator()) { $argumentos += @('-H', ("{0}: {1}" -f $e.Name, $e.Value)) }
        $argumentos += ($Base + $Ruta)
        $salida = & $Curl @argumentos
        $codigo = [int]("0" + ($salida | Out-String).Trim())
        $cuerpo = if (Test-Path $archivoCuerpo) { [IO.File]::ReadAllText($archivoCuerpo) } else { '' }
        $lineas = if (Test-Path $archivoCabeceras) { [IO.File]::ReadAllLines($archivoCabeceras) } else { @() }
    }
    foreach ($l in $lineas) {
        $i = $l.IndexOf(':')
        if ($i -gt 0) { $cabeceras[$l.Substring(0, $i).Trim().ToLowerInvariant()] = $l.Substring($i + 1).Trim() }
    }
    Escribir ''
    Escribir ("[{0}] {1} GET {2} -> {3}" -f (Get-Date -Format 'HH:mm:ss.fff'), $Caso, $Ruta, $codigo)
    return @{ Codigo = $codigo; Cuerpo = $cuerpo; Cabeceras = $cabeceras }
}

function Cabecera($r, [string]$Nombre) { if ($r.Cabeceras.ContainsKey($Nombre)) { return $r.Cabeceras[$Nombre] } return '(sin cabecera)' }
function EsHtml($r) { return $r.Cuerpo -match '(?i)<html' }

$codigoSalida = 0
try {
    New-Item -ItemType Directory -Path $Temporal | Out-Null
    Escribir ('=' * 70)
    Escribir $(if ($Simular) { 'MODO SIMULACION (sin red: simulacion\)' } else { "MODO REAL contra $Base (solo GET anonimos)" })
    Escribir ('=' * 70)
    Guardar-Estado $ArchivoEstado ($Estado + @{ etapa = 'en curso' })

    # H1: la raiz sirve index.html sin cache
    $h1 = Obtener 'H1' '/'
    $tipo = Cabecera $h1 'content-type'; $cache = Cabecera $h1 'cache-control'
    Control 'H1' ($h1.Codigo -eq 200 -and $tipo -like 'text/html*' -and $cache -eq 'no-cache' -and $h1.Cuerpo.Contains('id="root"')) `
        '200, text/html, no-cache, index.html' ("{0}, {1}, {2}" -f $h1.Codigo, $tipo, $cache)

    # H2: una ruta profunda de la SPA tambien sirve index.html
    $h2 = Obtener 'H2' '/proyectos/1/cronograma'
    $tipo = Cabecera $h2 'content-type'
    Control 'H2' ($h2.Codigo -eq 200 -and $tipo -like 'text/html*' -and $h2.Cuerpo.Contains('id="root"')) '200, text/html, index.html' ("{0}, {1}" -f $h2.Codigo, $tipo)

    # H3: un asset con hash (tomado de index.html) con cache larga
    $asset = [regex]::Match($h1.Cuerpo, 'src="(/assets/[^"]+\.js)"').Groups[1].Value
    if (-not $asset) {
        Omitido 'H3' 'requisito H1: index.html sin <script src="/assets/...">'
    } else {
        $h3 = Obtener 'H3' $asset
        $tipo = Cabecera $h3 'content-type'; $cache = Cabecera $h3 'cache-control'
        Control 'H3' ($h3.Codigo -eq 200 -and $tipo -like '*javascript*' -and $cache -like '*immutable*') `
            '200, javascript, public, max-age=31536000, immutable' ("{0}, {1}, {2}" -f $h3.Codigo, $tipo, $cache)
    }

    # H4 y H5: salud
    $h4 = Obtener 'H4' '/api/salud/vivo'
    Control 'H4' ($h4.Codigo -eq 200 -and $h4.Cuerpo.Contains('vivo')) '200 {"estado":"vivo"}' $h4.Codigo
    $h5 = Obtener 'H5' '/api/salud/listo'
    Control 'H5' ($h5.Codigo -eq 200 -and $h5.Cuerpo.Contains('listo')) '200 (la base de QA responde con profesiograma_test)' $h5.Codigo

    # H6: endpoint protegido sin token
    $h6 = Obtener 'H6' '/api/usuarios/me'
    Control 'H6' ($h6.Codigo -eq 401) '401' $h6.Codigo

    # H7: /api desconocido -> 404 sin HTML
    $h7 = Obtener 'H7' '/api/no-existe'
    Control 'H7' ($h7.Codigo -eq 404 -and -not (EsHtml $h7)) '404 sin HTML' ("{0}{1}" -f $h7.Codigo, $(if (EsHtml $h7) { ' con HTML' } else { '' }))

    # H8: X-Dev-User no existe en QA
    $h8 = Obtener 'H8' '/api/usuarios/me' @{ 'X-Dev-User' = 'admin' }
    Control 'H8' ($h8.Codigo -eq 401) '401 (DevAuth no existe en QA)' $h8.Codigo

    # H9 (J6): SOLO informa; el endurecimiento va en la 7d-2
    $h9 = Obtener 'H9' '/assets/'
    Escribir ("CONTROL H9 INFO Server: {0}; X-Powered-By: {1}; GET /assets/ -> {2}{3}" -f (Cabecera $h1 'server'), (Cabecera $h1 'x-powered-by'),
        $h9.Codigo, $(if ($h9.Cuerpo -match '(?i)<title>[^<]*/assets') { ' (parece un listado de directorio)' } else { '' }))

    Escribir ''
    Escribir 'Verificaciones visuales V1 y V2: ver LEEME.md, paso 8.'
    if ($script:Fallas -gt 0) { $codigoSalida = 1 }
    $Estado['etapa'] = 'terminado'
}
catch {
    $script:Fallas++
    Escribir ("CONTROL ERROR: {0}" -f $_.Exception.Message)
    $Estado['etapa'] = 'error'
    $codigoSalida = 1
}
finally {
    if (Test-Path $Temporal) { Remove-Item $Temporal -Recurse -Force -ErrorAction SilentlyContinue }
    Guardar-Estado $ArchivoEstado $Estado
    Guardar-Salida $ArchivoSalida
    Remove-Item $ArchivoCandado -Force -ErrorAction SilentlyContinue
}
exit $codigoSalida
