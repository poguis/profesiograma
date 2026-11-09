# ==========================================================================
# TAREA-25-1 - Usuarios y departamentos (Admin): GET/POST /api/admin/usuarios, GET/PUT /api/admin/usuarios/{id},
# PUT /api/admin/usuarios/{id}/departamentos y el efecto inmediato de la baja (gestor2).
# REQUISITO: la API REINICIADA con el appsettings.Development.json de la TAREA-25-1 (DevAuth gestor2 y el dominio de
# pruebas profesiograma.local). Patron obligatorio de tarea62\criterio.ps1 (version corregida).
# ESCRIBE en la base de desarrollo:
#   - usuario de prueba FIJO (correo ficticio del dominio de pruebas): se crea la primera vez y se reutiliza despues; al
#     terminar queda INACTIVO y SIN departamentos (las filas no se borran: la API no borra usuarios ni asignaciones);
#   - gestor2: se da de baja y se reactiva; al terminar queda ACTIVO (tambien tras una falla, en lo posible).
#   set API_BASE=https://localhost:7180
# Muestra SOLO codigos HTTP, Id, conteos, nombres cortos de departamentos y mensajes. NUNCA correos, nombres ni cedulas.
# Salida: resultado-usuarios.txt. Simulacion: SIMULAR=1 (simulacion\<caso>.json + .codigo, ficticios).
# ==========================================================================
. (Join-Path $PSScriptRoot '..\tarea26a\comun.ps1')
. (Join-Path $PSScriptRoot '..\tarea23-2\casos.ps1')
if (-not $env:SIMULACION) { $script:CarpetaSimulacion = Join-Path $PSScriptRoot 'simulacion' }

