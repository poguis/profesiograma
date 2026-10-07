# ==========================================================================
# TAREA-26a - Funciones comunes de los scripts de sondeo (PowerShell 5.1, archivo ASCII).
#
# MODO SIMULACION: set SIMULAR=1 (en el .cmd o en la consola).
#   - Ninguna funcion abre conexiones de red: Invocar-Http lanza un error si se llama en simulacion.
#   - Las respuestas se leen de la carpeta %SIMULACION% (por defecto .\simulacion): el nombre del archivo es la ruta
#     de la solicitud con todo lo que no sea letra o numero cambiado por "_" (p. ej. "api_erp_horarios.json"), y el
#     codigo HTTP opcional va en el mismo nombre con extension ".codigo" (si no existe: 200; si no existe el .json: 404).
#   - Las URL base se fuerzan a https://localhost:1 (invalida) como segunda proteccion.
#
# DATOS SENSIBLES: las respuestas solo viven en memoria. Ningun script escribe respuestas crudas a disco; los
# archivos resultado-*.txt contienen solo agregados (conteos, tipos, largos y valores de la lista permitida).
# ==========================================================================
Set-StrictMode -Version 2
$ErrorActionPreference = 'Stop'

$script:Simular = ($env:SIMULAR -eq '1')
$script:CarpetaSimulacion = if ($env:SIMULACION) { $env:SIMULACION } else { Join-Path $PSScriptRoot 'simulacion' }
$script:ArchivoSalida = $null
$script:UrlInvalida = 'https://localhost:1'

