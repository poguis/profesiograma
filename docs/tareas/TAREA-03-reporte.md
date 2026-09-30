# TAREA-03 — Corrección D1 + migraciones Inicial y Vistas + script SQL para revisión

**Fecha:** 2026-09-30
**Resultado:** completada. Las migraciones `Inicial` y `Vistas` están generadas y el script idempotente `backend/sql/ef/001_inicial_vistas.sql` está revisado. **No se aplicó a la base** (TAREA-04).
No se ejecutaron `dotnet ef database update`, `migrations list`, `dotnet run` ni comandos git que modifiquen el repositorio. No hubo conexión a SQL Server.

## 1. Parte 1 — Corrección D1

Opción 1 aprobada: si el endpoint tiene `IAllowAnonymous`, el middleware **nunca** aprovisiona, esté o no autenticado el usuario. Se eliminaron la variable `autenticado` y la guarda redundante de la TAREA-02.

Archivo: `backend/src/App.Api/Seguridad/UsuarioActualMiddleware.cs`. Código final de `InvokeAsync`:
```csharp
public async Task InvokeAsync(HttpContext contexto, UsuarioActual usuarioActual, IUsuarioProvisionamiento provisionamiento)
{
    // Endpoints anónimos no consultan dbo.Usuario; no deben escribir datos (auditoría usaría SISTEMA).
    if (contexto.GetEndpoint()?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
    {
        await next(contexto);
        return;
    }

    var principal = contexto.User;
    if (principal.Identity?.IsAuthenticated == true)
    {
        var email = principal.ObtenerEmail();
        if (string.IsNullOrWhiteSpace(email))
        {
            await TypedResults.Problem(
                title: "Token sin correo",
                detail: "El token no contiene preferred_username/email.",
                statusCode: StatusCodes.Status403Forbidden).ExecuteAsync(contexto);
            return;
        }

        var nombre = principal.ObtenerNombre();
        var resuelto = await provisionamiento.ResolverAsync(principal.ObtenerObjectId(), email, nombre ?? email, contexto.RequestAborted);
        if (!resuelto.Habilitado)
        {
            await TypedResults.Problem(
                title: "Usuario no habilitado",
                detail: resuelto.Motivo,
                statusCode: StatusCodes.Status403Forbidden).ExecuteAsync(contexto);
            return;
        }

        usuarioActual.Establecer(resuelto.UsuarioId, email.Trim().ToLowerInvariant(), nombre, principal.ObtenerRoles());
    }

    await next(contexto);
}
```
Efecto: en DevAuth, `/api/health/db` sin encabezado (usuario por defecto `admin`) ya no consulta `dbo.Usuario`. Resuelve el hallazgo D1 de la TAREA-02.
`dotnet build`: 0 advertencias, 0 errores.

## 2. Parte 2 — Herramienta EF local

| Paso | Resultado |
|---|---|
| `dotnet ef --version` (antes) | Falla: dotnet-ef **no estaba instalado** globalmente |
| `dotnet new tool-manifest` (desde `backend/`) | **Código de salida 73**, pero el manifiesto **sí se creó**: el SDK de .NET 10 lo crea en la carpeta actual (`backend/dotnet-tools.json`), no en `.config/` |
| `dotnet tool install dotnet-ef --version 10.0.12` | Correcto |
| Ajuste | El manifiesto se movió a la ruta aprobada **`backend/.config/dotnet-tools.json`**, y `dotnet tool list` lo encuentra |
| `dotnet ef --version` (después) | **10.0.12** |

`CLAUDE.md`, sección "Comandos EF": "La primera vez en un equipo: `dotnet tool restore` (desde backend/)."

## 3. Incidente: Smart App Control

