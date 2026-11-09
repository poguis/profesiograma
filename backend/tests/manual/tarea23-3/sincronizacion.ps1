# ==========================================================================
# TAREA-23-3 - Sincronizacion manual de PERMISO MEDICO: POST /api/admin/novedades/sincronizar-permisos-medicos.
# Requiere la API con NovedadesExternas:Modo = Simulado (appsettings.Development.json). Usa SOLO la fuente simulada
# (filas FICTICIAS NOV-SIMPM-0001..0010, ano 2075, empleados DEV001..DEV006). Escribe en la base de desarrollo y al final
# deja las de prueba ANULADAS (s11).
#   set API_BASE=https://localhost:7180
# Muestra SOLO codigos HTTP, conteos, codigos NOV-, Id y versiones. NUNCA nombres, correos ni cedulas.
# Salida: resultado-sincronizacion.txt. Simulacion: SIMULAR=1 (simulacion\s<caso>.json + .codigo, ficticios).
# ==========================================================================
. (Join-Path $PSScriptRoot '..\tarea26a\comun.ps1')
. (Join-Path $PSScriptRoot '..\tarea23-2\casos.ps1')
if (-not $env:SIMULACION) { $script:CarpetaSimulacion = Join-Path $PSScriptRoot 'simulacion' }

$script:Base = Base-Url $(if ($env:API_BASE) { $env:API_BASE } else { 'https://localhost:7180' })
if (-not $script:Simular) { Permitir-CertificadoLocal }
$ruta = 'api/admin/novedades/sincronizar-permisos-medicos'

