# TAREA-04 — Aplicar las migraciones a PROFESIOGRAMA_DEV

**Fecha:** 2026-09-30
**Resultado:** completada. Las migraciones `20260930161754_Inicial` y `20260930161822_Vistas` se aplicaron en `NIQUEL\SSDEV` / `PROFESIOGRAMA_DEV` sin errores.
Solo se usaron los comandos `dotnet ef` indicados, contra `PROFESIOGRAMA_DEV`.
No se ejecutaron `dotnet run`, `database drop`, `database update 0` ni SQL manual (sqlcmd / Invoke-Sqlcmd), no se tocaron otras bases y no se usaron comandos git que modifiquen el repositorio.

## 1. Qué se hizo

Todos los comandos se ejecutaron desde `backend/` con `--project src/App.Infrastructure --startup-project src/App.Api --context ProfesiogramaDbContext`, en entorno Development por defecto de la herramienta.

| Paso | Comando | Resultado |
|---|---|---|
| 1. Verificar destino | `dotnet ef dbcontext info` | `Data source: NIQUEL\SSDEV` · `Database name: PROFESIOGRAMA_DEV` ✅ (se mostraron solo esos dos valores) |
| 2. Estado previo | `dotnet ef migrations list` | `20260930161754_Inicial (Pending)` · `20260930161822_Vistas (Pending)` ✅ sin errores de conexión |
| 3. Aplicar | `dotnet ef database update` | Exit 0 (ver salida abajo) ✅ |
| 4. Estado posterior | `dotnet ef migrations list` | `20260930161754_Inicial` · `20260930161822_Vistas`, **sin "(Pending)"** ✅ |

Salida relevante de `database update` (sin líneas `dbug` ni el texto de los comandos SQL):
```
Build started...
Build succeeded.
info: Microsoft.EntityFrameworkCore.Migrations[20411]
Acquiring an exclusive lock for migration application. See https://aka.ms/efcore-docs-migrations-lock for more information if this takes too long.
info: Microsoft.EntityFrameworkCore.Migrations[20402]
Applying migration '20260930161754_Inicial'.
info: Microsoft.EntityFrameworkCore.Migrations[20402]
Applying migration '20260930161822_Vistas'.
Done.
```
La salida no contuvo ninguna línea `fail:`, `error` ni `exception`. El log temporal (`%TEMP%\ef_update.log`) se eliminó al terminar. La cadena de conexión no se mostró ni se escribió en ningún archivo.

## 2. Archivos tocados

| Acción | Archivo |
|---|---|
| Modificado | `docs/00_ESTADO_ACTUAL.md` (sección 5) |
| Creado | `docs/tareas/TAREA-04-reporte.md` |

No se modificó código. La base `PROFESIOGRAMA_DEV` recibió las 22 tablas, las semillas, las 2 vistas y `dbo.__EFMigrationsHistory`, tal como se revisaron en la TAREA-03.

## 3. Errores
Ninguno.

## 4. Pendientes (TAREA-05)
- **Verificación en SSMS:** ejecutar `backend/sql/dev/verificacion_paso2.sql` contra `PROFESIOGRAMA_DEV`. Esperado: 22 tablas, 2 vistas, 21 CHECK / 72 FK / 47 DEFAULT y los conteos de semillas (53 filas en 12 tablas).
- **Primera ejecución de la API** (`dotnet run`), que todavía no se ha hecho. En Development está `DatosPrueba:SembrarAlIniciar = true`, así que al arrancar se crearán los datos de prueba ficticios (usuarios DevAuth, compañía 9001, empleados DEV001–DEV008, proyectos PRY-DEV-*, novedades NOV-DEV-*).
- **Pruebas de `Profesiograma.Dev.http`:** health anónimo, `/api/usuarios/me` como admin y gestor, catálogos, parámetros (403 para gestor) y las respuestas 401.
