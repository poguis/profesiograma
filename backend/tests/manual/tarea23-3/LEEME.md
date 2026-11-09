# TAREA-23-3 — verificación manual: sincronización de PERMISO MEDICO

`sincronizacion.cmd` lo ejecuta **el usuario** con la API en marcha (`https://localhost:7180`, Development, DevAuth). Claude Code no lo ejecuta.

**Requisito:** `NovedadesExternas:Modo = Simulado`, que ya está en `appsettings.Development.json`. El script usa **solo la fuente simulada**: 10 filas **ficticias** `NOV-SIMPM-0001…0010`, año 2075, empleados DEV001–DEV006 (tienen fila en `Empleado`, así que funciona también con el ERP en modo Http). **No usa ningún CSV real.** Si la fuente no es la simulada, muestra un aviso y deja OMITIDOS los casos de la fuente.

- **MODO REAL** (por defecto): llama a la API. **MODO SIMULACION:** `set SIMULAR=1` (sin red; respuestas ficticias de `simulacion\`).
- Cada caso imprime `CONTROL <caso> OK|FALLA|OMITIDO`. Al final: «Casos con FALLA: n; OMITIDOS: m».
- Salida: `resultado-sincronizacion.txt` (solo códigos HTTP, conteos, códigos NOV-, Id y versiones; nunca nombres, correos ni cédulas).
- **Escribe en la base de desarrollo** (las 6 novedades válidas de la fuente simulada) y **al final las deja anuladas** (s11).

## Casos
| Caso | Qué hace | Esperado |
|---|---|---|
| s00 | `escenario=anuladas`: normaliza el punto de partida | 200, fuente «Simulado» |
| s01 | `escenario=base` | nuevas + reactivadas = 6; omitidas: `tipoNoSoportado` 1, `jsonInvalido` 1, `sinEmpleado` 1, `otroOrigen` 1; ausentes ≥ 1 (NOV-DEV-0004 del sembrador) |
| s02a / s02 / s02b | Versión de NOV-SIMPM-0002; `base` otra vez; versión otra vez | s02: 6 sin cambios y 0 nuevas/actualizadas/anuladas/reactivadas; **s02b: la versión no cambia** (sin cambios no hay UPDATE) |
| s03 / s04 | `fechas` (NOV-SIMPM-0003 pasa a 3 días) / `base` (vuelve a 1) | 1 actualizada y 5 sin cambios, cada una |
| s05 / s06 | `anuladas` (ESTADO no ACTIVA en el origen) / `base` | 6 anuladas / 6 reactivadas |
| s07 / s08 | Sincronizar como gestor / sin sesión | 403 / 401 |
| s09a / s09b | Listado `origen=PERMISOS_MEDICOS&texto=NOV-SIMPM` como admin / gestor | total 6 / total 0 |
| s10 | El admin edita NOV-SIMPM-0001 | 400 `origen` (solo lectura) |
| s11 | `anuladas` (limpieza) | 6 anuladas: no quedan PERMISOS_MEDICOS de prueba activas |

## Fuente ArchivoLocal (opcional, solo Development)
Para probar con un CSV exportado de la lista NOVEDADES ASISTENCIA: `NovedadesExternas:Modo = ArchivoLocal` y la ruta en **user-secrets** (`NovedadesExternas:RutaArchivo`). **Nunca en el repo:** el archivo contiene permisos médicos. Este script no cubre ese modo (el escenario solo existe en la fuente simulada).
