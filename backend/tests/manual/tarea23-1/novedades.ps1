# ==========================================================================
# TAREA-23-1 - Novedades: listado, opciones, detalle y CREACION (escribe en la base de desarrollo).
#   set CODIGO_EKON=DEV001     (empleado ACTIVO en la API para las PERSONA; por defecto DEV001, que existe en modo Simulado.
#                               Con el ERP real (modo Http) indique un codigo real activo)
#   set CODIGO_EKON_ADMIN=...  (empleado de la PERSONA que crea el admin para los casos 11 y 13; por defecto = CODIGO_EKON
#                               en ambos modos: no choca con n01 porque usa otro tipo y otra fecha. TAREA-23-2)
#   set PROYECTO_ID=14         (proyecto PROPIO del gestor y vigente; por defecto el primero ACTIVO de GET /api/proyectos del gestor)
#   set CORRIDA=7              (desplaza las fechas para repetir sin 409; por defecto se deriva del reloj y se muestra al inicio)
#   set API_BASE=https://localhost:7180
# Muestra SOLO codigos HTTP, codigos NOV-, Id, cantidades y mensajes de validacion. NUNCA nombres de personas ni de proyectos.
# Salida: resultado-novedades.txt. Simulacion: SIMULAR=1 (simulacion\n_<caso>.json + .codigo, ficticios).
# ==========================================================================
. (Join-Path $PSScriptRoot '..\tarea26a\comun.ps1')
if (-not $env:SIMULACION) { $script:CarpetaSimulacion = Join-Path $PSScriptRoot 'simulacion' }

$base = Base-Url $(if ($env:API_BASE) { $env:API_BASE } else { 'https://localhost:7180' })
if (-not $script:Simular) { Permitir-CertificadoLocal }
$codigoEkon = if ($env:CODIGO_EKON) { $env:CODIGO_EKON } else { 'DEV001' }
$codigoEkonAdmin = if ($env:CODIGO_EKON_ADMIN) { $env:CODIGO_EKON_ADMIN } else { $codigoEkon }
$corrida = if ($env:CORRIDA) { [int]$env:CORRIDA } else { [int](((Get-Date) - [datetime]'2026-10-01').TotalMinutes % 5000) }

# Fechas de la corrida: lejos de los datos reales (2032 en adelante), 3 dias por corrida.
$cultura = [Globalization.CultureInfo]::InvariantCulture
$dia0 = ([datetime]'2032-01-01').AddDays($corrida * 3)
$f = { param($d) $d.ToString('yyyy-MM-dd', $cultura) }
$d0 = & $f $dia0
$d1 = & $f $dia0.AddDays(1)
$d2 = & $f $dia0.AddDays(2)

$script:Fallos = 0
$script:Omitidos = 0
$script:Creadas = New-Object Collections.Generic.List[string]

# TAREA-23-2: un caso cuyo requisito previo no se creo no se ejecuta (antes llamaba a la API con un Id vacio).
function Omitir([string]$Nombre, [string]$Requisito) {
    $script:Omitidos++
    Escribir ''
    Escribir ("CONTROL {0} OMITIDO (requisito {1} no creado)" -f $Nombre, $Requisito)
}

# Respuesta con el codigo esperado (p. ej. 201) y un cuerpo leido: si no, el requisito no se cumplio.
function Cumplido($Resultado, [int]$Codigo) { return ($Resultado.Codigo -eq $Codigo -and $Resultado.Leido -is [Collections.IDictionary]) }

function Cuerpo([hashtable]$Datos) {
    Add-Type -AssemblyName System.Web.Extensions
    return (New-Object System.Web.Script.Serialization.JavaScriptSerializer).Serialize($Datos)
}

