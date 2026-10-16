# ==========================================================================
# TAREA-26c (Fase B1) - Analisis de horarios: GET /api/erp/horarios de NUESTRA API (NO escribe).
#   set DEV_USER=gestor        (gestor por defecto)
#   set API_BASE=...           (por defecto https://localhost:7180)
#   Por horario: codigo, descripcion, entrada, salida, minutos de jornada y trabajados, tipo y marcas (ver LEEME.md).
#   Totales por marca y por tipo, y matrices LISTA_NO_LABORAL frente a TRAB_CERO_O_NULO y ENTRADA_IGUAL_SALIDA.
#   Datos de catalogo (no personales).
# IMPORTANTE: refleja lo que ve la aplicacion, NO el ERP crudo: el endpoint ya filtra status = "A" y convierte
#   "2400" (u otra hora invalida) en null. Una salida nula con entrada se marca "posible 2400".
# Salida: resultado-horarios-analisis.txt. Simulacion: SIMULAR=1 (simulacion\horarios.json, datos ficticios).
# ==========================================================================
. (Join-Path $PSScriptRoot '..\tarea26a\comun.ps1')
if (-not $env:SIMULACION) { $script:CarpetaSimulacion = Join-Path $PSScriptRoot 'simulacion' }

$base = Base-Url $(if ($env:API_BASE) { $env:API_BASE } else { 'https://localhost:7180' })
if (-not $script:Simular) { Permitir-CertificadoLocal }
$usuario = if ($env:DEV_USER) { $env:DEV_USER } else { 'gestor' }

# Lista de no laborales de la TAREA-26a (pendiente 41), ya normalizada.
$NoLaborales = @('LIBRE', 'FERIADO', 'VACIO', 'VACACIONES', 'PERMISO MEDICO', 'CALAMIDAD DOMESTICA')
$Marcas = @('CRUZA_MEDIANOCHE', 'ENTRADA_IGUAL_SALIDA', 'TRAB_CERO_O_NULO', 'CAMPO_NULO', 'JORNADA_DIF', 'LISTA_NO_LABORAL', 'LISTA_PARCIAL')

# Mayusculas, sin tildes y con un solo espacio entre palabras.
function Normalizar([string]$Texto) {
    if ($null -eq $Texto) { return '' }
    return ((Sin-Tildes $Texto).ToUpperInvariant() -replace '\s+', ' ').Trim()
}

function Valor($Diccionario, [string]$Clave) {
    if ($Diccionario.ContainsKey($Clave)) { return $Diccionario[$Clave] }
    return $null
}

# "HH:mm" o "HH:mm:ss" (TimeOnly de la API) -> minutos desde las 00:00; null si falta o es ilegible.
function A-Minutos($Hora) {
    if ($null -eq $Hora) { return $null }
    if ("$Hora" -match '^(\d{1,2}):(\d{2})') { return [int]$Matches[1] * 60 + [int]$Matches[2] }
    return $null
}

function Mostrar($Valor) { if ($null -eq $Valor) { return '(nulo)' } else { return Texto-Linea (Texto-Invariante $Valor) } }

# Marcas de un horario. Jornada calculada = (salida - entrada + 24h) mod 24h; con entrada = salida vale 0 o 24h.
function Calcular-Marcas($H) {
    $entrada = A-Minutos (Valor $H 'horaEntrada')
    $salida = A-Minutos (Valor $H 'horaSalida')
    $jornada = Valor $H 'minutosJornada'
    $trabajados = Valor $H 'minutosTrabajados'
    $descripcion = Normalizar (Valor $H 'descripcion')
    $m = @{}
    foreach ($nombre in $Marcas) { $m[$nombre] = $false }

    if ($null -ne $entrada -and $null -ne $salida) {
        $m['CRUZA_MEDIANOCHE'] = $salida -lt $entrada
        $m['ENTRADA_IGUAL_SALIDA'] = $salida -eq $entrada
        if ($null -ne $jornada) {
            $calculada = ($salida - $entrada + 1440) % 1440
            $m['JORNADA_DIF'] = if ($calculada -eq 0) { [int]$jornada -notin @(0, 1440) } else { [int]$jornada -ne $calculada }
        }
    }
    $m['TRAB_CERO_O_NULO'] = ($null -eq $trabajados) -or ([int]$trabajados -eq 0)
    foreach ($campo in @('descripcion', 'horaEntrada', 'horaSalida', 'minutosJornada', 'minutosTrabajados', 'tipo')) {
        $v = Valor $H $campo
        if ($null -eq $v -or ($v -is [string] -and $v.Trim() -eq '')) { $m['CAMPO_NULO'] = $true }
    }
    $m['LISTA_NO_LABORAL'] = $NoLaborales -contains $descripcion
    # Informativa: contiene una palabra de la lista sin ser igual (p. ej. "DIA LIBRE"); la regla usa solo la exacta.
    $m['LISTA_PARCIAL'] = (-not $m['LISTA_NO_LABORAL']) -and @($NoLaborales | Where-Object { " $descripcion " -like "* $_ *" }).Count -gt 0
    $m['Posible2400'] = ($null -ne $entrada) -and ($null -eq $salida)
    return $m
}

