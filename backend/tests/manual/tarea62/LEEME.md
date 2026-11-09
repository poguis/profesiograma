# TAREA-62 — Sondeo de familias y grupos de EvolutionEmployee (solo agregados)

`sondeo-familias.cmd` lo ejecuta **usted**; Claude Code no lo ejecuta. Sirve para confirmar qué campo del ERP equivale al «Grupo» del original (`ESTRUCTURA GENERAL`, `Grupo = "ADMINISTRATIVO"`, FASE_1 §2.3 y §2.7) antes de la Fase B. No usa la API de PROFESIOGRAMA ni la base: solo la API de empleados del ERP.

| Archivo | Qué hace |
|---|---|
| `sondeo-familias.cmd` / `.ps1` | POST a `EvolutionEmployee/EmployeesEvolution` (ERP real, cuerpo fijo de FASE_4 §2) y **solo agregados**. Escribe `resultado-familias.txt` |
| `simulacion\empleados.json` | Respuesta **FICTICIA** para el modo simulación |

Usa las funciones comunes de `..\tarea26a\comun.ps1` (como el M1 de la TAREA-22-1). Muestra al inicio **MODO REAL** o **MODO SIMULACION**.

## Cómo ejecutarlo
1. VPN conectada (el ERP `backstack.sedemi.com:7048` no tiene autenticación).
2. Doble clic en `sondeo-familias.cmd`, o desde una consola en esta carpeta: `sondeo-familias.cmd`.
3. Tarda lo que la API de empleados (~2 s; tiempo máximo 180 s, ajustable con `set TIMEOUT_SEGUNDOS=…`).
4. **Pegue en el chat el contenido de `resultado-familias.txt`** (o lo que muestra la consola). Solo trae agregados.

Simulación (sin red): `set SIMULAR=1` y luego `sondeo-familias.cmd`. Lee `simulacion\empleados.json` y fuerza la URL a `https://localhost:1`.

## Qué imprime (y nada más)
1. Total de activos (`estado = "A"`).
2. Los **nombres** de las claves de primer nivel (unión de los registros activos; nunca valores).
3. Para `familiaPuesto`, `cargoTipo` y `posicion` (si existen) y cada clave cuyo nombre contenga «grupo», «familia», «tipo» o «clase» (sin distinguir mayúsculas):
   - distintos «tal cual» y distintos normalizados (recortar, colapsar espacios, sin tildes ni mayúsculas, como el buscador);
   - vacíos o nulos;
   - con **40 distintos o menos**, cada valor con su conteo. Un valor con **menos de 5 personas no se imprime**: se agrupa como «(otros: k valores, n personas)».
4. Para cada valor impreso de `familiaPuesto`: cuántos departamentos distintos y cuántas empresas distintas (solo el número).

**Protecciones:**
- Nunca imprime nombres, cédulas, correos ni códigos de empleados. La respuesta solo vive en memoria.
- Una clave cuyo nombre sugiere un dato personal (sangre, sexo, género, estado civil, discapacidad, etnia, religión, identificación, cédula, documento, salario, BPR, nacimiento) solo muestra sus conteos, nunca sus valores.

## Qué se decide con el resultado
- **Confirma `familiaPuesto`:** existe, tiene pocos valores (≤ 40 normalizados), entre ellos uno equivalente a ADMINISTRATIVO con una parte de los activos (no casi todos ni casi ninguno), con pocos vacíos y repartido en varios departamentos.
- **Lo refuta:** no existe, casi todo está vacío, no aparece ADMINISTRATIVO, tiene cientos de valores (parece un puesto) o hay otra clave (por ejemplo `cargoTipo` o un «grupo») con un valor ADMINISTRATIVO más coherente.

---

# TAREA-62-1 — Criterio de personal elegible (backend): `criterio.cmd`

> **Ejecute criterio.cmd una sola vez, desde una sola consola, y espere el mensaje Fin. antes de volver a empezar.**
>
> **Si el script termina con un error rojo, NO lo vuelva a ejecutar; pegue la salida completa en el chat.**

`criterio.cmd` lo ejecuta **usted**; Claude Code no lo ejecuta. Muestra al inicio **MODO REAL** o **MODO SIMULACION** y cada caso imprime `CONTROL <caso> OK|FALLA (esperado X, obtenido Y)`. Usa `..\tarea26a\comun.ps1` y la función `Caso` de `..\tarea23-2\casos.ps1`.

## Requisitos
- La API en ejecución (`https://localhost:7180`, Development, DevAuth `admin` y `gestor`), compilada con la TAREA-62-1.
- **Empleados en modo Simulado:** `ServiciosExternos:Modo = Simulado`. SIM010 es OPERATIVO **solo** en el ERP simulado (los SIM con i % 10 == 0). Si usó el ERP real por user-secrets (TAREA-26a), vuelva al simulado y reinicie la API:
  `dotnet user-secrets remove "ServiciosExternos:Modo" --project src/App.Api` (desde `backend/`).

