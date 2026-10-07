# ==========================================================================
# TAREA-26a - A1: conectividad con el ERP (backstack.sedemi.com, puertos 7048 y 7055).
#   1) Test-NetConnection a cada puerto (solo TCP).
#   2) GET :7048/api/Company/list_company (API ya usada por la app): SOLO codigo HTTP, tiempo y bytes.
#      El contenido de la respuesta no se muestra ni se guarda.
# Salida: resultado-conectividad.txt. Simulacion: SIMULAR=1 (archivos tcp_7048.txt / tcp_7055.txt con True o False,
# y api_Company_list_company.json).
# ==========================================================================
. (Join-Path $PSScriptRoot 'comun.ps1')

$servidor = 'backstack.sedemi.com'
Iniciar-Salida 'resultado-conectividad.txt'
Escribir "== A1 Conectividad con el ERP ($servidor) =="

foreach ($puerto in 7048, 7055) {
    if ($script:Simular) {
        $archivo = Join-Path $script:CarpetaSimulacion "tcp_$puerto.txt"
        $ok = (Test-Path $archivo) -and ((Get-Content $archivo -Raw).Trim() -eq 'True')
        $ms = 0
    }
    else {
        $cronometro = [Diagnostics.Stopwatch]::StartNew()
        $ok = Test-NetConnection -ComputerName $servidor -Port $puerto -InformationLevel Quiet -WarningAction SilentlyContinue
        $ms = $cronometro.ElapsedMilliseconds
    }
    Escribir ("TCP {0}: {1} ({2} ms)" -f $puerto, $(if ($ok) { 'CONECTA' } else { 'NO CONECTA' }), $ms)
}

$base = Base-Url "https://${servidor}:7048"
$r = Solicitar -Metodo 'GET' -Base $base -Ruta 'api/Company/list_company' -TimeoutSegundos 30
Escribir ("GET list_company: " + (Describir-Respuesta $r))
Remove-Variable r
Escribir 'Fin. Pegue en el chat solo este archivo (resultado-conectividad.txt).'
Cerrar-Salida