function Escribir-Matriz([string]$Fila, [string]$Columna, $Filas) {
    Escribir ''
    Escribir "Matriz $Fila (filas) frente a $Columna (columnas)"
    $c = @{ 'ss' = 0; 'sn' = 0; 'ns' = 0; 'nn' = 0 }
    foreach ($f in $Filas) {
        $clave = $(if ($f.M[$Fila]) { 's' } else { 'n' }) + $(if ($f.M[$Columna]) { 's' } else { 'n' })
        $c[$clave]++
    }
    Escribir ("  {0}=si: {1}=si {2} | {1}=no {3}" -f $Fila, $Columna, $c['ss'], $c['sn'])
    Escribir ("  {0}=no: {1}=si {2} | {1}=no {3}" -f $Fila, $Columna, $c['ns'], $c['nn'])
}

Iniciar-Salida (Join-Path $PSScriptRoot 'resultado-horarios-analisis.txt')
try {
    Escribir "== TAREA-26c Analisis de horarios (no escribe), usuario $usuario =="
    Escribir 'AVISO: refleja lo que ve la aplicacion (GET /api/erp/horarios), no el ERP crudo: solo status "A" y las horas'
    Escribir '       invalidas del ERP (p. ej. "2400") llegan como nulas. Salida nula con entrada = "posible 2400".'
    $r = Solicitar -Metodo 'GET' -Base $base -Ruta 'api/erp/horarios' -Encabezados @{ 'X-Dev-User' = $usuario } `
        -TimeoutSegundos 120 -NombreSimulacion 'horarios'
    Escribir ('GET /api/erp/horarios: ' + (Describir-Respuesta $r))
    if ($r.Codigo -ne 200 -or -not $r.Texto) { Escribir 'Sin datos (revise que la API este corriendo; modo Http para el ERP real).'; exit 1 }

    $horarios = Leer-Json $r.Texto
    if ($horarios -isnot [System.Array]) { Escribir 'La respuesta no es un arreglo de horarios.'; exit 1 }
    $filas = foreach ($h in ($horarios | Sort-Object { Valor $_ 'tipo' }, { Valor $_ 'codigo' })) {
        [pscustomobject]@{ H = $h; M = (Calcular-Marcas $h) }
    }
    $filas = @($filas)

    Escribir ''
    Escribir 'codigo | descripcion | entrada | salida | min jornada | min trabajados | tipo | marcas'
    foreach ($f in $filas) {
        $activas = @($Marcas | Where-Object { $f.M[$_] })
        if ($f.M['Posible2400']) { $activas += 'SALIDA_NULA(posible 2400)' }
        $h = $f.H
        Escribir ("{0} | {1} | {2} | {3} | {4} | {5} | {6} | {7}" -f (Mostrar (Valor $h 'codigo')), (Mostrar (Valor $h 'descripcion')),
            (Mostrar (Valor $h 'horaEntrada')), (Mostrar (Valor $h 'horaSalida')), (Mostrar (Valor $h 'minutosJornada')),
            (Mostrar (Valor $h 'minutosTrabajados')), (Mostrar (Valor $h 'tipo')), ($activas -join ' '))
    }

    Escribir ''
    Escribir "Total de horarios: $($filas.Count)"
    Escribir 'Por marca:'
    foreach ($nombre in $Marcas) { Escribir ("  {0}: {1}" -f $nombre, @($filas | Where-Object { $_.M[$nombre] }).Count) }
    Escribir ("  SALIDA_NULA(posible 2400): {0}" -f @($filas | Where-Object { $_.M['Posible2400'] }).Count)
    Escribir 'Por tipo:'
    foreach ($g in ($filas | Group-Object { $t = Valor $_.H 'tipo'; if ($t) { $t } else { '(vacio)' } } | Sort-Object Name)) {
        Escribir ("  {0}: {1}" -f $g.Name, $g.Count)
    }
    Escribir-Matriz 'LISTA_NO_LABORAL' 'TRAB_CERO_O_NULO' $filas
    Escribir-Matriz 'LISTA_NO_LABORAL' 'ENTRADA_IGUAL_SALIDA' $filas
    Escribir ''
    Escribir 'Fin. Pegue en el chat resultado-horarios-analisis.txt.'
}
finally { Cerrar-Salida }
