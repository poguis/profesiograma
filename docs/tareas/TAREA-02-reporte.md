# TAREA-02 — Integración del código del Paso 2 y compilación

**Fecha:** 2026-09-30
**Resultado:** completada. `dotnet build Profesiograma.slnx` termina con **0 advertencias y 0 errores**.
Sin migraciones, sin `dotnet ef`, sin `dotnet run`, sin conexión a SQL Server y sin comandos git que modifiquen el repositorio.
`D:\temp\paso2_respaldo\` y `D:\PROYECTOS\Profesiograma` no se modificaron.

## 1. Qué se hizo

### Fase A — Análisis (presentado y aprobado)
- **Conflictos:** 60 archivos nuevos sin conflicto. Solo `App.Api/appsettings.Development.json` existía en ambos lados.
- **Namespaces:** sin diferencias. `RootNamespace` es implícito (igual al nombre del proyecto) y todos los namespaces del Paso 2 cuelgan de su proyecto.
- **Paquetes:** EF Core SqlServer 10.0.12 exige `Microsoft.Data.SqlClient >= 6.1.6` y `Microsoft.Extensions.* >= 10.0.12`. Con 6.1.1 y 10.0.0 habría aparecido NU1605, por eso se subieron.

### Decisiones del usuario
| Id | Decisión |
|---|---|
| D1 = a | `/` y `/api/health/db` con `.AllowAnonymous()`. El middleware omite el aprovisionamiento solo si el endpoint es anónimo **y** el usuario no está autenticado. |
| D2 = sí | Agregar solo `"Microsoft.EntityFrameworkCore.Database.Command": "Warning"` al Logging del Paso 1 en Development. |
| D3 | Subir SqlClient a 6.1.6 y Extensions.*.Abstractions a 10.0.12, manteniendo las referencias explícitas. |
| Identity.Web | 4.15.0 aprobado (su numeración no sigue 10.0.x). |
| E1 | El sembrador se omite si no hay migraciones en el ensamblado. |
| E2 | Comentario de `ProfesiogramaDbContext.cs` apunta ahora a `docs/fases/`. |

### Fase B — Ejecución
1. Se copiaron 60 archivos del Paso 2 a `backend/src` (verificados con `cmp`: idénticos al respaldo) y `verificacion_paso2.sql` a `backend/sql/dev/`.
2. Se fusionaron `appsettings.json` y `appsettings.Development.json`.
3. Se integró el registro de servicios en `DependencyInjection.cs` y `Program.cs`.
4. Se aplicaron D1, E1 y E2.
5. Se actualizaron los `.csproj`.
6. Se ejecutaron `dotnet restore` y `dotnet build`.

## 2. Archivos tocados

| Acción | Archivo |
|---|---|
| Copiados del Paso 2 (60) | `App.Api`: `Configuracion/DatosPruebaExtensions.cs`, `Endpoints/{CatalogoEndpoints,UsuarioEndpoints}.cs`, `Profesiograma.Dev.http`, `Seguridad/{AutenticacionExtensions,ClaimsPrincipalExtensions,UsuarioActual,UsuarioActualMiddleware}.cs`, `Seguridad/DevAuth/{DevAuthHandler,DevAuthOptions}.cs` · `App.Application`: `Catalogos/*` (2), `Seguridad/*` (3) · `App.Domain`: 17 entidades · `App.Infrastructure`: `Consultas/CatalogoConsultas.cs`, `Persistencia/**` (25), `Seguridad/UsuarioProvisionamiento.cs` |
| Copiado | `backend/sql/dev/verificacion_paso2.sql` |
| Fusionados | `App.Api/appsettings.json`, `App.Api/appsettings.Development.json` |
| Modificados (Paso 1) | `App.Api/Program.cs`, `App.Api/Endpoints/HealthEndpoints.cs`, `App.Infrastructure/DependencyInjection.cs`, `App.Api/App.Api.csproj`, `App.Infrastructure/App.Infrastructure.csproj` |
| Modificados (Paso 2, tras copiar) | `App.Api/Seguridad/UsuarioActualMiddleware.cs` (D1), `App.Infrastructure/Persistencia/DatosPrueba/DatosPruebaSembrador.cs` (E1), `App.Infrastructure/Persistencia/ProfesiogramaDbContext.cs` (E2) |
| Documentación | `docs/00_ESTADO_ACTUAL.md` (sección 5), `docs/tareas/TAREA-02-reporte.md` |

## 3. Cambios de código (detalle)

### DependencyInjection.cs (propuesta A)
```csharp
using App.Infrastructure.Persistencia;
...
services.AddSingleton<IDatabaseDiagnostics>(_ => new SqlDatabaseDiagnostics(connectionString));
services.AddPersistencia(connectionString);   // [PASO 2] EF Core + auditoría + consultas + sembrador
```

### Program.cs (propuesta B)
Se agregaron:
- `builder.AddSeguridadProfesiograma()` y `app.UseSeguridadProfesiograma()`, este último después de `UseExceptionHandler`.
- `.AllowAnonymous()` en `/`.
- `MapUsuarioEndpoints()` y `MapCatalogoEndpoints()`.
- `await app.SembrarDatosPruebaAsync()`.

Se conservan `AddInfrastructure`, `AddProblemDetails`, `UseExceptionHandler` y `MapHealthEndpoints()`. No se agregaron `AddOpenApi` ni `UseHttpsRedirection`, porque el Paso 1 no los usa.

### HealthEndpoints.cs (D1)
```csharp
        })
        .AllowAnonymous(); // Diagnóstico: no debe depender de dbo.Usuario (ver UsuarioActualMiddleware).
```

### UsuarioActualMiddleware.cs (D1): código exacto del cambio
```csharp
using Microsoft.AspNetCore.Authorization;   // agregado
...
        var principal = contexto.User;
        var autenticado = principal.Identity?.IsAuthenticated == true;

        // Endpoint anónimo (p. ej. /api/health/db) sin usuario autenticado: no se consulta dbo.Usuario.
        // Si el usuario sí está autenticado, se aprovisiona normalmente.
        if (!autenticado && contexto.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
        {
            await next(contexto);
            return;
        }

        if (autenticado)          // antes: if (principal.Identity?.IsAuthenticated == true)
        {
            ... (sin cambios)
```

> ⚠️ **Hallazgo sobre D1 (para decisión del usuario).** Con la condición aprobada, la guarda del middleware **no cambia el comportamiento**: el código original ya solo aprovisionaba cuando `IsAuthenticated == true`. La guarda documenta la intención, pero es redundante. El efecto real de D1 viene de `.AllowAnonymous()`:
> - **Entra ID, petición sin token a `/api/health/db`:** pasa la FallbackPolicy y no se consulta `dbo.Usuario`. ✅ Se resolvió.
> - **DevAuth, petición sin encabezado:** `DevAuthHandler` autentica como `UsuarioPorDefecto = admin`, así que la petición llega **autenticada** y el middleware **sí aprovisiona**, consultando la base. Si SQL Server no responde o `dbo.Usuario` no existe (antes de la TAREA-03), `/api/health/db` devuelve 500 en lugar del 503 de diagnóstico. Para diagnosticar en DevAuth sin tocar `dbo.Usuario` hay que enviar `X-Dev-User: anonimo`. La prueba 1 de `Profesiograma.Dev.http` (sin encabezado) seguirá dependiendo de `dbo.Usuario`.
> - Opciones a futuro, sin aplicar: (i) omitir el aprovisionamiento en endpoints anónimos aunque el usuario esté autenticado; (ii) agregar `X-Dev-User: anonimo` a la prueba 1 del `.http`. [PENDIENTE DE CONFIRMAR]

### DatosPruebaSembrador.cs (E1)
```csharp
    public async Task SembrarAsync(IReadOnlyCollection<UsuarioPrueba> usuariosPrueba, CancellationToken ct = default)
    {
        if (!db.Database.GetMigrations().Any())
        {
            logger.LogWarning("Datos de prueba omitidos: no hay migraciones en el ensamblado. Ejecute la TAREA-03.");
            return;
        }
        // ... (sigue la revisión de migraciones pendientes original)
```
`GetMigrations()` lee las migraciones compiladas en el ensamblado y no se conecta a la base.

### ProfesiogramaDbContext.cs (E2)
`// igual que en claude/FASE_2_modelo_profesiograma.sql.` → `// igual que en docs/fases/FASE_2_modelo_profesiograma.sql.`

### appsettings
- `appsettings.json`: Paso 1 + `Autenticacion` (`EntraId`), `AzureAd` (valores `[PENDIENTE: ...]`) y `DatosPrueba.SembrarAlIniciar = false`.
- `appsettings.Development.json`: Logging del Paso 1 (`Default: Debug`, `Microsoft.AspNetCore: Information`) + `Microsoft.EntityFrameworkCore.Database.Command: Warning` (D2) + `Autenticacion` (`DevAuth`) + `DevAuth` (2 usuarios ficticios `@profesiograma.local`) + `DatosPrueba.SembrarAlIniciar = true`.

### Paquetes NuGet
| Proyecto | Paquete | Antes | Después |
|---|---|---|---|
| App.Infrastructure | Microsoft.Data.SqlClient | 6.1.1 | **6.1.6** |
| App.Infrastructure | Microsoft.Extensions.Configuration.Abstractions | 10.0.0 | **10.0.12** |
| App.Infrastructure | Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.0 | **10.0.12** |
| App.Infrastructure | Microsoft.EntityFrameworkCore.SqlServer | — | **10.0.12** |
| App.Api | Microsoft.EntityFrameworkCore.Design (`PrivateAssets=all`) | — | **10.0.12** |
| App.Api | Microsoft.Identity.Web | — | **4.15.0** |

Referencias entre proyectos: sin cambios.

## 4. Comandos ejecutados y resultado

| Comando (desde `backend/`) | Resultado |
|---|---|
| Consulta a `api.nuget.org` (índice de versiones y `.nuspec`) | EF 10.0.12 y Identity.Web 4.15.0 son las últimas estables. EF 10.0.12 depende de SqlClient 6.1.6 |
| `dotnet restore Profesiograma.slnx` | Correcto. **Sin NU1605 ni NU1608** |
| `dotnet build Profesiograma.slnx --no-restore` | **Compilación correcta: 0 advertencias, 0 errores** |
| `dotnet list Profesiograma.slnx package --include-transitive` | SqlClient resuelto en 6.1.6, EF Core en 10.0.12 |
| `dotnet list Profesiograma.slnx package --vulnerable --include-transitive` | Ningún proyecto tiene paquetes vulnerables |

## 5. Errores de compilación y correcciones
Ninguno. El código del Paso 2 compiló sin correcciones. Los únicos cambios al Paso 2 son los aprobados (D1, E1, E2).
Advertencias del build: 0. No se usó `NoWarn`.

## 6. Pendientes
- **TAREA-03:** generar las migraciones `Inicial` y `Vistas` y aplicarlas a `PROFESIOGRAMA_DEV`.
- **Hallazgo D1 en DevAuth** (sección 3): decidir si se amplía la guarda del middleware o se ajusta el `.http`. [PENDIENTE DE CONFIRMAR]
- **Antes de la TAREA-03**, si se ejecuta la API con DevAuth: el sembrador ya no falla (E1), pero cualquier petición autenticada consultará `dbo.Usuario`, que aún no existe, y fallará.
- **`AzureAd`:** valores `[PENDIENTE]` hasta la Fase 7 (App Registration).
