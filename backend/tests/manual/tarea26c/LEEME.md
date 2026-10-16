# TAREA-26c (Fase B1) — Análisis de horarios del ERP

Lo ejecuta el **usuario**. El script **solo lee**: hace un único `GET /api/erp/horarios` a **nuestra API** y no escribe en la base ni llama al ERP directamente.
Su resultado sirve como evidencia para decidir, en la Fase B2, el filtro de horarios no laborales (pendiente 41) y la regla de los turnos que cruzan la medianoche (pendiente 42).

> **Lo que ve la aplicación, no el ERP crudo.** El endpoint ya:
> - deja solo los horarios con `status = "A"` (los inactivos no aparecen);
> - convierte las horas inválidas del ERP en nulas, por ejemplo `"2400"` (`ReglasErp.AHora`).
>
> El DTO no trae el texto original de la hora. Por eso una **salida nula con entrada** se marca `SALIDA_NULA(posible 2400)`.

## Archivos
| Archivo | Qué es |
|---|---|
| `horarios-analisis.cmd` / `.ps1` | El script. Reutiliza `..\tarea26a\comun.ps1` |
| `simulacion\horarios.json` | Respuesta **ficticia** (códigos 91xx) para el modo simulación; se versiona |
| `resultado-horarios-analisis.txt` | Salida. Está en `.gitignore` |

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
