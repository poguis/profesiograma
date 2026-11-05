# ==========================================================================
# TAREA-7d-1 - funciones comunes de publicar-qa.ps1 y humo-qa.ps1: candado, archivo de estado y salida con CONTROL.
# Texto sin tildes a proposito (PowerShell 5.1 lee los .ps1 sin BOM como ANSI).
# ==========================================================================

$script:Lineas = New-Object 'System.Collections.Generic.List[string]'
$script:Fallas = 0
$script:Omitidos = 0

function Escribir([string]$Texto) {
    Write-Host $Texto
    $script:Lineas.Add($Texto)
}

function Control([string]$Caso, [bool]$Ok, [string]$Esperado, [string]$Obtenido) {
    if ($Ok) {
        Escribir ("CONTROL {0} OK ({1})" -f $Caso, $Obtenido)
    } else {
        $script:Fallas++
        Escribir ("CONTROL {0} FALLA (esperado {1}, obtenido {2})" -f $Caso, $Esperado, $Obtenido)
    }
}

function Omitido([string]$Caso, [string]$Motivo) {
    $script:Omitidos++
    Escribir ("CONTROL {0} OMITIDO ({1})" -f $Caso, $Motivo)
}

function Guardar-Salida([string]$Archivo) {
    Escribir ''
    Escribir ("Casos con FALLA: {0}; OMITIDOS: {1}" -f $script:Fallas, $script:Omitidos)
    [IO.File]::WriteAllText($Archivo, ($script:Lineas -join "`r`n"), (New-Object Text.UTF8Encoding($false)))
}

function Guardar-Estado([string]$Archivo, [hashtable]$Valores) {
    $texto = ($Valores.GetEnumerator() | Sort-Object Name | ForEach-Object { "{0}={1}" -f $_.Name, $_.Value }) -join "`r`n"
    [IO.File]::WriteAllText($Archivo, $texto, (New-Object Text.UTF8Encoding($false)))
}

# Candado de ejecucion (leccion de la TAREA-62-1): se crea ANTES de cualquier otra accion. Devuelve $true si es propio.
function Tomar-Candado([string]$Archivo, [string]$Nombre) {
    $mensaje = "Ya hay una ejecucion de $Nombre en curso (o termino mal hace menos de 30 minutos). No se ejecuta nada."
    if (Test-Path $Archivo) {
        if (((Get-Date) - (Get-Item $Archivo).LastWriteTime).TotalMinutes -lt 30) {
            Write-Host $mensaje
            Write-Host ("Candado: " + ([IO.File]::ReadAllText($Archivo)).Trim())
            return $false
        }
        Remove-Item $Archivo -Force
    }
    try {
        $flujo = [IO.File]::Open($Archivo, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
        $bytes = [Text.Encoding]::ASCII.GetBytes(("inicio {0}; PID {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss.fff'), $PID))
        $flujo.Write($bytes, 0, $bytes.Length)
        $flujo.Close()
        return $true
    }
    catch [IO.IOException] {
        Write-Host $mensaje
        return $false
    }
}
