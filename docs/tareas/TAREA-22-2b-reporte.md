# TAREA-22-2b — Avisos NU1902/NU1903 de System.Security.Cryptography.Pkcs

**Fecha:** 2026-10-21. **Resultado:** pin directo temporal de `System.Security.Cryptography.Pkcs` 10.0.13 en `App.Infrastructure.csproj`.
- Compilación: 0 advertencias (antes, 16).
- Paquetes vulnerables: ninguno.
- Pruebas: 801/801.

No se tocaron código de negocio, pruebas ni frontend.

## 1. Diagnóstico
- **Gestión de paquetes:** no hay gestión central (`Directory.Packages.props` no existe; `Directory.Build.props` solo define propiedades). Las versiones van en cada `.csproj`.
- **Rutas de `Pkcs` antes del cambio** (`dotnet nuget why`):

| Proyecto | Versión | Ruta |
|---|---|---|
| App.Infrastructure | 9.0.11 | `Microsoft.Data.SqlClient` 6.1.6 (directa y vía `Microsoft.EntityFrameworkCore.SqlServer` 10.0.12) |
| App.Api | 10.0.10 | Lo mismo, por App.Infrastructure, más `Microsoft.Identity.Web` 4.15.0 → `TokenAcquisition`/`TokenCache` |
| App.Infrastructure.Tests | 9.0.11 | Por App.Infrastructure |
| App.Api.Tests | 10.0.10 | Por App.Api |
| App.Domain, App.Application y sus pruebas | — | Sin dependencia |

- **Contradicción menor con el contexto de la tarea:** el repo usa `Microsoft.Data.SqlClient` **6.1.6**, no la 7.1.1. No se actualizó (fuera de alcance).
- **Usos directos:** con grep sobre `backend/src`, `backend/tests` y `frontend/src` no hay usos de `SignedCms`, `EnvelopedCms` ni `Pkcs`.

## 2. Cambio
`backend/src/App.Infrastructure/App.Infrastructure.csproj`: `<PackageReference Include="System.Security.Cryptography.Pkcs" Version="10.0.13" />`.
- Lleva un comentario XML con el motivo (los dos GHSA), de qué paquetes llega y cuándo retirarlo.
- Sin `NoWarn` y sin desactivar NuGetAudit.
- Como dependencia directa de Infrastructure, la 10.0.13 fluye a App.Api y a las pruebas. **No hizo falta otro pin** en App.Api.

## 3. Comandos y resultados
| Comando | Resultado |
|---|---|
| `dotnet nuget why …` (después) | App.Infrastructure y App.Api resuelven **10.0.13** |
| `dotnet restore Profesiograma.slnx --force` | OK, sin avisos NU* (tampoco NU1510) |
| `dotnet list Profesiograma.slnx package --vulnerable --include-transitive` | Los 8 proyectos: «no tiene paquetes vulnerables» |
| `dotnet build Profesiograma.slnx -c Release --no-incremental` | **Antes:** 16 advertencias (NU1902/NU1903, 8 distintas repetidas). **Después:** 0 advertencias, 0 errores |
| `dotnet test --solution Profesiograma.slnx -c Release` | 801/801 |
| `dotnet ef migrations has-pending-model-changes … --configuration Release` | Sin cambios |
| `dotnet publish src/App.Api -c Release -o <scratchpad>\publish-pkcs` | Incluye `System.Security.Cryptography.Pkcs.dll` (raíz y `runtimes\win\lib\net10.0\`), FileVersion **10.0.1326.50504**, ProductVersion 10.0.13. La carpeta se borró al terminar; no se tocó `bin\Debug` |

## 4. Pendientes
- **00_ESTADO, pendiente 58:** retirar el pin cuando SqlClient e Identity.Web resuelvan una versión corregida.
- **Hosting Bundle:** no hace falta un pendiente para la Fase 7, porque la publicación incluye la DLL corregida.
