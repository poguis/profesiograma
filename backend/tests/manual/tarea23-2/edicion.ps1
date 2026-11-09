# ==========================================================================
# TAREA-23-2 - Novedades: EDICION (PUT /api/novedades/{id}) y ANULACION (POST /api/novedades/{id}/anular).
# Escribe en la base de desarrollo: crea 4 novedades de prueba y al final las anula todas (lista sus codigos).
#   set CODIGO_EKON=DEV001     (empleado ACTIVO en la API; DEV001 existe en el ERP Simulado; con el ERP real, un codigo activo)
#   set CORRIDA=7              (desplaza las fechas para repetir sin 409; por defecto se deriva del reloj)
#   set API_BASE=https://localhost:7180
# Salida: resultado-edicion.txt. Simulacion: SIMULAR=1 (simulacion\e_<caso>.json + .codigo, ficticios).
# ==========================================================================
. (Join-Path $PSScriptRoot '..\tarea26a\comun.ps1')
. (Join-Path $PSScriptRoot 'casos.ps1')
if (-not $env:SIMULACION) { $script:CarpetaSimulacion = Join-Path $PSScriptRoot 'simulacion' }

$script:Base = Base-Url $(if ($env:API_BASE) { $env:API_BASE } else { 'https://localhost:7180' })
if (-not $script:Simular) { Permitir-CertificadoLocal }
$codigoEkon = if ($env:CODIGO_EKON) { $env:CODIGO_EKON } else { 'DEV001' }
$corrida = if ($env:CORRIDA) { [int]$env:CORRIDA } else { [int](((Get-Date) - [datetime]'2026-10-01').TotalMinutes % 5000) }

# Fechas: desde 2090 (lejos de los datos reales y de las corridas de la 23-1), 6 dias por corrida.
$cultura = [Globalization.CultureInfo]::InvariantCulture
$dia0 = ([datetime]'2090-01-01').AddDays($corrida * 6)
$f = { param($n) $dia0.AddDays($n).ToString('yyyy-MM-dd', $cultura) }
$d0 = & $f 0; $d1 = & $f 1; $d2 = & $f 2; $d3 = & $f 3; $d4 = & $f 4

$script:Creadas = New-Object Collections.Generic.List[object]

function Crear([string]$Nombre, [string]$Texto, [string]$Usuario, [hashtable]$Datos) {
    $r = Caso $Nombre $Texto 'POST' 'api/novedades' $Usuario 201 $Datos
    if (Cumplido $r 201) {
        Escribir ("   creada: id {0}, codigo {1}" -f $r.Leido['id'], $r.Leido['codigo'])
        $script:Creadas.Add([pscustomobject]@{ Id = $r.Leido['id']; Codigo = $r.Leido['codigo']; Nombre = $Nombre })
        return $r.Leido
    }
    return $null
}

# Version actual (GET detalle); $null si no se pudo leer.
function Version([string]$Nombre, $Id, [string]$Usuario) {
    $r = Caso $Nombre "detalle de la novedad $Id (para leer la version)" 'GET' "api/novedades/$Id" $Usuario 200
    if (Cumplido $r 200) { Escribir ("   version: {0}" -f $r.Leido['version']); return $r.Leido['version'] }
    return $null
}

function Editar([string]$Nombre, [string]$Texto, $Id, [string]$Usuario, [int]$Esperado, [hashtable]$Datos, [string]$Clave, [scriptblock]$Verificar) {
    return Caso $Nombre $Texto 'PUT' "api/novedades/$Id" $Usuario $Esperado $Datos $Clave $Verificar
}

