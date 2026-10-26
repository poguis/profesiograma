---
paths:
  - "backend/tests/manual/**/*"
---

# Scripts de pruebas manuales (`backend/tests/manual/`)

Las reglas críticas (nunca llamar a la API, nunca ejecutar estos scripts, nunca leer `resultado-*.txt`) están en `CLAUDE.md`.
Este archivo guarda el detalle de cómo se escriben y simulan los scripts.

## Estructura de una carpeta `tareaXX/`
- `LEEME.md`: quién lo ejecuta (el usuario, con la API en marcha), requisitos, modos, tabla de casos (caso → qué hace → esperado) y salida.
- `<nombre>.cmd` (lanzador) + `<nombre>.ps1` (lógica). Funciones compartidas en un `.ps1` aparte (p. ej. `casos.ps1` o `comun.ps1`).
- `simulacion\`: respuestas ficticias por caso (`<caso>.json` con el cuerpo y `<caso>.codigo` con el estado HTTP).
- Plantillas de datos solo ficticias (`*.ejemplo.json`). Los `crear-proyecto-*.json` que completa el usuario no se leen.

## Modos y salida
- El script muestra al inicio, de forma visible, «MODO SIMULACION» o «MODO REAL». Real por defecto; simulación con `set SIMULAR=1`
  (lee `simulacion\`, nunca invoca curl) o con una URL base inválida (`https://localhost:1`).
- Nunca depender del orden de búsqueda de ejecutables (en este equipo `NoDefaultCurrentDirectoryInExePath=1`: un `curl.cmd` en
  la carpeta actual NO reemplaza a `curl.exe`).
- Cada caso imprime `CONTROL <caso> OK|FALLA (esperado X, obtenido Y)` u `OMITIDO (requisito <n> no creado)`; un OMITIDO no
  llama a la API con datos vacíos. Al final: «Casos con FALLA: n; OMITIDOS: m».
- `resultado-*.txt`: solo códigos HTTP, conteos, códigos (PRY-, NOV-), Id, versiones y mensajes de validación; nunca nombres,
  correos ni cédulas.

## Simulación por Claude Code
- Solo en una COPIA de los scripts en el scratchpad, nunca dentro de `backend/tests/manual/` (la simulación escribe
  `resultado-*.txt` y puede pisar o mezclarse con los archivos del usuario).
- En simulación, solo plantillas ficticias (`*.ejemplo.json` o la carpeta `simulacion\`).
- Para comprobar que un archivo del usuario existe o no cambió: solo fecha y tamaño.
- Origen de estas reglas: incidentes de la TAREA-17 (`docs/tareas/TAREA-17-reporte.md` §6.5), TAREA-26d-3 (`retiro.ps1`) y
  TAREA-26c (`docs/tareas/TAREA-26c-reporte.md` §6.1).
