# ==========================================================================
# TAREA-22-1 - M1: sondeo del campo "cedula" de EvolutionEmployee (SOLO AGREGADOS). Copia adaptada del sondeo de la
# TAREA-26a (tarea26a\sondeo-empleados.ps1), con sus funciones comunes (tarea26a\comun.ps1).
#   POST https://backstack.sedemi.com:7048/api/EvolutionEmployee/EmployeesEvolution con el cuerpo fijo de la API
#   (todos los activos de todas las empresas). Sobre los registros con estado "A" muestra SOLO:
#   - tipo JSON de "cedula" (texto / numero / null / ausente / otro);
#   - textos vacios (o solo espacios); largo minimo, maximo y distribucion de largos (sin espacios extremos);
#   - con 10 digitos exactos, con algun caracter que no es digito, que empiezan en "0";
#   - cantidad de valores repetidos (y registros que los comparten).
# NUNCA muestra una cedula, un nombre ni un codigo: la respuesta solo vive en memoria.
# Salida: resultado-cedula.txt. Simulacion: SIMULAR=1 (simulacion\empleados.json, FICTICIO; .codigo opcional).
# ==========================================================================
. (Join-Path $PSScriptRoot '..\tarea26a\comun.ps1')
if (-not $env:SIMULACION) { $script:CarpetaSimulacion = Join-Path $PSScriptRoot 'simulacion' }

$base = Base-Url 'https://backstack.sedemi.com:7048'
$ruta = 'api/EvolutionEmployee/EmployeesEvolution'
$cuerpo = '{ "parameter": "", "estado": "A", "codEmpresa": "", "codDepartamento": "" }'
$timeout = if ($env:TIMEOUT_SEGUNDOS) { [int]$env:TIMEOUT_SEGUNDOS } else { 180 }

Iniciar-Salida (Join-Path $PSScriptRoot 'resultado-cedula.txt')
try {
    Escribir '== TAREA-22-1 M1: sondeo de "cedula" (solo agregados) =='
    $r = Solicitar -Metodo 'POST' -Base $base -Ruta $ruta -Cuerpo $cuerpo -TimeoutSegundos $timeout -NombreSimulacion 'empleados'
    Escribir ('Respuesta: ' + (Describir-Respuesta $r))
    if ($r.Codigo -ne 200 -or -not $r.Texto) { Escribir 'Sin respuesta util: no hay agregados.'; exit 1 }

    $raiz = Leer-Json $r.Texto
    $registros = if ($raiz -is [System.Array]) { $raiz } elseif ($raiz -is [Collections.IDictionary] -and $raiz['result'] -is [System.Array]) { $raiz['result'] } else { $null }
    if ($null -eq $registros) { Escribir 'Forma inesperada (sin "result"): no hay agregados.'; exit 1 }

    $activos = @($registros | Where-Object { $_ -is [Collections.IDictionary] -and (Texto-Invariante $_['estado']) -eq 'A' })
    $tipos = @{}
    $largos = @{}
    $conteoValores = @{}
    $vacios = 0; $diezDigitos = 0; $noDigitos = 0; $empiezanCero = 0
    foreach ($e in $activos) {
        $tipo = if ($e.ContainsKey('cedula')) { Tipo-Json $e['cedula'] } else { 'ausente' }
        $tipos[$tipo] = 1 + $(if ($tipos.ContainsKey($tipo)) { $tipos[$tipo] } else { 0 })
        if ($tipo -ne 'texto') { continue }
        $valor = ([string]$e['cedula']).Trim()
        if ($valor.Length -eq 0) { $vacios++; continue }
        $largos[$valor.Length] = 1 + $(if ($largos.ContainsKey($valor.Length)) { $largos[$valor.Length] } else { 0 })
        if ($valor -match '^[0-9]{10}$') { $diezDigitos++ }
        if ($valor -match '[^0-9]') { $noDigitos++ }
        if ($valor.StartsWith('0')) { $empiezanCero++ }
        $conteoValores[$valor] = 1 + $(if ($conteoValores.ContainsKey($valor)) { $conteoValores[$valor] } else { 0 })
    }

    $repetidos = @($conteoValores.Values | Where-Object { $_ -gt 1 })
    $registrosRepetidos = 0
    foreach ($n in $repetidos) { $registrosRepetidos += $n }
    $conTexto = 0
    foreach ($n in $largos.Values) { $conTexto += $n }

    Escribir ("Registros: {0}; activos (estado A): {1}" -f $registros.Count, $activos.Count)
    Escribir 'Tipo JSON de "cedula" (activos):'
    foreach ($t in ($tipos.Keys | Sort-Object)) { Escribir ("   {0}: {1}" -f $t, $tipos[$t]) }
    Escribir ("Textos vacios o solo espacios: {0}" -f $vacios)
    Escribir ("Textos con valor: {0}" -f $conTexto)
    if ($conTexto -gt 0) {
        $ordenados = @($largos.Keys | Sort-Object)
        Escribir ("Largo minimo: {0}; largo maximo: {1}" -f $ordenados[0], $ordenados[-1])
        Escribir 'Distribucion de largos (largo: registros):'
        foreach ($l in $ordenados) { Escribir ("   {0}: {1}" -f $l, $largos[$l]) }
    }
    Escribir ("Con 10 digitos exactos: {0}" -f $diezDigitos)
    Escribir ("Con algun caracter que no es digito: {0}" -f $noDigitos)
    Escribir ("Que empiezan en ""0"": {0}" -f $empiezanCero)
    Escribir ("Valores repetidos: {0} (registros que los comparten: {1})" -f $repetidos.Count, $registrosRepetidos)
    Escribir ''
    Escribir 'Pegue SOLO estas lineas (agregados) en el chat.'
}
finally {
    Cerrar-Salida
}
