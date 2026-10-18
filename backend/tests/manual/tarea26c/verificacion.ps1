# ==========================================================================
# TAREA-26c (Fase B2) - Verificacion de horarios no laborales y turno nocturno con VISTAS PREVIAS (NO escribe):
#   POST /api/proyectos/previsualizar y POST /api/proyectos/{id}/cabecera/previsualizar.
#   Base de "crear": ..\tarea26d\crear-proyecto-1.json (el de la V3 de la 26d) o la ruta de BASE_CREAR; solo se cambian
#   el horario y el almuerzo.
#   set PERFIL=http      (por defecto; codigos del ERP real de la B1: 1 LIBRE, 73 = 17:00-04:00, 118 sin almuerzo)
#   set PERFIL=simulado  (API en modo Simulado: 4 PATERNIDAD PROYECTOS, 6 = 22:00-06:00, 8 sin almuerzo)
#   set PROYECTO_ID=14   (proyecto ACTIVO para la edicion de cabecera; por defecto 14)
#   set DEV_USER=gestor  (por defecto)
#   Muestra por caso: codigo HTTP, si es el esperado y los errores por campo de la API (solo mensajes; nunca la solicitud).
# Salida: resultado-verificacion.txt. Simulacion: SIMULAR=1 (simulacion\v_<caso>.json + .codigo, ficticios).
# ==========================================================================
. (Join-Path $PSScriptRoot '..\tarea26a\comun.ps1')
if (-not $env:SIMULACION) { $script:CarpetaSimulacion = Join-Path $PSScriptRoot 'simulacion' }

$base = Base-Url $(if ($env:API_BASE) { $env:API_BASE } else { 'https://localhost:7180' })
if (-not $script:Simular) { Permitir-CertificadoLocal }
$usuario = if ($env:DEV_USER) { $env:DEV_USER } else { 'gestor' }
$proyectoId = if ($env:PROYECTO_ID) { $env:PROYECTO_ID } else { '14' }

# Perfiles: horario no laboral, nocturno (con almuerzo dentro, fuera y que cruza la medianoche) y sin almuerzo.
$perfiles = @{
    'http'     = @{ NoLaboral = 1; Nocturno = 73; Dentro = @('01:00', '02:00'); Fuera = @('13:00', '14:00'); Cruza = @('23:00', '01:00'); SinAlmuerzo = 118 }
    'simulado' = @{ NoLaboral = 4; Nocturno = 6; Dentro = @('02:00', '03:00'); Fuera = @('13:00', '14:00'); Cruza = @('23:00', '01:00'); SinAlmuerzo = 8 }
}
$nombrePerfil = if ($env:PERFIL) { $env:PERFIL.ToLowerInvariant() } else { 'http' }
$p = $perfiles[$nombrePerfil]

# En simulacion se usa siempre la plantilla (valores ficticios), nunca el archivo del usuario.
$archivo = if ($script:Simular) { Join-Path $PSScriptRoot '..\tarea26d\crear-proyecto.ejemplo.json' }
           elseif ($env:BASE_CREAR) { $env:BASE_CREAR }
           else { Join-Path $PSScriptRoot '..\tarea26d\crear-proyecto-1.json' }

Add-Type -AssemblyName System.Web.Extensions
$serializador = New-Object System.Web.Script.Serialization.JavaScriptSerializer

function Cuerpo-Crear([int]$Horario, [string]$Salida, [string]$Regreso) {
    $c = Leer-Json ([IO.File]::ReadAllText($archivo, [Text.Encoding]::UTF8))
    $c['horarioCodigo'] = $Horario
    $c['salidaAlmuerzo'] = $Salida
    $c['regresoAlmuerzo'] = $Regreso
    return $serializador.Serialize($c)
}

function Cuerpo-Cabecera([int]$Horario) { return $serializador.Serialize(@{ horarioCodigo = $Horario }) }

