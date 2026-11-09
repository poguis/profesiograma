# ==========================================================================
# TAREA-25-3-1 - CargoInfor (Admin): GET /api/admin/cargos-infor, GET y POST /api/admin/cargos-infor/importacion y
# PUT /api/admin/cargos-infor/{id}. Patron obligatorio de tarea62\criterio.ps1 (version corregida).
# Corre en el modo de empleados ACTUAL (Http en Development por user-secrets).
# ESCRIBE en la base de desarrollo:
#   - la importacion inserta en CargoInfor los puestos de la API que faltan (NO se revierten; nunca se borran filas);
#   - c04..c09 editan UN cargo elegido del listado (sin codigo y sin personas guardadas) y lo dejan como estaba
#     (sin codigo y activo); la restauracion corre siempre, tambien tras una falla.
# Orden fijo: c00 lista, c01 vista previa, c02 importar, c03 importar otra vez; DESPUES elige el candidato (sin codigo y
# sin personas guardadas). Si no hay ninguno, se DETIENE antes de c04 sin escribir nada mas (la importacion no se revierte).
#   set API_BASE=https://localhost:7180
# Muestra SOLO codigos HTTP, Id, conteos, el cargo elegido y codigos Infor de prueba. NUNCA nombres, cedulas ni correos.
# Salida: resultado-cargos.txt. Simulacion: SIMULAR=1 (simulacion\<caso>.json + .codigo, ficticios).
# ==========================================================================
. (Join-Path $PSScriptRoot '..\tarea26a\comun.ps1')
. (Join-Path $PSScriptRoot '..\tarea23-2\casos.ps1')
if (-not $env:SIMULACION) { $script:CarpetaSimulacion = Join-Path $PSScriptRoot 'simulacion' }

# --------------------------------------------------------------------------
# Candado de ejecucion (leccion de la TAREA-62-1): se crea ANTES de cualquier llamada y de abrir el resultado.
# --------------------------------------------------------------------------
$ArchivoCandado = Join-Path $PSScriptRoot 'resultado-cargos.lock'
$script:CandadoPropio = $false
$MensajeCandado = "Ya hay una ejecuci$([char]0xF3)n de cargos.cmd en curso (o termin$([char]0xF3) mal hace menos de 5 minutos). No se ejecuta nada."
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

$Ruta = 'api/admin/cargos-infor'
$CodigoPrueba = 'prueba-25.3'        # en minusculas a proposito: la API lo guarda en mayusculas
$CodigoGuardado = 'PRUEBA-25.3'
$ArchivoEstado = Join-Path $PSScriptRoot 'resultado-estado-cargos.txt'
$tA = [char]0xE1; $tO = [char]0xF3; $TO = [char]0xD3

$script:Incompleta = $false
$script:Iniciado = $false
$script:CargoId = $null       # Id del cargo editado en c04..c09 (se restaura: sin codigo y activo)
$script:CargoTexto = $null

function Mensajes($Leido, [string]$Clave) {
    if ($Leido -is [Collections.IDictionary] -and $Leido['errors'] -is [Collections.IDictionary] -and $Leido['errors'].ContainsKey($Clave)) {
        return @($Leido['errors'][$Clave])
    }
    return @()
}

# PUT del cargo. Cuerpo SOLO con [string], $null y [bool] (correccion de la TAREA-62-1: nada salido de un pipeline).
function Poner([string]$Nombre, [string]$Texto, [int]$Id, $Codigo, [bool]$Activo, $Anterior, [bool]$ActivoAnterior, [int]$Esperado,
               [string]$Usuario = 'admin', [string]$Clave, [scriptblock]$Verificar) {
    $c = if ($null -eq $Codigo) { $null } else { [string]$Codigo }
    $a = if ($null -eq $Anterior) { $null } else { [string]$Anterior }
    $cuerpo = @{ codigoInfor = $c; activo = [bool]$Activo; codigoInforAnterior = $a; activoAnterior = [bool]$ActivoAnterior }
    return Caso $Nombre $Texto 'PUT' "$Ruta/$Id" $Usuario $Esperado $cuerpo $Clave -Verificar $Verificar
}

# Busca un cargo activo SIN codigo y SIN personas guardadas (hasta 20 paginas de 100). Devuelve su fila o $null.
function Buscar-Candidato([string]$Prefijo) {
    for ($p = 1; $p -le 20; $p++) {
        $r = Caso ("{0}_p{1}" -f $Prefijo, $p) "GET listado sin codigo, pagina $p" 'GET' "${Ruta}?filtro=sinCodigo&tamano=100&pagina=$p" 'admin' 200
        if (-not (Cumplido $r 200)) { return $null }
        Escribir ("   total sin codigo (activos): {0}" -f $r.Leido['total'])
        foreach ($f in @($r.Leido['items'])) {
            if ($f -is [Collections.IDictionary] -and $null -eq $f['codigoInfor'] -and [int]$f['personasGuardadas'] -eq 0 -and $f['activo']) { return $f }
        }
        if (($p * 100) -ge [int]$r.Leido['total']) { return $null }
    }
    return $null
}

