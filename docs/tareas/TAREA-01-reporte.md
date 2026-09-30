# TAREA-01 — Reorganización del repositorio (estructura inicial con el Paso 1)

**Fecha:** 2026-09-30
**Resultado:** completada. La solución compila con 0 errores y el repositorio queda con el Paso 1 en `backend/`.

## 1. Qué se hizo

### Verificación inicial (alcance original: solo verificar)
- `backend/Profesiograma.slnx` existía y referenciaba los 4 proyectos.
- **No existía ningún `.csproj`** en `backend/src`, y sí había **62 archivos del Paso 2** (entidades, DbContext, configuraciones, DevAuth, endpoints, sembrador, `sql/dev/verificacion_paso2.sql`).
- `dotnet build` falló con 4 errores MSB3202 (no se encuentra el `.csproj`).
- Faltaban del Paso 1: los `.csproj`, `Program.cs`, `appsettings.json`, `launchSettings.json`, `HealthEndpoints.cs`, `Diagnostics/*`, `DependencyInjection.cs`, `AssemblyMarker.cs` y `sql/00_diagnostico_sqlserver.sql`.
- La tarea se detuvo y el usuario aprobó ampliar el alcance.

### Alcance ampliado (aprobado por el usuario)
- **A. Respaldo del Paso 2:** `D:\temp\PROFESIOGRAMA_ENTREGA\Profesiograma\backend\src` **no existía**, así que se aplicó la alternativa:
  - `backend/src` se copió a `D:\temp\paso2_respaldo\src` (61 archivos, incluido el `appsettings.Development.json` del Paso 2).
  - `backend/sql/dev` se copió a `D:\temp\paso2_respaldo\sql` (1 archivo: `verificacion_paso2.sql`).
  - Se comprobó con `diff -r` que la copia es idéntica (62 archivos) y **después** se eliminaron del repo.
- **B. Copia del Paso 1** desde `D:\PROYECTOS\Profesiograma` (solo lectura, sin cambios allí): se copiaron 14 archivos de `src/` (sin bin/obj) y `sql/00_diagnostico_sqlserver.sql`, y se comprobó con `cmp` que son idénticos.
  - `tests/` del original está vacía: no se copió nada. Se eliminó `backend/test/` (vacía) y se creó `backend/tests/` (vacía; git no la versiona).
  - `Directory.Build.props`, `global.json`, `README.md` y `Profesiograma.slnx` son idénticos al original.
- **C. Verificación:** compilación correcta y la clave de user-secrets existe (sección 3).
- **D. Documentación:**
  - `.gitignore`: las 5 entradas del original (`bin/`, `obj/`, `.vs/`, `*.user`, `appsettings.*.local.json`) ya estaban en el de la raíz, así que la fusión no produjo cambios. Se conserva la exclusión de `docs/origen/sharepoint/muestras/`.
  - `docs/00_ESTADO_ACTUAL.md`:
    - Rutas `claude/FASE_*` cambiadas a `docs/fases/FASE_*`.
    - Sección 4 con la nueva estructura, el original como respaldo de solo lectura y el `UserSecretsId`.
    - Sección 5 marcada "entregado como paquete; PENDIENTE de integración (TAREA-02 a TAREA-05)", sin migraciones aún.
  - `CLAUDE.md`: lista de documentos de fase, comando de compilación desde `backend/` y nota del `UserSecretsId`.

### Git
El usuario gestiona git: en esta tarea no se ejecutó `git add`, `commit` ni `push` (regla 7 agregada en `CLAUDE.md`). Solo se usaron comandos de lectura (`git status`, `git check-ignore`).

## 2. Archivos tocados