Iniciar-Salida (Join-Path $PSScriptRoot 'resultado-edicion.txt')
try {
    Escribir "== TAREA-23-2 Edicion y anulacion: CORRIDA=$corrida (fechas $d0 a $d4), CODIGO_EKON=$codigoEkon =="

    # ---------------------------------------------------------------- preparacion
    $p1 = Crear 'p01_persona' 'crear PERSONA VACACIONES de 3 dias' 'gestor' @{ tipoAplicacion = 'PERSONA'; tipoNovedad = 'VACACIONES'; codigoEkon = $codigoEkon; fechaInicio = $d0; fechaFin = $d2; observacion = 'Prueba TAREA-23-2' }
    $p2 = Crear 'p02_general' 'crear GENERAL FERIADO (admin)' 'admin' @{ tipoAplicacion = 'GENERAL'; tipoNovedad = 'FERIADO'; fechaInicio = $d0; fechaFin = $d0 }
    $p3 = Crear 'p03_persona_admin' 'crear PERSONA PERMISO (admin)' 'admin' @{ tipoAplicacion = 'PERSONA'; tipoNovedad = 'PERMISO'; codigoEkon = $codigoEkon; fechaInicio = $d4; fechaFin = $d4 }
    $p4 = Crear 'p04_persona_permiso' 'crear PERSONA PERMISO (gestor, para el duplicado)' 'gestor' @{ tipoAplicacion = 'PERSONA'; tipoNovedad = 'PERMISO'; codigoEkon = $codigoEkon; fechaInicio = $d3; fechaFin = $d3 }

    $editar = { param($v, $tipo, $desde, $hasta, $excluidos) @{ version = $v; tipoNovedad = $tipo; fechaInicio = $desde; fechaFin = $hasta; diasExcluidos = @($excluidos); observacion = 'Prueba TAREA-23-2' } }

    if ($p1) {
        $id1 = $p1['id']
        $v1 = $p1['version']
        # e01 rango y dia del medio
        $e01 = Editar 'e01_excluir_medio' "editar $id1 excluyendo el dia del medio ($d1)" $id1 'gestor' 200 (& $editar $v1 'VACACIONES' $d0 $d2 @($d1)) '' {
            param($r) if ($r['version'] -eq $v1) { return 'la version no cambio' }; $null }
        [void](Caso 'e01b_detalle' "detalle de $id1 tras e01 (dias $d0 y $d2)" 'GET' "api/novedades/$id1" 'gestor' 200 $null '' {
            param($n) $dias = @($n['dias']); if ($dias.Count -ne 2 -or $dias[0] -ne $d0 -or $dias[1] -ne $d2) { return "dias: $($dias -join ', ')" }; $null })
        $v2 = if (Cumplido $e01 200) { $e01.Leido['version'] } else { $null }

        # e02 cambio de tipo
        if ($v2) { $e02 = Editar 'e02_tipo' 'cambiar el tipo a CALAMIDAD_DOMESTICA' $id1 'gestor' 200 (& $editar $v2 'CALAMIDAD_DOMESTICA' $d0 $d2 @($d1)) }
        else { Omitir 'e02_tipo' 'e01'; $e02 = $null }
        $v3 = if (Cumplido $e02 200) { $e02.Leido['version'] } else { $null }

        # e03 duplicado al editar: PERMISO hasta $d3 choca con p04 (PERMISO el $d3)
        if ($v3 -and $p4) { [void](Editar 'e03_duplicado' "pasar a PERMISO hasta $d3 (choca con p04)" $id1 'gestor' 409 (& $editar $v3 'PERMISO' $d0 $d3 @($d1))) }
        else { Omitir 'e03_duplicado' 'e02 o p04' }

        # e04/e05 dos pestanas con la misma version: A cambia solo dias -> 200; B con la misma version -> 409 (A5/R1)
        if ($v3) {
            [void](Editar 'e04_pestana_a' "pestana A: solo dias (excluye $d0 y $d1)" $id1 'gestor' 200 (& $editar $v3 'CALAMIDAD_DOMESTICA' $d0 $d2 @($d0, $d1)) '' {
                param($r) if ($r['version'] -eq $v3) { return 'la version no cambio (sin R1 no habria UPDATE)' }; $null })
            [void](Editar 'e05_pestana_b' "pestana B: solo dias con la MISMA version (excluye $d1 y $d2)" $id1 'gestor' 409 (& $editar $v3 'CALAMIDAD_DOMESTICA' $d0 $d2 @($d1, $d2)))
            # e06-e08 version
            $sinVersion = & $editar $null 'CALAMIDAD_DOMESTICA' $d0 $d2 @($d1); $sinVersion.Remove('version')
            [void](Editar 'e06_sin_version' 'sin version' $id1 'gestor' 400 $sinVersion 'version')
            [void](Editar 'e07_version_vieja' 'version vieja (la de antes de la pestana A)' $id1 'gestor' 409 (& $editar $v3 'CALAMIDAD_DOMESTICA' $d0 $d2 @($d1)))
            [void](Editar 'e08_version_ilegible' 'version ilegible' $id1 'gestor' 400 (& $editar '@@no-es-base64@@' 'CALAMIDAD_DOMESTICA' $d0 $d2 @($d1)) 'version')
        }
        else { foreach ($n in 'e04_pestana_a', 'e05_pestana_b', 'e06_sin_version', 'e07_version_vieja', 'e08_version_ilegible') { Omitir $n 'e02' } }
    }
    else { foreach ($n in 'e01_excluir_medio', 'e02_tipo', 'e03_duplicado', 'e04_pestana_a', 'e05_pestana_b', 'e06_sin_version', 'e07_version_vieja', 'e08_version_ilegible') { Omitir $n 'p01' } }

    # e09 origen no editable: NOV-DEV-0004 (PERMISOS_MEDICOS del sembrador)
    $pm = Caso 'e09a_buscar_pm' 'buscar NOV-DEV-0004 (admin)' 'GET' 'api/novedades?texto=NOV-DEV-0004&incluirAnuladas=true' 'admin' 200
    $pmItem = if ((Cumplido $pm 200) -and @($pm.Leido['items']).Count -gt 0) { @($pm.Leido['items'])[0] } else { $null }
    if ($pmItem) {
        $vpm = Version 'e09b_version_pm' $pmItem['id'] 'admin'
        if ($vpm) { [void](Editar 'e09_origen' "editar NOV-DEV-0004 (id $($pmItem['id']))" $pmItem['id'] 'admin' 400 (& $editar $vpm 'PERMISO' $d0 $d0 @()) 'origen') }
        else { Omitir 'e09_origen' 'e09b' }
    }
    else { Omitir 'e09_origen' 'NOV-DEV-0004 (no se encontro)' }

    # e10 / e11 el gestor edita novedades ajenas
    if ($p2) {
        $vg = Version 'e10a_version_general' $p2['id'] 'gestor' # GENERAL: el gestor la ve en solo lectura
        if ($vg) { [void](Editar 'e10_general_ajena' "el gestor edita la GENERAL $($p2['id']) del admin" $p2['id'] 'gestor' 403 (& $editar $vg 'FERIADO' $d0 $d1 @())) }
        else { Omitir 'e10_general_ajena' 'e10a' }
    }
    else { Omitir 'e10_general_ajena' 'p02' }
    if ($p3) { [void](Editar 'e11_persona_ajena' "el gestor edita la PERSONA $($p3['id']) del admin" $p3['id'] 'gestor' 404 (& $editar $p3['version'] 'PERMISO' $d4 $d4 @())) }
    else { Omitir 'e11_persona_ajena' 'p03' }

    # e12-e15 anulacion
    if ($p1) {
        $va = Version 'e12a_version' $p1['id'] 'gestor'
        if ($va) {
            $e12 = Caso 'e12_anular' "anular $($p1['id']) con motivo" 'POST' "api/novedades/$($p1['id'])/anular" 'gestor' 200 @{ version = $va; motivo = 'Prueba TAREA-23-2: anular' }
            $vb = if (Cumplido $e12 200) { $e12.Leido['version'] } else { $va }
            [void](Caso 'e13_anular_otra_vez' 'anular otra vez' 'POST' "api/novedades/$($p1['id'])/anular" 'gestor' 409 @{ version = $vb; motivo = 'Prueba TAREA-23-2: otra vez' })
            [void](Editar 'e14_editar_anulada' 'editar la anulada' $p1['id'] 'gestor' 409 (& $editar $vb 'VACACIONES' $d0 $d0 @()))
            [void](Caso 'e15_detalle_anulada' 'detalle de la anulada' 'GET' "api/novedades/$($p1['id'])" 'gestor' 200 $null '' {
                param($n) if (-not $n['anulada']) { return 'anulada = false' }; if (-not $n['motivoAnulacion']) { return 'sin motivoAnulacion' }; if ($n['puedeEditar']) { return 'puedeEditar = true' }; $null })
        }
        else { foreach ($n in 'e12_anular', 'e13_anular_otra_vez', 'e14_editar_anulada', 'e15_detalle_anulada') { Omitir $n 'e12a' } }
    }
    else { foreach ($n in 'e12_anular', 'e13_anular_otra_vez', 'e14_editar_anulada', 'e15_detalle_anulada') { Omitir $n 'p01' } }

    # ---------------------------------------------------------------- limpieza: anula lo que creo la corrida (admin)
    foreach ($c in $script:Creadas) {
        if ($c.Nombre -eq 'p01_persona') { continue } # ya anulada en e12
        $vz = Version "z_version_$($c.Nombre)" $c.Id 'admin'
        if ($vz) { [void](Caso "z_anular_$($c.Nombre)" "anular $($c.Codigo) (limpieza)" 'POST' "api/novedades/$($c.Id)/anular" 'admin' 200 @{ version = $vz; motivo = 'Limpieza de pruebas TAREA-23-2' }) }
        else { Omitir "z_anular_$($c.Nombre)" "z_version_$($c.Nombre)" }
    }
    Escribir ''
    Escribir 'Novedades creadas en esta corrida (anuladas al final):'
    foreach ($c in $script:Creadas) { Escribir ("   {0} (id {1}, {2})" -f $c.Codigo, $c.Id, $c.Nombre) }
    Resumen-Final 'resultado-edicion.txt'
}
finally { Cerrar-Salida }
