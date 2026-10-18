# ==========================================================================
# TAREA-26d-3 - Retiro de empleadoId: POST /api/proyectos/previsualizar (NO escribe).
#   Usa ..\tarea26d\crear-proyecto-1.json (el de V3) como base y cambia SOLO el empleado del primer principal:
#     a) solo empleadoId = 1 (sin codigoEkon) -> esperado 400 principales[0].codigoEkon "El empleado es obligatorio."
#        (empleadoId ya no es parte del contrato: se ignora como cualquier campo desconocido)
#     b) solo codigoEkon (el del archivo)     -> esperado 200
#   Muestra codigo HTTP y, si hay, los errores de principales[0].* (nunca nombres).
# Salida: resultado-retiro.txt. Simulacion: SIMULAR=1 (simulacion\retiro_a.json, retiro_b.json + .codigo).
#   set RETIRO_B_SIMULACION=retiro_b_ilegible   (solo simulacion: caso b con un 400 de cuerpo ilegible)
# ==========================================================================
. (Join-Path $PSScriptRoot '..\tarea26a\comun.ps1')
if (-not $env:SIMULACION) { $script:CarpetaSimulacion = Join-Path $PSScriptRoot 'simulacion' }

$base = Base-Url $(if ($env:API_BASE) { $env:API_BASE } else { 'https://localhost:7180' })
if (-not $script:Simular) { Permitir-CertificadoLocal }

# En simulacion se usa siempre la plantilla (valores ficticios), nunca el archivo del usuario.
$archivo = if ($script:Simular) { Join-Path $PSScriptRoot '..\tarea26d\crear-proyecto.ejemplo.json' } `
           else { Join-Path $PSScriptRoot '..\tarea26d\crear-proyecto-1.json' }

Iniciar-Salida (Join-Path $PSScriptRoot 'resultado-retiro.txt')
try {
    Escribir '== TAREA-26d-3 Retiro de empleadoId (vista previa, no escribe) =='
    if (-not (Test-Path $archivo)) { Escribir 'No existe ..\tarea26d\crear-proyecto-1.json (ver ..\tarea26d\LEEME.md, V3).'; exit 1 }

    Add-Type -AssemblyName System.Web.Extensions
    $serializador = New-Object System.Web.Script.Serialization.JavaScriptSerializer
    $casos = @(
        @{ Nombre = 'retiro_a'; Descripcion = 'a) solo empleadoId (esperado 400 en codigoEkon)'; SoloId = $true; Esperado = 400 },
        @{ Nombre = 'retiro_b'; Descripcion = 'b) solo codigoEkon (esperado 200)'; SoloId = $false; Esperado = 200 }
    )
    foreach ($caso in $casos) {
        $cuerpo = Leer-Json ([IO.File]::ReadAllText($archivo, [Text.Encoding]::UTF8))
        if (-not ($cuerpo -is [Collections.IDictionary]) -or -not $cuerpo.ContainsKey('principales') -or @($cuerpo['principales']).Count -eq 0) {
            Escribir 'El archivo base no tiene "principales" con al menos un principal (nombres de campo en minusculas, ver ..\tarea26d\LEEME.md).'
            exit 1
        }
        $principal = $cuerpo['principales'][0]
        if ($caso.SoloId) {
            [void]$principal.Remove('codigoEkon')
            $principal['empleadoId'] = 1
        }
        else {
            [void]$principal.Remove('empleadoId')
        }
        $r = Solicitar -Metodo 'POST' -Base $base -Ruta 'api/proyectos/previsualizar' -Cuerpo ($serializador.Serialize($cuerpo)) `
            -Encabezados @{ 'X-Dev-User' = 'gestor' } -TimeoutSegundos 120 `
            -NombreSimulacion $(if ($caso.Nombre -eq 'retiro_b' -and $env:RETIRO_B_SIMULACION) { $env:RETIRO_B_SIMULACION } else { $caso.Nombre })
        Escribir ''
        Escribir "-- $($caso.Descripcion): $(Describir-Respuesta $r)"
        $esperado = $r.Codigo -eq $caso.Esperado
        if (-not $esperado) { Escribir "   NO ESPERADO (se esperaba HTTP $($caso.Esperado))." }
        if ($r.Codigo -ge 400 -and $r.Texto) {
            # Solo lo que devuelve la API (ProblemDetails): title/detail y errores por campo; nunca la solicitud.
            $p = $null
            try { $p = Leer-Json $r.Texto } catch { Escribir '   (respuesta no es JSON)' }
            if ($p -is [Collections.IDictionary]) {
                if (-not $esperado) {
                    if ($p.ContainsKey('title')) { Escribir "   title: $(Texto-Linea $p['title'])" }
                    if ($p.ContainsKey('detail') -and $p['detail']) { Escribir "   detail: $(Texto-Linea $p['detail'])" }
                }
                if ($p.ContainsKey('errors') -and $p['errors']) {
                    # Esperado: solo principales[0]; no esperado: todas las claves.
                    $claves = if ($esperado) { $p['errors'].Keys | Where-Object { $_ -like 'principales*0*' } } else { $p['errors'].Keys }
                    foreach ($k in $claves) { Escribir ("   {0}: {1}" -f $k, (Texto-Linea (@($p['errors'][$k]) -join ' '))) }
                }
                elseif (-not $esperado) {
                    Escribir '   (sin errores por campo: cuerpo ilegible o error general; ver title/detail)'
                }
            }
        }
    }
    Escribir ''
    Escribir 'Fin. Pegue en el chat resultado-retiro.txt.'
}
finally { Cerrar-Salida }
