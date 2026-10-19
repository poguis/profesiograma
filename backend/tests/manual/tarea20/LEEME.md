# TAREA-20 — Consulta del cronograma (backend)

Lo ejecuta el **usuario**. El script **solo lee**: hace `GET /api/cronograma` y `GET /api/proyectos/{id}/cronograma` en **nuestra API**. No escribe en la base.

> **Datos personales.** La salida muestra solo conteos, códigos EKON, Id de proyectos visibles, estados y siglas. **Nunca nombres** de personas ni de proyectos. Pegue en el chat solo `resultado-cronograma.txt`.

## Archivos
| Archivo | Qué es |
|---|---|
| `cronograma.cmd` / `.ps1` | El script. Reutiliza `..\tarea26a\comun.ps1` |
| `simulacion\c_<caso>.json` + `.codigo` | Respuestas **ficticias** para el modo simulación; se versionan |
| `resultado-cronograma.txt` | Salida. Está en `.gitignore` |

## Ejecución
1. API corriendo (Simulado o Http; no depende del ERP).
2. Opcional:
   - `set PROYECTO_ID=<Id>`: un proyecto visible para el gestor, para el filtro `proyectoId` y el endpoint por proyecto. Por defecto, 14.
   - `set PROYECTO_NO_VISIBLE=<Id>`: un proyecto que el gestor **no** ve. Por defecto, 3 (solo lo ve el admin).
   - `set CODIGO_EKON=<código>`: para el filtro `codigoEkon`. Por defecto, el primer código de la respuesta del gestor.
   - `set DEV_USER=admin`: usuario de los casos filtrados. Por defecto, `gestor`.
   - `set API_BASE=…`: por defecto, `https://localhost:7180`.
3. Ejecute `cronograma.cmd`. La primera línea dice **MODO REAL** o **MODO SIMULACION**. Si dice SIMULACION sin que usted lo quisiera, ejecute `set SIMULAR=` y repita.
4. Pegue `resultado-cronograma.txt`.

| Caso | Solicitud | Usuario | Esperado |
|---|---|---|---|
| Mes actual | `GET /api/cronograma` | gestor | 200 con solo sus proyectos |
| Mes actual | `GET /api/cronograma` | admin | 200 con todos los proyectos |
| Filtro de proyecto | `?proyectoId=<PROYECTO_ID>` | `DEV_USER` | 200: las personas de ese proyecto, con todos sus proyectos visibles |
| Filtro de persona | `?codigoEkon=<código>` | `DEV_USER` | 200: solo esa persona |
| 91 días | `?desde=<día 1 del mes>&hasta=<+90 días>` | `DEV_USER` | 400 en `hasta`: «El rango no puede superar 90 días.» |
| Rango invertido | `hasta` anterior a `desde` | `DEV_USER` | 400 en `hasta` |
| Por proyecto | `GET /api/proyectos/<PROYECTO_ID>/cronograma` | `DEV_USER` | 200; sin fechas, desde el inicio del proyecto (máximo 90 días) |
| Por proyecto no visible | `GET /api/proyectos/<PROYECTO_NO_VISIBLE>/cronograma` | gestor | 404 |

**Cómo leer la salida:**
- `celdas <ESTADO>`: NOVEDAD, CHOQUE, PROYECTO, OTRO_PROYECTO o DESCANSO. Los días libres no se envían.
- `sigla`: siglas de novedades (V, F, P, PM…).
- `otrosProyectos`: días en proyectos que no vienen en la respuesta (D2). Para el gestor son proyectos de otros usuarios; en el endpoint por proyecto, los demás proyectos.
- `novedadesExtra`: novedades PERSONA adicionales del mismo día (D9).

## Simulación (sin red)
`set SIMULAR=1` lee `simulacion\c_<caso>.json` y su `.codigo`; el caso «no visible» no tiene archivo y da 404. La URL base se fuerza a `https://localhost:1`.

Claude Code simula solo en una **copia** de los scripts en el scratchpad (regla de `CLAUDE.md`).