# Fila actual del cargo (por Id, buscando por su texto, incluidos inactivos). $null si no se pudo leer.
function Leer-Cargo([string]$Nombre) {
    $texto = [Uri]::EscapeDataString([string]$script:CargoTexto)
    $r = Caso $Nombre "GET el cargo Id $($script:CargoId)" 'GET' "${Ruta}?texto=$texto&incluirInactivos=true&tamano=100" 'admin' 200
    if (-not (Cumplido $r 200)) { return $null }
    foreach ($f in @($r.Leido['items'])) { if ($f -is [Collections.IDictionary] -and [int]$f['id'] -eq [int]$script:CargoId) { return $f } }
    return $null
}

function Restauracion-Pendiente([string]$Que) {
    $script:Incompleta = $true
    Escribir ("RESTAURACI{0}N PENDIENTE: {1}. En SSMS (PROFESIOGRAMA_DEV):" -f $TO, $Que)
    Escribir ("   UPDATE dbo.CargoInfor SET CodigoInfor = NULL, Activo = 1, ModificadoPorId = 1, FechaModificacion = SYSUTCDATETIME() WHERE Id = {0};" -f $script:CargoId)
    Escribir ("   Despu{0}s borre {1}." -f [char]0xE9, (Split-Path $ArchivoEstado -Leaf))
}

# Deja el cargo como estaba al elegirlo: sin codigo y activo (siempre, tambien tras una falla).
function Restaurar {
    if ($null -eq $script:CargoId) { return }
    Escribir ''
    Escribir '== Restauracion =='
    $f = Leer-Cargo 'r00_leer'
    if ($null -eq $f) { Restauracion-Pendiente "no se pudo leer el cargo Id $($script:CargoId)"; return }
    if ($null -eq $f['codigoInfor'] -and $f['activo']) { Escribir ("Cargo Id {0}: ya sin c{1}digo y activo." -f $script:CargoId, $tO) }
    else {
        $r = Poner 'r01_restaurar' 'PUT sin codigo y activo' ([int]$script:CargoId) $null $true $f['codigoInfor'] ([bool]$f['activo']) 200
        if (-not (Cumplido $r 200)) { Restauracion-Pendiente "el cargo Id $($script:CargoId) no qued$tO sin c$($tO)digo y activo"; return }
        Escribir ("Cargo Id {0}: restaurado (sin c{1}digo y activo)." -f $script:CargoId, $tO)
    }
    if (Test-Path $ArchivoEstado) { Remove-Item $ArchivoEstado; Escribir 'Archivo de estado borrado (restauracion completa).' }
}

# $ControlExtra (no $Verificar): dentro de Caso, $Verificar es el bloque de abajo y se llamaria a si mismo.
function Importar([string]$Nombre, [string]$Texto, [scriptblock]$ControlExtra) {
    $script:ControlImportacion = $ControlExtra
    return Caso $Nombre $Texto 'POST' "$Ruta/importacion" 'admin' 200 -Verificar {
        param($l)
        Escribir ("   puestosApi {0}, insertados {1}, yaExistentes {2}, existentesInactivos {3}" -f $l['puestosApi'], $l['insertados'], $l['yaExistentes'], $l['existentesInactivos'])
        if ($script:ControlImportacion) { & $script:ControlImportacion $l } else { $null }
    }
}

