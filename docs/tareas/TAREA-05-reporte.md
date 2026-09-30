# TAREA-05 — Pruebas HTTP de la API (DevAuth)

**Fecha:** 2026-09-30
**Resultado:** completada. **Las 9 pruebas pasan.**
El usuario arrancó la API (`https://localhost:7180`, entorno Development, DevAuth). Solo se hicieron peticiones HTTP a localhost.
No se ejecutaron `dotnet run`, `dotnet ef` ni SQL manual, no se detuvo la API y no se usaron comandos git que modifiquen el repositorio.

## 1. Resultados

Comando base: `curl -k -sS -i -m 30 [-H "X-Dev-User: <clave>"] https://localhost:7180<ruta>`

| # | Prueba | Esperado | Obtenido | Resultado |
|---|---|---|---|---|
| 1 | `GET /api/health/db` (sin encabezado) | 200, anónimo | **200**. `servidor NIQUEL\SSDEV`, `baseDatos PROFESIOGRAMA_DEV`, `usuarioConectado profesiograma_dev`, SQL Server 2019 (15.0.2190.7), compatibilidad 150, `cumpleRequisitos: true` | OK |
| 2 | `GET /api/usuarios/me` · `X-Dev-User: admin` | 200, admin.dev@profesiograma.local, `[Admin]` | **200**. `id 2`, `admin.dev@profesiograma.local`, `ADMIN DESARROLLO`, roles `["Admin"]` | OK |
| 3 | `GET /api/usuarios/me` · `X-Dev-User: gestor` | 200, `[Gestor]` | **200**. `id 3`, `gestor.dev@profesiograma.local`, `GESTOR DESARROLLO`, roles `["Gestor"]` | OK |
| 4 | `GET /api/catalogos` · `X-Dev-User: gestor` | 200 y conteos por catálogo | **200**. Conteos en la tabla 1.1 | OK |
| 5 | `GET /api/admin/parametros` · `X-Dev-User: admin` | 200, 9 parámetros | **200**, **9** parámetros | OK |
| 6 | `GET /api/admin/parametros` · `X-Dev-User: gestor` | 403 | **403 Forbidden** (cuerpo vacío) | OK |
| 7 | `GET /api/catalogos` · `X-Dev-User: anonimo` | 401 | **401 Unauthorized** (cuerpo vacío) | OK |
| 8 | `GET /api/catalogos` · `X-Dev-User: inexistente` | 401 | **401 Unauthorized** (cuerpo vacío) | OK |
| 9 | `GET /api/usuarios/me` (sin encabezado) | 200 como admin | **200**. `id 2`, `admin.dev@profesiograma.local`, roles `["Admin"]` | OK |

### 1.1 Conteos de `/api/catalogos` (prueba 4)
| Catálogo (clave JSON) | Esperado | Obtenido |
|---|---|---|
| estadosProyecto | 4 | 4 |
| tiposMovimiento | 7 | 7 |
| gruposProyecto | 3 | 3 |
| jornadas | 4 | 4 |
| rolesAsignacion | 3 | 3 |
| tiposAplicacionNovedad | 3 | 3 |
| origenesNovedad | 2 | 2 |
| tiposNovedad | 7 | 7 |
| departamentos | 7 | 7 |

### 1.2 Parámetros (prueba 5)
`ALMUERZO_REGRESO_OPCIONES`, `ALMUERZO_SALIDA_OPCIONES`, `BACK_MAX_DIAS_DESCANSO`, `CRONOGRAMA_MAX_DIAS`, `EMPLEADO_FAMILIAS_ASIGNABLES`, `PROYECTO_MAX_BACKS`, `PROYECTO_MAX_PRINCIPALES`, `PROYECTO_VISIBILIDAD`, `ZONA_HORARIA` (9).

## 2. Observaciones
- **Primer intento sin respuesta:** la primera tanda de peticiones (con `-s`, que oculta los errores) devolvió cuerpos vacíos en todas las pruebas. Al repetir con `-sS`, la API respondió con normalidad. Lo más probable es que la API todavía no estuviera lista, pero no hay evidencia del motivo porque `-s` suprimió el mensaje de curl. Los resultados de la tabla corresponden a la segunda tanda completa, a las 16:48 UTC. No se modificó nada entre ambas tandas.
- **Prueba 1, "sin consultar dbo.Usuario":** HTTP no permite observarlo directamente. El comportamiento se apoya en el código de `UsuarioActualMiddleware` (TAREA-03, Parte 1): en endpoints `AllowAnonymous` no se llama a `IUsuarioProvisionamiento`. La prueba confirma el 200 sin encabezado, cuando DevAuth autentica como `admin` por defecto.
- **Id de usuarios:** `SISTEMA` = 1 (semilla), `admin` = 2 y `gestor` = 3. Los usuarios de DevAuth están registrados en `dbo.Usuario`.
- Las respuestas 401 y 403 no traen cuerpo; en la API actual solo se devuelve el código de estado.

## 3. Archivos tocados
| Acción | Archivo |
|---|---|
| Creado | `docs/tareas/TAREA-05-reporte.md` |
| Modificado | `docs/00_ESTADO_ACTUAL.md` (sección 5) |

Las respuestas crudas quedaron en el scratchpad de la sesión, fuera del repositorio.

## 4. Pendientes
- Ejecutar `backend/sql/dev/verificacion_paso2.sql` en SSMS. Esta tarea no lo incluía y no se ha hecho.
- Siguiente paso según `00_ESTADO_ACTUAL.md` (sección 6): Fase 4 (mapeo de flujos) y Fase 5 del módulo Proyectos.
