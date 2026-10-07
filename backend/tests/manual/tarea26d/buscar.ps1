# ==========================================================================
# TAREA-26d-1 - V1: buscador de empleados desde la API (GET /api/empleados en NUESTRA API, X-Dev-User: gestor).
#   Muestra SOLO: codigo HTTP, total, pagina, si hay avisoErp (lista anterior) y los CODIGOS de los primeros 10.
#   Nunca nombres. Variantes:
#     1) soloMisDepartamentos=true (por defecto del gestor) y 2) soloMisDepartamentos=false, sin texto;
#     3) y 4) con el texto de %TEXTO% (p. ej. un apellido) tal cual y SIN tildes (deben dar el mismo total).
# Base: %API_BASE% (por defecto https://localhost:7180). Salida: resultado-buscar.txt. NO escribe en la base.
# Simulacion: SIMULAR=1 (respuestas buscar_1.json ... buscar_4.json en %SIMULACION%).
# ==========================================================================
. (Join-Path $PSScriptRoot '..\tarea26a\comun.ps1')
if (-not $env:SIMULACION) { $script:CarpetaSimulacion = Join-Path $PSScriptRoot 'simulacion' }

$base = Base-Url $(if ($env:API_BASE) { $env:API_BASE } else { 'https://localhost:7180' })
if (-not $script:Simular) { Permitir-CertificadoLocal }

$variantes = @(
    @{ Nombre = 'buscar_1'; Descripcion = 'sin texto, soloMisDepartamentos=true'; Texto = $null; SoloMis = 'true' },
    @{ Nombre = 'buscar_2'; Descripcion = 'sin texto, soloMisDepartamentos=false'; Texto = $null; SoloMis = 'false' }
)
if ($env:TEXTO) {
    $variantes += @{ Nombre = 'buscar_3'; Descripcion = 'texto de TEXTO tal cual, soloMisDepartamentos=false'; Texto = $env:TEXTO; SoloMis = 'false' }
    $variantes += @{ Nombre = 'buscar_4'; Descripcion = 'texto de TEXTO sin tildes, soloMisDepartamentos=false'; Texto = (Sin-Tildes $env:TEXTO); SoloMis = 'false' }
}

Iniciar-Salida (Join-Path $PSScriptRoot 'resultado-buscar.txt')
try {
    Escribir '== V1 Buscador de empleados (desde la API) =='
    if (-not $env:TEXTO) { Escribir '(sin TEXTO: se omiten las variantes 3 y 4; set TEXTO=<apellido> para probar tildes)' }
    foreach ($v in $variantes) {
        $ruta = "api/empleados?soloMisDepartamentos=$($v.SoloMis)&pagina=1&tamano=20"
        if ($v.Texto) { $ruta += '&texto=' + [Uri]::EscapeDataString($v.Texto) }
        $r = Solicitar -Metodo 'GET' -Base $base -Ruta $ruta -Encabezados @{ 'X-Dev-User' = 'gestor' } -NombreSimulacion $v.Nombre
        Escribir ''
        Escribir "-- $($v.Descripcion): $(Describir-Respuesta $r)"
        if ($r.Codigo -eq 200) {
            $d = Leer-Json $r.Texto
            $codigos = @($d['items'] | Select-Object -First 10 | ForEach-Object { $_['codigoEkon'] })
            Escribir ("   total = {0}; pagina = {1}; avisoErp = {2}" -f $d['total'], $d['pagina'], $(if ($d['avisoErp']) { Texto-Linea $d['avisoErp'] } else { '(ninguno)' }))
            Escribir ("   codigos (primeros 10): {0}" -f ($codigos -join ', '))
        }
        elseif ($r.Codigo -gt 0 -and $r.Texto) {
            try { Escribir ('   title: ' + (Texto-Linea (Leer-Json $r.Texto)['title'])) } catch { Escribir '   (respuesta sin ProblemDetails)' }
        }
    }
    Escribir ''
    Escribir 'Fin. Pegue en el chat resultado-buscar.txt.'
}
finally { Cerrar-Salida }