# --------------------------------------------------------------------------
# Candado de ejecucion (leccion de la TAREA-62-1): se crea ANTES de cualquier llamada y de abrir el resultado.
# --------------------------------------------------------------------------
$ArchivoCandado = Join-Path $PSScriptRoot 'resultado-usuarios.lock'
$script:CandadoPropio = $false
$MensajeCandado = "Ya hay una ejecuci$([char]0xF3)n de usuarios.cmd en curso (o termin$([char]0xF3) mal hace menos de 5 minutos). No se ejecuta nada."
if (Test-Path $ArchivoCandado) {
    if (((Get-Date) - (Get-Item $ArchivoCandado).LastWriteTime).TotalMinutes -lt 5) {
        Write-Host $MensajeCandado
        Write-Host ("Candado: " + ([IO.File]::ReadAllText($ArchivoCandado)).Trim())
        exit 2
    }
    Remove-Item $ArchivoCandado -Force
}
try {
    $flujo = [IO.File]::Open($ArchivoCandado, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    $texto = [Text.Encoding]::ASCII.GetBytes(("inicio {0}; PID {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff'), $PID))
    $flujo.Write($texto, 0, $texto.Length)
    $flujo.Close()
    $script:CandadoPropio = $true
}
catch [IO.IOException] {
    Write-Host $MensajeCandado
    exit 2
}

# Hora local con milisegundos antes de cada caso (envuelve a Caso de tarea23-2\casos.ps1 sin cambiarla).
$script:CasoBase = ${function:Caso}
function Caso {
    Escribir ''
    Escribir ("[{0}]" -f (Get-Date -Format 'HH:mm:ss.fff'))
    return (& $script:CasoBase @args)
}

$script:Base = Base-Url $(if ($env:API_BASE) { $env:API_BASE } else { 'https://localhost:7180' })
if (-not $script:Simular) {
    Permitir-CertificadoLocal
    [Net.ServicePointManager]::Expect100Continue = $false
    [Net.ServicePointManager]::FindServicePoint([Uri]$script:Base).ConnectionLeaseTimeout = 0
}

# Correo FICTICIO del usuario de prueba (dominio de pruebas de appsettings.Development.json). Nunca se imprime.
$EmailPrueba = 'prueba.tarea25@profesiograma.local'
$NombrePrueba = 'PRUEBA TAREA25'
$Ruta = 'api/admin/usuarios'
$ArchivoEstado = Join-Path $PSScriptRoot 'resultado-estado-usuarios.txt'
$tA = [char]0xE1; $tI = [char]0xED; $tO = [char]0xF3; $TO = [char]0xD3

$script:Incompleta = $false
$script:Iniciado = $false
$script:PruebaId = $null      # Id del usuario de prueba (se deja inactivo y sin departamentos al final)
$script:Gestor2Id = $null     # Id de gestor2 (se deja activo al final)

function Mensajes($Leido, [string]$Clave) {
    if ($Leido -is [Collections.IDictionary] -and $Leido['errors'] -is [Collections.IDictionary] -and $Leido['errors'].ContainsKey($Clave)) {
        return @($Leido['errors'][$Clave])
    }
    return @()
}

# Ids de una lista de departamentos de la respuesta, como [int[]] plano (sin PSObject: correccion de la TAREA-62-1).
function Ids-De($Lista) {
    $ids = New-Object 'System.Collections.Generic.List[int]'
    foreach ($x in @($Lista)) { if ($x -is [Collections.IDictionary]) { $ids.Add([int]$x['id']) } }
    return , $ids.ToArray()
}

function Texto-Ids([int[]]$Ids) { if ($Ids.Count -eq 0) { return '(ninguno)' } return ($Ids -join ', ') }

function Detalle([string]$Nombre, [int]$Id, [string]$Texto) {
    $r = Caso $Nombre $Texto 'GET' "$Ruta/$Id" 'admin' 200
    if (Cumplido $r 200) {
        Escribir ("   Id {0}: activo {1}, conIdentidad {2}, departamentos [{3}]" -f $r.Leido['id'], $r.Leido['activo'], $r.Leido['conIdentidad'], (Texto-Ids (Ids-De $r.Leido['departamentos'])))
    }
    return $r
}

# PUT del usuario con los valores que se acaban de leer (compare-and-set). Cuerpo SOLO con [string] y [bool].
function Poner-Activo([string]$Nombre, [string]$Texto, [int]$Id, $Leido, [bool]$Activo, [int]$Esperado, [string]$Clave) {
    $cuerpo = @{ nombreMostrar = [string]$Leido['nombreMostrar']; activo = [bool]$Activo
                 nombreAnterior = [string]$Leido['nombreMostrar']; activoAnterior = [bool]$Leido['activo'] }
    return Caso $Nombre $Texto 'PUT' "$Ruta/$Id" 'admin' $Esperado $cuerpo $Clave
}

# PUT de departamentos. Cuerpo SOLO con [int[]].
function Poner-Departamentos([string]$Nombre, [string]$Texto, [int]$Id, [int[]]$Ids, [int[]]$Anteriores, [int]$Esperado,
                             [string]$Usuario = 'admin', [string]$Clave, [scriptblock]$Verificar) {
    $cuerpo = @{ departamentoIds = [int[]]$Ids; anteriores = [int[]]$Anteriores }
    return Caso $Nombre $Texto 'PUT' "$Ruta/$Id/departamentos" $Usuario $Esperado $cuerpo $Clave -Verificar $Verificar
}

function Guardar-Estado {
    $lineas = @()
    if ($null -ne $script:PruebaId) { $lineas += "prueba=$($script:PruebaId)" }
    if ($null -ne $script:Gestor2Id) { $lineas += "gestor2=$($script:Gestor2Id)" }
    [IO.File]::WriteAllText($ArchivoEstado, ($lineas -join "`r`n"), (New-Object Text.UTF8Encoding($false)))
}

function Restauracion-Pendiente([string]$Que, [string]$Sql) {
    $script:Incompleta = $true
    Escribir ("RESTAURACI{0}N PENDIENTE: {1}. En SSMS (PROFESIOGRAMA_DEV):" -f $TO, $Que)
    Escribir ('   ' + $Sql)
    Escribir ("   Despu{0}s reinicie la API (cach{0} de usuarios de 10 minutos). Se conserva {1}." -f [char]0xE9, (Split-Path $ArchivoEstado -Leaf))
}

# --------------------------------------------------------------------------
# Restauracion (siempre, tambien tras una falla): prueba inactivo y sin departamentos; gestor2 activo.
# --------------------------------------------------------------------------
function Restaurar-Prueba {
    if ($null -eq $script:PruebaId) { return $true }
    $id = [int]$script:PruebaId
    $sql = "UPDATE dbo.UsuarioDepartamento SET Activo = 0, ModificadoPorId = 1, FechaModificacion = SYSUTCDATETIME() WHERE UsuarioId = $id AND Activo = 1; UPDATE dbo.Usuario SET Activo = 0, ModificadoPorId = 1, FechaModificacion = SYSUTCDATETIME() WHERE Id = $id;"
    $d = Detalle 'r00_detalle_prueba' $id 'usuario de prueba antes de restaurar'
    if (-not (Cumplido $d 200)) { Restauracion-Pendiente "el usuario de prueba (Id $id) debe quedar inactivo y sin departamentos" $sql; return $false }
    $actuales = Ids-De $d.Leido['departamentos']
    if ($actuales.Count -gt 0) {
        $r = Poner-Departamentos 'r01_sin_departamentos' 'retirar los departamentos del usuario de prueba' $id ([int[]]@()) $actuales 200
        if (-not (Cumplido $r 200)) { Restauracion-Pendiente "el usuario de prueba (Id $id) qued$tO con departamentos" $sql; return $false }
    }
    if ($d.Leido['activo']) {
        $r = Poner-Activo 'r02_baja_prueba' 'dejar inactivo al usuario de prueba' $id $d.Leido $false 200
        if (-not (Cumplido $r 200)) { Restauracion-Pendiente "el usuario de prueba (Id $id) qued$tO activo" $sql; return $false }
    }
    Escribir ("Usuario de prueba (Id {0}): inactivo y sin departamentos." -f $id)
    return $true
}

function Restaurar-Gestor2 {
    if ($null -eq $script:Gestor2Id) { return $true }
    $id = [int]$script:Gestor2Id
    $sql = "UPDATE dbo.Usuario SET Activo = 1, ModificadoPorId = 1, FechaModificacion = SYSUTCDATETIME() WHERE Id = $id;"
    $d = Detalle 'r03_detalle_gestor2' $id 'gestor2 antes de restaurar'
    if (-not (Cumplido $d 200)) { Restauracion-Pendiente "gestor2 (Id $id) debe quedar ACTIVO" $sql; return $false }
    if (-not $d.Leido['activo']) {
        $r = Poner-Activo 'r04_reactivar_gestor2' 'reactivar a gestor2' $id $d.Leido $true 200
        if (-not (Cumplido $r 200)) { Restauracion-Pendiente "gestor2 (Id $id) qued$tO INACTIVO" $sql; return $false }
    }
    Escribir ("gestor2 (Id {0}): activo." -f $id)
    return $true
}

function Restaurar {
    if ($null -eq $script:PruebaId -and $null -eq $script:Gestor2Id) { return }
    Escribir ''
    Escribir '== Restauracion =='
    $ok = $true
    try { $ok = (Restaurar-Prueba) -and $ok }
    catch {
        Escribir ("ERROR NO CONTROLADO restaurando el usuario de prueba: {0} (linea {1})" -f $_.Exception.Message, $_.InvocationInfo.ScriptLineNumber)
        Restauracion-Pendiente "el usuario de prueba (Id $($script:PruebaId)) debe quedar inactivo y sin departamentos" "UPDATE dbo.UsuarioDepartamento SET Activo = 0, ModificadoPorId = 1, FechaModificacion = SYSUTCDATETIME() WHERE UsuarioId = $($script:PruebaId) AND Activo = 1; UPDATE dbo.Usuario SET Activo = 0, ModificadoPorId = 1, FechaModificacion = SYSUTCDATETIME() WHERE Id = $($script:PruebaId);"
        $ok = $false
    }
    try { $ok = (Restaurar-Gestor2) -and $ok }
    catch {
        Escribir ("ERROR NO CONTROLADO restaurando a gestor2: {0} (linea {1})" -f $_.Exception.Message, $_.InvocationInfo.ScriptLineNumber)
        Restauracion-Pendiente "gestor2 (Id $($script:Gestor2Id)) debe quedar ACTIVO" "UPDATE dbo.Usuario SET Activo = 1, ModificadoPorId = 1, FechaModificacion = SYSUTCDATETIME() WHERE Id = $($script:Gestor2Id);"
        $ok = $false
    }
    if ($ok -and (Test-Path $ArchivoEstado)) { Remove-Item $ArchivoEstado; Escribir 'Archivo de estado borrado (restauracion completa).' }
}

# --------------------------------------------------------------------------
# Casos
# --------------------------------------------------------------------------
function Casos-Usuario {
    # c00: lista (y reutilizacion del usuario de prueba si ya existe).
    $r = Caso 'c00_lista' 'GET lista (texto del usuario de prueba, incluidos inactivos)' 'GET' "${Ruta}?texto=prueba.tarea25&incluirInactivos=true" 'admin' 200 -Verificar {
        param($l) Escribir ("   total {0}" -f $l['total']); $null
    }
    if (-not (Cumplido $r 200)) { Escribir 'Sin la lista no se continua (nada se cambio).'; $script:Incompleta = $true; return }
    $existente = $null
    foreach ($u in @($r.Leido['items'])) { if ($u -is [Collections.IDictionary] -and [string]$u['email'] -eq $EmailPrueba) { $existente = [int]$u['id'] } }

    # c01: alta 201, o 409 con usuarioId si ya existe (reutilizacion).
    $alta = @{ email = [string]$EmailPrueba; nombreMostrar = [string]$NombrePrueba }
    if ($null -eq $existente) {
        $c01 = Caso 'c01_alta' 'POST alta del usuario de prueba' 'POST' $Ruta 'admin' 201 $alta
        if (Cumplido $c01 201) { $script:PruebaId = [int]$c01.Leido['id'] }
    }
    else {
        $c01 = Caso 'c01_alta' "POST alta: ya existe (Id $existente), se reutiliza" 'POST' $Ruta 'admin' 409 $alta -Verificar {
            param($l) if ([int]$l['usuarioId'] -ne $existente) { "usuarioId $($l['usuarioId'])" } else { $null }
        }
        $script:PruebaId = $existente
    }
    if ($null -eq $script:PruebaId) { Omitir 'c02..c13' 'c01'; return }
    $id = [int]$script:PruebaId
    Guardar-Estado
    Escribir ("   usuario de prueba: Id {0}" -f $id)

    # c02..c04: duplicado y validaciones del correo.
    [void](Caso 'c02_duplicado' 'POST el mismo correo otra vez' 'POST' $Ruta 'admin' 409 $alta -Verificar {
        param($l) if ([int]$l['usuarioId'] -ne $id) { "usuarioId $($l['usuarioId']) (esperado $id)" } else { $null }
    })
    [void](Caso 'c03_dominio' 'POST con un dominio no permitido' 'POST' $Ruta 'admin' 400 @{ email = 'prueba.tarea25@otro-dominio.test'; nombreMostrar = [string]$NombrePrueba } 'email' -Verificar {
        param($l) if (-not ((Mensajes $l 'email') -join ' ').StartsWith('El correo debe ser de un dominio permitido:')) { 'mensaje distinto' } else { $null }
    })
    [void](Caso 'c04_invalido' 'POST con un correo invalido' 'POST' $Ruta 'admin' 400 @{ email = 'no-es-un-correo'; nombreMostrar = [string]$NombrePrueba } 'email' -Verificar {
        param($l) if ((Mensajes $l 'email') -notcontains "El correo no tiene un formato v$($tA)lido.") { 'mensaje distinto' } else { $null }
    })

    # c05: detalle; c06: activo (punto de partida, aunque una corrida anterior lo dejo inactivo).
    $d = Detalle 'c05_detalle' $id 'GET detalle del usuario de prueba'
    if (-not (Cumplido $d 200)) { Omitir 'c06..c12' 'c05'; return }
    $disponibles = New-Object 'System.Collections.Generic.List[int]'
    foreach ($x in @($d.Leido['departamentosDisponibles'])) {
        if ($x -is [Collections.IDictionary] -and $x['activo'] -and $disponibles.Count -lt 2) { $disponibles.Add([int]$x['id']) }
    }
    Escribir ("   departamentos activos elegidos para la prueba: [{0}]" -f (Texto-Ids $disponibles.ToArray()))
    [void](Poner-Activo 'c06_activar' 'PUT activo (punto de partida)' $id $d.Leido $true 200)

    # c07..c10: departamentos.
    if ($disponibles.Count -eq 0) { Omitir 'c07..c10' 'departamentos activos' }
    else {
        [int[]]$elegidos = $disponibles.ToArray()
        $antes = Ids-De $d.Leido['departamentos']
        [void](Poner-Departamentos 'c07_asignar' "PUT departamentos [$(Texto-Ids $elegidos)]" $id $elegidos $antes 200 -Verificar {
            param($l) $ids = Ids-De $l['departamentos']; Escribir ("   resultado: [{0}]" -f (Texto-Ids $ids)); if ($ids.Count -ne $elegidos.Count) { 'conjunto distinto' } else { $null }
        })
        [void](Poner-Departamentos 'c08_desfasado' 'PUT con anteriores desfasados (vacio)' $id ([int[]]@($elegidos[0])) ([int[]]@()) 409 -Verificar {
            param($l) if ([string]$l['title'] -ne "Otro usuario cambi$($tO) los departamentos; recargue.") { 'mensaje distinto' } else { $null }
        })
        [void](Poner-Departamentos 'c09_inexistente' 'PUT con un departamento inexistente (999999)' $id ([int[]]@(999999)) $elegidos 400 'admin' 'departamentoIds')
        [void](Poner-Departamentos 'c10_vacio' 'PUT conjunto vacio' $id ([int[]]@()) $elegidos 200 -Verificar {
            param($l) if ((Ids-De $l['departamentos']).Count -ne 0) { 'no quedo vacio' } else { $null }
        })
    }

    # c11 / c12: baja y reactivacion del usuario de prueba.
    $d = Detalle 'c11a_detalle' $id 'GET detalle antes de la baja'
    if (Cumplido $d 200) {
        $b = Poner-Activo 'c11_baja' 'PUT baja del usuario de prueba' $id $d.Leido $false 200
        $d = Detalle 'c12a_detalle' $id 'GET detalle antes de reactivar'
        if ((Cumplido $b 200) -and (Cumplido $d 200)) { [void](Poner-Activo 'c12_reactivar' 'PUT reactivacion' $id $d.Leido $true 200) }
        else { Omitir 'c12_reactivar' 'c11_baja' }
    }
    else { Omitir 'c11..c12' 'c11a_detalle' }
}

function Casos-Admin-Y-Gestor {
    # c13: el Admin no puede darse de baja a si mismo.
    $me = Caso 'c13a_me_admin' 'GET /api/usuarios/me (admin)' 'GET' 'api/usuarios/me' 'admin' 200
    if (Cumplido $me 200) {
        $adminId = [int]$me.Leido['id']
        $d = Detalle 'c13b_detalle_admin' $adminId 'GET detalle del admin'
        if (Cumplido $d 200) {
            [void](Poner-Activo 'c13c_baja_propia' 'PUT baja de si mismo' $adminId $d.Leido $false 400 'activo')
        }
        else { Omitir 'c13c_baja_propia' 'c13b' }
    }
    else { Omitir 'c13b..c13c' 'c13a' }

    # c14: el gestor recibe 403 en todos los endpoints (Id del usuario de prueba o 1 si no se creo).
    $id = if ($null -ne $script:PruebaId) { [int]$script:PruebaId } else { 1 }
    [void](Caso 'c14a_gestor_lista' 'GET lista como gestor' 'GET' $Ruta 'gestor' 403)
    [void](Caso 'c14b_gestor_detalle' 'GET detalle como gestor' 'GET' "$Ruta/$id" 'gestor' 403)
    [void](Caso 'c14c_gestor_alta' 'POST alta como gestor' 'POST' $Ruta 'gestor' 403 @{ email = [string]$EmailPrueba; nombreMostrar = [string]$NombrePrueba })
    [void](Caso 'c14d_gestor_put' 'PUT usuario como gestor' 'PUT' "$Ruta/$id" 'gestor' 403 @{ nombreMostrar = 'X'; activo = $false; nombreAnterior = 'X'; activoAnterior = $true })
    [void](Poner-Departamentos 'c14e_gestor_departamentos' 'PUT departamentos como gestor' $id ([int[]]@()) ([int[]]@()) 403 'gestor')
}

# O8: efecto inmediato de la baja (sin esperar los 10 minutos de la cache).
function Casos-Efecto-Inmediato {
    $e01 = Caso 'e01_gestor2_me' 'GET /api/usuarios/me (gestor2 queda aprovisionado)' 'GET' 'api/usuarios/me' 'gestor2' 200
    if (-not (Cumplido $e01 200)) {
        Escribir '   gestor2 no respondio 200: reinicie la API con el appsettings.Development.json de la TAREA-25-1 (DevAuth gestor2).'
        Omitir 'e02..e06' 'e01'
        return
    }
    $script:Gestor2Id = [int]$e01.Leido['id']
    Guardar-Estado
    $d = Detalle 'e02_detalle_gestor2' $script:Gestor2Id 'GET detalle de gestor2'
    if (-not (Cumplido $d 200)) { Omitir 'e03..e06' 'e02'; return }
    if (-not $d.Leido['activo']) { Escribir '   gestor2 estaba INACTIVO (corrida anterior incompleta?): se reactiva al final.' }
    $b = Poner-Activo 'e03_baja_gestor2' 'PUT baja de gestor2 (admin)' $script:Gestor2Id $d.Leido $false 200
    if (-not (Cumplido $b 200)) { Omitir 'e04..e06' 'e03'; return }
    [void](Caso 'e04_gestor2_inactivo' 'GET /api/usuarios/me (gestor2) INMEDIATAMENTE despues de la baja' 'GET' 'api/usuarios/me' 'gestor2' 403 -Verificar {
        param($l) Escribir ("   detail: {0}" -f (Texto-Linea ([string]$l['detail']))); if (-not ([string]$l['detail']).StartsWith('El usuario est')) { 'detail distinto' } else { $null }
    })
    $d = Detalle 'e05a_detalle_gestor2' $script:Gestor2Id 'GET detalle de gestor2 antes de reactivar'
    if (-not (Cumplido $d 200)) { Omitir 'e05..e06' 'e05a'; return }
    $a = Poner-Activo 'e05_reactivar_gestor2' 'PUT reactivacion de gestor2' $script:Gestor2Id $d.Leido $true 200
    if (Cumplido $a 200) { [void](Caso 'e06_gestor2_activo' 'GET /api/usuarios/me (gestor2) despues de reactivar' 'GET' 'api/usuarios/me' 'gestor2' 200) }
    else { Omitir 'e06' 'e05' }
}

function Ejecutar-Casos {
    Escribir '== TAREA-25-1: usuarios y departamentos (requiere la API reiniciada con DevAuth gestor2) =='
    if (Test-Path $ArchivoEstado) {
        Escribir ''
        Escribir ("ADVERTENCIA: una corrida anterior NO termin{0} la restauraci{0}n ({1}): esta corrida la repite al final." -f $tO, ((([IO.File]::ReadAllText($ArchivoEstado)).Trim()) -replace "\s+", '; '))
    }
    Casos-Usuario
    Casos-Admin-Y-Gestor
    Casos-Efecto-Inmediato
}

# --------------------------------------------------------------------------
# Ejecucion: try (casos) / catch (ERROR NO CONTROLADO) / finally (restauracion, resumen, Fin. y candado).
# --------------------------------------------------------------------------
try {
    Iniciar-Salida (Join-Path $PSScriptRoot 'resultado-usuarios.txt')
    $script:Iniciado = $true
    Escribir ("Proceso: PID {0}" -f $PID)
    Ejecutar-Casos
}
catch {
    $script:Incompleta = $true
    $mensaje = "ERROR NO CONTROLADO: {0} (linea {1} de {2})" -f $_.Exception.Message, $_.InvocationInfo.ScriptLineNumber, (Split-Path ([string]$_.InvocationInfo.ScriptName) -Leaf)
    if ($script:Iniciado) { Escribir ''; Escribir $mensaje } else { Write-Host $mensaje }
}
finally {
    if ($script:Iniciado) {
        Restaurar
        if ($script:Incompleta) {
            Escribir ''
            Escribir '=================================================================='
            Escribir ("EJECUCI{0}N INCOMPLETA: no repita el script sin leer este mensaje." -f $TO)
            Escribir '=================================================================='
        }
        Resumen-Final 'resultado-usuarios.txt'
        Cerrar-Salida
    }
    if ($script:CandadoPropio -and (Test-Path $ArchivoCandado)) { Remove-Item $ArchivoCandado -Force }
}
exit $(if ($script:Incompleta) { 2 } elseif ($script:Fallos -gt 0) { 1 } else { 0 })
