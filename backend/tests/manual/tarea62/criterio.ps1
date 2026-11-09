# ==========================================================================
# TAREA-62-1 - Criterio de personal elegible (Parametro EMPLEADO_FAMILIAS_ASIGNABLES): GET /api/admin/empleados/familias,
# PUT /api/admin/empleados/familias-asignables, el filtro en GET /api/empleados y en POST /api/novedades (PERSONA).
# REQUISITO: la API con ServiciosExternos:Modo = Simulado (SIM010 es OPERATIVO solo en el ERP simulado) y DevAuth.
# ESCRIBE en la base de desarrollo:
#   - cambia el criterio y al final RESTAURA el valor original (tambien tras una falla, en lo posible);
#   - crea 1 novedad PERSONA de SIM010 con fechas de 2070 (su Id se imprime: anularla con tarea23-2\limpiar.cmd).
#   set CORRIDA=7              (desplaza las fechas para repetir sin 409; por defecto se deriva del reloj)
#   set API_BASE=https://localhost:7180
# Muestra SOLO codigos HTTP, familias, conteos, codigos NOV-/SIM y mensajes. NUNCA nombres, cedulas ni correos.
# Salida: resultado-criterio.txt. Simulacion: SIMULAR=1 (simulacion\<caso>.json + .codigo, ficticios).
# ==========================================================================
. (Join-Path $PSScriptRoot '..\tarea26a\comun.ps1')
. (Join-Path $PSScriptRoot '..\tarea23-2\casos.ps1')
if (-not $env:SIMULACION) { $script:CarpetaSimulacion = Join-Path $PSScriptRoot 'simulacion' }