| Aspecto | Detalle |
|---|---|
| Síntoma | El primer `dotnet ef migrations has-pending-model-changes` compiló, pero no pudo construir el `DbContext` |
| Error | `Could not load file or assembly '...\backend\src\App.Api\bin\Debug\net10.0\App.Application.dll'. Una directiva de Control de aplicaciones bloqueó este archivo. (0x800711C7)`, seguido de `Unable to resolve service for type 'DbContextOptions<ProfesiogramaDbContext>'` |
| Comandos afectados | `has-pending-model-changes` normal y con `--no-build` (ambos exit 1) |
| Evidencia | Registro `HKLM\SYSTEM\CurrentControlSet\Control\CI\Policy\VerifiedAndReputablePolicyState = 1` (Smart App Control **aplicado**). Registro `Microsoft-Windows-CodeIntegrity/Operational`, 2026-09-30 10:03:07: eventos **3033** y **3077**, "dotnet.exe attempted to load …\App.Application.dll that did not [meet the signing level requirements]" |
| Causa | Smart App Control impedía que `dotnet.exe` cargara las DLL sin firma del proyecto. `dotnet build` funcionaba porque solo genera las DLL, pero `dotnet ef`, `dotnet run` y la depuración necesitan cargarlas |
| Opciones evaluadas | A) TI desactiva SAC o define una política App Control for Business; B) WSL2 con el SDK .NET 10; C) aplicar el script desde SSMS; D) otro equipo o VM sin SAC |
| Resolución | **El usuario desactivó Smart App Control** (opción A). Se verificó `VerifiedAndReputablePolicyState = 0` (solo lectura) y se retomó desde el paso 4. Claude no modificó ninguna configuración de seguridad |
| Nota | La desactivación de SAC es permanente: no se puede reactivar sin reinstalar Windows. Queda a criterio del usuario/TI definir una política de App Control for Business para equipos de desarrollo |

## 4. Parte 3 — Migraciones

Todos los comandos se ejecutaron desde `backend/` con `--project src/App.Infrastructure --startup-project src/App.Api --context ProfesiogramaDbContext`, en la **forma sin `-- --environment Development`**. La herramienta tomó el entorno Development por defecto: se leyeron los user-secrets y los logs `dbug` confirmaron la configuración de Development. No fue necesario usar la forma alternativa.

| Paso | Comando | Resultado |
|---|---|---|
| 4 | `migrations has-pending-model-changes` | "Changes have been made to the model since the last migration" (exit 1). Esperado: aún no había migraciones |
| 5 | `migrations add Inicial ... --output-dir Persistencia/Migraciones` | `20260930161754_Inicial.cs`, `.Designer.cs` y `ProfesiogramaDbContextModelSnapshot.cs`. Sin advertencias de EF |
| 6 | `migrations add Vistas ...` | `20260930161822_Vistas.cs`, generada vacía. Up/Down reemplazados (ver 4.1), con el namespace y la clase que generó EF, más `using App.Infrastructure.Persistencia.Vistas;` |
| 7 | `migrations has-pending-model-changes` | **"No changes have been made to the model since the last migration."** (exit 0) |
| 8 | `dotnet build Profesiograma.slnx` | **0 advertencias, 0 errores** |
| 9 | `migrations script --idempotent ... -o sql/ef/001_inicial_vistas.sql` | Generado (1.374 líneas). Regenerado tras la corrección 4.1 |

### 4.1 Corrección aprobada: vistas envueltas en `EXEC` (diferencia clase (c))
**Problema detectado en la revisión:** en modo `--idempotent`, EF envuelve cada operación en `IF NOT EXISTS (...) BEGIN … END`. El SQL de `migrationBuilder.Sql(...)` quedaba como `CREATE OR ALTER VIEW` dentro de `BEGIN…END`, y SQL Server exige que `CREATE VIEW` sea la primera instrucción del lote. Aplicado desde SSMS, el lote de la migración `Vistas` habría fallado. `dotnet ef database update` no se ve afectado, porque ejecuta cada instrucción por separado.

**Cambio aplicado** (solo en `Up`; `Down` y `VistasSql` sin cambios):
```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    migrationBuilder.Sql($"EXEC(N'{VistasSql.VwProyectoResumen_V1.Replace("'", "''")}');");
    migrationBuilder.Sql($"EXEC(N'{VistasSql.VwNovedadDiaVigente_V1.Replace("'", "''")}');");
}
```
Verificación posterior:
- `has-pending-model-changes`: sin cambios (exit 0).
- `dotnet build`: 0 advertencias, 0 errores.
- Script regenerado: las dos vistas están en `EXEC(N'CREATE OR ALTER VIEW …')` (líneas 1283 y 1341).
- **Ningún `CREATE [OR ALTER] VIEW` queda fuera de `EXEC`**.
- Las comillas quedaron duplicadas correctamente (`N'' | ''`, `''-05:00''`).
- El cuerpo de ambas vistas, una vez desenvuelto, es idéntico al de la Fase 2.

## 5. Parte 4 — Revisión del script frente a `docs/fases/FASE_2_modelo_profesiograma.sql`

