# TAREA-26d-3 — Comparación con el ERP y retiro de `empleadoId` (verificación con scripts)

> **Datos sensibles.** Pegue en el chat **solo** los `resultado-*.txt`. Los scripts muestran códigos EKON, tipos de aviso y conteos; **nunca** nombres, cargos ni departamentos.
>
> Claude Code no ejecuta estos scripts contra la API ni el SQL: los ejecuta usted.

## Requisitos
- API reiniciada con este código, en modo **Http** (user-secrets de la 26a).
- Para `retiro.cmd`: `..\tarea26d\crear-proyecto-1.json` (el de V3). Recuerde: `proyectoErpId` y `actividadId` son **texto entre comillas** (p. ej. `"20"`); un número sin comillas da 400 "La solicitud no tiene el formato esperado." y el caso b) no llega a 200.

## Archivos
| Archivo | Qué hace | ¿Escribe? |
|---|---|---|
| `comparacion.cmd` | `GET /api/proyectos/{id}/personal/comparacion-erp` de los proyectos de `set PROYECTO_IDS=15 17` (por defecto 15), como `gestor` (o `set DEV_USER=admin`). Muestra el código HTTP, el aviso general y, por persona, el código EKON y los tipos de aviso | No |
| `retiro.cmd` | Vista previa de crear: a) solo `empleadoId` → 400 en `principales[0].codigoEkon` "El empleado es obligatorio."; b) solo `codigoEkon` → 200. Si un caso no da el código esperado, muestra `title`/`detail` y todos los errores por campo de la respuesta (nunca la solicitud) | No |
| `caso_a_preparar.sql` | Cambia `Puesto` y `Departamento` de **una** fila de `dbo.Empleado` (parámetro `@CodigoEkon`) a valores de prueba. Solo `PROFESIOGRAMA_DEV` | **Sí (1 fila de Empleado)** |
| `caso_a_revertir.sql` | Devuelve esa fila a sus valores originales (los copia usted del SELECT "ANTES") | **Sí (la misma fila)** |
| `simulacion\` | Respuestas ficticias para `SIMULAR=1` | — |

Las salidas `resultado-*.txt` están en `.gitignore`.

## Casos
| # | Caso | Pasos | Esperado |
|---|---|---|---|
| C1 | (b) No activo | `set PROYECTO_IDS=1 2 4` (proyectos con DEV001–DEV008, que no están en la API en modo Http) → `comparacion.cmd` | 200; las personas DEV con `NO_ACTIVO`; sin aviso general |
| C2 | Sin diferencias | `set PROYECTO_IDS=15 17` → `comparacion.cmd` | 200; sin personas con avisos (salvo cambios reales en el ERP) |
| C3 | (a) Cambio de cargo y departamento | `caso_a_preparar.sql` en SSMS con `@CodigoEkon` de una persona del Id 15 → `comparacion.cmd` con `PROYECTO_IDS=15` → `caso_a_revertir.sql` | Esa persona con `CAMBIO_CARGO, CAMBIO_DEPARTAMENTO`; después de revertir, sin avisos |
| C4 | (c) ERP caído | `dotnet user-secrets set "ServiciosExternos:ErpBase7048" "https://localhost:1" --project src/App.Api` (URL inválida recomendada), reiniciar la API, sin lista previa (o más de 60 min) → `comparacion.cmd` | 200 con aviso general "No se pudo comparar con el ERP." y sin personas (nunca 503). Restaurar con `dotnet user-secrets remove "ServiciosExternos:ErpBase7048" --project src/App.Api` (el valor real viene de `appsettings.json`) y reiniciar |
| C5 | Retiro de `empleadoId` | `retiro.cmd` | a) 400 `principales[0].codigoEkon`; b) 200 |
| C6 | No visible | `set DEV_USER=gestor` y `set PROYECTO_IDS=3` (solo visible para el admin) → `comparacion.cmd` | 404 |

La caché de la API dura 10 min: tras `caso_a_preparar.sql` el aviso aparece enseguida (lo guardado cambió), pero un cambio real en el ERP puede tardar hasta 10 min en verse. Con el ERP caído se usa la lista anterior hasta 60 min, con el aviso "Comparado con la lista de las HH:mm; el ERP no respondió."

## Modo simulación (sin red)
`set SIMULAR=1` antes de ejecutar un `.cmd`. Las respuestas se leen de `%SIMULACION%` (por defecto `.\simulacion`):
- `comparacion_<id>.json`: el 15 trae avisos ficticios y el 17 el aviso general; un Id sin archivo da 404;
- `retiro_a.json` (con `retiro_a.codigo` = 400) y `retiro_b.json` (con `retiro_b.codigo` = 400: un 400 de cuerpo ilegible, para ver cómo el script muestra un resultado no esperado con su title/detail).

En simulación, `retiro.cmd` usa `..\tarea26d\crear-proyecto.ejemplo.json` (valores ficticios), nunca su `crear-proyecto-1.json`. Ninguna función abre conexiones (ver `..\tarea26a\comun.ps1`).