# --------------------------------------------------------------------------
# Candado de ejecucion (diagnostico 2 de la TAREA-62-1: dos ejecuciones simultaneas se pisaban el criterio y la novedad).
# Se crea ANTES de cualquier llamada a la API y de abrir resultado-criterio.txt; se borra en el finally del final.
# Un candado de menos de 5 minutos = otra ejecucion en curso (o una que termino mal hace poco): se aborta sin llamar a la API.
# --------------------------------------------------------------------------
$ArchivoCandado = Join-Path $PSScriptRoot 'resultado-criterio.lock'
$script:CandadoPropio = $false
$MensajeCandado = "Ya hay una ejecuci$([char]0xF3)n de criterio.cmd en curso (o termin$([char]0xF3) mal hace menos de 5 minutos). No se ejecuta nada."
if (Test-Path $ArchivoCandado) {
    if (((Get-Date) - (Get-Item $ArchivoCandado).LastWriteTime).TotalMinutes -lt 5) {
        Write-Host $MensajeCandado
        Write-Host ("Candado: " + ([IO.File]::ReadAllText($ArchivoCandado)).Trim())
        exit 2
    }
    Remove-Item $ArchivoCandado -Force # candado viejo: una ejecucion anterior termino mal hace mas de 5 minutos
}
try {
    # CreateNew es atomico: si otra ejecucion lo creo entre medio, este Open falla y se aborta.
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

# Hora local con milisegundos antes de cada caso: envuelve a Caso (tarea23-2\casos.ps1) sin cambiarla.
$script:CasoBase = ${function:Caso}
function Caso {
    Escribir ''
    Escribir ("[{0}]" -f (Get-Date -Format 'HH:mm:ss.fff'))
    return (& $script:CasoBase @args)
}

$script:Base = Base-Url $(if ($env:API_BASE) { $env:API_BASE } else { 'https://localhost:7180' })
if (-not $script:Simular) {
    Permitir-CertificadoLocal
    # Diagnostico de la TAREA-62-1 (c06 recibio 409 con su propio codigo): HttpWebRequest de .NET Framework puede REENVIAR
    # un POST con cuerpo en memoria si una conexion keep-alive reutilizada falla [SUPUESTO]. Cada solicitud usa una conexion
    # nueva (ConnectionLeaseTimeout = 0) y sin "Expect: 100-continue": no hay conexion reutilizada que reintentar.
    [Net.ServicePointManager]::Expect100Continue = $false
    [Net.ServicePointManager]::FindServicePoint([Uri]$script:Base).ConnectionLeaseTimeout = 0
}
# Valor original de una corrida que no pudo restaurarlo (se borra al restaurar con exito). Nunca contiene datos personales.
$ArchivoEstado = Join-Path $PSScriptRoot 'resultado-estado-criterio.txt'
$Semilla = 'ADMINISTRATIVO'
$corrida = if ($env:CORRIDA) { [int]$env:CORRIDA } else { [int](((Get-Date) - [datetime]'2026-10-01').TotalMinutes % 300) }
$cultura = [Globalization.CultureInfo]::InvariantCulture
$inicio = ([datetime]'2070-01-01').AddDays($corrida).ToString('yyyy-MM-dd', $cultura)  # siempre dentro de 2070 (limpiar.cmd)

$Admin = 'ADMINISTRATIVO'; $Operativo = 'OPERATIVO'
$MensajeNoAsignable = 'El empleado SIM010 no pertenece a un grupo asignable.'
$Ruta = 'api/admin/empleados/familias-asignables'

function Mensajes($Leido, [string]$Clave) {
    if ($Leido -is [Collections.IDictionary] -and $Leido['errors'] -is [Collections.IDictionary] -and $Leido['errors'].ContainsKey($Clave)) {
        return @($Leido['errors'][$Clave])
    }
    return @()
}

# Lee el criterio actual (texto guardado); $null si no se pudo.
function Valor-Actual([string]$Nombre) {
    $r = Caso $Nombre 'GET familias (valor actual)' 'GET' 'api/admin/empleados/familias' 'admin' 200
    if (Cumplido $r 200) { Escribir ("   valorActual: [{0}]; sinCriterio: {1}" -f $r.Leido['valorActual'], $r.Leido['sinCriterio']); return [string]$r.Leido['valorActual'] }
    return $null
}

# Cuerpo SOLO con [string] y [string[]] (correccion de la TAREA-62-1): un elemento salido de un pipeline (Split |
# ForEach-Object) es un PSObject que envuelve la cadena, y JavaScriptSerializer lo recorre por sus propiedades
# (PSParameterizedProperty) hasta caer en una referencia circular. [string[]] desenvuelve cada elemento.
function Guardar([string]$Nombre, [string]$Texto, [object[]]$Familias, [string]$Anterior, [int]$Esperado, [string]$Usuario = 'admin',
                 [scriptblock]$Verificar) {
    [string[]]$lista = @($Familias | ForEach-Object { [string]$_ })
    $cuerpo = @{ familias = $lista; valorAnterior = [string]$Anterior }
    return Caso $Nombre $Texto 'PUT' $Ruta $Usuario $Esperado $cuerpo -Verificar $Verificar
}

# "A, b ,a" -> "a,b" (sin mayusculas ni espacios extremos, ordenado): solo para comparar criterios.
function Normalizar-Criterio([string]$Valor) {
    if ($null -eq $Valor) { return '' }
    return (@($Valor.Split(',') | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_ } | Sort-Object -Unique) -join ',')
}

# Novedades ACTIVAS de SIM010 que tocan el dia de la prueba (gestor: ve las que creo). Imprime Id, codigo y creacion.
function Novedades-Sim010([string]$Nombre, [string]$Texto, [int]$TotalEsperado) {
    $r = Caso $Nombre $Texto 'GET' "api/novedades?texto=SIM010&desde=$inicio&hasta=$inicio&tamano=50" 'gestor' 200 -Verificar {
        param($l)
        foreach ($n in @($l['items'])) { Escribir ("   novedad: id {0}, codigo {1}, creada {2} (UTC)" -f $n['id'], $n['codigo'], $n['fechaCreacion']) }
        if ($l['total'] -ne $TotalEsperado) { "total $($l['total'])" } else { $null }
    }
    return $r
}