Método: análisis automatizado de ambos scripts (sin comentarios, en solo lectura), con estos pasos:
- extraer tablas, columnas, tipos, constraints, índices, vistas y semillas;
- normalizar el formato (`[ ]`, `CAST(1 AS bit)` ≡ `(1)`, `EXEC(N'…')`);
- comparar definición por definición.

Resultados sobre el script **regenerado**.

### 5.1 Tablas (paso 10)
| Tabla | Existe | Columnas (F2/EF) | Tipos clave (iguales en F2 y EF) |
|---|---|---|---|
| Usuario | sí | 11 / 11 | DATETIME2(0)×2 |
| Empleado | sí | 27 / 27 | DATETIME2(0)×3 |
| Departamento | sí | 11 / 11 | DATETIME2(0)×2 |
| UsuarioDepartamento | sí | 10 / 10 | DATETIME2(0)×3 |
| Parametro | sí | 9 / 9 | DATETIME2(0)×2 |
| EstadoProyecto | sí | 10 / 10 | TINYINT×2, DATETIME2(0)×2 |
| TipoMovimiento | sí | 9 / 9 | TINYINT×2, DATETIME2(0)×2 |
| GrupoProyecto | sí | 11 / 11 | TINYINT×2, DATETIME2(0)×2 |
| Jornada | sí | 11 / 11 | TINYINT×4, DATETIME2(0)×2 |
| RolAsignacion | sí | 10 / 10 | TINYINT×2, DATETIME2(0)×2 |
| TipoAplicacionNovedad | sí | 9 / 9 | TINYINT×2, DATETIME2(0)×2 |
| OrigenNovedad | sí | 10 / 10 | TINYINT×2, DATETIME2(0)×2 |
| TipoNovedad | sí | 20 / 20 | TINYINT×2, DATETIME2(0)×2 |
| CargoInfor | sí | 8 / 8 | DATETIME2(0)×2 |
| Compania | sí | 8 / 8 | DATETIME2(0)×2 |
| Proyecto | sí | 32 / 32 | TINYINT×2, DATE×2, TIME(0)×4, ROWVERSION×1, DATETIME2(0)×2 |
| ProyectoPersonal | sí | 20 / 20 | TINYINT×4, DATE×2, DATETIME2(0)×2 |
| ProyectoAsignacionDia | sí | 13 / 13 | DATE×1, TINYINT×1, DATETIME2(0)×2 |
| ProyectoEtapa | sí | 16 / 16 | TINYINT×2, DATE×3, NVARCHAR(MAX)×1, DATETIME2(0)×2 |
| ProyectoActividad | sí | 15 / 15 | TINYINT×1, DATE×2, DATETIME2(0)×2 |
| Novedad | sí | 25 / 25 | TINYINT×3, DATE×2, NVARCHAR(MAX)×1, DATETIME2(0)×3, ROWVERSION×1 |
| NovedadDia | sí | 7 / 7 | DATE×1, DATETIME2(0)×2 |

La comparación columna por columna (nombre, tipo, IDENTITY, nulabilidad) dio **0 diferencias** en las 22 tablas.

### 5.2 Conteos (paso 11)
| Elemento | Fase 2 | EF | Comentario |
|---|---|---|---|
| Tablas del modelo | 22 | 22 | EF agrega `__EFMigrationsHistory` |
| CHECK (esperado 21) | 21 | **21** | |
| FOREIGN KEY (esperado 72) | 72 | **72** | Acción de borrado igual. La única con CASCADE, en ambos, es `FK_NovedadDia_Novedad` |
| DEFAULT con nombre `DF_` (esperado 47) | 47 | **47** | Ningún DEFAULT sin nombre |
| PRIMARY KEY | 22 | 23 | + `PK___EFMigrationsHistory` |
| Únicos `UQ_` | 26 constraints | 25 índices únicos + 1 constraint (`UQ_ProyectoPersonal_Clave`) | Mismos nombres y columnas |
| Índices únicos filtrados `UX_` | 7 | 7 | Mismas columnas y filtros |
| Índices `IX_` | 16 | 16 | Mismas columnas, INCLUDE y filtros |
| Vistas | 2 | 2 | Cuerpos idénticos |

Comparación detallada de **211 definiciones** (22 PK + 47 DF + 21 CK + 72 FK + 26 UQ + 16 IX + 7 UX): expresiones CHECK, valores DEFAULT, columnas y destino de FK, columnas, INCLUDE y filtros de índices. Resultado: **0 diferencias reales** después de normalizar el formato.
**Constraints o índices con nombre autogenerado (sin prefijo estándar):** ninguno. Solo `PK___EFMigrationsHistory`, que es propia de EF.

