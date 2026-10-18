# TAREA-26c — Horarios del ERP: análisis (B1) y verificación (B2)

Los ejecuta el **usuario**. Los dos scripts **solo leen**: llaman a **nuestra API**, no escriben en la base y no llaman al ERP directamente.
- `horarios-analisis.cmd` (B1): un único `GET /api/erp/horarios`. Fue la evidencia para decidir el filtro de horarios no laborales (pendiente 41) y la regla de los turnos que cruzan la medianoche (pendiente 42).
- `verificacion.cmd` (B2): vistas previas que comprueban esas reglas (sección «Verificación B2»).

Desde la B2 la lista ya **excluye los no laborales**. Por eso el análisis muestra solo los seleccionables.

> **Lo que ve la aplicación, no el ERP crudo.** El endpoint ya:
> - deja solo los horarios con `status = "A"` (los inactivos no aparecen);
> - convierte las horas inválidas del ERP en nulas (`ReglasErp.AHora`). Desde la B2, `"2400"` se lee como 00:00 (fin del día).
>
> El DTO no trae el texto original de la hora. Por eso una **salida nula con entrada** se marca `SALIDA_NULA(posible 2400)`; desde la B2 indica otra hora ilegible.

## Archivos
| Archivo | Qué es |
|---|---|
| `horarios-analisis.cmd` / `.ps1` | B1: análisis de la lista. Reutiliza `..\tarea26a\comun.ps1` |
| `verificacion.cmd` / `.ps1` | B2: verificación con vistas previas |
| `simulacion\horarios.json` | Respuesta **ficticia** (códigos 91xx) para el análisis en simulación; se versiona |
| `simulacion\v_*.json` + `.codigo` | Respuestas **ficticias** de la verificación en simulación; se versionan |
| `resultado-horarios-analisis.txt`, `resultado-verificacion.txt` | Salidas. Están en `.gitignore` |

## Ejecución
1. API corriendo. Para ver los horarios reales, en **modo Http**: ver `..\tarea26a\LEEME.md`, «Activar y desactivar el ERP real».
2. Opcional:
   - `set DEV_USER=admin` (por defecto `gestor`);
   - `set API_BASE=…` (por defecto `https://localhost:7180`).
3. Ejecute `horarios-analisis.cmd`.
4. Pegue en el chat `resultado-horarios-analisis.txt`. Son datos de catálogo, sin datos personales.

Simulación, sin red: `set SIMULAR=1` antes de ejecutar. Lee `simulacion\horarios.json` (o la carpeta de `%SIMULACION%`) y usa la URL `https://localhost:1`.

## Salida
Una línea por horario: código | descripción | entrada | salida | min jornada (`horas` del ERP) | min trabajados (`horasTrab`) | tipo | marcas.

| Marca | Significado |
|---|---|
| `CRUZA_MEDIANOCHE` | salida < entrada (p. ej. 22:00–06:00 o 16:00–00:00) |
| `ENTRADA_IGUAL_SALIDA` | entrada = salida (ambas con valor) |
| `TRAB_CERO_O_NULO` | minutos trabajados = 0 o nulos |
| `CAMPO_NULO` | descripción, entrada, salida, jornada, trabajados o tipo nulos o vacíos |
| `JORNADA_DIF` | jornada calculada `(salida − entrada + 24h) mod 24h` ≠ la del ERP. Con entrada = salida, el cálculo vale 0 o 24 h |
| `LISTA_NO_LABORAL` | la descripción normalizada (mayúsculas, sin tildes, un solo espacio) es **igual** a LIBRE, FERIADO, VACIO, VACACIONES, PERMISO MEDICO o CALAMIDAD DOMESTICA |
| `LISTA_PARCIAL` | (informativa) contiene una de esas palabras sin ser igual, p. ej. «DIA LIBRE». Sirve para ver si la lista exacta se queda corta |
| `SALIDA_NULA(posible 2400)` | entrada con valor y salida nula |

Totales al final:
- total de horarios;
- cantidad por marca y por tipo (M/D);
- dos matrices de 2×2: `LISTA_NO_LABORAL` frente a `TRAB_CERO_O_NULO` y frente a `ENTRADA_IGUAL_SALIDA`.

**Cómo leer las matrices.** Una regla por datos separa bien los no laborales solo si las celdas «si/no» y «no/si» valen **0**.

## Verificación B2 (`verificacion.cmd`)
Hace solo **vistas previas**, que no escriben:
- `POST /api/proyectos/previsualizar`;
- `POST /api/proyectos/{id}/cabecera/previsualizar`.

La base de «crear» es `..\tarea26d\crear-proyecto-1.json` (el de la V3 de la 26d) o el archivo de `set BASE_CREAR=…`. El script solo cambia el horario y el almuerzo.

**Antes de ejecutar:**
1. Aplique la migración `ParametroHorariosNoLaborales` (inserta el `Parametro` 11 `HORARIOS_NO_LABORALES`; desde `backend/`):
   ```
   dotnet ef database update --project src/App.Infrastructure --startup-project src/App.Api --context ProfesiogramaDbContext --configuration Release
   ```
2. Reinicie la API.
3. Elija el perfil según el modo de la API:
   - `set PERFIL=http` (por defecto): ERP real, con los códigos de la B1;
   - `set PERFIL=simulado`: modo Simulado.
4. `set PROYECTO_ID=<Id>`: un proyecto **ACTIVO** con almuerzo diurno (p. ej. 13:00–14:00). Por defecto, 14.

| Caso | http | simulado | Esperado |
|---|---|---|---|
| Crear con horario no laboral | 1 (LIBRE) | 4 (PATERNIDAD PROYECTOS) | 400 en `horarioCodigo`: «El horario no es laborable o no está activo.» |
| Cabecera: elegir ese horario | 1 | 4 | 400 en `horarioCodigo` |
| Crear nocturno con almuerzo dentro | 73 (17:00–04:00), 01:00–02:00 | 6 (22:00–06:00), 02:00–03:00 | 200 |
| Crear nocturno con almuerzo fuera | 73, 13:00–14:00 | 6, 13:00–14:00 | 400 en `salidaAlmuerzo` y `regresoAlmuerzo`: «El almuerzo debe estar dentro del turno (…).» |
| Crear nocturno, almuerzo que cruza la medianoche | 73, 23:00–01:00 | 6, 23:00–01:00 | 400 en `regresoAlmuerzo`: «El almuerzo no puede cruzar la medianoche.» |
| Crear sin almuerzo con horario que no lo descuenta | 118 | 8 | 200 |
| Cabecera: cambiar al nocturno sin enviar almuerzo (D7) | 73 | 6 | 400 en `salidaAlmuerzo` (el almuerzo guardado queda fuera del turno) |

La salida muestra, por caso, el código HTTP, si fue el esperado (un 400 cuenta solo si trae el error en el campo esperado) y los errores por campo de la API, solo sus mensajes. Pegue en el chat `resultado-verificacion.txt`.

**Verificación en pantalla** (opcional, lo hace usted):
- «Nuevo proyecto»: los horarios no laborales no aparecen.
- Al elegir un nocturno, las opciones de almuerzo son horas del turno.
- Con 118 (o el 8 del simulado), el almuerzo se puede dejar vacío.
- «Editar datos generales» de un proyecto con horario no laboral muestra: «Este horario ya no está disponible para nuevos proyectos; se conserva si no elige otro.»

**Simulación:** `set SIMULAR=1` lee `simulacion\v_<caso>.json` y su `.codigo` (ficticios) y, como base de «crear», `..\tarea26d\crear-proyecto.ejemplo.json`, nunca su archivo.

