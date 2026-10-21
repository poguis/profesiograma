# TAREA-22-2 — Reporte Excel por proyecto: verificación manual

Los scripts los ejecuta **usted** (Claude Code no los ejecuta contra la API). Requisitos: VPN, la API en ejecución (`https://localhost:7180`, DevAuth) y SSMS sobre `PROFESIOGRAMA_DEV`. Ninguno escribe en la API ni en la base.

| Archivo | Qué hace |
|---|---|
| `resumen.cmd ID DESDE HASTA` | `GET /api/proyectos/{ID}/reporte/resumen`: imprime solo el código HTTP y el JSON de conteos. Escribe `resultado-resumen.txt` |
| `reporte.cmd ID DESDE HASTA` | `GET /api/proyectos/{ID}/reporte`: descarga `Reporte.xlsx` en esta carpeta e imprime solo el estado HTTP, `X-Reporte-Filas`, `Cache-Control`, el nombre de `Content-Disposition` y el tamaño. Escribe `resultado-reporte.txt` |
| `conteos-tipos.sql` | R1: agregados de `ProyectoErpId`, `CompaniaId` y `CodigoEkon` para la regla Z7. Solo lectura |

- **Usuario:** `set DEV_USER=gestor` (por defecto `admin`). Las fechas van en `yyyy-MM-dd`.
- **Banner:** los dos scripts muestran al inicio **MODO REAL** o **MODO SIMULACION**.
- **Encabezados de curl:** `reporte.cmd` usa `curl -D` (encabezados a un temporal que se borra) y `-o Reporte.xlsx`, **no** `-i`: con `-i` los encabezados quedarían dentro del `.xlsx` y Excel pediría repararlo.

## Matriz

| # | Caso | Cómo | Esperado |
|---|---|---|---|
| R1 | Tipos para Z7 | `conteos-tipos.sql` | Usted pega los agregados |
| R2 | Resumen | `resumen.cmd ID` con el rango del proyecto (proyecto con personal) | Conteos coherentes con `/cronograma` |
| R3 | Abrir el libro | `reporte.cmd` del mismo proyecto; abrir `Reporte.xlsx` | Abre sin pedir reparación; hoja `PROYECTO `; tabla `Tabla1` |
| R4 | CEDULA y FECHA | Revisar las columnas C y E | Texto; cédulas con "0" inicial intactas; `2/10/2026` sin ceros a la izquierda |
| R5 | Horas | Columnas F–I | Se ven `hh:mm:ss`; en un día libre, las 4 en `00:00:00` |
| R6 | Novedad / descanso | Un día con novedad y uno de descanso | ESTADO y OBSERVACION según la tabla de `FASE_5_Cronograma.md` §8 |
| R7 | Nocturno | Proyecto con turno nocturno (p. ej. horario 73 o 118) | SALIDA DE JORNADA menor que INGRESO, tal cual |
| R8 | PLANTA/OFICINAS | Proyecto PLANTA u OFICINAS | PROYECTO vacío |
| R9 | Sin almuerzo | Proyecto con horario sin almuerzo (TAREA-26c) | Almuerzo `00:00:00` |
| R10 | Rango largo | `reporte.cmd ID 2025-01-01 2026-12-31` en un proyecto de meses | 200 (sin 400) y las mismas filas que con su rango exacto |
| R11 | Parámetros | `resumen.cmd ID "" 2026-10-31`, `resumen.cmd ID 01-10-2026 2026-10-31`, `resumen.cmd ID 2026-10-31 2026-10-01` | 400 en `desde`, `desde` y `hasta` |
| R12 | Proyecto ajeno | `set DEV_USER=gestor` y un proyecto que el gestor no ve (p. ej. 3) | 404 |
| R13 | Sin asignaciones | Un rango del proyecto sin días asignados | 200, `X-Reporte-Filas: 0`, tabla vacía |
| R14 | Cantidad de filas | Filas = personas × días con asignación | Compare con la pestaña Cronograma del proyecto |
| R15 | CARGO INFOR | Revisar la columna N | Solo trae código quien tenga `CodigoInfor` asignado; el resto, vacío y contado en `personasSinCodigoInfor` |

`Reporte.xlsx` y los `resultado-*.txt` quedan en esta carpeta: no los suba al repositorio (contienen datos del proyecto).

## Modo simulación (sin red)
- `set SIMULAR=1` antes del script.
- `resumen.cmd` lee `simulacion\resumen.json`.
- `reporte.cmd` solo muestra `simulacion\reporte_encabezados.txt`: **no descarga ningún archivo**.
- Los dos archivos de `simulacion\` son **FICTICIOS**. La URL base se fuerza a `https://localhost:1`.