| Acción | Archivos |
|---|---|
| Retirados del repo (respaldados en `D:\temp\paso2_respaldo\`) | `backend/src/**` del Paso 2 (61), `backend/sql/dev/verificacion_paso2.sql` |
| Copiados del original | `backend/src/App.Api/{App.Api.csproj, Program.cs, appsettings.json, appsettings.Development.json, Properties/launchSettings.json, Endpoints/HealthEndpoints.cs}`, `backend/src/App.Application/{App.Application.csproj, Diagnostics/DatabaseInfo.cs, Diagnostics/IDatabaseDiagnostics.cs}`, `backend/src/App.Domain/{App.Domain.csproj, AssemblyMarker.cs}`, `backend/src/App.Infrastructure/{App.Infrastructure.csproj, DependencyInjection.cs, Diagnostics/SqlDatabaseDiagnostics.cs}`, `backend/sql/00_diagnostico_sqlserver.sql` |
| Carpetas | eliminada `backend/test/`; creada `backend/tests/` |
| Modificados | `docs/00_ESTADO_ACTUAL.md`, `CLAUDE.md` |
| Renombrado | `docs/fases/FASE_1_Analisis_funcional.md` → `FASE_1_Analisis_Funcional.md` |
| Creado | `docs/tareas/TAREA-01-reporte.md` |
| Revisados, sin modificar (ya existían) | `.gitignore`, `docs/fases/*` (4), `docs/origen/powerapps/*` (17), `docs/origen/sharepoint/esquemas/*` (4), `docs/referencia/*` (3), `frontend/README.md` |

## 3. Comandos ejecutados y resultado

| Comando (desde `backend/`) | Resultado |
|---|---|
| `dotnet build Profesiograma.slnx` (antes) | 4 errores MSB3202 (faltaban los `.csproj`) |
| `dotnet user-secrets list --project src/App.Api` (antes) | Error: no hay proyecto MSBuild |
| `diff -r` del respaldo / `cmp` de la copia del Paso 1 | Sin diferencias |
| `dotnet build Profesiograma.slnx` (después) | **Compilación correcta: 0 advertencias, 0 errores** |
| `dotnet user-secrets list --project src/App.Api` (después) | Existe la clave `ConnectionStrings:Profesiograma` (valor no mostrado) |

No se tocó la base de datos. No se copió nada del Paso 2 al proyecto.

## 4. Referencias entre proyectos y paquetes NuGet

| Proyecto | ProjectReference | NuGet |
|---|---|---|
| App.Domain | — | — |
| App.Application | App.Domain | — |
| App.Infrastructure | App.Application | Microsoft.Data.SqlClient 6.1.1; Microsoft.Extensions.Configuration.Abstractions 10.0.0; Microsoft.Extensions.DependencyInjection.Abstractions 10.0.0 |
| App.Api (Sdk.Web, UserSecretsId `profesiograma-api-4d2f7c1e`) | App.Application, App.Infrastructure | — |

## 5. Errores y cómo se resolvieron
- **Faltaban los `.csproj` y los archivos base del Paso 1, y había Paso 2 en su lugar:** el Paso 2 se respaldó y retiró, y el Paso 1 se copió desde el original.
- **No existía la ruta de respaldo `D:\temp\PROFESIOGRAMA_ENTREGA\`:** se usó `D:\temp\paso2_respaldo\`, como indicó el usuario.

## 6. Contradicciones detectadas
1. `docs/fases/` estaba vacía al iniciar la tarea. Durante la tarea aparecieron los 4 documentos (copiados fuera de esta sesión). `FASE_1_Analisis_funcional.md` se renombró a `FASE_1_Analisis_Funcional.md` (en dos pasos, pasando por un nombre temporal) para que coincida con las referencias de `00_ESTADO_ACTUAL.md` y `CLAUDE.md`.
2. `00_ESTADO_ACTUAL.md` daba el Paso 2 como entregado con migraciones, pero el paquete no trae migraciones ni `.csproj`. Ya se corrigió en el documento.
3. El `README.md` de `backend/` (idéntico al original) sigue describiendo el Paso 1 y la carpeta `tests/`. Es coherente con el estado actual.

## 7. Pendientes
- Integrar el Paso 2 desde `D:\temp\paso2_respaldo\` (TAREA-02 a TAREA-05). Hay que agregar los paquetes EF Core y Microsoft.Identity.Web, fusionar `Program.cs` y `appsettings.json` con `docs/referencia/` y generar las migraciones.
- Revisión de datos (informativa): `docs/origen/powerapps/*.pa.yaml`, `docs/fases/FASE_0_Inventario.md`, `FASE_2_modelo_profesiograma.sql` y `esquemas/ReporteProyectoSIG.json` contienen direcciones de correo corporativas `@sedemi.com` (en su mayoría buzones funcionales) y números de póliza por empresa. No se encontraron secretos. `docs/origen/sharepoint/muestras/` está excluida por `.gitignore` (verificado con `git check-ignore`).
- Sugerencia (no aplicada, requiere aprobación): agregar a `CLAUDE.md` el comando `dotnet run --project src/App.Api --launch-profile https` y la advertencia de que `D:\PROYECTOS\Profesiograma` es de solo lectura.
