# ==========================================================================
# TAREA-62 - Sondeo de "familiaPuesto" y de las claves de grupo/familia/tipo/clase de EvolutionEmployee (SOLO
# AGREGADOS). Mismo patron que el M1 de la TAREA-22-1 (tarea22-1\sondeo-cedula.ps1) con las funciones comunes de la
# TAREA-26a (tarea26a\comun.ps1).
#   POST https://backstack.sedemi.com:7048/api/EvolutionEmployee/EmployeesEvolution con el cuerpo fijo de la API
#   (todos los activos de todas las empresas). Sobre los registros con estado "A" muestra SOLO:
#   1. total de activos;
#   2. los NOMBRES de las claves de primer nivel (union de los registros activos; nunca valores);
#   3. para familiaPuesto y cada clave cuyo nombre contenga grupo/familia/tipo/clase (y cargoTipo y posicion si
#      existen): distintos tal cual, distintos normalizados, vacios o nulos y, con 40 distintos o menos, cada valor con
#      su conteo; un valor con menos de 5 personas NO se imprime (se agrupa en "otros");
#   4. para cada valor impreso de familiaPuesto: cuantos departamentos y cuantas empresas distintas (solo el numero).
# NUNCA muestra nombres, cedulas, correos ni codigos de empleados: la respuesta solo vive en memoria.
# Claves que podrian ser datos personales (ver $ClavesSinValores): solo conteos, nunca sus valores.
# Salida: resultado-familias.txt. Simulacion: SIMULAR=1 (simulacion\empleados.json, FICTICIO).
# ==========================================================================
. (Join-Path $PSScriptRoot '..\tarea26a\comun.ps1')
if (-not $env:SIMULACION) { $script:CarpetaSimulacion = Join-Path $PSScriptRoot 'simulacion' }

$base = Base-Url 'https://backstack.sedemi.com:7048'
$ruta = 'api/EvolutionEmployee/EmployeesEvolution'
$cuerpo = '{ "parameter": "", "estado": "A", "codEmpresa": "", "codDepartamento": "" }'
$timeout = if ($env:TIMEOUT_SEGUNDOS) { [int]$env:TIMEOUT_SEGUNDOS } else { 180 }
$MinimoPersonas = 5
$MaximoValores = 40
# Claves cuyos VALORES nunca se imprimen aunque su nombre contenga "tipo" (p. ej. tipo de sangre o de identificacion).
$ClavesSinValores = 'sangre|sexo|genero|civil|discap|etnia|religion|identific|cedula|documento|salari|bpr|naci'

# Recortar, colapsar espacios, sin tildes y en minusculas (como el buscador y ComparacionEmpleadosErp.Iguales).
function Normalizar([string]$Valor) {
    if ($null -eq $Valor) { return '' }
    return (Sin-Tildes (($Valor -replace '\s+', ' ').Trim())).ToLowerInvariant()
}

function Sumar([hashtable]$Tabla, [string]$Clave) {
    $Tabla[$Clave] = 1 + $(if ($Tabla.ContainsKey($Clave)) { $Tabla[$Clave] } else { 0 })
}