function Ejecutar-Casos {
    Escribir '== TAREA-25-3-1: cargos Infor =='
    Escribir 'Modo de empleados esperado: Http (ERP real, por user-secrets). La importacion lee la API real.'
    Escribir ("AVISO: la importaci{0}n (c02) deja filas PERMANENTES en CargoInfor (no se revierten ni se borran)." -f $tO)
    if (Test-Path $ArchivoEstado) {
        $previo = ([IO.File]::ReadAllText($ArchivoEstado)).Trim()
        Escribir ''
        Escribir ("ADVERTENCIA: una corrida anterior NO termin{0} la restauraci{0}n ({1}). Rest{2}urela con el SQL del LEEME antes de seguir." -f $tO, $previo, $tA)
        $script:Incompleta = $true
        return
    }

    # c00: listado (lectura).
    [void](Caso 'c00_lista' 'GET listado (activos, todos los filtros por defecto)' 'GET' $Ruta 'admin' 200 -Verificar {
        param($l) Escribir ("   total de cargos activos: {0}" -f $l['total']); $null
    })
    # c01: vista previa (lectura, con la lista en cache).
    [void](Caso 'c01_vista_previa' 'GET vista previa de la importacion' 'GET' "$Ruta/importacion" 'admin' 200 -Verificar {
        param($l)
        Escribir ("   puestosApi {0}, nuevos {1}, yaExistentes {2}, existentesInactivos {3}, fusionados {4}, gruposVariantes {5}, esAnterior {6}" -f `
            $l['puestosApi'], $l['nuevos'], $l['yaExistentes'], $l['existentesInactivos'], $l['fusionados'], $l['gruposVariantes'], $l['esAnterior'])
        $null
    })
    # c02 / c03: importar dos veces (la segunda no inserta nada: idempotente). Las filas importadas NO se revierten.
    [void](Importar 'c02_importar' 'POST importacion (lectura fresca)' $null)
    [void](Importar 'c03_reimportar' 'POST importacion otra vez' { param($l) if ([int]$l['insertados'] -ne 0) { "insertados $($l['insertados'])" } else { $null } })

    # DESPUES de importar: candidato (lectura). Sin candidato se detiene antes de c04: no se escribe nada mas y no hay nada
    # que restaurar (la importacion no se revierte; el archivo de estado no se crea).
    $candidato = Buscar-Candidato 'c03b_lista'
    if ($null -eq $candidato) {
        Escribir ''
        Escribir ("No hay ning{0}n cargo activo sin c{1}digo y sin personas guardadas, ni siquiera tras importar: el script se detiene antes de c04." -f [char]0xFA, $tO)
        Escribir ("No se escribi{0} nada m{1}s; no hay nada que restaurar (las filas importadas en c02 quedan, como siempre)." -f $tO, $tA)
        $script:Incompleta = $true
        return
    }

    if ($null -ne $candidato) {
        $script:CargoId = [int]$candidato['id']
        $script:CargoTexto = [string]$candidato['cargo']
        [IO.File]::WriteAllText($ArchivoEstado, ("cargo={0}; original: sin codigo, activo" -f $script:CargoId), (New-Object Text.UTF8Encoding($false)))
        Escribir ''
        Escribir ("Cargo elegido: Id {0} [{1}] (sin c{2}digo, sin personas guardadas)" -f $script:CargoId, $script:CargoTexto, $tO)
        $id = [int]$script:CargoId

        # c04..c09: codigo, concurrencia, validaciones, baja y reactivacion.
        [void](Poner 'c04_codigo' "PUT codigo [$CodigoPrueba]" $id $CodigoPrueba $true $null $true 200 -Verificar {
            param($l) if ([string]$l['codigoInfor'] -cne $CodigoGuardado) { "codigoInfor [$($l['codigoInfor'])]" } else { $null }
        })
        [void](Poner 'c05_desfasado' 'PUT con el anterior desfasado (sin codigo)' $id 'OTRO' $true $null $true 409 -Verificar {
            param($l) if ([string]$l['title'] -ne "Otro usuario cambi$($tO) el cargo; recargue.") { 'mensaje distinto' } else { $null }
        })
        [void](Poner 'c06_formato' 'PUT con un codigo invalido (P 1)' $id 'P 1' $true $CodigoGuardado $true 400 'admin' 'codigoInfor')
        [void](Poner 'c07_inexistente' 'PUT a un Id inexistente (999999999)' 999999999 'X' $true $null $true 404)
        [void](Poner 'c08_baja' 'PUT baja (conserva el codigo)' $id $CodigoGuardado $false $CodigoGuardado $true 200)
        [void](Poner 'c09_reactivar' 'PUT reactivar y quitar el codigo (estado original)' $id $null $true $CodigoGuardado $false 200 -Verificar {
            param($l) if ($null -ne $l['codigoInfor'] -or -not $l['activo']) { 'no quedo sin codigo y activo' } else { $null }
        })
    }

    # c10: el gestor recibe 403 en los 4 endpoints.
    $idGestor = if ($null -ne $script:CargoId) { [int]$script:CargoId } else { 1 }
    [void](Caso 'c10a_gestor_lista' 'GET listado como gestor' 'GET' $Ruta 'gestor' 403)
    [void](Caso 'c10b_gestor_vista' 'GET vista previa como gestor' 'GET' "$Ruta/importacion" 'gestor' 403)
    [void](Caso 'c10c_gestor_importar' 'POST importacion como gestor' 'POST' "$Ruta/importacion" 'gestor' 403)
    [void](Poner 'c10d_gestor_put' 'PUT como gestor' $idGestor 'X' $true $null $true 403 'gestor')
}

# --------------------------------------------------------------------------
# Ejecucion: try (casos) / catch (ERROR NO CONTROLADO) / finally (restauracion, resumen, Fin. y candado).
# --------------------------------------------------------------------------
try {
    Iniciar-Salida (Join-Path $PSScriptRoot 'resultado-cargos.txt')
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
        try { Restaurar }
        catch {
            Escribir ''
            Escribir ("ERROR NO CONTROLADO en la restauracion: {0} (linea {1})" -f $_.Exception.Message, $_.InvocationInfo.ScriptLineNumber)
            Restauracion-Pendiente "fallo la restauracion del cargo Id $($script:CargoId)"
        }
        if ($script:Incompleta) {
            Escribir ''
            Escribir '=================================================================='
            Escribir ("EJECUCI{0}N INCOMPLETA: no repita el script sin leer este mensaje." -f $TO)
            Escribir '=================================================================='
        }
        Resumen-Final 'resultado-cargos.txt'
        Cerrar-Salida
    }
    if ($script:CandadoPropio -and (Test-Path $ArchivoCandado)) { Remove-Item $ArchivoCandado -Force }
}
exit $(if ($script:Incompleta) { 2 } elseif ($script:Fallos -gt 0) { 1 } else { 0 })