### 5.3 Semillas (paso 12)
| Tabla | Esperado | EF | Valores iguales a la Fase 2 |
|---|---|---|---|
| Usuario | 1 | 1 | sí |
| EstadoProyecto | 4 | 4 | sí |
| TipoMovimiento | 7 | 7 | sí |
| GrupoProyecto | 3 | 3 | sí |
| Jornada | 4 | 4 | sí |
| RolAsignacion | 3 | 3 | sí |
| TipoAplicacionNovedad | 3 | 3 | sí |
| OrigenNovedad | 2 | 2 | sí |
| TipoNovedad | 7 | 7 | sí |
| CargoInfor | 3 | 3 | sí |
| Departamento | 7 | 7 | sí |
| Parametro | 9 | 9 | sí |
| **Total** | **53** | **53** | Id incluidos |

### 5.4 Seguridad del script (paso 13)
0 coincidencias de `DROP DATABASE`, `CREATE DATABASE`, `ALTER DATABASE`, `USE`, nombres de tres partes (`base.dbo.objeto`), `OPENROWSET/OPENQUERY/OPENDATASOURCE` y `DROP TABLE/VIEW/INDEX`. El script solo crea objetos en `dbo` de la base actual y registra las migraciones en `dbo.__EFMigrationsHistory`.

### 5.5 Clasificación de diferencias (paso 14)
| # | Diferencia | Clase | Estado |
|---|---|---|---|
| 1 | 25 `UQ_*` como índices únicos con el mismo nombre (no constraints), salvo `UQ_ProyectoPersonal_Clave` | (a) esperada | — |
| 2 | Tabla adicional `__EFMigrationsHistory` (+ su PK) | (a) esperada | — |
| 3 | `FechaCreacion` fija `2026-09-29T00:00:00Z` en semillas | (a) esperada | — |
| 4 | Vistas creadas por migración SQL | (a) esperada | — |
| 5 | Formato EF: `CAST(1 AS bit)` en lugar de `(1)`; `Activo = 1` explícito en las semillas, igual al DEFAULT; Id explícitos con `IDENTITY_INSERT` en CargoInfor, Departamento y Parametro, con los mismos valores 1..n | (b) nueva sin impacto | — |
| 6 | EF envuelve los índices filtrados y los INSERT en `EXEC(N'…')` | (b) nueva sin impacto | — |
| 7 | `CREATE OR ALTER VIEW` dentro de `IF … BEGIN…END` en el script idempotente | (c) requería corrección | **Corregida** (4.1, aprobada por el usuario) |

## 6. Archivos tocados

| Acción | Archivo |
|---|---|
| Modificado | `backend/src/App.Api/Seguridad/UsuarioActualMiddleware.cs` |
| Creado | `backend/.config/dotnet-tools.json` |
| Creados (EF) | `backend/src/App.Infrastructure/Persistencia/Migraciones/20260930161754_Inicial.cs`, `20260930161754_Inicial.Designer.cs`, `20260930161822_Vistas.Designer.cs`, `ProfesiogramaDbContextModelSnapshot.cs` |
| Creado y editado | `backend/src/App.Infrastructure/Persistencia/Migraciones/20260930161822_Vistas.cs` (Up con `EXEC`, Down con `DROP VIEW IF EXISTS`) |
| Creado | `backend/sql/ef/001_inicial_vistas.sql` |
| Modificado | `CLAUDE.md` (nota `dotnet tool restore`) |
| Modificado | `docs/00_ESTADO_ACTUAL.md` (sección 5; pendiente 10 actualizado) |
| Creado | `docs/tareas/TAREA-03-reporte.md` |

## 7. Pendientes
- **TAREA-04:** aplicar las migraciones a `PROFESIOGRAMA_DEV` (`dotnet ef database update` o el script idempotente desde SSMS) y ejecutar `backend/sql/dev/verificacion_paso2.sql`.
- **Revisar antes de aplicar:** el sembrador (`DatosPrueba:SembrarAlIniciar = true` en Development) creará datos de prueba en el primer `dotnet run` después de migrar.
- **Seguridad del equipo:** con Smart App Control desactivado, conviene que TI evalúe una política App Control for Business para desarrollo. [PENDIENTE DE DECISIÓN DEL USUARIO/TI]