Iniciar-Salida (Join-Path $PSScriptRoot 'resultado-verificacion.txt')
try {
    Escribir "== TAREA-26c Verificacion (solo vistas previas, no escribe), usuario $usuario, perfil $nombrePerfil, proyecto $proyectoId =="
    if (-not $p) { Escribir "PERFIL desconocido: $nombrePerfil (use http o simulado)."; exit 1 }
    if (-not (Test-Path $archivo)) { Escribir "No existe $archivo (ver ..\tarea26d\LEEME.md, V3, o set BASE_CREAR=...)."; exit 1 }

    $rutaCabecera = "api/proyectos/$proyectoId/cabecera/previsualizar"
    $casos = @(
        @{ Nombre = 'v_crear_no_laboral'; Texto = "crear con horario no laboral $($p.NoLaboral)"; Ruta = 'api/proyectos/previsualizar'
           Cuerpo = (Cuerpo-Crear $p.NoLaboral '13:00' '14:00'); Esperado = 400; Clave = 'horarioCodigo' },
        @{ Nombre = 'v_cabecera_no_laboral'; Texto = "cabecera: elegir el horario no laboral $($p.NoLaboral)"; Ruta = $rutaCabecera
           Cuerpo = (Cuerpo-Cabecera $p.NoLaboral); Esperado = 400; Clave = 'horarioCodigo' },
        @{ Nombre = 'v_crear_nocturno_dentro'; Texto = "crear nocturno $($p.Nocturno) con almuerzo $($p.Dentro -join '-')"; Ruta = 'api/proyectos/previsualizar'
           Cuerpo = (Cuerpo-Crear $p.Nocturno $p.Dentro[0] $p.Dentro[1]); Esperado = 200 },
        @{ Nombre = 'v_crear_nocturno_fuera'; Texto = "crear nocturno $($p.Nocturno) con almuerzo $($p.Fuera -join '-')"; Ruta = 'api/proyectos/previsualizar'
           Cuerpo = (Cuerpo-Crear $p.Nocturno $p.Fuera[0] $p.Fuera[1]); Esperado = 400; Clave = 'salidaAlmuerzo' },
        @{ Nombre = 'v_crear_cruza_medianoche'; Texto = "crear nocturno $($p.Nocturno) con almuerzo $($p.Cruza -join '-')"; Ruta = 'api/proyectos/previsualizar'
           Cuerpo = (Cuerpo-Crear $p.Nocturno $p.Cruza[0] $p.Cruza[1]); Esperado = 400; Clave = 'regresoAlmuerzo' },
        @{ Nombre = 'v_crear_sin_almuerzo'; Texto = "crear con horario sin almuerzo $($p.SinAlmuerzo), sin almuerzo"; Ruta = 'api/proyectos/previsualizar'
           Cuerpo = (Cuerpo-Crear $p.SinAlmuerzo $null $null); Esperado = 200 },
        @{ Nombre = 'v_cabecera_nocturno_sin_almuerzo'; Texto = "cabecera: cambiar al nocturno $($p.Nocturno) sin enviar almuerzo (D7)"; Ruta = $rutaCabecera
           Cuerpo = (Cuerpo-Cabecera $p.Nocturno); Esperado = 400; Clave = 'salidaAlmuerzo' }
    )

    $fallos = 0
    foreach ($caso in $casos) {
        $r = Solicitar -Metodo 'POST' -Base $base -Ruta $caso.Ruta -Cuerpo $caso.Cuerpo -Encabezados @{ 'X-Dev-User' = $usuario } `
            -TimeoutSegundos 120 -NombreSimulacion $caso.Nombre
        $ok = $r.Codigo -eq $caso.Esperado
        $errores = $null
        if ($r.Codigo -ge 400 -and $r.Texto) {
            try { $pd = Leer-Json $r.Texto; if ($pd -is [Collections.IDictionary] -and $pd.ContainsKey('errors')) { $errores = $pd['errors'] } }
            catch { }
        }
        # Un 400 esperado debe traer el error en el campo esperado (no cualquier 400).
        if ($ok -and $caso.Esperado -eq 400 -and -not ($errores -and $errores.ContainsKey($caso.Clave))) { $ok = $false }
        if (-not $ok) { $fallos++ }
        Escribir ''
        Escribir ("-- {0}: {1} -> {2}" -f $caso.Texto, (Describir-Respuesta $r), $(if ($ok) { 'OK' } else { "NO ESPERADO (se esperaba HTTP $($caso.Esperado)$(if ($caso.Clave) { " en $($caso.Clave)" }))" }))
        if ($errores) {
            foreach ($k in $errores.Keys) { Escribir ("   {0}: {1}" -f $k, (Texto-Linea (@($errores[$k]) -join ' '))) }
        }
    }
    Escribir ''
    Escribir "Resumen: $($casos.Count - $fallos) de $($casos.Count) casos como se esperaba."
    Escribir 'Fin. Pegue en el chat resultado-verificacion.txt.'
}
finally { Cerrar-Salida }
