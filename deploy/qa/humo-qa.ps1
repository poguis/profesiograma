# ==========================================================================
# TAREA-7d-1 / TAREA-7d-3 - Prueba de humo de QA SIN inicio de sesion (H1-H13). Lo ejecuta el USUARIO desde su PC.
# Solo GET anonimos con curl.exe (ruta completa: nunca un curl.cmd de la carpeta actual). Ningun token, ninguna escritura.
#   set QA_BASE=https://sdmapptest2.sedemi.local:7083   (por defecto; H1-H12)
#   set QA_HTTP=http://192.168.1.77:7082                (por defecto; H13)
#   Certificado autofirmado (E6), en este orden:
#     set QA_CA=D:\PROFESIOGRAMA_DEPLOY\QA\certificados\qa_7083.pem   -> --cacert + --ssl-no-revoke (por defecto, si existe)
#     set QA_INSEGURO=1                                                -> -k (NO verifica el certificado; solo QA)
#     sin ninguno de los dos: los casos HTTPS quedan OMITIDO.
#   set SIMULAR=1   (sin red: lee simulacion\<caso>.codigo, .json y .cabeceras)
# Salida: resultado-humo-qa.txt (codigos HTTP, tipos de contenido y cabeceras; nunca datos personales).
# ==========================================================================
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'comun7d1.ps1')

$ArchivoCandado = Join-Path $PSScriptRoot 'resultado-humo-qa.lock'
$ArchivoEstado = Join-Path $PSScriptRoot 'resultado-estado-humo-qa.txt'
$ArchivoSalida = Join-Path $PSScriptRoot 'resultado-humo-qa.txt'
if (-not (Tomar-Candado $ArchivoCandado 'humo-qa.cmd')) { exit 2 }

