# TAREA-25-1 — Usuarios y departamentos (backend): `usuarios.cmd`

> **Antes de ejecutarlo, REINICIE la API**: la TAREA-25-1 cambió `appsettings.Development.json` (usuario DevAuth `gestor2` y el dominio de pruebas `profesiograma.local` en `Usuarios:DominiosPermitidos`). Sin reiniciar, `gestor2` no existe y el alta del usuario de prueba da 400 por dominio.
>
> **Ejecútelo una sola vez, desde una sola consola, y espere el mensaje Fin.** Si termina con un error rojo o con «EJECUCIÓN INCOMPLETA», **no lo repita**: pegue la salida completa en el chat.

`usuarios.cmd` lo ejecuta **usted**; Claude Code no lo ejecuta. Muestra al inicio **MODO REAL** o **MODO SIMULACION** y cada caso imprime `CONTROL <caso> OK|FALLA (esperado X, obtenido Y)` con la hora local en milisegundos. Usa `..\tarea26a\comun.ps1` y la función `Caso` de `..\tarea23-2\casos.ps1`, con el patrón corregido de `..\tarea62\criterio.ps1`: candado, archivo de estado, `try/catch` general, restauración protegida, banner y cuerpos solo con tipos planos (`[string]`, `[bool]`, `[int[]]`, hashtables).

## Requisitos
- La API en ejecución (`https://localhost:7180`, Development, DevAuth `admin`, `gestor` y `gestor2`), compilada con la TAREA-25-1 y **reiniciada**.
- El modo de empleados (Simulado o Http) no importa: no se usan empleados.

## Qué escribe en la base de desarrollo
- **Usuario de prueba FIJO** (correo ficticio del dominio de pruebas, nombre «PRUEBA TAREA25»): la primera corrida lo crea; las siguientes lo **reutilizan** (c01 recibe 409 con su `usuarioId`). Al terminar queda **inactivo y sin departamentos**. Sus filas (`Usuario` y `UsuarioDepartamento`) **no se borran**: la API no borra usuarios ni asignaciones (baja lógica) y este script no ejecuta SQL.
- **gestor2**: se da de baja y se reactiva. Al terminar queda **activo**, también tras una falla (restauración en el `finally`).
- Cada asignación nueva o reactivada queda con `Notificado = 0` (el correo de aviso es el pendiente 71).

## Casos
| Caso | Qué hace | Esperado |
|---|---|---|
| c00 | GET lista con el texto del usuario de prueba (incluidos inactivos) | 200 |
| c01 | POST alta del usuario de prueba | 201 la primera vez; si ya existe, 409 con `usuarioId` = su Id (reutilización) |
| c02 | POST el mismo correo otra vez | 409 «Ya existe un usuario con ese correo.» con `usuarioId` |
| c03 | POST con un dominio no permitido | 400 en `email`: «El correo debe ser de un dominio permitido: …» |
| c04 | POST con un correo inválido | 400 en `email`: «El correo no tiene un formato válido.» |
| c05 / c06 | Detalle (elige hasta 2 departamentos activos) y PUT activo (punto de partida) | 200 / 200 |
| c07 | PUT departamentos con los elegidos | 200 con ese conjunto |
| c08 | PUT con `anteriores` desfasados (vacío) | 409 «Otro usuario cambió los departamentos; recargue.» |
| c09 | PUT con el departamento 999999 | 400 en `departamentoIds` |
| c10 | PUT conjunto vacío | 200, sin departamentos |
| c11 / c12 | Baja y reactivación del usuario de prueba (con su detalle antes) | 200 / 200 |
| c13a–c | El admin intenta darse de baja a sí mismo | 400 en `activo`: «No puedes darte de baja a ti mismo.» |
| c14a–e | Los 5 endpoints como gestor | 403 |
| e01 | gestor2: `GET /api/usuarios/me` (queda aprovisionado y en la caché) | 200 |
| e02 / e03 | Admin: detalle y baja de gestor2 | 200 / 200 |
| e04 | gestor2: `GET /api/usuarios/me` **inmediatamente** | 403 con detail «El usuario está inactivo en la aplicación.» (sin esperar los 10 minutos de la caché) |
| e05 / e06 | Admin reactiva a gestor2; gestor2 vuelve a pedir `me` | 200 / 200 |
| r00–r04 | Restauración (siempre): usuario de prueba sin departamentos e inactivo; gestor2 activo | 200 (solo si hay algo que restaurar) |

Al final: «Casos con FALLA: n; OMITIDOS: m» y «Fin.». Códigos de salida: 0 = todo OK; 1 = algún CONTROL FALLA; 2 = ejecución incompleta.

**Archivo de estado** `resultado-estado-usuarios.txt`: guarda solo los Id (`prueba=…`, `gestor2=…`) mientras hay algo que restaurar; se borra cuando la restauración termina bien. Si existe al empezar, aparece una ADVERTENCIA y la corrida vuelve a restaurar al final. **Candado** `resultado-usuarios.lock`: igual que en la tarea62 (5 minutos).

## Después de ejecutarlo (SQL de solo lectura, en SSMS sobre PROFESIOGRAMA_DEV)
Reemplace `<Id>` por el Id del usuario de prueba que imprimió c01 y `<IdGestor2>` por el de e01:
```sql
SELECT Id, Activo, EsSistema, CASE WHEN EntraObjectId IS NULL THEN 0 ELSE 1 END AS ConIdentidad,
       CreadoPorId, FechaCreacion, ModificadoPorId, FechaModificacion
FROM dbo.Usuario WHERE Id IN (<Id>, <IdGestor2>);

SELECT Id, UsuarioId, DepartamentoId, Activo, Notificado, FechaNotificacion, CreadoPorId, FechaCreacion, ModificadoPorId, FechaModificacion
FROM dbo.UsuarioDepartamento WHERE UsuarioId = <Id> ORDER BY DepartamentoId;
```
Esperado: el usuario de prueba con `Activo = 0` y `ModificadoPorId` = el Id del admin; gestor2 con `Activo = 1`; en `UsuarioDepartamento`, **una sola fila** por departamento (las corridas repetidas reactivan la misma fila, no duplican), todas con `Activo = 0`, `Notificado = 0` y `ModificadoPorId` = el admin. `FechaModificacion` en UTC (hora de Ecuador + 5 h).

**Restauración manual** (solo si el script imprime «RESTAURACIÓN PENDIENTE»: use la sentencia exacta que imprime, con los Id ya puestos) y luego **reinicie la API** (caché de usuarios de 10 minutos) y borre `resultado-estado-usuarios.txt`.

Pegue en el chat `resultado-usuarios.txt` (solo códigos HTTP, Id, conteos y mensajes; nunca correos ni nombres).

Simulación (sin red): `set SIMULAR=1` y `usuarios.cmd`. Lee `simulacion\<caso>.json` y `.codigo` (ficticios) y fuerza la URL a `https://localhost:1`.
