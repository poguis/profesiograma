# ==========================================================================
# TAREA-26a - A5 / pendiente 17: largo real de los Id del ERP frente a las columnas (a traves de NUESTRA API).
#   GET /api/erp/companias; por compania: /proyectos y /dimensiones; actividades solo de los primeros N proyectos
#   (N = %ERP_N_ACTIVIDADES%, por defecto 20; total entre todas las companias).
#   Columnas: Proyecto.ProyectoErpId 30, Proyecto.DimensionUegpId 30, ProyectoActividad.ActividadCodigo 20.
#   Muestra el largo maximo, cuantos exceden y los 5 Id mas largos (Id de catalogo del ERP, no datos personales).
# Requisitos: API corriendo en modo Http (ver LEEME.md). Base: %API_BASE% (por defecto https://localhost:7180).
# Salida: resultado-largos.txt. Simulacion: SIMULAR=1 (api_erp_companias.json, api_erp_companias_<id>_proyectos.json,
# api_erp_companias_<id>_dimensiones.json, api_erp_companias_<id>_proyectos_<proyecto>_actividades.json).
# ==========================================================================
. (Join-Path $PSScriptRoot 'comun.ps1')

$base = Base-Url $(if ($env:API_BASE) { $env:API_BASE } else { 'https://localhost:7180' })
$encabezados = @{ 'X-Dev-User' = 'gestor' }
$maximoActividades = if ($env:ERP_N_ACTIVIDADES) { [int]$env:ERP_N_ACTIVIDADES } else { 20 }
if (-not $script:Simular) { Permitir-CertificadoLocal }

function Pedir([string]$Ruta) {
    $r = Solicitar -Metodo 'GET' -Base $base -Ruta $Ruta -Encabezados $encabezados
    if ($r.Codigo -eq 200) { return , (Leer-Json $r.Texto) }
    $script:fallos += "$Ruta -> $(Describir-Respuesta $r)"
    return , @()
}

function Informar([string]$Titulo, [object[]]$Ids, [int]$Limite) {
    Escribir ''
    Escribir "$Titulo (columna: $Limite)"
    if ($Ids.Count -eq 0) { Escribir '  sin datos'; return }
    $largos = $Ids | ForEach-Object { $_.Id.Length } | Measure-Object -Maximum
    $exceden = @($Ids | Where-Object { $_.Id.Length -gt $Limite }).Count
    Escribir "  cantidad: $($Ids.Count); largo maximo: $($largos.Maximum); exceden la columna: $exceden$(if ($exceden -gt 0) { '  <-- EXCEDE' })"
    foreach ($i in ($Ids | Sort-Object { $_.Id.Length } -Descending | Select-Object -First 5)) {
        Escribir ("  {0} caracteres: {1} (compania {2})" -f $i.Id.Length, $i.Id, $i.Compania)
    }
}

$script:fallos = @()
Iniciar-Salida 'resultado-largos.txt'
Escribir '== A5 Largo de los Id del ERP (pendiente 17) =='
$companias = Pedir 'api/erp/companias'
Escribir "Companias: $($companias.Count)"

$proyectos = @(); $dimensiones = @(); $actividades = @()
$consultadas = 0
foreach ($c in $companias) {
    $id = $c['id']
    foreach ($p in (Pedir "api/erp/companias/$id/proyectos")) { $proyectos += [pscustomobject]@{ Id = [string]$p['id']; Compania = $id } }
    foreach ($d in (Pedir "api/erp/companias/$id/dimensiones")) { $dimensiones += [pscustomobject]@{ Id = [string]$d['uegpId']; Compania = $id } }
}
foreach ($p in $proyectos) {
    if ($consultadas -ge $maximoActividades) { break }
    $consultadas++
    $ruta = "api/erp/companias/$($p.Compania)/proyectos/$([Uri]::EscapeDataString($p.Id))/actividades"
    foreach ($a in (Pedir $ruta)) { $actividades += [pscustomobject]@{ Id = [string]$a['id']; Compania = $p.Compania } }
}

Informar 'Proyectos ERP (proyectoErpId)' $proyectos 30
Informar 'Dimensiones (uegpId)' $dimensiones 30
Informar "Actividades (activityId) de $consultadas proyectos" $actividades 20
Escribir ''
Escribir "Solicitudes sin 200: $($script:fallos.Count)"
foreach ($f in $script:fallos) { Escribir "  $f" }
Escribir 'Fin. Pegue en el chat resultado-largos.txt.'
Cerrar-Salida
