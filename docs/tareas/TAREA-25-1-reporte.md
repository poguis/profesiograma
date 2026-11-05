# TAREA-25-1 — Reporte: usuarios y departamentos, backend

**Fecha:** 29/10/2026 · **Fase:** B (implementación del plan de la TAREA-25) · **Estado:** ✅ Verificada por el usuario el 29/10/2026

> **Nota de la verificación (29/10/2026):** `usuarios.cmd` terminó limpio a la primera (0 FALLA, 0 OMITIDOS) con el patrón de la 62 (candado, archivo de estado, restauración protegida). La baja inmediata de `gestor2` (e04) se comprobó y el estado final quedó correcto. Quedan en la base los usuarios Id 4 y 5 creados por la prueba (no se borran filas). En la 25-2, el 503 del bloqueo por usuario pasó a tener su propio mensaje.

Contratos, reglas, mensajes y decisiones: `docs/fases/FASE_5_Usuarios_Departamentos.md` (nuevo).

## 1. Qué se hizo
- **Control previo:** 24d ✅ en la tabla de `FASE_5_Novedades.md`; 62-2 ✅ en `00_ESTADO_ACTUAL.md` y en `TAREA-62-2-reporte.md`; pendiente 6 cerrado (y el 62, terminado); pendientes 70 y 71 nuevos. En `00_ESTADO_ACTUAL.md` la 24d ya figuraba verificada: no había líneas que corregir.
- **Endpoints `Politica.Admin`** (`/api/admin/usuarios`): lista paginada (sin SISTEMA, por correo, una sola consulta con los departamentos), detalle con `departamentosDisponibles` y `esUsuarioActual`, alta por correo (201 / 400 / 409 con `usuarioId`), edición con compare-and-set por valores (`ExecuteUpdate`), y conjunto de departamentos con `sp_getapplock` por usuario, relectura y comparación de conjuntos (409). Sin migración.
- **Efecto inmediato (P6):** `IUsuarioProvisionamiento.Invalidar(email, oid)` quita las dos claves de la caché; se llama tras el alta y la edición.
- **Configuración:** `Usuarios:DominiosPermitidos` (`appsettings.json`: `sedemi.com`; Development: + `profesiograma.local`, el dominio de DevAuth). DevAuth `gestor2` (Gestor, `33333333-…`).
- **O6 y O7** documentados en FASE_5 §5–§6, sin cambios de código: un usuario sin departamentos ve a todos en el buscador y crea proyectos sin departamento, sin mensaje.
- **Script manual** `backend/tests/manual/tarea25/usuarios.cmd` (+ `.ps1`, `LEEME.md` y `simulacion\`, 46 archivos ficticios), con el patrón de `tarea62/criterio.ps1`.

## 2. Archivos tocados
| Archivo | Cambio |
|---|---|
| `src/App.Application/Seguridad/UsuariosAdminContratos.cs`, `UsuariosAdminServicio.cs` | Nuevos: DTO, repositorio, configuración y reglas |
| `src/App.Application/Seguridad/IUsuarioProvisionamiento.cs`, `src/App.Infrastructure/Seguridad/UsuarioProvisionamiento.cs` | `Invalidar` y `ClaveCache` |
| `src/App.Infrastructure/Persistencia/Seguridad/UsuariosAdminRepositorio.cs` | Nuevo: consultas, `ExecuteUpdate`, applock, plan de asignación |
| `src/App.Application/DependencyInjection.cs`, `src/App.Infrastructure/DependencyInjection.cs`, `…/PersistenciaServiceCollectionExtensions.cs` | Registro y dominios permitidos |
| `src/App.Infrastructure/Persistencia/DatosPrueba/DatosPruebaSembrador.cs` | Departamento de prueba solo para `admin` y `gestor` |
| `src/App.Api/Endpoints/UsuarioEndpoints.cs` | Grupo `/api/admin/usuarios` y logs con Id |
| `src/App.Api/appsettings.json`, `appsettings.Development.json` | `Usuarios:DominiosPermitidos`; `gestor2` |
| `tests/App.Application.Tests/Seguridad/UsuariosAdminServicioTests.cs`, `tests/App.Infrastructure.Tests/Consultas/UsuariosAdminSqlTests.cs`, `tests/App.Api.Tests/UsuariosAdminEndpointsTests.cs` | Nuevos (47 pruebas) |
| `backend/tests/manual/tarea25/*` | Script manual (no ejecutado) |
| `docs/fases/FASE_5_Usuarios_Departamentos.md` (nuevo), `FASE_2_Modelo_Datos.md` (J4), `FASE_5_Novedades.md`, `docs/00_ESTADO_ACTUAL.md`, `docs/HISTORIAL.md` (H13), `docs/tareas/TAREA-62-2-reporte.md` | Documentación |

## 3. Comandos y resultado (desde `backend/`)
| Comando | Resultado |
|---|---|
| `dotnet build Profesiograma.slnx -c Release` | Compilación correcta, 0 errores |
| `dotnet test --solution Profesiograma.slnx -c Release` | **1121/1121** (1074 + 47). Ninguna prueba existente se ajustó |
| `dotnet ef migrations has-pending-model-changes … --configuration Release` | «No changes have been made to the model since the last migration.» |
| Análisis sintáctico de `usuarios.ps1` (parser de PowerShell, sin ejecutarlo) | 0 errores; archivo 100 % ASCII |
| `00_ESTADO_ACTUAL.md` | **24 992 B**; lo reemplazado va textual a `HISTORIAL.md` H13 (comprobado contra `HEAD`: 0 líneas perdidas, salvo el pendiente 2, que solo cambió «TAREA-25» por «TAREA-25-3»; el fragmento está en H13) |

## 4. Desviaciones, errores y decisiones
- **La corrida en simulación de `usuarios.ps1` (copia en el scratchpad) fue denegada** por el control de permisos; solo se hizo el análisis sintáctico. La primera ejecución real puede revelar errores de script: si aparece «ERROR NO CONTROLADO», pegue la salida.
- **`nombreMostrar` es obligatorio en el alta:** la columna es NOT NULL y el provisionamiento no completa el nombre de un precargado (FASE_5 §6).
- **Dominios:** se registró `ConfiguracionUsuarios` en Infrastructure leyendo la sección a mano (sin `IOptions` en Application). Sin valor → `sedemi.com`.
- **PUT del usuario:** responde 200 con el usuario releído. La clave del 400 por faltar los valores anteriores es `anterior`. El 409 de alta omite `usuarioId` si el correo es el de SISTEMA (abrirlo daría 404).
- **503 del applock por usuario:** reutiliza `RegistroOcupadoException`; su detail dice «Hay otro registro de asignaciones en curso…», un texto genérico que aquí no es exacto. [PENDIENTE DE DECISIÓN DEL USUARIO: ¿un mensaje propio?]
- **Sembrador:** antes asignaba el departamento SIG a todos los usuarios de DevAuth. Ahora solo a `admin` y `gestor`, para que `gestor2` quede sin departamentos. Solo rige si se vuelve a sembrar; hoy no siembra porque ya existen los PRY-DEV.
- **Riesgo conocido:** si una solicitud de un usuario empieza antes de la baja y termina después de `Invalidar`, puede volver a guardar «activo» en la caché hasta 10 minutos. La ventana es de milisegundos y no se corrige; queda anotado.
- **O1, O2, O4 y O5:** su texto no venía en el pedido. [PENDIENTE DE CONFIRMAR] si piden algo distinto de lo implementado (FASE_5 §4).
- **25-2:** para entrar como `gestor2`, agregar `'gestor2'` a `USUARIOS_DEV` (`frontend/src/auth/devAuth.ts`) y su etiqueta en `SelectorUsuarioDev.tsx`.

## 5. Verificación para el usuario
**Primero, reinicie la API** (cambió `appsettings.Development.json`). Instrucciones completas en `backend/tests/manual/tarea25/LEEME.md`.

| # | Caso | Esperado |
|---|---|---|
| V1 | `usuarios.cmd` (MODO REAL) | c00–c14 y e01–e06 con CONTROL OK; «Casos con FALLA: 0; OMITIDOS: 0»; sin «RESTAURACIÓN PENDIENTE» |
| V2 | e04 | 403 **inmediato** de `gestor2` tras la baja, con detail «El usuario está inactivo en la aplicación.» |
| V3 | SQL de solo lectura del LEEME | Usuario de prueba `Activo = 0`, sin asignaciones activas y **una fila** por departamento (`Notificado = 0`); `gestor2` `Activo = 1`; `ModificadoPorId` = admin |
| V4 | Segunda corrida de `usuarios.cmd` | c01 reutiliza (409 con su Id); c07 reactiva las mismas filas (V3 sigue con una fila por departamento) |
| V5 | Log de la API | Un Information por escritura con Id (usuario, departamentos, admin); sin correos ni nombres |
| V6 | Regresión: el buscador y «Nuevo proyecto» como `gestor` | Igual que antes (gestor conserva SIG) |

## 6. Pendientes
- Verificación (§5) y decisión sobre el mensaje del 503 (§4).
- 25-2 (frontend; pendiente 70), 25-3 (CargoInfor, pendiente 2), pendiente 71 (aviso por Graph, Fase 7).
