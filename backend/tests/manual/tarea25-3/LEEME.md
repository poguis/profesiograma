# TAREA-25-3-1 — Cargos Infor (backend): `cargos.cmd`

> **Reinicie la API** con la compilación de la TAREA-25-3-1 (endpoints nuevos). **Ejecútelo una sola vez, desde una sola consola, y espere el mensaje Fin.** Si termina con un error rojo o con «EJECUCIÓN INCOMPLETA», **no lo repita**: pegue la salida completa en el chat.

`cargos.cmd` lo ejecuta **usted**; Claude Code no lo ejecuta. Muestra al inicio **MODO REAL** o **MODO SIMULACION** y cada caso imprime `CONTROL <caso> OK|FALLA (esperado X, obtenido Y)` con la hora local en milisegundos. Usa `..\tarea26a\comun.ps1` y `Caso` de `..\tarea23-2\casos.ps1`, con el patrón corregido de `..\tarea62\criterio.ps1`: candado, archivo de estado, `try/catch` general, restauración protegida, banner y cuerpos solo con tipos planos (`[string]`, `$null`, `[bool]`, hashtables).

## Requisitos
- La API en ejecución (`https://localhost:7180`, Development, DevAuth `admin` y `gestor`), **reiniciada** con la TAREA-25-3-1.
- Corre en el **modo de empleados actual** (Http por user-secrets). La importación lee la API real: los puestos no son datos personales.

## Qué escribe en la base de desarrollo
- **Orden fijo:** c00 lista → c01 vista previa → c02 importar → c03 importar otra vez → **después** elige el candidato → c04–c09 → c10 gestor → r00 restauración. Al inicio imprime el modo esperado (Http) y el aviso de que la importación deja filas permanentes.
- **c02/c03 (importación):** inserta en `CargoInfor` los puestos de la API que faltan, sin código y activos. **Estas filas NO se revierten** (la API nunca borra cargos). La segunda importación no inserta nada (idempotente).
- **c04–c09:** editan **un** cargo elegido del listado DESPUÉS de importar: activo, **sin código** y **sin personas guardadas** (no afecta a ningún reporte). Al terminar queda **como estaba** (sin código y activo); la restauración corre siempre, también tras una falla.
- **Sin candidato tras c03:** el script **se detiene antes de c04** con un mensaje claro y no escribe nada más. No hay nada que restaurar (la importación no se revierte) y no se crea el archivo de estado. Termina con «EJECUCIÓN INCOMPLETA» (código 2).

## Casos
| Caso | Qué hace | Esperado |
|---|---|---|
| c00 | GET listado | 200; imprime el total de cargos activos |
| c01 | GET `/importacion` (vista previa, lista en caché) | 200 con `puestosApi`, `nuevos`, `yaExistentes`, `existentesInactivos`, `fusionados`, `gruposVariantes`, `esAnterior` |
| c02 | POST `/importacion` (lectura fresca) | 200; imprime los conteos |
| c03 | POST `/importacion` otra vez | 200 con `insertados = 0` |
| c03b_lista_pN | GET `?filtro=sinCodigo&tamano=100&pagina=N` hasta encontrar el candidato | 200 (sin candidato: se detiene antes de c04) |
| c04 | PUT código `prueba-25.3` | 200 con `codigoInfor` = `PRUEBA-25.3` (mayúsculas) |
| c05 | PUT con el anterior desfasado (sin código) | 409 «Otro usuario cambió el cargo; recargue.» |
| c06 | PUT código `P 1` | 400 en `codigoInfor` (formato) |
| c07 | PUT al Id 999999999 | 404 «El cargo no existe.» |
| c08 / c09 | Baja (conserva el código) y reactivación quitando el código | 200 / 200 (estado original) |
| c10a–d | Los 4 endpoints como gestor | 403 |
| r00 / r01 | Restauración (siempre) | Sin código y activo |

Al final: «Casos con FALLA: n; OMITIDOS: m» y «Fin.». Códigos de salida: 0 = todo OK; 1 = algún CONTROL FALLA; 2 = incompleta (incluye la detención sin candidato tras c03).

**Archivo de estado** `resultado-estado-cargos.txt`: guarda solo el Id del cargo mientras está editado; se borra al restaurar. Si existe al empezar, el script no hace nada y pide restaurar con el SQL de abajo. **Candado** `resultado-cargos.lock` (5 minutos).

## Después de ejecutarlo (SQL de solo lectura, en SSMS sobre PROFESIOGRAMA_DEV)
Reemplace `<Id>` por el del «Cargo elegido»:
```sql
SELECT Id, Cargo, CodigoInfor, Activo, CreadoPorId, FechaCreacion, ModificadoPorId, FechaModificacion
FROM dbo.CargoInfor WHERE Id = <Id>;

SELECT COUNT(*) AS Cargos, SUM(CASE WHEN CodigoInfor IS NULL THEN 1 ELSE 0 END) AS SinCodigo,
       SUM(CASE WHEN Activo = 0 THEN 1 ELSE 0 END) AS Inactivos
FROM dbo.CargoInfor;
```
Esperado: el cargo elegido con `CodigoInfor` NULL, `Activo = 1` y `ModificadoPorId` = el Id del admin (`FechaModificacion` en UTC, hora de Ecuador + 5 h). Las filas nuevas de c02 con `CreadoPorId` = el admin.

**Restauración manual** (solo si el script imprime «RESTAURACIÓN PENDIENTE»; use la sentencia que imprime, con el Id ya puesto):
```sql
UPDATE dbo.CargoInfor SET CodigoInfor = NULL, Activo = 1, ModificadoPorId = 1, FechaModificacion = SYSUTCDATETIME() WHERE Id = <Id>;
```
Después, borre `resultado-estado-cargos.txt` de esta carpeta.

Pegue en el chat `resultado-cargos.txt` (solo códigos HTTP, Id, conteos, el cargo elegido y códigos de prueba).

Simulación (sin red): `set SIMULAR=1` y `cargos.cmd`. Lee `simulacion\<caso>.json` y `.codigo` (ficticios) y fuerza la URL a `https://localhost:1`.
