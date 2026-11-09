# TAREA-23-2 — verificación manual: edición y anulación de novedades

Los scripts los ejecuta **el usuario** con la API en marcha (`https://localhost:7180`, Development, DevAuth). Claude Code no los ejecuta.

- **MODO REAL** (por defecto): llama a la API; el modo se muestra en la primera línea.
- **MODO SIMULACION:** `set SIMULAR=1`. Sin llamadas de red; lee las respuestas ficticias de `simulacion\` (sirve para revisar los scripts).
- Cada caso imprime `CONTROL <caso> OK|FALLA (esperado X, obtenido Y)`. Si su requisito no se creó: `CONTROL <caso> OMITIDO (requisito <n> no creado)`, que no cuenta como FALLA. Al final: «Casos con FALLA: n; OMITIDOS: m».
- Salidas: `resultado-edicion.txt` y `resultado-limpiar.txt` (solo códigos HTTP, códigos NOV-, Id, versiones, fechas y mensajes; nunca nombres de personas ni de proyectos).

## `edicion.cmd`
**Escribe en la base de desarrollo:** crea 4 novedades de prueba y **al final las anula todas** (lista sus códigos).

| Variable | Por defecto | Uso |
|---|---|---|
| `CODIGO_EKON` | `DEV001` | Empleado activo en la API (DEV001 existe en el ERP Simulado; con el ERP real, un código activo) |
| `CORRIDA` | Derivada del reloj | Desplaza las fechas (desde 2090, 6 días por corrida) para repetir sin 409 |
| `API_BASE` | `https://localhost:7180` | Base de la API |

Preparación (con `CORRIDA`, días d0…d4): p01 PERSONA VACACIONES d0–d2 (gestor); p02 GENERAL FERIADO d0 (admin); p03 PERSONA PERMISO d4 (admin); p04 PERSONA PERMISO d3 (gestor, para el duplicado).

| Caso | Qué hace | Esperado |
|---|---|---|
| e01 (+ e01b) | Editar p01 excluyendo d1; el detalle muestra d0 y d2 | 200 con versión nueva distinta |
| e02 | Cambiar el tipo a CALAMIDAD_DOMESTICA | 200 |
| e03 | Pasar a PERMISO hasta d3 (choca con p04) | 409 con el código de p04 (el gestor la ve) |
| e04 | Pestaña A: solo días, con la versión v3 | 200 y versión distinta (R1: hay UPDATE aunque solo cambien días) |
| e05 | Pestaña B: solo días, con la MISMA v3 | **409** (A5: sin R1 daría 200) |
| e06 / e07 / e08 | Sin `version` / versión vieja / versión ilegible | 400 `version` / 409 / 400 `version` |
| e09 | Editar NOV-DEV-0004 (PERMISOS_MEDICOS del sembrador) como admin | 400 `origen` (OMITIDO si no se encuentra) |
| e10 | El gestor edita la GENERAL del admin (la ve en solo lectura) | 403 |
| e11 | El gestor edita la PERSONA del admin (no la ve) | 404 |
| e12 / e13 | Anular p01 con motivo / anularla otra vez | 200 / 409 «La novedad ya está anulada.» |
| e14 / e15 | Editar la anulada / su detalle | 409 «La novedad está anulada.» / `anulada` true, `motivoAnulacion` y `puedeEditar` false |
| z_* | Limpieza: anula p02, p03 y p04 (admin) | 200 |

## `limpiar.cmd` (novedades de prueba de la TAREA-23-1)
Anula (borrado lógico, sin SQL) como **admin** con el motivo «Limpieza de pruebas TAREA-23-1».

| Variable | Uso |
|---|---|
| `IDS` | **Obligatoria.** Id o rangos: `set IDS=5-14` o `set IDS=5,7,9-11` |
| `CONFIRMAR` | Sin `set CONFIRMAR=1` es una **VISTA PREVIA**: lista código, aplicación, origen, fechas y si está anulada, y dice qué haría |

**Protección:** solo anula una novedad si su Id está en `IDS`, su `FechaInicio` cae en **2070** y su origen es **PROFESIOGRAMA**; si algo no coincide (o ya está anulada, o no existe) la **omite e informa**. **Nunca** toca los Id 1–4 (semillas del sembrador).

Uso sugerido: primero `set IDS=5-14` y `limpiar.cmd` (vista previa); revisar la lista; después `set CONFIRMAR=1` y `limpiar.cmd`.
