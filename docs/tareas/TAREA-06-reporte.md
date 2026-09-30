# TAREA-06 — Reducir el ruido de logs en Development

**Fecha:** 2026-09-30
**Resultado:** completada y aprobada por el usuario, con una observación sobre la compilación en Debug (sección 3). Solo cambió configuración y documentación; no hay cambios de lógica.
No se ejecutaron `dotnet run`, `dotnet ef`, SQL ni comandos git que modifiquen el repositorio.

## 1. Qué se hizo
En `backend/src/App.Api/appsettings.Development.json`, sección `Logging:LogLevel`:

| Categoría | Antes | Después |
|---|---|---|
| `Default` | Debug | **Information** |
| `Microsoft.AspNetCore` | Information | **Warning** |
| `Microsoft.Hosting.Lifetime` | — | **Information** |
| `Microsoft.EntityFrameworkCore` | — | **Warning** |
| `Microsoft.EntityFrameworkCore.Database.Command` | Warning | Warning |
| `App` | — | **Debug** |
| `App.Api.Seguridad.DevAuth` | — | **Warning** (agregado tras la aprobación, ver sección 6) |

El resto del archivo (`Autenticacion`, `DevAuth`, `DatosPrueba`) no cambió. Se validó que el JSON es correcto y conserva sus 4 secciones.

## 2. Archivos tocados
| Acción | Archivo |
|---|---|
| Modificado | `backend/src/App.Api/appsettings.Development.json` |
| Modificado | `CLAUDE.md` (sección "Compilar (desde backend/)", ver sección 6) |
| Creado | `docs/tareas/TAREA-06-reporte.md` |

## 3. Comandos ejecutados y resultado
| Comando (desde `backend/`) | Resultado |
|---|---|
| `dotnet build Profesiograma.slnx` (Debug) | **Falló: 6 errores, 30 advertencias**, todos de **copia de archivos**, no de compilación: MSB3021/MSB3027 "No se puede copiar …\App.Application.dll / App.Domain.dll / App.Infrastructure.dll en bin\Debug\net10.0 … The process cannot access the file" y MSB3026 (reintentos). Causa: la API en ejecución (arrancada por el usuario) bloquea las DLL de `App.Api\bin\Debug\net10.0`. No se detuvo la API porque está prohibido |
| `dotnet build Profesiograma.slnx -c Release` | **0 advertencias, 0 errores.** Confirma que la solución compila. Release escribe en `bin\Release`, así que no choca con los archivos bloqueados |

Al reiniciar la API, `dotnet run` recompila en Debug. Con la API detenida, las DLL dejan de estar bloqueadas y la copia debería funcionar normalmente.

## 4. Salida esperada en consola al reiniciar (a confirmar por el usuario)
Estas categorías seguirán visibles al arrancar:
- **Aviso de DevAuth** (`Warning`, categoría `App.Api`): "ATENCIÓN: autenticación SIMULADA (DevAuth) activa…"
- **Sembrador** (`Information`, categoría `App.Infrastructure.Persistencia.DatosPrueba.DatosPruebaSembrador`): como los datos ya existen, se espera "Datos de prueba ya existentes (proyectos PRY-DEV-*). No se vuelven a crear."
- **`Microsoft.Hosting.Lifetime`** (`Information`): además de "Now listening on" (una línea por URL: https 7180 y http 5180), esta categoría escribe "Application started. Press Ctrl+C to shut down.", "Hosting environment: Development" y "Content root path: …". Son mensajes estándar de .NET y no se pueden separar de "Now listening on" sin bajar el nivel de toda la categoría.

Durante las peticiones:
- Los mensajes `dbug`/`info` de autenticación de `App.Api.Seguridad.DevAuth.DevAuthHandler` (p. ej. "AuthenticationScheme: DevAuth was successfully authenticated" o los challenge/forbid en 401/403) **quedan silenciados** con `"App.Api.Seguridad.DevAuth": "Warning"` (sección 6).
- El aviso de DevAuth al arrancar **sigue visible**: lo escribe `app.Logger`, cuya categoría es `App.Api`, no `App.Api.Seguridad.DevAuth`.
- Puede aparecer "Usuario provisionado: …" (`UsuarioProvisionamiento`, Information) cuando un usuario entra por primera vez.

## 5. Pendientes
- **Usuario:** reiniciar la API y confirmar la salida de consola descrita en la sección 4.

## 6. Cambios adicionales aprobados
1. **`appsettings.Development.json`:** se agregó `"App.Api.Seguridad.DevAuth": "Warning"` en `Logging:LogLevel`. El JSON se validó y conserva sus 4 secciones (`Logging`, `Autenticacion`, `DevAuth`, `DatosPrueba`).
2. **`CLAUDE.md`, sección "Compilar (desde backend/)":** se agregó:
   ```
   - La API suele estar corriendo (la ejecuta el usuario) y bloquea bin\Debug.
     Para verificar compilación usa: dotnet build Profesiograma.slnx -c Release
     Para comandos EF agrega: --configuration Release
     Nunca detengas la API; si se requiere Debug, pide al usuario que la detenga.
   ```

| Comando (desde `backend/`) | Resultado |
|---|---|
| `dotnet build Profesiograma.slnx -c Release` | **0 advertencias, 0 errores** |