Iniciar-Salida (Join-Path $PSScriptRoot 'resultado-familias.txt')
try {
    Escribir '== TAREA-62: sondeo de familias y grupos (solo agregados) =='
    $r = Solicitar -Metodo 'POST' -Base $base -Ruta $ruta -Cuerpo $cuerpo -TimeoutSegundos $timeout -NombreSimulacion 'empleados'
    Escribir ('Respuesta: ' + (Describir-Respuesta $r))
    if ($r.Codigo -ne 200 -or -not $r.Texto) { Escribir 'Sin respuesta util: no hay agregados.'; exit 1 }

    $raiz = Leer-Json $r.Texto
    $registros = if ($raiz -is [System.Array]) { $raiz } elseif ($raiz -is [Collections.IDictionary] -and $raiz['result'] -is [System.Array]) { $raiz['result'] } else { $null }
    if ($null -eq $registros) { Escribir 'Forma inesperada (sin "result"): no hay agregados.'; exit 1 }
    $activos = @($registros | Where-Object { $_ -is [Collections.IDictionary] -and (Texto-Invariante $_['estado']) -eq 'A' })

    # 1. Total
    Escribir ("1. Activos (estado A): {0}" -f $activos.Count)

    # 2. Nombres de las claves de primer nivel
    $claves = @{}
    foreach ($e in $activos) { foreach ($k in $e.Keys) { $claves[[string]$k] = $true } }
    $nombres = @($claves.Keys | Sort-Object)
    Escribir ("2. Claves de primer nivel ({0}): {1}" -f $nombres.Count, ($nombres -join ', '))

    # 3. Claves a analizar
    $analizar = @($nombres | Where-Object { $_ -eq 'familiaPuesto' -or $_ -eq 'cargoTipo' -or $_ -eq 'posicion' -or $_ -match '(?i)grupo|familia|tipo|clase' })
    Escribir ("3. Claves analizadas: {0}" -f ($(if ($analizar.Count) { $analizar -join ', ' } else { '(ninguna)' })))
    $impresosFamilia = @()
    foreach ($clave in $analizar) {
        # "Tal cual" distingue mayusculas (un @{} de PowerShell no las distingue y juntaria "Administrativo" con "ADMINISTRATIVO").
        $tal = New-Object Collections.Hashtable ([StringComparer]::Ordinal); $norm = @{}; $vacios = 0
        foreach ($e in $activos) {
            $v = if ($e.ContainsKey($clave)) { Texto-Invariante $e[$clave] } else { $null }
            if ($null -eq $v -or $v.Trim().Length -eq 0) { $vacios++; continue }
            Sumar $tal $v
            Sumar $norm (Normalizar $v)
        }
        Escribir ''
        Escribir ("-- {0}: distintos tal cual {1}; distintos normalizados {2}; vacios o nulos {3}" -f $clave, $tal.Count, $norm.Count, $vacios)
        if ($clave -match "(?i)$ClavesSinValores") { Escribir '   (clave que puede contener datos personales: valores no impresos)'; continue }
        if ($tal.Count -gt $MaximoValores) { Escribir ("   (mas de {0} valores distintos: no se imprimen)" -f $MaximoValores); continue }
        $otrosValores = 0; $otrasPersonas = 0
        foreach ($par in ($tal.GetEnumerator() | Sort-Object -Property @{ Expression = 'Value'; Descending = $true }, Name)) {
            if ($par.Value -lt $MinimoPersonas) { $otrosValores++; $otrasPersonas += $par.Value; continue }
            Escribir ("   [{0}]: {1}" -f (Texto-Linea $par.Name 80), $par.Value)
            if ($clave -eq 'familiaPuesto') { $impresosFamilia += $par.Name }
        }
        if ($otrosValores -gt 0) { Escribir ("   (otros: {0} valores, {1} personas)" -f $otrosValores, $otrasPersonas) }
    }

    # 4. Departamentos y empresas por familia impresa (solo numeros)
    Escribir ''
    if ($impresosFamilia.Count -eq 0) {
        Escribir '4. familiaPuesto: sin valores impresos (no existe, mas de 40 distintos o todos con menos de 5 personas).'
    }
    else {
        Escribir '4. familiaPuesto: departamentos y empresas distintas por valor impreso'
        foreach ($familia in $impresosFamilia) {
            $deps = @{}; $emps = @{}
            foreach ($e in $activos) {
                if ((Texto-Invariante $e['familiaPuesto']) -cne $familia) { continue }
                $d = Texto-Invariante $e['codDepartamento']; if (-not $d) { $d = Normalizar (Texto-Invariante $e['departamento']) }
                $m = Texto-Invariante $e['codEmpresa']; if (-not $m) { $m = Normalizar (Texto-Invariante $e['empresa']) }
                if ($d) { $deps[$d] = $true }
                if ($m) { $emps[$m] = $true }
            }
            Escribir ("   [{0}]: departamentos {1}; empresas {2}" -f (Texto-Linea $familia 80), $deps.Count, $emps.Count)
        }
    }
    Escribir ''
    Escribir 'Pegue SOLO estas lineas (agregados) en el chat.'
}
finally {
    Cerrar-Salida
}