$Base = $(if ($env:QA_BASE) { $env:QA_BASE } else { 'https://sdmapptest2.sedemi.local:7083' }).TrimEnd('/')
$BaseHttp = $(if ($env:QA_HTTP) { $env:QA_HTTP } else { 'http://192.168.1.77:7082' }).TrimEnd('/')
$Simular = $env:SIMULAR -eq '1' -or $Base -eq 'https://localhost:1'
$Curl = Join-Path $env:SystemRoot 'System32\curl.exe'
$CarpetaSimulacion = Join-Path $PSScriptRoot 'simulacion'
$Temporal = Join-Path ([IO.Path]::GetTempPath()) ("humo-qa-" + [Guid]::NewGuid().ToString('N'))
$Estado = @{ base = $Base; http = $BaseHttp; modo = $(if ($Simular) { 'SIMULACION' } else { 'REAL' }); inicio = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') }

# Opciones TLS para la base https (E6).
$Ca = $(if ($env:QA_CA) { $env:QA_CA } else { 'D:\PROFESIOGRAMA_DEPLOY\QA\certificados\qa_7083.pem' })
$OpcionesTls = @()
$HttpsListo = $true
$ModoTls = 'no aplica (http)'
if ($Base.StartsWith('https:') -and -not $Simular) {
    if (Test-Path $Ca) {
        $OpcionesTls = @('--cacert', $Ca, '--ssl-no-revoke'); $ModoTls = "--cacert $Ca --ssl-no-revoke"
    } elseif ($env:QA_INSEGURO -eq '1') {
        $OpcionesTls = @('-k'); $ModoTls = '-k (QA_INSEGURO=1: NO se verifica el certificado)'
    } else {
        $HttpsListo = $false; $ModoTls = 'sin QA_CA ni QA_INSEGURO=1'
    }
}

# GET anonimo. Devuelve Codigo (int), Cuerpo (texto) y Cabeceras (hashtable, nombres en minusculas).
function Obtener([string]$Caso, [string]$Url, [hashtable]$Encabezados = @{}, [string[]]$Tls = @()) {
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
        $argumentos = @('-s', '-o', $archivoCuerpo, '-D', $archivoCabeceras, '-w', '%{http_code}', '--max-time', '30') + $Tls
        foreach ($e in $Encabezados.GetEnumerator()) { $argumentos += @('-H', ("{0}: {1}" -f $e.Name, $e.Value)) }
        $argumentos += $Url
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
    Escribir ("[{0}] {1} GET {2} -> {3}" -f (Get-Date -Format 'HH:mm:ss.fff'), $Caso, $Url, $codigo)
    return @{ Codigo = $codigo; Cuerpo = $cuerpo; Cabeceras = $cabeceras }
}

function Https([string]$Caso, [string]$Ruta, [hashtable]$Encabezados = @{}) { return Obtener $Caso ($Base + $Ruta) $Encabezados $OpcionesTls }
function Cabecera($r, [string]$Nombre) { if ($r.Cabeceras.ContainsKey($Nombre)) { return $r.Cabeceras[$Nombre] } return '(sin cabecera)' }
function EsHtml($r) { return $r.Cuerpo -match '(?i)<html' }
function EsIndex($r) { return $r.Codigo -eq 200 -and (Cabecera $r 'content-type') -like 'text/html*' -and $r.Cuerpo.Contains('id="root"') }

# TAREA-7d-3 (K1, H10): los 4 encabezados de seguridad.
$EncabezadosSeguridad = [ordered]@{
    'x-content-type-options' = 'nosniff'
    'referrer-policy' = 'strict-origin-when-cross-origin'
    'x-frame-options' = 'SAMEORIGIN'
    'content-security-policy' = "frame-ancestors 'self'"
}
function Faltantes($r) {
    return @($EncabezadosSeguridad.Keys | Where-Object { (Cabecera $r $_) -ne $EncabezadosSeguridad[$_] })
}

$codigoSalida = 0
try {
    New-Item -ItemType Directory -Path $Temporal | Out-Null
    Escribir ('=' * 70)
    Escribir $(if ($Simular) { 'MODO SIMULACION (sin red: simulacion\)' } else { "MODO REAL contra $Base y $BaseHttp (solo GET anonimos)" })
    Escribir ("TLS: {0}" -f $ModoTls)
    if ($OpcionesTls -contains '-k') { Escribir 'AVISO: -k NO verifica el certificado. Use QA_CA con el .pem en cuanto pueda.' }
    Escribir ('=' * 70)
    Guardar-Estado $ArchivoEstado ($Estado + @{ etapa = 'en curso' })

    $casosHttps = 'H1', 'H2', 'H3', 'H4', 'H5', 'H6', 'H7', 'H8', 'H9', 'H10', 'H11', 'H12'
    if (-not $HttpsListo) {
        foreach ($c in $casosHttps) { Omitido $c 'requisito: QA_CA (PEM) o QA_INSEGURO=1; ver LEEME, certificado' }
    } else {
        # H1: la raiz sirve index.html sin cache
        $h1 = Https 'H1' '/'
        $tipo = Cabecera $h1 'content-type'; $cache = Cabecera $h1 'cache-control'
        Control 'H1' ((EsIndex $h1) -and $cache -eq 'no-cache') '200, text/html, no-cache, index.html' ("{0}, {1}, {2}" -f $h1.Codigo, $tipo, $cache)

        # H2: una ruta profunda de la SPA tambien sirve index.html
        $h2 = Https 'H2' '/proyectos/1/cronograma'
        Control 'H2' (EsIndex $h2) '200, text/html, index.html' ("{0}, {1}" -f $h2.Codigo, (Cabecera $h2 'content-type'))

        # H3: un asset con hash (tomado de index.html) con cache larga
        $asset = [regex]::Match($h1.Cuerpo, 'src="(/assets/[^"]+\.js)"').Groups[1].Value
        if (-not $asset) {
            Omitido 'H3' 'requisito H1: index.html sin <script src="/assets/...">'
        } else {
            $h3 = Https 'H3' $asset
            $tipo = Cabecera $h3 'content-type'; $cache = Cabecera $h3 'cache-control'
            Control 'H3' ($h3.Codigo -eq 200 -and $tipo -like '*javascript*' -and $cache -like '*immutable*') `
                '200, javascript, public, max-age=31536000, immutable' ("{0}, {1}, {2}" -f $h3.Codigo, $tipo, $cache)
        }

        # H4 y H5: salud
        $h4 = Https 'H4' '/api/salud/vivo'
        Control 'H4' ($h4.Codigo -eq 200 -and $h4.Cuerpo.Contains('vivo')) '200 {"estado":"vivo"}' $h4.Codigo
        $h5 = Https 'H5' '/api/salud/listo'
        Control 'H5' ($h5.Codigo -eq 200 -and $h5.Cuerpo.Contains('listo')) '200 (la base de QA responde con profesiograma_test)' $h5.Codigo

        # H6: endpoint protegido sin token
        $h6 = Https 'H6' '/api/usuarios/me'
        Control 'H6' ($h6.Codigo -eq 401) '401' $h6.Codigo

        # H7: /api desconocido -> 404 sin HTML
        $h7 = Https 'H7' '/api/no-existe'
        Control 'H7' ($h7.Codigo -eq 404 -and -not (EsHtml $h7)) '404 sin HTML' ("{0}{1}" -f $h7.Codigo, $(if (EsHtml $h7) { ' con HTML' } else { '' }))

        # H8: X-Dev-User no existe en QA
        $h8 = Https 'H8' '/api/usuarios/me' @{ 'X-Dev-User' = 'admin' }
        Control 'H8' ($h8.Codigo -eq 401) '401 (DevAuth no existe en QA)' $h8.Codigo

        # H9: SOLO informa (X-Powered-By: Sedemi es del servidor; se consulta a TI)
        Escribir ("CONTROL H9 INFO Server: {0}; X-Powered-By: {1}" -f (Cabecera $h1 'server'), (Cabecera $h1 'x-powered-by'))

        # H10: encabezados de seguridad en / y en /api/salud/vivo
        $faltan = @(Faltantes $h1) + @(Faltantes $h4 | ForEach-Object { "$_ (vivo)" })
        Control 'H10' ($faltan.Count -eq 0) 'nosniff, strict-origin-when-cross-origin, SAMEORIGIN y frame-ancestors en / y vivo' $(if ($faltan.Count -eq 0) { '4 de 4 en los dos' } else { 'faltan o difieren: ' + ($faltan -join ', ') })

        # H11: sin HSTS en QA (E3)
        $hsts = $h1.Cabeceras.ContainsKey('strict-transport-security') -or $h4.Cabeceras.ContainsKey('strict-transport-security')
        Control 'H11' (-not $hsts) 'sin Strict-Transport-Security' $(if ($hsts) { 'con Strict-Transport-Security' } else { 'sin HSTS' })

        # H12: /assets/ -> index.html (no un listado); asset inexistente -> 404
        $h12a = Https 'H12a' '/assets/'
        Control 'H12a' (EsIndex $h12a) '200 index.html' ("{0}, {1}" -f $h12a.Codigo, (Cabecera $h12a 'content-type'))
        $h12b = Https 'H12b' '/assets/no-existe.js'
        Control 'H12b' ($h12b.Codigo -eq 404) '404' $h12b.Codigo
    }

    # H13: http 7082 sirve index.html (la redireccion a la URL publica la hace el navegador, E1)
    $h13 = Obtener 'H13' ($BaseHttp + '/')
    Control 'H13' (EsIndex $h13) '200 index.html por http' ("{0}, {1}" -f $h13.Codigo, (Cabecera $h13 'content-type'))

    Escribir ''
    Escribir 'Siguen L1-L6 y F1-F4: ver LEEME.md.'
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
