# ==========================================================================
# TAREA-26a - A2: sondeo del contrato de EvolutionEmployee (SOLO AGREGADOS).
#   POST https://backstack.sedemi.com:7048/api/EvolutionEmployee/EmployeesEvolution con cada cuerpo-empleados-*.json
#   de esta carpeta (copiar la plantilla cuerpo-empleados.ejemplo.json a cuerpo-empleados-1.json).
#
# Muestra por cada cuerpo, por separado:
#   - codigo HTTP, tiempo, bytes; si la respuesta es un arreglo o una envoltura (nombres de las claves);
#   - cantidad de registros y advertencia de posible paginacion;
#   - por campo: nombre, tipos JSON, ausentes, nulos, vacios y LARGO MAXIMO (nunca el valor);
#   - valores distintos con su conteo SOLO de los campos de la LISTA PERMITIDA (abajo);
#   - analisis del codigo EKON (codPersona): total, distintos, duplicados, no numericos, con "." o ",", largos y
#     cuantos empiezan por "DEV";
#   - coincidencias con los 7 departamentos de la tabla Departamento;
#   - nombres de campos que parecen sensibles (solo el NOMBRE del campo, para no mapearlos en la 26b).
# NUNCA muestra nombres de personas, cedulas, correos, sueldos, fechas ni ningun campo fuera de la lista permitida.
# La respuesta solo vive en memoria; resultado-sondeo.txt contiene solo agregados.
# Simulacion: SIMULAR=1; respuesta en %SIMULACION%\empleados_<nombre del cuerpo>.json (y .codigo opcional).
# ==========================================================================
. (Join-Path $PSScriptRoot 'comun.ps1')

$UrlBase = 'https://backstack.sedemi.com:7048'
$Ruta = 'api/EvolutionEmployee/EmployeesEvolution'
$TimeoutSegundos = if ($env:TIMEOUT_SEGUNDOS) { [int]$env:TIMEOUT_SEGUNDOS } else { 180 }

# ---------------------------------------------------------------- LISTA PERMITIDA (revisar aqui)
# Solo de estos campos (nombre del campo, sin distinguir mayusculas) se muestran VALORES distintos con su conteo.
# Son datos organizacionales de baja cardinalidad (P15 de la TAREA-26). Cualquier otro campo: solo agregados.
$CamposPermitidos = @(
    '^estado$',
    '^(cod|nom|nombre)?_?empresa$',
    '^(cod|des|nom|nombre)?_?(departamento|unidad|area|seccion)$',
    '^(cod|des|nom|nombre)?_?familia_?(de_?)?puesto$',
    '^cargo_?tipo$',
    '^tipo_?contrato$'
)
# Campos de departamento / unidad (de primer nivel) para comparar con la tabla Departamento (subconjunto de la lista permitida).
$CamposDepartamento = '^(des|nom|nombre)?_?(departamento|unidad)$'
# Codigo EKON del empleado (campo de primer nivel).
$CampoCodigo = '^codPersona$'
# Nombres de campo que parecen sensibles: solo se informa el NOMBRE del campo (nunca su valor).
# Correccion A2: tambien direcciones y personas a quien reporta (barrio, calles, numero de casa, provincia, canton, reportaA).
$PatronSensible = '(salari|sueldo|bpr|nacimiento|telefono|celular|movil|direcci|domicilio|personal|cedula|identificaci|documento|remuneraci|banco|cuenta|mail|correo|sexo|genero|edad|barrio|calle|numero_?casa|provincia|canton|reporta)'
# Correo de la empresa: laboral, se mapea a CorreoEmpresa (no se marca como sensible; su valor tampoco se muestra).
$CorreoEmpresa = '^(mail|correo|email)_?empresa$'
# Claves de la envoltura cuyo valor (simple) se puede mostrar (metadatos de paginacion o estado).
$ClavesMeta = '^(statusCode|isSuccess|success|total\w*|count|page\w*|pageSize|pageNumber|hasNext\w*|next\w*|skip|take|limit|offset)$'
# Los 7 departamentos/unidades de la tabla Departamento (semillas de la migracion Inicial).
$Departamentos = @(
    'DEPARTAMENTO DE INFRAESTRUCTURA', 'DEPARTAMENTO SEDEMI TELECOM', 'DEPARTAMENTO SEDEMI PETROLEO Y GAS',
    'DEPARTAMENTO SEDEMI ENERGIA', 'DEPARTAMENTO DE INFRAESTRUCTURA METALICA', 'DEPARTAMENTO SEDEMI MINERIA',
    'UNIDAD SISTEMA INTEGRADO DE GESTION'
)
$MaximoValoresPorCampo = 30
$CantidadesRedondas = @(50, 100, 200, 250, 500, 1000, 2000, 2500, 5000, 10000)