## Qué hace (escribe en la base de desarrollo)
| Caso | Qué hace | Esperado |
|---|---|---|
| c00 | GET `/api/admin/empleados/familias` (admin); guarda el valor original | 200; aparecen ADMINISTRATIVO y OPERATIVO |
| c01 | PUT `[ADMINISTRATIVO]` (punto de partida) | 200 |
| c02 | GET `/api/empleados?texto=SIM010&soloMisDepartamentos=false` (gestor) | 200, total 0, `familiasAsignables` = [ADMINISTRATIVO] |
| c03 | POST novedad PERSONA de SIM010 (VACACIONES, un día de 2070) | 400 en `codigoEkon`: «El empleado SIM010 no pertenece a un grupo asignable.» |
| c04 | PUT `[ADMINISTRATIVO, OPERATIVO]` | 200 (imprime la hora del cambio) |
| c05 | Buscador otra vez | total 1 (efecto inmediato, sin caché) |
| c05b | Novedades activas de SIM010 ese día ANTES del POST | 0 (si hay alguna, la prueba no es limpia) |
| c06 | La misma novedad: **un solo** POST | 201 (si c05b encontró una previa, el script se detiene antes) |
| c06b | Novedades activas de SIM010 ese día DESPUÉS del POST | Exactamente 1; imprime SIEMPRE su Id (también si c06 no fue 201) y, si c06 dio 409 con 0 antes y 1 después, «DOBLE ENVÍO» |
| c07 | PUT con `valorAnterior` desfasado | 409 «Otro usuario cambió el criterio; recargue.» |
| c08 | PUT sin familias | 400 «Seleccione al menos una familia.» |
| c09 | PUT con «ZZ INEXISTENTE» y «ADMINISTRATIVO,OPERATIVO» | 400 con los dos mensajes (no existe; no puede contener comas) |
| c10a / c10b | GET y PUT como gestor | 403 |
| r00 / r01 | Restauración (siempre, también tras una falla) | Vuelve al valor original. Si el original era vacío, la API no lo acepta: lo informa como RESTAURACIÓN PENDIENTE |

Opciones: `set CORRIDA=7` (desplaza la fecha dentro de 2070 para repetir sin 409 de duplicado) y `set API_BASE=…`.

## Después de ejecutarlo
1. **Limpieza:** en el caso normal ya no hace falta: el script anula la novedad de c06 al final (`l06_anular`, con las salvaguardas de `limpiar.ps1`: 2070, origen PROFESIOGRAMA y no anulada). Solo si `l06_anular` falla, use el comando que imprime: desde `..\tarea23-2`, `set IDS=<Id>`, `set CONFIRMAR=1` y `limpiar.cmd`.
2. **Auditoría (a mano, solo lectura, en SSMS sobre PROFESIOGRAMA_DEV):**
   ```sql
   SELECT Clave, Valor, ModificadoPorId, FechaModificacion
   FROM dbo.Parametro WHERE Clave = 'EMPLEADO_FAMILIAS_ASIGNABLES';
   ```
   `ModificadoPorId` = el Id del admin de DevAuth y `FechaModificacion` (UTC, en segundos) posterior a la hora que imprimió c04 (hora local de Ecuador + 5 h). El valor debe ser el original (restaurado).
3. Pegue en el chat `resultado-criterio.txt` (solo códigos HTTP, familias, conteos, códigos y mensajes).

Simulación (sin red): `set SIMULAR=1` y `criterio.cmd`. Lee `simulacion\<caso>.json` y `.codigo` (ficticios) y fuerza la URL a `https://localhost:1`.

## Cambios tras la verificación del 28/10/2026 (diagnóstico de la TAREA-62-1)
Síntomas: c06 recibió 409 «Ya existe…» con un código creado ese mismo día, y c00 mostró «ADMINISTRATIVO, OPERATIVO» como valor original.
- **Un solo POST en c06:** ya era una sola llamada (`Caso` → `Solicitar` → `Invocar-Http`, sin reintentos). Ahora cada solicitud usa una conexión nueva (`ConnectionLeaseTimeout = 0`) y sin `Expect: 100-continue`, para que `HttpWebRequest` no pueda reenviar el POST por una conexión keep-alive reutilizada [SUPUESTO sobre la causa]. Los casos **c05b** (antes) y **c06b** (después) cuentan las novedades activas de SIM010 de ese día: si hay 0 antes y 1 después pero c06 dio 409, el script imprime «DOBLE ENVÍO».
- **Valor original siempre en vivo:** se lee en c00 y se guarda en `resultado-estado-criterio.txt` **solo** para restaurar si la corrida falla; se borra al restaurar con éxito (o si el valor ya estaba vigente). Si el archivo existe al empezar, una corrida anterior no restauró: aparece una **ADVERTENCIA** con los dos valores y al final se restaura el guardado entonces. c01 usa siempre el valor en vivo como `valorAnterior`.
- **Valor distinto de la semilla:** si el criterio en vivo no es `ADMINISTRATIVO`, aparece una **ADVERTENCIA** visible (no se acepta en silencio).
- **Id siempre:** c06 imprime el Id de la respuesta y, si no fue 201, los Id que muestra c06b para la limpieza.
- **CORRIDA:** por defecto son los minutos desde el 01/10/2026 módulo 300 (se repite cada 5 horas: dos corridas en horas «múltiplos de 5 h» usan la misma fecha). Para repetir, use `set CORRIDA=` con un número no usado.

