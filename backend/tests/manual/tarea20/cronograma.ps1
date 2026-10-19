# ==========================================================================
# TAREA-20 - Consulta del cronograma (NO escribe): GET /api/cronograma y GET /api/proyectos/{id}/cronograma.
#   set PROYECTO_ID=14           (proyecto visible para el filtro proyectoId y el endpoint por proyecto; por defecto 14)
#   set PROYECTO_NO_VISIBLE=3    (proyecto que el gestor NO ve: esperado 404; por defecto 3)
#   set CODIGO_EKON=12345        (filtro codigoEkon; por defecto el primer codigo de la respuesta del gestor)
#   set DEV_USER=gestor          (usuario de los casos filtrados; por defecto gestor)
#   Por respuesta 200 muestra SOLO: rango, cantidad de personas, codigos EKON, Id de proyectos, celdas por estado,
#   siglas de novedades, otrosProyectos y novedadesExtra. NUNCA nombres de personas ni de proyectos.
# Salida: resultado-cronograma.txt. Simulacion: SIMULAR=1 (simulacion\c_<caso>.json + .codigo, ficticios).
# ==========================================================================
. (Join-Path $PSScriptRoot '..\tarea26a\comun.ps1')
if (-not $env:SIMULACION) { $script:CarpetaSimulacion = Join-Path $PSScriptRoot 'simulacion' }

$base = Base-Url $(if ($env:API_BASE) { $env:API_BASE } else { 'https://localhost:7180' })
if (-not $script:Simular) { Permitir-CertificadoLocal }
$usuario = if ($env:DEV_USER) { $env:DEV_USER } else { 'gestor' }
$proyectoId = if ($env:PROYECTO_ID) { $env:PROYECTO_ID } else { '14' }
$noVisible = if ($env:PROYECTO_NO_VISIBLE) { $env:PROYECTO_NO_VISIBLE } else { '3' }

$hoy = Get-Date
$inicioMes = Get-Date -Year $hoy.Year -Month $hoy.Month -Day 1
$f = { param($d) $d.ToString('yyyy-MM-dd', [Globalization.CultureInfo]::InvariantCulture) }
$desde91 = & $f $inicioMes
$hasta91 = & $f $inicioMes.AddDays(90)   # 91 dias

function Resumir($Texto) {
    $c = Leer-Json $Texto
    $personas = @($c['personas'])
    $celdas = @($personas | ForEach-Object { @($_['dias']) })
    Escribir ("   rango: {0} a {1} (maximo {2} dias)" -f $c['desde'], $c['hasta'], $c['maxDias'])
    Escribir ("   personas: {0}; proyectos visibles (Id): {1}" -f $personas.Count, ((@($c['proyectos']) | ForEach-Object { $_['id'] }) -join ', '))
    $codigos = @($personas | ForEach-Object { $_['codigoEkon'] })
    $mas = if ($codigos.Count -gt 30) { " ... y $($codigos.Count - 30) mas" } else { '' }
    Escribir ("   codigos EKON: {0}{1}" -f (($codigos | Select-Object -First 30) -join ', '), $mas)
    foreach ($g in ($celdas | Group-Object { $_['estado'] } | Sort-Object Name)) { Escribir ("   celdas {0}: {1}" -f $g.Name, $g.Count) }
    $siglas = @($celdas | Where-Object { $_['novedad'] } | ForEach-Object { $_['novedad']['sigla'] })
    foreach ($g in ($siglas | Group-Object | Sort-Object Name)) { Escribir ("   sigla {0}: {1}" -f $g.Name, $g.Count) }
    $otros = ($celdas | ForEach-Object { [int]$_['otrosProyectos'] } | Measure-Object -Sum).Sum
    $extra = ($celdas | ForEach-Object { [int]$_['novedadesExtra'] } | Measure-Object -Sum).Sum
    Escribir ("   otrosProyectos (suma): {0}; novedadesExtra (suma): {1}" -f [int]$otros, [int]$extra)
    return $codigos
}

function Caso([string]$Nombre, [string]$Texto, [string]$Ruta, [string]$Usuario, [int]$Esperado, [string]$Clave) {
    $r = Solicitar -Metodo 'GET' -Base $base -Ruta $Ruta -Encabezados @{ 'X-Dev-User' = $Usuario } -TimeoutSegundos 120 -NombreSimulacion $Nombre
    $ok = $r.Codigo -eq $Esperado
    $errores = $null
    if ($r.Codigo -eq 400 -and $r.Texto) {
        try { $pd = Leer-Json $r.Texto; if ($pd -is [Collections.IDictionary] -and $pd.ContainsKey('errors')) { $errores = $pd['errors'] } } catch { }
        if ($ok -and $Clave -and -not ($errores -and $errores.ContainsKey($Clave))) { $ok = $false }
    }
    if (-not $ok) { $script:Fallos++ }
    Escribir ''
    Escribir ("-- {0} [{1}]: {2} -> {3}" -f $Texto, $Usuario, (Describir-Respuesta $r), $(if ($ok) { 'OK' } else { "NO ESPERADO (se esperaba HTTP $Esperado$(if ($Clave) { " en $Clave" }))" }))
    if ($errores) { foreach ($k in $errores.Keys) { Escribir ("   {0}: {1}" -f $k, (Texto-Linea (@($errores[$k]) -join ' '))) } }
    if ($r.Codigo -eq 200 -and $r.Texto) { return (Resumir $r.Texto) }
    return @()
}

Iniciar-Salida (Join-Path $PSScriptRoot 'resultado-cronograma.txt')
try {
    $script:Fallos = 0
    Escribir "== TAREA-20 Cronograma (solo lectura), proyecto $proyectoId, no visible $noVisible =="
    $codigos = @(Caso 'c_gestor' 'mes actual' 'api/cronograma' 'gestor' 200)
    [void](Caso 'c_admin' 'mes actual' 'api/cronograma' 'admin' 200)
    [void](Caso 'c_proyecto' "mes actual, proyectoId=$proyectoId" "api/cronograma?proyectoId=$proyectoId" $usuario 200)
    $codigo = if ($env:CODIGO_EKON) { $env:CODIGO_EKON } elseif ($codigos.Count -gt 0) { $codigos[0] } else { '' }
    if ($codigo) {
        [void](Caso 'c_codigo' "mes actual, codigoEkon=$codigo" "api/cronograma?codigoEkon=$([Uri]::EscapeDataString($codigo))" $usuario 200)
    }
    else {
        Escribir ''; Escribir '-- codigoEkon: omitido (sin personas en el mes; use set CODIGO_EKON=...)'
    }
    [void](Caso 'c_91dias' "91 dias ($desde91 a $hasta91)" "api/cronograma?desde=$desde91&hasta=$hasta91" $usuario 400 'hasta')
    [void](Caso 'c_invertido' 'hasta anterior a desde' "api/cronograma?desde=$hasta91&hasta=$desde91" $usuario 400 'hasta')
    [void](Caso 'c_por_proyecto' "por proyecto $proyectoId (sin fechas: desde su inicio)" "api/proyectos/$proyectoId/cronograma" $usuario 200)
    [void](Caso 'c_no_visible' "por proyecto $noVisible (no visible)" "api/proyectos/$noVisible/cronograma" 'gestor' 404)
    Escribir ''
    Escribir "Casos no esperados: $script:Fallos"
    Escribir 'Fin. Pegue en el chat resultado-cronograma.txt.'
}
finally { Cerrar-Salida }
