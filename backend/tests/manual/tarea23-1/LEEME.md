# TAREA-23-1 — verificación manual: novedades (listado, opciones, detalle y creación)

`novedades.cmd` lo ejecuta **el usuario** con la API en marcha (`https://localhost:7180`, Development, DevAuth). Claude Code no lo ejecuta.

**Escribe en la base de desarrollo:** crea 4 novedades por corrida (casos 1, 2, 4 y 11a). Todavía no se pueden anular (TAREA-23-2); el script las lista al final con su código.

## Modo
- **MODO REAL** (por defecto): llama a la API. El modo se muestra en la primera línea.
- **MODO SIMULACION:** `set SIMULAR=1` antes de ejecutar. No hace llamadas de red: lee las respuestas ficticias de `simulacion\` (sirve para revisar el script).

## Variables (opcionales, en la consola antes de ejecutar)
| Variable | Por defecto | Uso |
|---|---|---|
| `CODIGO_EKON` | `DEV001` | Empleado **activo en la API** de las PERSONA del gestor. DEV001 existe en el ERP **Simulado**; con el ERP real (modo Http por user-secrets, TAREA-26a) indique un código real activo. |
| `CODIGO_EKON_ADMIN` | = `CODIGO_EKON` | Empleado de la PERSONA que crea el admin (casos 11 y 13). Desde la TAREA-23-2 el valor por defecto es el mismo `CODIGO_EKON` en los dos modos del ERP (antes `DEV002`, que solo existe en el Simulado): no choca con n01 porque usa otro tipo (PERMISO) y otra fecha. |
| `PROYECTO_ID` | El primero ACTIVO del gestor | Proyecto **propio y vigente** del gestor (caso 2). |
| `CORRIDA` | Derivada del reloj | Desplaza las fechas (desde 2032, 3 días por corrida) para repetir el script sin el 409 de duplicado. Se muestra al inicio. |
| `API_BASE` | `https://localhost:7180` | Base de la API. |

## Casos (cada uno imprime `CONTROL <caso> OK|FALLA (esperado X, obtenido Y)`)
Desde la TAREA-23-2, un caso cuyo requisito no se creó (n06 y n12 dependen de n01; n11b, n11c y n13 de n11a; n02 de un proyecto ACTIVO) imprime `CONTROL <caso> OMITIDO (requisito <n> no creado)` y no cuenta como FALLA (antes llamaba a la API con un Id vacío). Las novedades que crea este script se anulan con `tarea23-2\limpiar.cmd`.

| # | Caso | Usuario | Esperado |
|---|---|---|---|
| n01 | Crear PERSONA VACACIONES (2 días) | gestor | 201 |
| n02 | Crear PROYECTO FERIADO sobre un proyecto propio vigente | gestor | 201 |
| n03 | GENERAL | gestor | 403 |
| n04 | GENERAL FERIADO | admin | 201 |
| n05a–c | RN16: DESCANSO en PERSONA; VACACIONES en PROYECTO; PERMISO en GENERAL | gestor / admin | 400 `tipoNovedad` |
| n06 | El mismo cuerpo de n01 (duplicado exacto) | gestor | 409 (con el código, porque el gestor la ve) |
| n07 | Fin anterior al inicio | gestor | 400 `fechaFin` |
| n08 | 367 días | gestor | 400 `fechaFin` |
| n09 | `codigoEkon` inexistente (`ZZ99999`) | gestor | 400 `codigoEkon` |
| n10a / n10b | `opciones-formulario` | gestor / admin | 200 con `puedeRegistrarGeneral` false / true |
| n11a | Crear PERSONA PERMISO | admin | 201 |
| n11b / n11c | Listado con `texto=<código de n11a>` | gestor / admin | total 0 / total 1 (el gestor no ve las PERSONA de otros) |
| n11d / n11e | Listado con `origen=PERMISOS_MEDICOS` | gestor / admin | total 0 / informativo (el sembrador tiene NOV-DEV-0004) |
| n12 | Detalle de n01 | gestor | 200 con `version`, 2 días, sin registrante y `puedeEditar` true |
| n13 | Detalle de n11a | gestor | 404 |
| n14 | Listado sin sesión | anonimo | 401 |

## Salida
`resultado-novedades.txt` (solo códigos HTTP, códigos NOV-, Id, cantidades y mensajes de validación; nunca nombres de personas ni de proyectos). Al final: las novedades creadas y «Casos con FALLA: n».
