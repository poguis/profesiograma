# PROFESIOGRAMA — Entorno base (Paso 1: conexión a SQL Server)

## Requisitos
- .NET 10 SDK → verificar con `dotnet --list-sdks` (debe aparecer 10.0.x).
- Acceso de red al SQL Server de pruebas.
- SSMS o Azure Data Studio (opcional, para el diagnóstico manual).

## Estructura
```
Profesiograma.slnx
src/
  App.Domain/          entidades y reglas (vacío por ahora)
  App.Application/     contratos y DTOs (IDatabaseDiagnostics)
  App.Infrastructure/  SQL Server (Microsoft.Data.SqlClient)
  App.Api/             Minimal API: GET /api/health/db
sql/
  00_diagnostico_sqlserver.sql
tests/
```

## Paso A — Diagnóstico manual (SSMS)
Abrir `sql/00_diagnostico_sqlserver.sql` conectado a la BD de pruebas y ejecutar.

## Paso B — Diagnóstico desde la API
```powershell
cd src\App.Api
dotnet user-secrets set "ConnectionStrings:Profesiograma" "Server=SERVIDOR;Database=BASE;Trusted_Connection=True;Encrypt=True;TrustServerCertificate=True"
# o con usuario SQL:
# dotnet user-secrets set "ConnectionStrings:Profesiograma" "Server=SERVIDOR;Database=BASE;User Id=USUARIO;Password=CLAVE;Encrypt=True;TrustServerCertificate=True"
cd ..\..
dotnet build
dotnet run --project src\App.Api --launch-profile https
```
Abrir `https://localhost:7180/api/health/db`.

- `CumpleRequisitos: true` → se puede ejecutar el script de la Fase 2 tal cual.
- `503` → revisar servidor, puerto (1433 o instancia `SERVIDOR\INSTANCIA`), firewall y credenciales.

La cadena de conexión **no** se guarda en el repositorio (user-secrets en desarrollo; variables de entorno en QA/Producción).