# Ejecuta un caso, imprime "CONTROL <caso> OK|FALLA (esperado X, obtenido Y)" y devuelve { Codigo, Leido } (TAREA-23-2:
# antes devolvia solo el cuerpo, y un 400 con ProblemDetails pasaba por un requisito cumplido).
# $Clave: en un 400, el campo que debe traer el error. $Verificar (opcional): recibe la respuesta leida y devuelve un texto
# de error o $null.
function Caso([string]$Nombre, [string]$Texto, [string]$Metodo, [string]$Ruta, [string]$Usuario, [int]$Esperado,
              [hashtable]$Datos, [string]$Clave, [scriptblock]$Verificar) {
    $cuerpo = if ($Datos) { Cuerpo $Datos } else { $null }
    $r = Solicitar -Metodo $Metodo -Base $base -Ruta $Ruta -Cuerpo $cuerpo -Encabezados @{ 'X-Dev-User' = $Usuario } `
        -TimeoutSegundos 120 -NombreSimulacion $Nombre
    $leido = $null
    if ($r.Texto) { try { $leido = Leer-Json $r.Texto } catch { } }
    $ok = $r.Codigo -eq $Esperado
    $detalle = $null
    $errores = if ($leido -is [Collections.IDictionary] -and $leido.ContainsKey('errors')) { $leido['errors'] } else { $null }
    if ($ok -and $Clave -and -not ($errores -and $errores.ContainsKey($Clave))) { $ok = $false; $detalle = "sin error en $Clave" }
    if ($ok -and $Verificar) { $detalle = & $Verificar $leido; if ($detalle) { $ok = $false } }
    if (-not $ok) { $script:Fallos++ }

    Escribir ''
    Escribir ("-- {0} [{1}] {2}" -f $Texto, $Usuario, (Describir-Respuesta $r))
    if ($errores) { foreach ($k in $errores.Keys) { Escribir ("   {0}: {1}" -f $k, (Texto-Linea (@($errores[$k]) -join ' '))) } }
    elseif ($leido -is [Collections.IDictionary] -and $leido.ContainsKey('title') -and $r.Codigo -ge 400) {
        Escribir ("   title: {0}" -f (Texto-Linea $leido['title']))
        if ($leido.ContainsKey('codigo')) { Escribir ("   codigo existente: {0}" -f $leido['codigo']) }
    }
    if ($r.Codigo -eq 201 -and $leido -is [Collections.IDictionary]) {
        Escribir ("   creada: id {0}, codigo {1}" -f $leido['id'], $leido['codigo'])
        $script:Creadas.Add("$($leido['codigo']) (id $($leido['id']), $Texto)")
    }
    $estado = if ($ok) { 'OK' } else { 'FALLA' }
    $extra = if ($detalle) { "; $detalle" } else { '' }
    Escribir ("CONTROL {0} {1} (esperado {2}, obtenido {3}{4})" -f $Nombre, $estado, $Esperado, $r.Codigo, $extra)
    return [pscustomobject]@{ Codigo = $r.Codigo; Leido = $leido }
}

function Total($Leido) { if ($Leido -is [Collections.IDictionary] -and $Leido.ContainsKey('total')) { return [int]$Leido['total'] } return -1 }

Iniciar-Salida (Join-Path $PSScriptRoot 'resultado-novedades.txt')
try {
    Escribir "== TAREA-23-1 Novedades: CORRIDA=$corrida (fechas $d0 a $d2), CODIGO_EKON=$codigoEkon, CODIGO_EKON_ADMIN=$codigoEkonAdmin =="

    # Proyecto propio y vigente del gestor (si no se indico).
    $proyectoId = $env:PROYECTO_ID
    if (-not $proyectoId) {
        $lista = Caso 'n00_proyectos' 'proyectos ACTIVO del gestor (para elegir PROYECTO_ID)' 'GET' 'api/proyectos?estado=ACTIVO&tamano=1' 'gestor' 200
        if ((Cumplido $lista 200) -and @($lista.Leido['items']).Count -gt 0) { $proyectoId = [string]@($lista.Leido['items'])[0]['id'] }
    }
    Escribir "PROYECTO_ID=$proyectoId"

    $persona = @{ tipoAplicacion = 'PERSONA'; tipoNovedad = 'VACACIONES'; codigoEkon = $codigoEkon; fechaInicio = $d0; fechaFin = $d1; observacion = 'Prueba TAREA-23-1' }

    # 1 PERSONA (gestor) -> 201
    $c1 = Caso 'n01_persona' 'crear PERSONA VACACIONES' 'POST' 'api/novedades' 'gestor' 201 $persona
    # 2 PROYECTO sobre un proyecto propio vigente -> 201
    if ($proyectoId) {
        [void](Caso 'n02_proyecto' "crear PROYECTO FERIADO (proyecto $proyectoId)" 'POST' 'api/novedades' 'gestor' 201 `
            @{ tipoAplicacion = 'PROYECTO'; tipoNovedad = 'FERIADO'; proyectoId = [int]$proyectoId; fechaInicio = $d0; fechaFin = $d0 })
    }
    else { Omitir 'n02_proyecto' 'n00 (el gestor no tiene proyectos ACTIVO; use set PROYECTO_ID=...)' }
    # 3 y 4 GENERAL
    $general = @{ tipoAplicacion = 'GENERAL'; tipoNovedad = 'FERIADO'; fechaInicio = $d2; fechaFin = $d2 }
    [void](Caso 'n03_general_gestor' 'crear GENERAL como gestor' 'POST' 'api/novedades' 'gestor' 403 $general)
    [void](Caso 'n04_general_admin' 'crear GENERAL como admin' 'POST' 'api/novedades' 'admin' 201 $general)
    # 5 RN16
    [void](Caso 'n05a_rn16_persona' 'DESCANSO en PERSONA' 'POST' 'api/novedades' 'gestor' 400 `
        @{ tipoAplicacion = 'PERSONA'; tipoNovedad = 'DESCANSO'; codigoEkon = $codigoEkon; fechaInicio = $d0; fechaFin = $d0 } 'tipoNovedad')
    [void](Caso 'n05b_rn16_proyecto' 'VACACIONES en PROYECTO' 'POST' 'api/novedades' 'gestor' 400 `
        @{ tipoAplicacion = 'PROYECTO'; tipoNovedad = 'VACACIONES'; proyectoId = 1; fechaInicio = $d0; fechaFin = $d0 } 'tipoNovedad')
    [void](Caso 'n05c_rn16_general' 'PERMISO en GENERAL (admin)' 'POST' 'api/novedades' 'admin' 400 `
        @{ tipoAplicacion = 'GENERAL'; tipoNovedad = 'PERMISO'; fechaInicio = $d0; fechaFin = $d0 } 'tipoNovedad')
    # 6 duplicado exacto (el mismo cuerpo del caso 1)
    if (Cumplido $c1 201) { [void](Caso 'n06_duplicado' 'PERSONA repetida (mismo empleado, tipo y dias)' 'POST' 'api/novedades' 'gestor' 409 $persona) }
    else { Omitir 'n06_duplicado' 'n01' }
    # 7 y 8 fechas
    [void](Caso 'n07_fin_menor' 'fin anterior al inicio' 'POST' 'api/novedades' 'gestor' 400 `
        @{ tipoAplicacion = 'PERSONA'; tipoNovedad = 'PERMISO'; codigoEkon = $codigoEkon; fechaInicio = $d1; fechaFin = $d0 } 'fechaFin')
    [void](Caso 'n08_mas_366' 'rango de 367 dias' 'POST' 'api/novedades' 'gestor' 400 `
        @{ tipoAplicacion = 'PERSONA'; tipoNovedad = 'PERMISO'; codigoEkon = $codigoEkon; fechaInicio = $d0; fechaFin = (& $f $dia0.AddDays(366)) } 'fechaFin')
    # 9 empleado inexistente
    [void](Caso 'n09_ekon_inexistente' 'codigoEkon inexistente' 'POST' 'api/novedades' 'gestor' 400 `
        @{ tipoAplicacion = 'PERSONA'; tipoNovedad = 'PERMISO'; codigoEkon = 'ZZ99999'; fechaInicio = $d0; fechaFin = $d0 } 'codigoEkon')
    # 10 opciones
    [void](Caso 'n10a_opciones_gestor' 'opciones-formulario (gestor)' 'GET' 'api/novedades/opciones-formulario' 'gestor' 200 $null '' {
        param($o) if ($o['puedeRegistrarGeneral']) { return 'puedeRegistrarGeneral = true' }; if (@($o['tiposPorAplicacion']['GENERAL']).Count -ne 0) { return 'GENERAL con tipos' }; $null })
    [void](Caso 'n10b_opciones_admin' 'opciones-formulario (admin)' 'GET' 'api/novedades/opciones-formulario' 'admin' 200 $null '' {
        param($o) if (-not $o['puedeRegistrarGeneral']) { return 'puedeRegistrarGeneral = false' }; $null })
    # 11 listado: PERSONA del admin (no la ve el gestor) y PERMISOS_MEDICOS
    $cAdmin = Caso 'n11a_persona_admin' 'crear PERSONA PERMISO como admin (para 11 y 13)' 'POST' 'api/novedades' 'admin' 201 `
        @{ tipoAplicacion = 'PERSONA'; tipoNovedad = 'PERMISO'; codigoEkon = $codigoEkonAdmin; fechaInicio = $d2; fechaFin = $d2 }
    $adminCreada = Cumplido $cAdmin 201
    if ($adminCreada) {
        $codigoAdmin = $cAdmin.Leido['codigo']
        [void](Caso 'n11b_lista_gestor' "listado del gestor con texto=$codigoAdmin (no la ve)" 'GET' "api/novedades?texto=$codigoAdmin" 'gestor' 200 $null '' {
            param($l) if ((Total $l) -ne 0) { return "total $(Total $l), se esperaba 0" }; $null })
        [void](Caso 'n11c_lista_admin' "listado del admin con texto=$codigoAdmin (la ve)" 'GET' "api/novedades?texto=$codigoAdmin" 'admin' 200 $null '' {
            param($l) if ((Total $l) -ne 1) { return "total $(Total $l), se esperaba 1" }; $null })
    }
    else { Omitir 'n11b_lista_gestor' 'n11a'; Omitir 'n11c_lista_admin' 'n11a' }
    [void](Caso 'n11d_pm_gestor' 'listado del gestor con origen=PERMISOS_MEDICOS' 'GET' 'api/novedades?origen=PERMISOS_MEDICOS&incluirAnuladas=true' 'gestor' 200 $null '' {
        param($l) if ((Total $l) -ne 0) { return "total $(Total $l), se esperaba 0" }; $null })
    [void](Caso 'n11e_pm_admin' 'listado del admin con origen=PERMISOS_MEDICOS (informativo: total >= 0)' 'GET' 'api/novedades?origen=PERMISOS_MEDICOS&incluirAnuladas=true' 'admin' 200 $null '' {
        param($l) Escribir ("   total PERMISOS_MEDICOS (admin): {0}" -f (Total $l)); $null })
    # 12 detalle con version (del caso 1)
    if (Cumplido $c1 201) {
        $id1 = $c1.Leido['id']
        [void](Caso 'n12_detalle' "detalle de la novedad $id1 (gestor)" 'GET' "api/novedades/$id1" 'gestor' 200 $null '' {
            param($n)
            if (-not $n['version']) { return 'sin version' }
            if (@($n['dias']).Count -ne 2) { return "dias $(@($n['dias']).Count), se esperaban 2" }
            if ($n['registro']) { return 'el gestor ve el registrante' }
            if (-not $n['puedeEditar']) { return 'puedeEditar = false' }
            Escribir ("   version: {0}; dias: {1}; puedeEditar: {2}" -f $n['version'], @($n['dias']).Count, $n['puedeEditar']); $null })
    }
    else { Omitir 'n12_detalle' 'n01' }
    # 13 detalle no visible (la PERSONA del admin)
    if ($adminCreada) {
        $idAdmin = $cAdmin.Leido['id']
        [void](Caso 'n13_no_visible' "detalle de la novedad $idAdmin del admin (gestor)" 'GET' "api/novedades/$idAdmin" 'gestor' 404)
    }
    else { Omitir 'n13_no_visible' 'n11a' }
    # 14 sin sesion
    [void](Caso 'n14_sin_sesion' 'listado sin sesion' 'GET' 'api/novedades' 'anonimo' 401)

    Escribir ''
    Escribir 'Novedades creadas en esta corrida (quedan en la base; se anulan con tarea23-2\limpiar.cmd):'
    foreach ($c in $script:Creadas) { Escribir "   $c" }
    Escribir ''
    Escribir "Casos con FALLA: $script:Fallos; OMITIDOS: $script:Omitidos"
    Escribir 'Fin. Pegue en el chat resultado-novedades.txt.'
}
finally { Cerrar-Salida }
