# ==========================================================================
# TAREA-7d-1 - Publicacion de QA (J5). Lo ejecuta el USUARIO en su PC. NO llama a la API, NO se conecta a ningun
# servidor ni a la base y NO compila en la copia de trabajo:
#   1. exige "git status --porcelain" vacio y anota el hash de HEAD;
#   2. git archive HEAD -> carpeta temporal FUERA del repo;
#   3. alli: npm ci, npm run build -- --mode qa, dotnet tool restore, dotnet publish (QA) y el script de migraciones
#      --idempotent del MISMO commit;
#   4. resultado en D:\PROFESIOGRAMA_DEPLOY\QA\<yyyyMMdd_HHmm>_<hash7>\ (app\, migraciones-QA.sql, version.txt);
#   5. borra la carpeta temporal al final, aunque algo falle.
# Otra carpeta base: set DESTINO_QA=E:\otra\ruta
# El script de migraciones lo genera dotnet ef con la configuracion de Development del usuario (no se conecta a la base).
# Salida: resultado-publicar-qa.txt (CONTROL, rutas y hash; nunca cadenas de conexion).
# ==========================================================================
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 'comun7d1.ps1')

$ArchivoCandado = Join-Path $PSScriptRoot 'resultado-publicar-qa.lock'
$ArchivoEstado = Join-Path $PSScriptRoot 'resultado-estado-publicar-qa.txt'
$ArchivoSalida = Join-Path $PSScriptRoot 'resultado-publicar-qa.txt'
if (-not (Tomar-Candado $ArchivoCandado 'publicar-qa.cmd')) { exit 2 }

