# ==========================================================================
# TAREA-26d-1 - V10: transicion empleadoId / codigoEkon (P2) contra POST /api/proyectos/previsualizar (NO escribe).
#   Usa crear-proyecto-1.json como base y cambia SOLO el empleado del primer principal:
#     a) solo empleadoId = %EMPLEADO_ID%   (Id de una fila de Empleado de un empleado ACTIVO en la API; ver conteos.sql)
#     b) solo codigoEkon = %CODIGO_EKON%
#     c) ambos                              -> esperado 400 principales[0].codigoEkon "Indique codigoEkon o empleadoId, no ambos."
#     d) ninguno                            -> esperado 400 principales[0].empleadoId "El empleado es obligatorio."
#   Muestra codigo HTTP y, si hay, los errores de principales[0].*.
# Salida: resultado-transicion.txt. Simulacion: SIMULAR=1 (transicion_a.json ... transicion_d.json + .codigo).
# ==========================================================================
. (Join-Path $PSScriptRoot '..\tarea26a\comun.ps1')
if (-not $env:SIMULACION) { $script:CarpetaSimulacion = Join-Path $PSScriptRoot 'simulacion' }

$base = Base-Url $(if ($env:API_BASE) { $env:API_BASE } else { 'https://localhost:7180' })
if (-not $script:Simular) { Permitir-CertificadoLocal }

$archivo = Join-Path $PSScriptRoot 'crear-proyecto-1.json'
Iniciar-Salida (Join-Path $PSScriptRoot 'resultado-transicion.txt')
try {
    Escribir '== V10 Transicion empleadoId / codigoEkon (vista previa, no escribe) =='
    if (-not (Test-Path $archivo)) { Escribir 'No existe crear-proyecto-1.json (ver LEEME.md).'; exit 1 }
    if (-not $env:EMPLEADO_ID -or -not $env:CODIGO_EKON) { Escribir 'Indique set EMPLEADO_ID=<id> y set CODIGO_EKON=<codigo> (ver LEEME.md).'; exit 1 }

    Add-Type -AssemblyName System.Web.Extensions
    $serializador = New-Object System.Web.Script.Serialization.JavaScriptSerializer
    $casos = @(
        @{ Nombre = 'transicion_a'; Descripcion = 'a) solo empleadoId'; Id = [int]$env:EMPLEADO_ID; Codigo = $null },
        @{ Nombre = 'transicion_b'; Descripcion = 'b) solo codigoEkon'; Id = $null; Codigo = $env:CODIGO_EKON },
        @{ Nombre = 'transicion_c'; Descripcion = 'c) ambos (esperado 400)'; Id = [int]$env:EMPLEADO_ID; Codigo = $env:CODIGO_EKON },
        @{ Nombre = 'transicion_d'; Descripcion = 'd) ninguno (esperado 400)'; Id = $null; Codigo = $null }
    )
    foreach ($caso in $casos) {
        $cuerpo = Leer-Json ([IO.File]::ReadAllText($archivo, [Text.Encoding]::UTF8))
        $principal = $cuerpo['principales'][0]
        $principal['empleadoId'] = $caso.Id
        $principal['codigoEkon'] = $caso.Codigo
        $r = Solicitar -Metodo 'POST' -Base $base -Ruta 'api/proyectos/previsualizar' -Cuerpo ($serializador.Serialize($cuerpo)) `
            -Encabezados @{ 'X-Dev-User' = 'gestor' } -TimeoutSegundos 120 -NombreSimulacion $caso.Nombre
        Escribir ''
        Escribir "-- $($caso.Descripcion): $(Describir-Respuesta $r)"
        if ($r.Codigo -eq 400 -and $r.Texto) {
            $p = Leer-Json $r.Texto
            if ($p['errors']) {
                foreach ($k in ($p['errors'].Keys | Where-Object { $_ -like 'principales*0*' })) {
                    Escribir ("   {0}: {1}" -f $k, (Texto-Linea (@($p['errors'][$k]) -join ' ')))
                }
            }
        }
    }
    Escribir ''
    Escribir 'Fin. Pegue en el chat resultado-transicion.txt.'
}
finally { Cerrar-Salida }