function Es-Permitido([string]$Campo) {
    foreach ($patron in $CamposPermitidos) { if ($Campo -match $patron) { return $true } }
    return $false
}

function Hoja([string]$Nombre) { return ($Nombre -split '\.')[-1] }

# Pares (campo, valor) de un registro; los objetos anidados se aplanan con "padre.hijo".
function Aplanar($Objeto, [string]$Prefijo, [System.Collections.Generic.List[object]]$Pares) {
    foreach ($clave in $Objeto.Keys) {
        $nombre = if ($Prefijo) { "$Prefijo.$clave" } else { [string]$clave }
        $valor = $Objeto[$clave]
        if ($valor -is [System.Collections.IDictionary]) { Aplanar $valor $nombre $Pares }
        else { $Pares.Add(@($nombre, $valor)) }
    }
}

function Describir-Cuerpo($Cuerpo) {
    $partes = @()
    foreach ($clave in $Cuerpo.Keys) {
        $valor = Texto-Invariante $Cuerpo[$clave]
        if ($clave -eq 'parameter') {
            # parameter puede ser un codigo o una cedula: nunca se muestra su valor.
            $texto = if ([string]::IsNullOrEmpty($valor)) { '(vacio)' } else { "(con valor de $($valor.Length) caracteres; no se muestra)" }
        }
        elseif ($clave -in 'estado', 'codEmpresa', 'codDepartamento') {
            $texto = if ([string]::IsNullOrEmpty($valor)) { '(vacio)' } else { '"' + $valor + '"' }
        }
        else { $texto = '(no se muestra)' }
        $partes += "$clave=$texto"
    }
    return ($partes -join ', ')
}