function Buscar-Sim010([string]$Nombre, [int]$TotalEsperado) {
    return Caso $Nombre "GET /api/empleados?texto=SIM010 (total esperado $TotalEsperado)" 'GET' 'api/empleados?texto=SIM010&soloMisDepartamentos=false' 'gestor' 200 `
        -Verificar { param($l) if ($l['total'] -ne $TotalEsperado) { "total $($l['total'])" } else { Escribir ("   familiasAsignables: [{0}]" -f (@($l['familiasAsignables']) -join ', ')); $null } }
}

# --------------------------------------------------------------------------
# Estado de la ejecucion (correccion de la TAREA-62-1: ninguna caida pasa inadvertida).
# --------------------------------------------------------------------------
$tA = [char]0xE1; $tI = [char]0xED; $tO = [char]0xF3; $TO = [char]0xD3   # a, i, o, O con tilde (archivo ASCII)
$script:Incompleta = $false     # excepcion, restauracion pendiente o detencion en c05b
$script:Iniciado = $false       # ya se abrio resultado-criterio.txt
$script:Original = $null        # valor a restaurar al final ($null = no se llego a leer: no hay nada que restaurar)
$script:CreadaId = $null        # Id de la novedad creada por c06 (201): se anula al final (l06_anular)
$SqlRestaurar = "UPDATE dbo.Parametro SET Valor = N'{0}', ModificadoPorId = 1, FechaModificacion = SYSUTCDATETIME() WHERE Clave = 'EMPLEADO_FAMILIAS_ASIGNABLES';"

function Restauracion-Pendiente([string]$Actual, [string]$Debe) {
    $script:Incompleta = $true
    Escribir ("RESTAURACI{0}N PENDIENTE: el criterio qued{1} en [{2}] y debe quedar en [{3}]. En SSMS (PROFESIOGRAMA_DEV):" -f $TO, $tO, $Actual, $Debe)
    Escribir ('   ' + ($SqlRestaurar -f $Debe.Replace("'", "''")))
    Escribir ("   Se conserva {0} para la proxima corrida." -f (Split-Path $ArchivoEstado -Leaf))
}

# Vuelve el criterio a $script:Original (siempre, tambien tras una falla). Borra el archivo de estado solo si lo logra.
function Restaurar {
    if ($null -eq $script:Original) { return }
    Escribir ''
    Escribir '== Restauracion =='
    $actual = Valor-Actual 'r00_valor_actual'
    $familias = [string[]]@($script:Original.Split(',') | ForEach-Object { [string]$_.Trim() } | Where-Object { $_ })
    if ($null -eq $actual) { Restauracion-Pendiente '(no se pudo leer)' $script:Original; return }
    if ($actual -ceq $script:Original) { Escribir ("Valor original [{0}] ya vigente: nada que restaurar." -f $script:Original) }
    elseif ($familias.Count -eq 0) { Restauracion-Pendiente $actual $script:Original; return }  # vacio: la API no lo acepta
    else {
        $r = Guardar 'r01_restaurar' "PUT restaurar [$($script:Original)]" $familias $actual 200
        if (-not (Cumplido $r 200)) { Restauracion-Pendiente $actual $script:Original; return }
    }
    if (Test-Path $ArchivoEstado) { Remove-Item $ArchivoEstado; Escribir 'Archivo de estado borrado (restauracion completa).' }
}

# l06: anula la novedad creada por c06 con las salvaguardas de tarea23-2\limpiar.ps1 (2070, PROFESIOGRAMA, no anulada).
function Autolimpiar {
    if ($null -eq $script:CreadaId) { return }
    $id = $script:CreadaId
    $manual = "desde ..\tarea23-2: set IDS=$id, set CONFIRMAR=1 y limpiar.cmd"
    Escribir ''
    Escribir '== Autolimpieza de la novedad de c06 =='
    $r = Solicitar -Metodo 'GET' -Base $script:Base -Ruta "api/novedades/$id" -Encabezados @{ 'X-Dev-User' = 'admin' } `
        -TimeoutSegundos 120 -NombreSimulacion 'l06_detalle'
    $n = $null
    if ($r.Codigo -eq 200 -and $r.Texto) { try { $n = Leer-Json $r.Texto } catch { } }
    if (-not ($n -is [Collections.IDictionary])) { Escribir ("   Id {0}: no se pudo leer ({1}). Anulela a mano: {2}" -f $id, (Describir-Respuesta $r), $manual); return }
    $omision = if ($n['origen'] -ne 'PROFESIOGRAMA') { "origen $($n['origen'])" }
        elseif (-not ([string]$n['fechaInicio']).StartsWith('2070-')) { "FechaInicio $($n['fechaInicio']) fuera de 2070" }
        elseif ($n['anulada']) { 'ya anulada' }
        else { $null }
    if ($omision) { Escribir ("   Id {0}: no se anula ({1})." -f $id, $omision); return }
    $a = Caso 'l06_anular' "anular $($n['codigo']) (Id $id)" 'POST' "api/novedades/$id/anular" 'admin' 200 `
        @{ version = [string]$n['version']; motivo = 'Limpieza automatica de prueba TAREA-62-1' }
    if (-not (Cumplido $a 200)) { Escribir ("   No se pudo anular el Id {0}. Anulela a mano: {1}" -f $id, $manual) }
}

function Ejecutar-Casos {
    Escribir '== TAREA-62-1: criterio de personal elegible (requiere ServiciosExternos:Modo = Simulado) =='
    Escribir ("Corrida {0} ({1}): novedad del {2}" -f $corrida, $(if ($env:CORRIDA) { 'de la variable CORRIDA de esta consola' } else { 'del reloj' }), $inicio)

    # c00: familias y valor original (para restaurarlo al final).
    $r = Caso 'c00_familias' 'GET familias (admin)' 'GET' 'api/admin/empleados/familias' 'admin' 200 -Verificar {
        param($l)
        foreach ($f in @($l['familias'])) { Escribir ("   [{0}] activos {1}, asignable {2}" -f $f['familia'], $f['activos'], $f['asignable']) }
        $nombres = @($l['familias'] | ForEach-Object { $_['familia'] })
        if ($nombres -notcontains $Admin -or $nombres -notcontains $Operativo) { 'faltan ADMINISTRATIVO u OPERATIVO (modo Simulado?)' } else { $null }
    }
    if (-not (Cumplido $r 200)) { Escribir 'Sin el GET de familias no se continua (nada se cambio).'; $script:Incompleta = $true; return }
    $enVivo = [string]$r.Leido['valorActual']   # siempre la lectura viva de la API
    $original = $enVivo
    if (Test-Path $ArchivoEstado) {
        # Una corrida anterior termino sin restaurar: se restaura al valor que ella guardo, no al que dejo cambiado.
        $guardado = [string](([IO.File]::ReadAllText($ArchivoEstado, [Text.Encoding]::UTF8)).Trim())
        Escribir ''
        Escribir ("ADVERTENCIA: una corrida anterior NO restauro el criterio. Valor guardado entonces: [{0}]; valor en vivo ahora: [{1}]. Al final se restaura [{0}]." -f $guardado, $enVivo)
        $original = $guardado
    }
    else {
        [IO.File]::WriteAllText($ArchivoEstado, $original, (New-Object Text.UTF8Encoding($false)))
    }
    $script:Original = $original
    Escribir ("   valor original (en vivo): [{0}]; se restaura al final: [{1}]" -f $enVivo, $original)
    if ((Normalizar-Criterio $enVivo) -ne (Normalizar-Criterio $Semilla)) {
        Escribir ''
        Escribir ("ADVERTENCIA: el criterio en vivo [{0}] NO es la semilla [{1}]. Revise con el SQL del LEEME si alguien lo cambio o si una corrida anterior lo dejo asi." -f $enVivo, $Semilla)
    }

    # c01: punto de partida = ADMINISTRATIVO.
    [void](Guardar 'c01_put_admin' 'PUT [ADMINISTRATIVO] (punto de partida)' @($Admin) $enVivo 200)

    # c02 / c03: con ADMINISTRATIVO, SIM010 (OPERATIVO) no aparece ni se puede usar.
    [void](Buscar-Sim010 'c02_buscar_sin' 0)
    $novedad = @{ tipoAplicacion = 'PERSONA'; tipoNovedad = 'VACACIONES'; codigoEkon = 'SIM010'; fechaInicio = [string]$inicio; fechaFin = [string]$inicio }
    [void](Caso 'c03_novedad_400' 'POST novedad PERSONA de SIM010 con ADMINISTRATIVO' 'POST' 'api/novedades' 'gestor' 400 $novedad 'codigoEkon' -Verificar {
        param($l) if ((Mensajes $l 'codigoEkon') -notcontains $MensajeNoAsignable) { 'mensaje distinto' } else { $null }
    })

    # c04 / c05 / c06: con ADMINISTRATIVO y OPERATIVO, SIM010 aparece y la novedad se crea (efecto inmediato).
    $antesC04 = Get-Date
    [void](Guardar 'c04_put_ambas' 'PUT [ADMINISTRATIVO, OPERATIVO]' @($Admin, $Operativo) $Admin 200)
    Escribir ("   hora del cambio (local): {0} (compare con FechaModificacion en UTC, ver LEEME)" -f $antesC04.ToString('yyyy-MM-dd HH:mm:ss'))
    [void](Buscar-Sim010 'c05_buscar_con' 1)

    # c05b: antes del POST no debe haber una novedad activa de SIM010 ese dia. Si la hay, se DETIENE antes de c06
    # (el finally restaura el criterio que c01/c04 cambiaron).
    $previa = Novedades-Sim010 'c05b_sin_previas' "novedades activas de SIM010 el $inicio ANTES de c06 (esperado 0)" 0
    if (-not (Cumplido $previa 200)) {
        Escribir ''
        Escribir 'No se pudo comprobar si ya habia una novedad de SIM010 ese dia: se detiene antes de c06.'
        $script:Incompleta = $true
        return
    }
    if ([int]$previa.Leido['total'] -gt 0) {
        $p = @($previa.Leido['items'])[0]
        $hora = if ([string]$p['fechaCreacion'] -match 'T(\d{2}:\d{2}:\d{2})') { "$($Matches[1])Z" } else { [string]$p['fechaCreacion'] }
        Escribir ''
        Escribir ("Ya hab{0}a una novedad de SIM010 ese d{0}a (id {1}, creada a las {2}): hay una ejecuci{3}n previa con la misma CORRIDA. Limpie y use otra CORRIDA." -f $tI, $p['id'], $hora, $tO)
        $script:Incompleta = $true
        return
    }

    # c06: UN solo POST (una llamada a Caso; Invocar-Http no reintenta; conexion nueva, ver arriba).
    $c06 = Caso 'c06_novedad_201' 'POST novedad PERSONA de SIM010 con ambas familias (un solo envio)' 'POST' 'api/novedades' 'gestor' 201 $novedad
    if (Cumplido $c06 201) { $script:CreadaId = $c06.Leido['id'] }
    # c06b: despues del POST debe haber exactamente 1; imprime SIEMPRE su Id (tambien si c06 no fue 201).
    $posterior = Novedades-Sim010 'c06b_creada' "novedades activas de SIM010 el $inicio DESPUES de c06 (esperado 1)" 1
    if (Cumplido $c06 201) {
        Escribir ("   creada: id {0}, codigo {1} (se anula al final: l06_anular)" -f $c06.Leido['id'], $c06.Leido['codigo'])
        # La respuesta del POST ({ id, codigo, version }) no trae la hora de creacion: se muestra solo si viniera.
        if ($c06.Leido.ContainsKey('fechaCreacion')) { Escribir ("   hora de creacion (API, UTC): {0}" -f $c06.Leido['fechaCreacion']) }
        else { Escribir '   la respuesta del POST no trae hora de creacion; ver "creada" en c06b (fechaCreacion UTC del listado).' }
    }
    elseif (Cumplido $posterior 200) {
        $ids = @($posterior.Leido['items'] | ForEach-Object { $_['id'] }) -join ','
        Escribir ("   c06 no fue 201; Id de las novedades de SIM010 ese dia: [{0}] (anularlas con tarea23-2\limpiar.cmd: set IDS={0})" -f $ids)
        if ($c06.Codigo -eq 409 -and [int]$posterior.Leido['total'] -eq 1) {
            Escribir '   DIAGNOSTICO: no habia ninguna antes y ahora hay 1, pero c06 recibio 409: la API recibio el POST DOS veces (doble envio).'
        }
    }

    # c07..c09: concurrencia y validaciones (nada cambia).
    [void](Guardar 'c07_put_409' 'PUT con valorAnterior desfasado' @($Admin) $Admin 409)
    [void](Guardar 'c08_put_vacio' 'PUT sin familias' @() "$Admin, $Operativo" 400 -Verificar {
        param($l) if ((Mensajes $l 'familias') -notcontains 'Seleccione al menos una familia.') { 'mensaje distinto' } else { $null }
    })
    [void](Guardar 'c09_put_invalidas' 'PUT con una familia inexistente y otra con coma' @('ZZ INEXISTENTE', "$Admin,$Operativo") "$Admin, $Operativo" 400 -Verificar {
        param($l)
        $m = Mensajes $l 'familias'
        if ($m -notcontains 'La familia ZZ INEXISTENTE no existe.' -or $m -notcontains 'La familia ADMINISTRATIVO,OPERATIVO no puede contener comas.') { 'mensajes distintos' } else { $null }
    })

    # c10: el gestor no puede leer ni guardar.
    [void](Caso 'c10a_gestor_get' 'GET familias como gestor' 'GET' 'api/admin/empleados/familias' 'gestor' 403)
    [void](Guardar 'c10b_gestor_put' 'PUT como gestor' @($Admin) "$Admin, $Operativo" 403 'gestor')
}

# --------------------------------------------------------------------------
# Ejecucion: try (casos) / catch (ERROR NO CONTROLADO) / finally (restauracion, autolimpieza, resumen, Fin. y candado).
# --------------------------------------------------------------------------
try {
    Iniciar-Salida (Join-Path $PSScriptRoot 'resultado-criterio.txt')
    $script:Iniciado = $true
    $padre = '(desconocido)'
    try {
        $idPadre = (Get-CimInstance Win32_Process -Filter "ProcessId = $PID").ParentProcessId
        $padre = "{0} (PID {1})" -f (Get-Process -Id $idPadre -ErrorAction Stop).ProcessName, $idPadre
    } catch { }
    Escribir ("Proceso: PID {0}; proceso padre: {1}" -f $PID, $padre)
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
            Restauracion-Pendiente '(desconocido)' ([string]$script:Original)
        }
        try { Autolimpiar }
        catch {
            Escribir ''
            Escribir ("ERROR NO CONTROLADO en la autolimpieza: {0}. Anule a mano el Id {1} (desde ..\tarea23-2: set IDS={1}, set CONFIRMAR=1 y limpiar.cmd)." -f $_.Exception.Message, $script:CreadaId)
            $script:Incompleta = $true
        }
        if ($script:Incompleta) {
            Escribir ''
            Escribir '=================================================================='
            Escribir ("EJECUCI{0}N INCOMPLETA: no repita el script sin leer este mensaje." -f $TO)
            Escribir '=================================================================='
        }
        Resumen-Final 'resultado-criterio.txt'   # "Casos con FALLA: n; OMITIDOS: m" y "Fin. ..."
        Cerrar-Salida
    }
    if ($script:CandadoPropio -and (Test-Path $ArchivoCandado)) { Remove-Item $ArchivoCandado -Force }
}
exit $(if ($script:Incompleta) { 2 } elseif ($script:Fallos -gt 0) { 1 } else { 0 })