function Conteos($r) {
    $o = $r['omitidas']
    $om = if ($o -is [Collections.IDictionary]) { ($o.Keys | Sort-Object | ForEach-Object { "$_=$($o[$_])" }) -join ', ' } else { '' }
    return ("fuente {0}: leidas {1}, nuevas {2}, actualizadas {3}, anuladas {4}, reactivadas {5}, sin cambios {6}, ausentes {7}; omitidas: {8}" -f `
        $r['fuente'], $r['leidas'], $r['nuevas'], $r['actualizadas'], $r['anuladas'], $r['reactivadas'], $r['sinCambios'], $r['ausentesEnOrigen'], $om)
}

# Verifica que los conteos indicados tengan el valor esperado (los no indicados no se revisan).
function Esperar($r, [hashtable]$Esperado) {
    Escribir ("   " + (Conteos $r))
    foreach ($k in $Esperado.Keys) { if ([int]$r[$k] -ne [int]$Esperado[$k]) { return "$k = $($r[$k]), se esperaba $($Esperado[$k])" } }
    return $null
}

function Sincronizar([string]$Nombre, [string]$Texto, [string]$Escenario, [scriptblock]$Verificar) {
    return Caso $Nombre $Texto 'POST' "$ruta`?escenario=$Escenario" 'admin' 200 $null '' $Verificar
}

# Version del detalle (admin) de una novedad simulada, buscada por su codigo.
function Version-De([string]$Nombre, [string]$Codigo) {
    $l = Caso "${Nombre}_buscar" "buscar $Codigo (admin)" 'GET' "api/novedades?origen=PERMISOS_MEDICOS&incluirAnuladas=true&texto=$Codigo" 'admin' 200
    if (-not ((Cumplido $l 200) -and @($l.Leido['items']).Count -eq 1)) { return $null }
    $id = @($l.Leido['items'])[0]['id']
    $d = Caso "${Nombre}_detalle" "detalle de $Codigo (id $id, admin)" 'GET' "api/novedades/$id" 'admin' 200
    if (-not (Cumplido $d 200)) { return $null }
    Escribir ("   id {0}, version {1}" -f $id, $d.Leido['version'])
    return [pscustomobject]@{ Id = $id; Version = $d.Leido['version']; Detalle = $d.Leido }
}

$sincronizados = @('s01_base', 's02a_version', 's02_repetir', 's02b_version_igual', 's03_fechas', 's04_base', 's05_anuladas', 's06_reactivadas', 's09_listados', 's10_editar_pm', 's11_limpieza')

Iniciar-Salida (Join-Path $PSScriptRoot 'resultado-sincronizacion.txt')
try {
    Escribir '== TAREA-23-3 Sincronizacion de PERMISO MEDICO (fuente simulada, NOV-SIMPM-0001..0010) =='

    # s00 normaliza: deja las 6 de prueba anuladas (o las omite si nunca se crearon).
    $s00 = Sincronizar 's00_normalizar' 'escenario anuladas (normaliza el punto de partida)' 'anuladas' $null
    $simulada = (Cumplido $s00 200) -and $s00.Leido['fuente'] -eq 'Simulado'
    if (-not $simulada) {
        Escribir ''
        Escribir 'AVISO: la fuente no es la Simulada (o la API no respondio 200). Configure NovedadesExternas:Modo = Simulado; los casos de la fuente quedan OMITIDOS.'
        foreach ($n in $sincronizados) { Omitir $n 's00 (fuente Simulada)' }
    }
    else {
        Escribir ("   " + (Conteos $s00.Leido))
        [void](Sincronizar 's01_base' 'escenario base' 'base' {
            param($r)
            $creadas = [int]$r['nuevas'] + [int]$r['reactivadas']
            if ($creadas -ne 6) { Escribir ("   " + (Conteos $r)); return "nuevas + reactivadas = $creadas, se esperaba 6" }
            foreach ($cat in 'tipoNoSoportado', 'jsonInvalido', 'sinEmpleado', 'otroOrigen') { if ([int]$r['omitidas'][$cat] -ne 1) { Escribir ("   " + (Conteos $r)); return "omitidas.$cat = $($r['omitidas'][$cat]), se esperaba 1" } }
            if ([int]$r['ausentesEnOrigen'] -lt 1) { return 'ausentesEnOrigen < 1 (se esperaba al menos NOV-DEV-0004)' }
            Escribir ("   " + (Conteos $r)); $null })

        $v1 = Version-De 's02a_version' 'NOV-SIMPM-0002'
        [void](Sincronizar 's02_repetir' 'escenario base otra vez (idempotente)' 'base' {
            param($r) Esperar $r @{ sinCambios = 6; nuevas = 0; actualizadas = 0; anuladas = 0; reactivadas = 0 } })
        $v2 = Version-De 's02b_version' 'NOV-SIMPM-0002'
        Escribir ''
        if ($v1 -and $v2) {
            $igual = $v1.Version -eq $v2.Version
            if (-not $igual) { $script:Fallos++ }
            Escribir ("CONTROL s02b_version_igual {0} (version antes {1}, despues {2}: sin cambios no hay UPDATE)" -f $(if ($igual) { 'OK' } else { 'FALLA' }), $v1.Version, $v2.Version)
        }
        else { Omitir 's02b_version_igual' 's02a/s02b' }

        [void](Sincronizar 's03_fechas' 'escenario fechas (NOV-SIMPM-0003 pasa a 3 dias)' 'fechas' { param($r) Esperar $r @{ actualizadas = 1; sinCambios = 5; nuevas = 0 } })
        [void](Sincronizar 's04_base' 'escenario base (NOV-SIMPM-0003 vuelve a 1 dia)' 'base' { param($r) Esperar $r @{ actualizadas = 1; sinCambios = 5 } })
        [void](Sincronizar 's05_anuladas' 'escenario anuladas (ESTADO no ACTIVA en el origen)' 'anuladas' { param($r) Esperar $r @{ anuladas = 6; sinCambios = 0 } })
        [void](Sincronizar 's06_reactivadas' 'escenario base (vuelven a ACTIVA)' 'base' { param($r) Esperar $r @{ reactivadas = 6; anuladas = 0 } })

        [void](Caso 's09a_lista_admin' 'listado del admin: origen=PERMISOS_MEDICOS&texto=NOV-SIMPM' 'GET' 'api/novedades?origen=PERMISOS_MEDICOS&texto=NOV-SIMPM' 'admin' 200 $null '' {
            param($l) if ([int]$l['total'] -ne 6) { return "total $($l['total']), se esperaba 6" }; $null })
        [void](Caso 's09b_lista_gestor' 'listado del gestor: origen=PERMISOS_MEDICOS&texto=NOV-SIMPM' 'GET' 'api/novedades?origen=PERMISOS_MEDICOS&texto=NOV-SIMPM' 'gestor' 200 $null '' {
            param($l) if ([int]$l['total'] -ne 0) { return "total $($l['total']), se esperaba 0" }; $null })

        $pm = Version-De 's10a' 'NOV-SIMPM-0001'
        if ($pm) {
            [void](Caso 's10_editar_pm' "el admin edita NOV-SIMPM-0001 (id $($pm.Id))" 'PUT' "api/novedades/$($pm.Id)" 'admin' 400 `
                @{ version = $pm.Version; tipoNovedad = 'PERMISO_MEDICO'; fechaInicio = '2075-03-01'; fechaFin = '2075-03-01'; diasExcluidos = @() } 'origen')
        }
        else { Omitir 's10_editar_pm' 's10a' }
    }

    # s07/s08 no dependen de la fuente.
    [void](Caso 's07_gestor' 'sincronizar como gestor' 'POST' $ruta 'gestor' 403)
    [void](Caso 's08_sin_sesion' 'sincronizar sin sesion' 'POST' $ruta 'anonimo' 401)

    if ($simulada) {
        [void](Sincronizar 's11_limpieza' 'escenario anuladas (al final no quedan PERMISOS_MEDICOS de prueba activas)' 'anuladas' { param($r) Esperar $r @{ anuladas = 6 } })
    }

    Resumen-Final 'resultado-sincronizacion.txt'
}
finally { Cerrar-Salida }