function Analizar($raiz) {
    $registros = $null
    if ($raiz -is [System.Array]) {
        Escribir 'Forma: ARREGLO directo de registros.'
        $registros = $raiz
    }
    elseif ($raiz -is [System.Collections.IDictionary]) {
        Escribir ('Forma: ENVOLTURA (objeto). Claves: ' + (($raiz.Keys | ForEach-Object { "$_ [$(Tipo-Json $raiz[$_])]" }) -join ', '))
        foreach ($clave in $raiz.Keys) {
            $valor = $raiz[$clave]
            if ($valor -is [System.Array] -and $null -eq $registros) {
                Escribir "Registros tomados de la clave: $clave"
                $registros = $valor
            }
            elseif ($clave -match $ClavesMeta -and -not ($valor -is [System.Collections.IDictionary])) {
                Escribir "  meta $clave = $(Texto-Invariante $valor)"
            }
        }
        if ($null -eq $registros) { Escribir 'No se encontro un arreglo de registros en la envoltura.'; return }
    }
    else { Escribir ('Forma inesperada: ' + (Tipo-Json $raiz)); return }

    $total = $registros.Count
    Escribir "Registros: $total"
    $porPaginacion = $false
    if ($raiz -is [System.Collections.IDictionary]) {
        foreach ($clave in $raiz.Keys) { if ($clave -match '(page|total|next|skip|offset|limit)') { $porPaginacion = $true } }
    }
    if ($porPaginacion -or $CantidadesRedondas -contains $total) {
        Escribir 'ADVERTENCIA: posible paginacion (cantidad redonda o claves de pagina/total). Verificar si faltan registros.'
    }
    if ($total -eq 0) { return }

    # ---- estadisticas por campo
    $estadisticas = [ordered]@{}
    $valoresPermitidos = @{}
    $codigos = New-Object System.Collections.Generic.List[string]
    $tiposCodigo = @{}
    $coincidencias = @{}
    foreach ($d in $Departamentos) { $coincidencias[$d] = 0 }
    $noCoinciden = @{}

    foreach ($registro in $registros) {
        if (-not ($registro -is [System.Collections.IDictionary])) { continue }
        $pares = New-Object System.Collections.Generic.List[object]
        Aplanar $registro '' $pares
        $departamentosRegistro = @()
        foreach ($par in $pares) {
            $campo = $par[0]; $valor = $par[1]
            if (-not $estadisticas.Contains($campo)) {
                $estadisticas[$campo] = @{ Presentes = 0; Nulos = 0; Vacios = 0; LargoMax = 0; Tipos = @{} }
            }
            $e = $estadisticas[$campo]
            $e.Presentes++
            $tipo = Tipo-Json $valor
            $e.Tipos[$tipo] = $true
            $valorTexto = $null
            if ($null -eq $valor) { $e.Nulos++ }
            elseif ($tipo -eq 'arreglo') { $valorTexto = $null }
            else {
                $valorTexto = Texto-Invariante $valor
                if ($valorTexto.Trim().Length -eq 0) { $e.Vacios++ }
                if ($valorTexto.Length -gt $e.LargoMax) { $e.LargoMax = $valorTexto.Length }
            }
            $hoja = Hoja $campo
            if ((Es-Permitido $hoja) -and $null -ne $valorTexto) {
                if (-not $valoresPermitidos.ContainsKey($campo)) { $valoresPermitidos[$campo] = @{} }
                $clave = $valorTexto.Trim()
                $valoresPermitidos[$campo][$clave] = 1 + [int]$valoresPermitidos[$campo][$clave]
                if ($campo -match $CamposDepartamento -and $clave.Length -gt 0) { $departamentosRegistro += $clave.ToUpperInvariant() }
            }
            if ($campo -match $CampoCodigo) {   # solo el campo de primer nivel (no jefe.codPersona, etc.)
                $tiposCodigo[$tipo] = $true
                if ($null -ne $valorTexto) { $codigos.Add($valorTexto.Trim()) }
            }
        }
        $coincide = $false
        foreach ($d in $Departamentos) {
            if ($departamentosRegistro -contains $d) { $coincidencias[$d]++; $coincide = $true }
        }
        if (-not $coincide) {
            foreach ($nombreDep in ($departamentosRegistro | Select-Object -Unique)) { $noCoinciden[$nombreDep] = $true }
        }
    }

    Escribir ''
    Escribir 'CAMPOS (nombre | tipos | ausentes | nulos | vacios | largo maximo | sensible?)'
    foreach ($campo in $estadisticas.Keys) {
        $e = $estadisticas[$campo]
        $hojaCampo = Hoja $campo
        $sensible = if ($hojaCampo -match $PatronSensible -and -not (Es-Permitido $hojaCampo) -and $hojaCampo -notmatch $CorreoEmpresa) {
            'POSIBLE SENSIBLE (no mapear)'
        } else { '' }
        Escribir ("  {0} | {1} | {2} | {3} | {4} | {5} | {6}" -f $campo, (($e.Tipos.Keys | Sort-Object) -join '/'), ($total - $e.Presentes),
            $e.Nulos, $e.Vacios, $e.LargoMax, $sensible)
    }

    Escribir ''
    Escribir 'VALORES DE LA LISTA PERMITIDA (valor = cantidad)'
    if ($valoresPermitidos.Count -eq 0) { Escribir '  (ningun campo de la respuesta coincide con la lista permitida)' }
    foreach ($campo in ($valoresPermitidos.Keys | Sort-Object)) {
        $valores = $valoresPermitidos[$campo]
        Escribir "  $campo ($($valores.Count) distintos):"
        $ordenados = $valores.GetEnumerator() | Sort-Object -Property @{ Expression = 'Value'; Descending = $true }, @{ Expression = 'Key' }
        $mostrados = 0
        foreach ($v in $ordenados) {
            if ($mostrados -ge $MaximoValoresPorCampo) { break }
            $etiqueta = if ($v.Key.Length -eq 0) { '(vacio)' } else { Texto-Linea $v.Key }
            Escribir "    $etiqueta = $($v.Value)"
            $mostrados++
        }
        if ($valores.Count -gt $MaximoValoresPorCampo) { Escribir "    ... y $($valores.Count - $MaximoValoresPorCampo) mas" }
    }

    Escribir ''
    Escribir 'CODIGO EKON (codPersona)'
    if ($codigos.Count -eq 0) { Escribir '  No se encontro el campo codPersona (revisar el nombre en la lista de campos).' }
    else {
        $grupos = $codigos | Group-Object
        $duplicados = @($grupos | Where-Object { $_.Count -gt 1 })
        $noNumericos = @($codigos | Where-Object { $_ -notmatch '^\d+$' }).Count
        $conSeparador = @($codigos | Where-Object { $_ -match '[.,]' }).Count
        $dev = @($codigos | Where-Object { $_ -match '^DEV' }).Count
        $largos = $codigos | ForEach-Object { $_.Length } | Measure-Object -Minimum -Maximum
        Escribir "  tipos JSON: $(($tiposCodigo.Keys | Sort-Object) -join '/')"
        $afectados = 0
        foreach ($d in $duplicados) { $afectados += $d.Count }
        Escribir "  con valor: $($codigos.Count); distintos: $(@($grupos).Count); codigos repetidos: $($duplicados.Count) (registros afectados: $afectados)"
        Escribir "  no numericos: $noNumericos; con '.' o ',': $conSeparador; empiezan por DEV: $dev"
        Escribir "  largo minimo: $($largos.Minimum); largo maximo: $($largos.Maximum) (columna CodigoEkon: 20)"
    }

    Escribir ''
    Escribir 'DEPARTAMENTOS de la tabla Departamento (registros cuyo departamento o unidad coincide)'
    foreach ($d in $Departamentos) { Escribir "  $d = $($coincidencias[$d])" }
    Escribir "  Nombres de departamento/unidad sin coincidencia: $($noCoinciden.Count) distintos (se listan arriba en la lista permitida)."
}