$Repo = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$DestinoBase = if ($env:DESTINO_QA) { $env:DESTINO_QA } else { 'D:\PROFESIOGRAMA_DEPLOY\QA' }
$Temporal = Join-Path ([IO.Path]::GetTempPath()) ("profesiograma-7d1-" + [Guid]::NewGuid().ToString('N'))
$Estado = @{ etapa = 'inicio'; inicio = (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') }

function Etapa([string]$Nombre) {
    $Estado['etapa'] = $Nombre
    Guardar-Estado $ArchivoEstado $Estado
    Escribir ''
    Escribir ("[{0}] {1}" -f (Get-Date -Format 'HH:mm:ss'), $Nombre)
}

# Ejecuta un programa y falla si su codigo de salida no es 0 (la salida se muestra en la consola, no en el resultado).
function Ejecutar([string]$Programa, [string[]]$Argumentos, [string]$Carpeta) {
    Push-Location $Carpeta
    try {
        & $Programa @Argumentos | Out-Host
        if ($LASTEXITCODE -ne 0) { throw ("{0} {1} termino con codigo {2}." -f $Programa, ($Argumentos -join ' '), $LASTEXITCODE) }
    }
    finally { Pop-Location }
}

$codigo = 0
try {
    Escribir 'PUBLICACION DE QA (TAREA-7d-1) - no se conecta a ningun servidor'

    # ---------------------------------------------------------------- 1. arbol limpio y hash
    Etapa '1. git status y hash'
    $sucio = @(& git -C $Repo status --porcelain)
    Control 'P1-arbol-limpio' ($sucio.Count -eq 0) '0 cambios sin commit' ("{0} cambios sin commit" -f $sucio.Count)
    if ($sucio.Count -ne 0) { throw 'Haga commit (o descarte los cambios) antes de publicar.' }
    $hash = (& git -C $Repo rev-parse HEAD).Trim()
    $hash7 = $hash.Substring(0, 7)
    $rama = (& git -C $Repo rev-parse --abbrev-ref HEAD).Trim()
    Escribir ("Commit {0} (rama {1})" -f $hash, $rama)

    $Destino = Join-Path $DestinoBase ("{0}_{1}" -f (Get-Date -Format 'yyyyMMdd_HHmm'), $hash7)
    if (Test-Path $Destino) { throw "La carpeta $Destino ya existe." }
    $Estado['commit'] = $hash; $Estado['destino'] = $Destino; $Estado['temporal'] = $Temporal

    # ---------------------------------------------------------------- 2. git archive fuera del repo
    Etapa '2. git archive HEAD a la carpeta temporal'
    New-Item -ItemType Directory -Path $Temporal | Out-Null
    $zip = "$Temporal.zip"
    Ejecutar 'git' @('-C', $Repo, 'archive', '--format=zip', '-o', $zip, 'HEAD') $Repo
    Expand-Archive -Path $zip -DestinationPath $Temporal
    Remove-Item $zip -Force

    # ---------------------------------------------------------------- 3. frontend
    Etapa '3. frontend: npm ci y npm run build -- --mode qa'
    $front = Join-Path $Temporal 'frontend'
    Ejecutar 'npm.cmd' @('ci') $front
    Ejecutar 'npm.cmd' @('run', 'build', '--', '--mode', 'qa') $front

    # ---------------------------------------------------------------- 4. backend
    Etapa '4. backend: dotnet tool restore y dotnet publish (QA)'
    $back = Join-Path $Temporal 'backend'
    $app = Join-Path $Destino 'app'
    Ejecutar 'dotnet' @('tool', 'restore') $back
    Ejecutar 'dotnet' @('publish', 'src/App.Api', '-c', 'Release', '-p:EnvironmentName=QA', '-o', $app) $back
    Copy-Item -Path (Join-Path $front 'dist\*') -Destination (New-Item -ItemType Directory -Path (Join-Path $app 'wwwroot') -Force) -Recurse

    # ---------------------------------------------------------------- 5. migraciones del mismo commit
    Etapa '5. dotnet ef migrations script --idempotent'
    $sqlEf = Join-Path $Temporal 'migraciones-ef.sql'
    Ejecutar 'dotnet' @('ef', 'migrations', 'script', '--idempotent', '--configuration', 'Release',
        '--project', 'src/App.Infrastructure', '--startup-project', 'src/App.Api',
        '--context', 'ProfesiogramaDbContext', '-o', $sqlEf) $back
    $sql = Join-Path $Destino 'migraciones-QA.sql'
    $contenidoEf = [IO.File]::ReadAllText($sqlEf)
    [IO.File]::WriteAllText($sql, "USE PROFESIOGRAMA_QA;`r`nGO`r`n`r`n" + $contenidoEf, (New-Object Text.UTF8Encoding($true)))

    # ---------------------------------------------------------------- 6. version.txt
    Etapa '6. version.txt'
    $version = @("commit=$hash", "rama=$rama", ("fecha=" + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss')), 'entorno=QA') -join "`r`n"
    [IO.File]::WriteAllText((Join-Path $Destino 'version.txt'), $version, (New-Object Text.UTF8Encoding($false)))

    # ---------------------------------------------------------------- 7. comprobaciones
    Etapa '7. comprobaciones de la salida'
    $todos = @(Get-ChildItem -Path $Destino -Recurse -File -Force)
    $prohibidos = @($todos | Where-Object {
        $_.Name -eq 'appsettings.Development.json' -or $_.Name -like '*.local' -or $_.Name -like '.env*' -or $_.Name -like '*Tests*.dll' })
    Control 'C1-sin-archivos-prohibidos' ($prohibidos.Count -eq 0) '0 (Development.json, *.local, .env*, *Tests*.dll)' ("{0}: {1}" -f $prohibidos.Count, (($prohibidos | ForEach-Object Name) -join ', '))

    $textos = @($todos | Where-Object { $_.Extension -in '.json', '.config', '.js', '.html', '.txt', '.xml' })
    $conCadena = @($textos | Where-Object { Select-String -Path $_.FullName -Pattern 'Password=', 'User ID=' -SimpleMatch -CaseSensitive -Quiet })
    Control 'C2-sin-cadenas-de-conexion' ($conCadena.Count -eq 0) '0 archivos con Password= o User ID=' ("{0} archivos" -f $conCadena.Count)

    Control 'C3-appsettings.QA.json' (Test-Path (Join-Path $app 'appsettings.QA.json')) 'existe' $(if (Test-Path (Join-Path $app 'appsettings.QA.json')) { 'existe' } else { 'no existe' })
    Control 'C4-wwwroot-index.html' (Test-Path (Join-Path $app 'wwwroot\index.html')) 'existe' $(if (Test-Path (Join-Path $app 'wwwroot\index.html')) { 'existe' } else { 'no existe' })

    $webConfig = [IO.File]::ReadAllText((Join-Path $app 'web.config'))
    $entornoQa = $webConfig -match 'name="ASPNETCORE_ENVIRONMENT"\s+value="QA"'
    $enProceso = $webConfig.Contains('hostingModel="inprocess"')
    Control 'C5-web.config' ($entornoQa -and $enProceso) 'ASPNETCORE_ENVIRONMENT=QA y hostingModel=inprocess' ("entorno QA: {0}; inprocess: {1}" -f $entornoQa, $enProceso)

    $runtimeConfig = [IO.File]::ReadAllText((Join-Path $app 'App.Api.runtimeconfig.json'))
    $nls = $runtimeConfig -match '"System\.Globalization\.UseNls"\s*:\s*true'
    Control 'C6-UseNls' $nls 'System.Globalization.UseNls: true' $(if ($nls) { 'presente' } else { 'ausente' })

    $pkcs = Join-Path $app 'System.Security.Cryptography.Pkcs.dll'
    $versionPkcs = if (Test-Path $pkcs) { (Get-Item $pkcs).VersionInfo.ProductVersion } else { 'sin la DLL' }
    Control 'C7-Pkcs-10.0.13' ($versionPkcs -like '10.0.13*') '10.0.13' $versionPkcs

    $textoSql = [IO.File]::ReadAllText($sql)
    $inicio = $textoSql.StartsWith("USE PROFESIOGRAMA_QA;`r`nGO")
    Control 'C8-sql-USE-y-GO' $inicio 'empieza con USE PROFESIOGRAMA_QA; y GO' $(if ($inicio) { 'si' } else { 'no' })
    $upn = $textoSql.Contains('AgregarUpnUsuario')
    Control 'C9-sql-AgregarUpnUsuario' $upn 'presente' $(if ($upn) { 'presente' } else { 'ausente' })
    $exec = $textoSql.Contains("EXEC(N'")
    Control 'C10-sql-EXEC' $exec "EXEC(N' presente" $(if ($exec) { 'presente' } else { 'ausente' })
    $drop = $textoSql.IndexOf('DROP TABLE', [StringComparison]::OrdinalIgnoreCase) -ge 0
    Control 'C11-sql-sin-DROP-TABLE' (-not $drop) 'sin DROP TABLE' $(if ($drop) { 'contiene DROP TABLE' } else { 'sin DROP TABLE' })

    Escribir ''
    Escribir "Carpeta de la publicacion: $Destino"
    Escribir 'Siguiente paso: LEEME.md, paso 2 (respaldo de QA).'
    $Estado['etapa'] = if ($script:Fallas -eq 0) { 'terminado' } else { 'terminado con fallas' }
    if ($script:Fallas -gt 0) { $codigo = 1 }
}
catch {
    $script:Fallas++
    Escribir ("CONTROL ERROR en la etapa '{0}': {1}" -f $Estado['etapa'], $_.Exception.Message)
    $Estado['etapa'] = 'error en ' + $Estado['etapa']
    $codigo = 1
}
finally {
    # J5: la carpeta temporal se borra siempre (el resultado ya esta en el destino).
    if (Test-Path $Temporal) { Remove-Item $Temporal -Recurse -Force -ErrorAction SilentlyContinue }
    if (Test-Path "$Temporal.zip") { Remove-Item "$Temporal.zip" -Force -ErrorAction SilentlyContinue }
    $temporalBorrado = -not (Test-Path $Temporal)
    Control 'C12-temporal-borrado' $temporalBorrado 'borrada' $(if ($temporalBorrado) { 'borrada' } else { "queda $Temporal" })
    Guardar-Estado $ArchivoEstado $Estado
    Guardar-Salida $ArchivoSalida
    Remove-Item $ArchivoCandado -Force -ErrorAction SilentlyContinue
}
exit $codigo