# Salida: consola + archivo resultado-*.txt. El archivo se escribe con UN solo StreamWriter abierto durante toda la
# ejecucion (TAREA-26a, correccion A2: abrir y cerrar el archivo en cada linea con Add-Content puede fallar con
# IOException si otro proceso lo toma un instante, p. ej. el antivirus o el indexador).
# La consola puede fallar con caracteres no ASCII (PowerShell 5.1 con la pagina de codigos 65001): ese error no
# detiene el script; la linea se repite en la consola sin tildes y el archivo siempre la recibe completa.
function Iniciar-Salida([string]$Nombre) {
    Cerrar-Salida
    # Nombre relativo: carpeta de comun.ps1 (tarea26a). Ruta absoluta: la indicada (p. ej. tarea26b, que reutiliza este archivo).
    $script:ArchivoSalida = if ([IO.Path]::IsPathRooted($Nombre)) { $Nombre } else { Join-Path $PSScriptRoot $Nombre }
    $script:Escritor = New-Object IO.StreamWriter($script:ArchivoSalida, $false, (New-Object Text.UTF8Encoding($true)))
    $script:Escritor.AutoFlush = $true
    $script:AvisoConsola = $false
    if ($script:Simular) {
        Escribir "MODO SIMULACION (SIMULAR=1): sin llamadas de red; respuestas leidas de $script:CarpetaSimulacion"
    }
    Escribir ("Fecha: " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
}

function Cerrar-Salida {
    if ((Test-Path variable:script:Escritor) -and $script:Escritor) {
        $script:Escritor.Dispose()
        $script:Escritor = $null
    }
}

function Sin-Tildes([string]$Texto) {
    $descompuesto = $Texto.Normalize([Text.NormalizationForm]::FormD)
    $sb = New-Object Text.StringBuilder
    foreach ($c in $descompuesto.ToCharArray()) {
        $categoria = [Globalization.CharUnicodeInfo]::GetUnicodeCategory($c)
        if ($categoria -eq [Globalization.UnicodeCategory]::NonSpacingMark) { continue }
        if ([int]$c -lt 128) { [void]$sb.Append($c) } else { [void]$sb.Append('?') }
    }
    return $sb.ToString()
}

function Escribir([string]$Texto = '') {
    if ((Test-Path variable:script:Escritor) -and $script:Escritor) {
        try { $script:Escritor.WriteLine($Texto) }
        catch { throw (New-Object IO.IOException("No se pudo escribir en $($script:ArchivoSalida): $($_.Exception.Message)")) }
    }
    try { Write-Host $Texto }
    catch {
        if (-not $script:AvisoConsola) {
            $script:AvisoConsola = $true
            try { Write-Host '(aviso: la consola no admite algunos caracteres; se muestran sin tildes. El archivo de resultado los tiene completos.)' } catch { }
        }
        try { Write-Host (Sin-Tildes $Texto) } catch { }
    }
}

# Texto de un valor para mostrarlo en una sola linea: sin saltos de linea ni tabuladores y con un largo maximo.
function Texto-Linea([string]$Texto, [int]$Maximo = 100) {
    if ($null -eq $Texto) { return '' }
    $limpio = ($Texto -replace '\s+', ' ').Trim()   # saltos de linea, tabuladores y espacios repetidos -> un espacio
    if ($limpio.Length -gt $Maximo) { return $limpio.Substring(0, $Maximo) + '... (' + $limpio.Length + ' caracteres)' }
    return $limpio
}

function Base-Url([string]$Real) {
    if ($script:Simular) { return $script:UrlInvalida }
    return $Real
}

# JSON -> Dictionary[string,object] / object[] / valores simples. JavaScriptSerializer con limite maximo
# (ConvertFrom-Json de PowerShell 5.1 falla con respuestas de mas de 2 MB).
function Leer-Json([string]$Texto) {
    Add-Type -AssemblyName System.Web.Extensions
    $serializador = New-Object System.Web.Script.Serialization.JavaScriptSerializer
    $serializador.MaxJsonLength = [int]::MaxValue
    $serializador.RecursionLimit = 100
    return , $serializador.DeserializeObject($Texto)
}

function Tipo-Json($Valor) {
    if ($null -eq $Valor) { return 'null' }
    if ($Valor -is [string]) { return 'texto' }
    if ($Valor -is [bool]) { return 'booleano' }
    if ($Valor -is [System.Collections.IDictionary]) { return 'objeto' }
    if ($Valor -is [System.Array]) { return 'arreglo' }
    if ($Valor -is [int] -or $Valor -is [long] -or $Valor -is [decimal] -or $Valor -is [double]) { return 'numero' }
    return $Valor.GetType().Name
}

function Texto-Invariante($Valor) {
    if ($null -eq $Valor) { return $null }
    if ($Valor -is [string]) { return $Valor }
    return [System.Convert]::ToString($Valor, [System.Globalization.CultureInfo]::InvariantCulture)
}

# Certificado de desarrollo de ASP.NET Core: se acepta SOLO para el host "localhost" (nuestra API). Para cualquier
# otro host (p. ej. backstack.sedemi.com) se usa la validacion estandar de Windows.
function Permitir-CertificadoLocal {
    if (-not ('CertificadoLocalTarea26a' -as [type])) {
        Add-Type -TypeDefinition @'
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
public static class CertificadoLocalTarea26a {
    public static bool Validar(object emisor, X509Certificate certificado, X509Chain cadena, SslPolicyErrors errores) {
        if (errores == SslPolicyErrors.None) { return true; }
        HttpWebRequest solicitud = emisor as HttpWebRequest;
        return solicitud != null && solicitud.RequestUri.Host == "localhost";
    }
    public static void Activar() { ServicePointManager.ServerCertificateValidationCallback = Validar; }
}
'@
    }
    [CertificadoLocalTarea26a]::Activar()
}

# UNICA funcion que abre conexiones HTTP. En simulacion nunca se ejecuta (y si se ejecutara, se detiene).
function Invocar-Http {
    param([string]$Metodo, [string]$Url, [string]$Cuerpo, [hashtable]$Encabezados = @{}, [int]$TimeoutSegundos = 60)
    if ($script:Simular) { throw 'ERROR INTERNO: llamada de red en modo simulacion (no deberia ocurrir).' }
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $cronometro = [Diagnostics.Stopwatch]::StartNew()
    $respuesta = $null
    $errorRed = $null
    try {
        $solicitud = [Net.HttpWebRequest]::Create($Url)
        $solicitud.Method = $Metodo
        $solicitud.Timeout = $TimeoutSegundos * 1000
        $solicitud.ReadWriteTimeout = $TimeoutSegundos * 1000
        $solicitud.Accept = 'application/json'
        foreach ($clave in $Encabezados.Keys) { $solicitud.Headers[$clave] = $Encabezados[$clave] }
        if ($Cuerpo) {
            $bytes = [Text.Encoding]::UTF8.GetBytes($Cuerpo)
            $solicitud.ContentType = 'application/json; charset=utf-8'
            $solicitud.ContentLength = $bytes.Length
            $flujo = $solicitud.GetRequestStream()
            $flujo.Write($bytes, 0, $bytes.Length)
            $flujo.Close()
        }
        $respuesta = $solicitud.GetResponse()
    }
    catch [Net.WebException] {
        $respuesta = $_.Exception.Response
        if (-not $respuesta) { $errorRed = $_.Exception.Status.ToString() }
    }
    if (-not $respuesta) {
        return [pscustomobject]@{ Codigo = 0; Milisegundos = $cronometro.ElapsedMilliseconds; Bytes = 0; Texto = $null; Error = $errorRed }
    }
    $memoria = New-Object IO.MemoryStream
    $respuesta.GetResponseStream().CopyTo($memoria)
    $codigo = [int]$respuesta.StatusCode
    $respuesta.Close()
    $contenido = $memoria.ToArray()
    return [pscustomobject]@{
        Codigo = $codigo; Milisegundos = $cronometro.ElapsedMilliseconds; Bytes = $contenido.Length
        Texto = [Text.Encoding]::UTF8.GetString($contenido); Error = $null
    }
}

# Simulacion: respuesta leida de un archivo (ver el encabezado). Nunca abre conexiones.
function Respuesta-Simulada([string]$Ruta) {
    $nombre = ($Ruta -replace '[^A-Za-z0-9]+', '_').Trim('_')
    $json = Join-Path $script:CarpetaSimulacion "$nombre.json"
    $archivoCodigo = Join-Path $script:CarpetaSimulacion "$nombre.codigo"
    if (-not (Test-Path $json)) {
        return [pscustomobject]@{ Codigo = 404; Milisegundos = 0; Bytes = 0; Texto = ''; Error = $null }
    }
    $codigo = 200
    if (Test-Path $archivoCodigo) { $codigo = [int]((Get-Content $archivoCodigo -Raw).Trim()) }
    $texto = [IO.File]::ReadAllText($json, [Text.Encoding]::UTF8)
    return [pscustomobject]@{ Codigo = $codigo; Milisegundos = 0; Bytes = [Text.Encoding]::UTF8.GetByteCount($texto); Texto = $texto; Error = $null }
}

# Solicitud real o simulada. $Ruta identifica la respuesta simulada (sin base ni parametros sensibles).
function Solicitar {
    param([string]$Metodo, [string]$Base, [string]$Ruta, [string]$Cuerpo, [hashtable]$Encabezados = @{}, [int]$TimeoutSegundos = 60,
          [string]$NombreSimulacion)
    if ($script:Simular) {
        if (-not $NombreSimulacion) { $NombreSimulacion = $Ruta }
        return Respuesta-Simulada $NombreSimulacion
    }
    return Invocar-Http -Metodo $Metodo -Url ($Base.TrimEnd('/') + '/' + $Ruta.TrimStart('/')) -Cuerpo $Cuerpo `
        -Encabezados $Encabezados -TimeoutSegundos $TimeoutSegundos
}

function Describir-Respuesta($R) {
    if ($R.Codigo -eq 0) { return "sin respuesta ($($R.Error)), $($R.Milisegundos) ms" }
    return "HTTP $($R.Codigo), $($R.Milisegundos) ms, $($R.Bytes) bytes"
}