# ---------------------------------------------------------------- principal
Iniciar-Salida 'resultado-sondeo.txt'
Escribir '== A2 Sondeo de EvolutionEmployee (solo agregados) =='
$cuerpos = @(Get-ChildItem -Path $PSScriptRoot -Filter 'cuerpo-empleados-*.json' | Sort-Object Name)
if ($cuerpos.Count -eq 0) {
    Escribir 'No hay cuerpo-empleados-*.json. Copie cuerpo-empleados.ejemplo.json a cuerpo-empleados-1.json y vuelva a ejecutar.'
    exit 1
}

$base = Base-Url $UrlBase
try {
foreach ($archivo in $cuerpos) {
    Escribir ''
    Escribir "---------------------------------------------------------------- $($archivo.Name)"
    $textoCuerpo = [IO.File]::ReadAllText($archivo.FullName, [Text.Encoding]::UTF8)
    try { $cuerpo = Leer-Json $textoCuerpo }
    catch { Escribir 'El cuerpo no es JSON valido (los comentarios // no son JSON). Se omite.'; continue }
    if (-not ($cuerpo -is [System.Collections.IDictionary])) { Escribir 'El cuerpo debe ser un objeto JSON. Se omite.'; continue }
    Escribir ('Cuerpo: ' + (Describir-Cuerpo $cuerpo))

    $nombreSimulacion = 'empleados_' + [IO.Path]::GetFileNameWithoutExtension($archivo.Name)
    $r = Solicitar -Metodo 'POST' -Base $base -Ruta $Ruta -Cuerpo $textoCuerpo -TimeoutSegundos $TimeoutSegundos -NombreSimulacion $nombreSimulacion
    Escribir ('Respuesta: ' + (Describir-Respuesta $r))
    if ($r.Codigo -ge 200 -and $r.Codigo -lt 300 -and $r.Texto) {
        $raiz = $null
        $jsonValido = $true
        try { $raiz = Leer-Json $r.Texto }
        catch { $jsonValido = $false; Escribir "La respuesta no es JSON valido ($($_.Exception.GetType().Name)). No se muestra el contenido." }
        if ($jsonValido) {
            try { Analizar $raiz }
            catch [System.IO.IOException] {
                # Error al ESCRIBIR el resultado (no es un problema del JSON). Solo la consola: el archivo fallo.
                Write-Host "ERROR al escribir resultado-sondeo.txt: $($_.Exception.Message). El informe quedo incompleto; vuelva a ejecutar."
            }
            catch {
                # Solo el tipo y la linea del script: el mensaje de la excepcion podria incluir un valor de la respuesta.
                Escribir ("ERROR al generar el informe ({0}, linea {1} del script). La respuesta SI era JSON valido; reporte este error." -f
                    $_.Exception.GetType().Name, $_.InvocationInfo.ScriptLineNumber)
            }
        }
        Remove-Variable raiz -ErrorAction SilentlyContinue
    }
    elseif ($r.Codigo -eq 401 -or $r.Codigo -eq 403) {
        Escribir 'La API pide autenticacion (401/403): anotar para la 26b. No se muestra el contenido.'
    }
    elseif ($r.Codigo -ne 0) {
        Escribir 'Respuesta no exitosa: no se muestra el contenido.'
    }
    # La respuesta solo vivio en memoria.
    Remove-Variable r, textoCuerpo, cuerpo -ErrorAction SilentlyContinue
    [GC]::Collect()
}
Escribir ''
Escribir 'Fin. Pegue en el chat solo resultado-sondeo.txt. No comparta respuestas crudas del ERP.'
}
finally { Cerrar-Salida }
