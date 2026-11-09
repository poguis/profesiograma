# ==========================================================================
# TAREA-23-2 (A1) - Limpieza de las novedades de prueba de la TAREA-23-1: las ANULA (borrado logico; sin SQL).
#   set IDS=5-14         (obligatorio: Id o rangos separados por coma, p. ej. 5-14 o 5,7,9-11)
#   set CONFIRMAR=1      (sin esto es una VISTA PREVIA: solo lista lo que haria)
#   set API_BASE=https://localhost:7180
# Proteccion: solo anula una novedad si su Id esta en IDS, su FechaInicio cae en 2070 y su origen es PROFESIOGRAMA; si algo
# no coincide la omite e informa. Nunca toca los Id 1-4 (semillas del sembrador). Anula como admin con el motivo
# "Limpieza de pruebas TAREA-23-1".
# Salida: resultado-limpiar.txt. Simulacion: SIMULAR=1 (simulacion\l<id>_<paso>.json + .codigo, ficticios).
# ==========================================================================
. (Join-Path $PSScriptRoot '..\tarea26a\comun.ps1')
. (Join-Path $PSScriptRoot 'casos.ps1')
if (-not $env:SIMULACION) { $script:CarpetaSimulacion = Join-Path $PSScriptRoot 'simulacion' }

$script:Base = Base-Url $(if ($env:API_BASE) { $env:API_BASE } else { 'https://localhost:7180' })
if (-not $script:Simular) { Permitir-CertificadoLocal }
$confirmar = $env:CONFIRMAR -eq '1'
$anioPruebas = 2070
$semillas = 1..4
$motivo = 'Limpieza de pruebas TAREA-23-1'

function Leer-Ids([string]$Texto) {
    $ids = New-Object Collections.Generic.List[int]
    foreach ($parte in ($Texto -split ',')) {
        $p = $parte.Trim()
        if ($p -match '^(\d+)-(\d+)$') { foreach ($i in [int]$Matches[1]..[int]$Matches[2]) { $ids.Add($i) } }
        elseif ($p -match '^\d+$') { $ids.Add([int]$p) }
        elseif ($p) { throw "IDS no valido: '$p' (use p. ej. 5-14 o 5,7,9-11)" }
    }
    return $ids | Sort-Object -Unique
}

Iniciar-Salida (Join-Path $PSScriptRoot 'resultado-limpiar.txt')
try {
    if (-not $env:IDS) { Escribir 'Indique los Id: set IDS=5-14 (y set CONFIRMAR=1 para anular).'; exit 1 }
    $ids = @(Leer-Ids $env:IDS)
    $modo = if ($confirmar) { 'CONFIRMADO (CONFIRMAR=1): anula' } else { 'VISTA PREVIA (sin CONFIRMAR=1): no anula nada' }
    Escribir "== TAREA-23-2 Limpieza de la 23-1: $modo; IDS=$($env:IDS); anio exigido $anioPruebas; origen PROFESIOGRAMA =="

    foreach ($id in $ids) {
        if ($semillas -contains $id) { Escribir ''; Escribir "-- Id ${id}: OMITIDA (semilla del sembrador; nunca se toca)"; continue }
        # Lectura informativa (sin CONTROL): un Id que no existe no es una falla, se omite.
        $r = Solicitar -Metodo 'GET' -Base $script:Base -Ruta "api/novedades/$id" -Encabezados @{ 'X-Dev-User' = 'admin' } `
            -TimeoutSegundos 120 -NombreSimulacion "l${id}_detalle"
        Escribir ''
        Escribir ("-- Id {0}: detalle {1}" -f $id, (Describir-Respuesta $r))
        $n = $null
        if ($r.Codigo -eq 200 -and $r.Texto) { try { $n = Leer-Json $r.Texto } catch { } }
        if (-not ($n -is [Collections.IDictionary])) { Escribir "   Id ${id}: OMITIDA (no existe o no se pudo leer)"; continue }
        Escribir ("   {0}: aplicacion {1}, origen {2}, {3} a {4}, anulada {5}" -f $n['codigo'], $n['aplicacion'], $n['origen'], $n['fechaInicio'], $n['fechaFin'], $n['anulada'])
        $motivoOmision = if ($n['origen'] -ne 'PROFESIOGRAMA') { "origen $($n['origen'])" }
            elseif (-not ([string]$n['fechaInicio']).StartsWith("$anioPruebas-")) { "FechaInicio $($n['fechaInicio']) fuera de $anioPruebas" }
            elseif ($n['anulada']) { 'ya anulada' }
            else { $null }
        if ($motivoOmision) { Escribir "   Id ${id}: OMITIDA ($motivoOmision)"; continue }
        if (-not $confirmar) { Escribir "   Id ${id}: se anularia (VISTA PREVIA)"; continue }
        [void](Caso "l${id}_anular" "anular $($n['codigo'])" 'POST' "api/novedades/$id/anular" 'admin' 200 @{ version = $n['version']; motivo = $motivo })
    }
    Resumen-Final 'resultado-limpiar.txt'
}
finally { Cerrar-Salida }
