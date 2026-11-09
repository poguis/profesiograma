# ==========================================================================
# TAREA-23-2 - Funciones de casos comunes a edicion.ps1 y limpiar.ps1 (se cargan despues de tarea26a\comun.ps1).
# Cada caso imprime "CONTROL <caso> OK|FALLA (esperado X, obtenido Y)"; un caso cuyo requisito no se cumplio imprime
# "CONTROL <caso> OMITIDO (requisito <n> no creado)" y no cuenta como FALLA.
# Muestra SOLO codigos HTTP, codigos NOV-, Id, versiones, cantidades y mensajes. NUNCA nombres de personas ni de proyectos.
# ==========================================================================
$script:Fallos = 0
$script:Omitidos = 0

function Cuerpo([hashtable]$Datos) {
    Add-Type -AssemblyName System.Web.Extensions
    return (New-Object System.Web.Script.Serialization.JavaScriptSerializer).Serialize($Datos)
}

function Omitir([string]$Nombre, [string]$Requisito) {
    $script:Omitidos++
    Escribir ''
    Escribir ("CONTROL {0} OMITIDO (requisito {1} no creado)" -f $Nombre, $Requisito)
}

# Respuesta con el codigo indicado y un cuerpo leido (requisito cumplido).
function Cumplido($Resultado, [int]$Codigo) { return ($null -ne $Resultado -and $Resultado.Codigo -eq $Codigo -and $Resultado.Leido -is [Collections.IDictionary]) }

# Ejecuta un caso y devuelve { Codigo, Leido }. $Clave: en un 400, el campo que debe traer el error. $Verificar
# (opcional): recibe la respuesta leida y devuelve un texto de error o $null.
function Caso([string]$Nombre, [string]$Texto, [string]$Metodo, [string]$Ruta, [string]$Usuario, [int]$Esperado,
              [hashtable]$Datos, [string]$Clave, [scriptblock]$Verificar) {
    $cuerpo = if ($Datos) { Cuerpo $Datos } else { $null }
    $r = Solicitar -Metodo $Metodo -Base $script:Base -Ruta $Ruta -Cuerpo $cuerpo -Encabezados @{ 'X-Dev-User' = $Usuario } `
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
    $estado = if ($ok) { 'OK' } else { 'FALLA' }
    $extra = if ($detalle) { "; $detalle" } else { '' }
    Escribir ("CONTROL {0} {1} (esperado {2}, obtenido {3}{4})" -f $Nombre, $estado, $Esperado, $r.Codigo, $extra)
    return [pscustomobject]@{ Codigo = $r.Codigo; Leido = $leido }
}

function Resumen-Final([string]$Archivo) {
    Escribir ''
    Escribir "Casos con FALLA: $script:Fallos; OMITIDOS: $script:Omitidos"
    Escribir "Fin. Pegue en el chat $Archivo."
}