## Cambios del diagnóstico 2 (28/10/2026)
Evidencia del usuario: la novedad del 409 se creó 5–9 s **antes** de la corrida pegada, el log mostró 4 PUT para «una» ejecución y c00 leyó un valor distinto del de la base. Todo apunta a **dos ejecuciones superpuestas**. `criterio.cmd` hace una sola llamada a PowerShell (línea 6, sin `start`, `call`, bucles ni reintentos), igual que los `.cmd` de la tarea23-2 y la tarea26a: una invocación no puede producir dos ejecuciones. Además, `CORRIDA` persiste en la consola si se hizo `set CORRIDA=…`: dos ejecuciones en esa consola usan la misma fecha.
- **Candado:** al empezar, `resultado-criterio.lock` (hora de inicio y PID) se crea de forma atómica antes de abrir el resultado y de llamar a la API. Si ya existe y tiene menos de 5 minutos, el script se detiene con «Ya hay una ejecución de criterio.cmd en curso (o terminó mal hace menos de 5 minutos). No se ejecuta nada.» y no llama a la API. Se borra al terminar, también tras una falla. Si quedara uno de más de 5 minutos (ejecución cortada), se descarta solo.
- **Horas:** antes de cada caso se imprime la hora local con milisegundos (`[HH:mm:ss.fff]`). La respuesta del POST de c06 no trae la hora de creación (solo `id`, `codigo` y `version`): se usa la `fechaCreacion` (UTC) que imprime c06b.
- **Proceso:** al inicio se imprimen el PID y el proceso padre (p. ej. `cmd (PID …)`), y de dónde sale la corrida (variable `CORRIDA` de la consola o reloj).
- Se mantienen: un solo POST (conexión nueva), c05b y c06b, el archivo de estado y la advertencia si el criterio no es ADMINISTRATIVO.

## Corrección tras la corrida 160 (28/10/2026)
**Causa de la caída** («referencia circular al serializar … PSParameterizedProperty», `tarea23-2\casos.ps1` línea 12): en la restauración (r01) la lista de familias se armaba con un pipeline (`Split | ForEach-Object | Where-Object`). En PowerShell 5.1 cada elemento sale como un `PSObject` que envuelve la cadena, y `JavaScriptSerializer` lo recorre por sus propiedades hasta caer en una referencia circular. c01 y c04 funcionaban porque usan literales (`@($Admin)`), que son cadenas puras. Por eso la primera ejecución de cada par se caía en la restauración y dejaba el criterio en «ADMINISTRATIVO, OPERATIVO», y las siguientes lo tomaban como «original».
- **Cuerpo plano:** `Guardar` convierte siempre las familias a `[string[]]` y `valorAnterior` a `[string]`; el cuerpo es una hashtable plana.
- **Ninguna caída pasa inadvertida:** un `try/catch` general imprime «ERROR NO CONTROLADO: {mensaje} (línea n)». El `finally` siempre restaura, anula la novedad de c06, imprime «Casos con FALLA: n; OMITIDOS: m» y «Fin.», y borra el candado.
- **Restauración en su propio try/catch:** si falla, imprime «RESTAURACIÓN PENDIENTE: el criterio quedó en [x] y debe quedar en [y]» con la sentencia SQL de abajo, conserva el archivo de estado y el código de salida es 2.
- **Banner** «EJECUCIÓN INCOMPLETA: no repita el script sin leer este mensaje» si hubo una excepción, una restauración pendiente o una detención en c05b. Códigos de salida: 0 = todo OK; 1 = algún CONTROL FALLA; 2 = incompleta.
- **c05b se detiene:** si ya hay una novedad activa de SIM010 ese día, imprime «Ya había una novedad de SIM010 ese día (id N, creada a las HH:mm:ssZ): hay una ejecución previa con la misma CORRIDA. Limpie y use otra CORRIDA.» y no envía c06. El criterio que cambiaron c01 y c04 se restaura en el `finally`.
- **Autolimpieza (`l06_anular`):** si c06 devolvió 201, al final se anula esa novedad (motivo «Limpieza automatica de prueba TAREA-62-1»).

**Restauración manual** (solo si el script lo pide; reemplace `<valor>` por el que indica, normalmente `ADMINISTRATIVO`):
```sql
UPDATE dbo.Parametro SET Valor = N'<valor>', ModificadoPorId = 1, FechaModificacion = SYSUTCDATETIME() WHERE Clave = 'EMPLEADO_FAMILIAS_ASIGNABLES';
```
Después, borre `resultado-estado-criterio.txt` de esta carpeta.
