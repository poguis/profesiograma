# TAREA-22-1 — Cédula en la alta puntual (D15) y «completar cédulas»: verificación manual

Los scripts los ejecuta **usted** (Claude Code no los ejecuta contra la API ni contra el ERP). Requisitos: VPN, la API en ejecución (`https://localhost:7180`, DevAuth) y SSMS sobre `PROFESIOGRAMA_DEV`.

| Archivo | Qué hace |
|---|---|
| `sondeo-cedula.cmd` / `.ps1` | M1: POST a EvolutionEmployee (ERP real) y **solo agregados** de `cedula`. Escribe `resultado-cedula.txt` |
| `completar.cmd` | POST `/api/empleados/completar-cedulas`. **Escribe** en `Empleado.Cedula`. Imprime solo el JSON de conteos y el código HTTP |
| `conteos-cedula.sql` | M2 / M5: agregados de `Empleado.Cedula` (nunca valores). Solo lectura |

Los dos scripts muestran al inicio **MODO REAL** o **MODO SIMULACION**. Ninguno muestra cédulas, nombres ni códigos.

## Matriz

| # | Caso | Cómo | Esperado |
|---|---|---|---|
| M1 | Sondeo de `cedula` | `sondeo-cedula.cmd` en MODO REAL | Usted pega **solo los agregados** (tipos, vacíos, largos, 10 dígitos, no dígitos, con "0", repetidos) |
| M2 | Línea base | `conteos-cedula.sql` antes de completar | Anote `SinCedula` y `NoDevSinCedula` |
| M3 | Gestor | `set DEV_USER=gestor` y `completar.cmd` | `HTTP 403`; M2 sin cambios |
| M4 | Simulado, admin | API en modo Simulado (`ServiciosExternos:Modo = Simulado`); `set DEV_USER=admin` y `completar.cmd` | `completadas` = filas NULL de empleados SIM ya guardados; las filas con códigos reales cuentan en `noEncontradasEnApi`. **Los DEV no cuentan:** el sembrador ya les puso su cédula ficticia ("99999999" + n) y nunca se sobrescribe (decisión del 20/10/2026) |
| M5 | Después | `conteos-cedula.sql` | `SinCedula` de M2 − `completadas` de M4 |
| M6 | Segunda llamada | `completar.cmd` otra vez | `completadas` = 0 (idempotente) |
| M7 | ERP no disponible | `dotnet user-secrets set "ServiciosExternos:ErpBase7048" "https://localhost:1" --project src/App.Api` (como la C4 de la 26d-3), modo Http, reiniciar la API → `completar.cmd` | `HTTP 503` «Servicio de empleados no disponible»; `conteos-cedula.sql` sin cambios. Restaurar con `dotnet user-secrets remove "ServiciosExternos:ErpBase7048" --project src/App.Api` y reiniciar |
| M8 | MODO REAL (Http) | `completar.cmd` con el ERP real | Las filas NULL que quedan (`conteos-cedula.sql`) = `sinCedulaEnApi` + `noEncontradasEnApi` |
| M9 | Persona nueva | Crear un proyecto con una persona que no tenga fila (p. ej. `tarea26d\crear-proyecto.cmd`) | Su fila queda con cédula (`conteos-cedula.sql`: `ConCedula` sube en 1). En las herramientas del navegador (Red), `GET /api/proyectos/{id}` y `GET /api/empleados` **no** contienen "cedula" |

## Modo simulación (sin red)
- `set SIMULAR=1` antes del script.
- `completar.cmd` lee `simulacion\completar_<DEV_USER>.json` y nunca llama a curl.
- `sondeo-cedula.cmd` lee `simulacion\empleados.json`. La URL base se fuerza a `https://localhost:1`.
- Todos los archivos de `simulacion\` son **FICTICIOS**: cédulas inventadas que empiezan en "00".

## Qué no se verifica aquí
- El reporte Excel (TAREA-22-2) y el 503 sin base de datos (TAREA-22c).
