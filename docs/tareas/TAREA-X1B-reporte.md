# TAREA-X1-B — Reporte: eficiencia de contexto (documentación y configuración de Claude Code)

**Fecha:** 26/10/2026 · **Fase:** B (implementación del plan X1 aprobado) · **Estado:** implementada. Sin cambios en `backend/` ni `frontend/`.

## 1. Qué se hizo
- **graphify:** sin referencias en `CLAUDE.md` ni en `.claude/settings.json` (el usuario ya había ejecutado `graphify claude uninstall`; el hook quedó como `"PreToolUse": []`). `.claude/CLAUDE.md` contenía solo la sección graphify: quedó **vacío**.
- **B1:** `docs/00_ESTADO_ACTUAL.md` partido en el estado vigente (157 líneas, 24 928 B) y `docs/HISTORIAL.md` nuevo (198 líneas, 73 509 B; índice H1–H8 y fecha de partición). Las líneas se movieron con `sed` desde un respaldo, sin reescribirlas.
- **Vigente:** cabecera corta con tabla por área (incluye la 24a ✅ del 26/10/2026, la 23-3 ✅ con N13 cerrado y el objetivo de aprendizaje del usuario); **Índice de documentos** (documento → cuándo → secciones) y lecturas por tipo de tarea; §2 Alcance, §3 Decisiones y §4 Entorno textuales; §5 Hoja de ruta de lo que falta (24b siguiente, 24c–24e, criterio de personal, 23-4, 25, Fase 3/7) y la decisión del 29/09; §7 pendientes abiertos; §7.1 reglas técnicas textuales.
- **Pendientes:** 7, 62 y 63 reescritos según el prompt (el texto anterior va textual a HISTORIAL H6); 64 **cerrado** (texto original y nota de cierre en H5); **65** (entornos y despliegue) y **66** («anular ausentes» para la 23-4) nuevos.
- **B2:** `CLAUDE.md` de 95 a 65 líneas. Sin graphify; las reglas de git, base de datos, datos sensibles (incluidos los CSV de NOVEDADES ASISTENCIA) y la regla crítica de API, scripts y `resultado-*.txt` se quedan; flujo Fase A / Fase B; lectura por Índice (el `.sql` solo si se toca el modelo); regla de Grep y Glob; regla de reportes. El detalle de las pruebas manuales pasó a `.claude/rules/pruebas-manuales.md` con `paths: "backend/tests/manual/**/*"` (formato de [memory: path-specific rules](https://code.claude.com/docs/en/memory)). Nueva `docs/tareas/PLANTILLA_REPORTE.md`.
- **B4:** `.claude/settings.json` con `permissions.deny`: `Read(./docs/origen/sharepoint/muestras/**)`, `Read(**/muestras/**)` y `Read(**/resultado-*.txt)` (sintaxis gitignore, [settings](https://code.claude.com/docs/en/settings) y [permissions](https://code.claude.com/docs/en/permissions)). `docs/origen/powerapps` y `sharepoint/esquemas` siguen legibles (no hay otra carpeta `muestras` en `docs/`). JSON validado con `node`.
- `FASE_5_Novedades.md`: fila TAREA-24 → «24a ✅ verificada por el usuario (26/10/2026); 24b–24e pendientes».

## 2. Archivos tocados
| Archivo | Antes | Después |
|---|---|---|
| `CLAUDE.md` | 95 lín · 7034 B | 65 lín · 5452 B |
| `.claude/CLAUDE.md` | 3 lín · 226 B (solo graphify) | 0 B |
| `docs/00_ESTADO_ACTUAL.md` | 261 lín · 90 177 B | 157 lín · 24 928 B |
| `docs/HISTORIAL.md` (nuevo) | — | 198 lín · 73 509 B |
| `.claude/rules/pruebas-manuales.md` (nuevo) | — | 33 lín · 2234 B |
| `docs/tareas/PLANTILLA_REPORTE.md` (nuevo) | — | 29 lín · 973 B |
| `.claude/settings.json` | 4 lín · 45 B | 12 lín · 201 B |
| `docs/fases/FASE_5_Novedades.md` | — | 1 fila |

## 3. Comprobaciones
| Comprobación | Resultado |
|---|---|
| Cada línea no vacía del estado original (233, sin contar separadores) existe textual en el vigente o en HISTORIAL | **0 perdidas** (comparación línea por línea con `grep -qxF` contra el respaldo) |
| Bytes: original 90 177 → vigente 24 928 + historial 73 509 | La suma crece por el índice, los encabezados H y los textos nuevos (pendientes 62, 63, 65, 66, cierre del 64) |
| Grep de `graphify` en `CLAUDE.md`, `.claude/CLAUDE.md`, `.claude/settings.json` y `.claude/rules/` | Sin coincidencias |
| `.claude/settings.json` | JSON válido |

## 4. Medición base (tokens ≈ bytes/4 [SUPUESTO])
| Archivo | Antes | Después |
|---|---|---|
| `CLAUDE.md` + `.claude/CLAUDE.md` (siempre en contexto) | 7260 B · ~1815 tok | 5452 B · ~1363 tok |
| `docs/00_ESTADO_ACTUAL.md` (se lee en cada tarea) | 90 177 B · ~22 544 tok | 24 928 B · ~6232 tok |
| `docs/HISTORIAL.md` (solo a pedido) | — | 73 509 B · ~18 377 tok |
| `.claude/rules/*` (solo al tocar `backend/tests/manual/`) | — | 2234 B · ~558 tok |
| **Lectura inicial por tarea** | **~24 359 tok** | **~7595 tok (−69 %)** |

## 5. Desviaciones y decisiones
- **Pendientes al historial: 23 (más el 64), no 22.** Se movieron 3, 14, 17, 22, 23, 25, 26, 28, 29, 30, 31, 32, 33, 34, 35, 40, 41 y 42 (cerrados), 51, 52 (decidido e implementado), 55, 56 y 57.
- **Quedan en el vigente por dudosos:** **10** (Smart App Control resuelto, pero «Pendiente para TI: evaluar App Control for Business»); **20** (503 del ERP verificado, pero «el 503 de registro ocupado sigue sin prueba visual»); **53** y **54** ([SUPUESTO] aprobado «hasta que Infor confirme»).
- **§5 «Paso 2» y la tabla «1. Documentos del proyecto» van al historial** (H3, H2): el prompt no los listaba en el vigente. La tabla se reemplazó por el Índice de documentos.
- La numeración del vigente salta de §5 a §7: se conservaron textuales los encabezados «7. Pendientes abiertos» y «7.1 Reglas técnicas» para no romper los enlaces a §7.1 de `docs/fases/`.
- `CLAUDE.md`: la regla de las muestras pasó de «solo lectura» a «no se lee en las tareas», coherente con el deny de B4. Se quitó `npm install` (no lo ejecuta Claude Code). El origen de las reglas de los scripts (TAREA-17, 26d-3, 26c) quedó en el archivo de reglas.
- La regla con `paths` se carga cuando Claude lee, escribe o edita un archivo de esa carpeta: por eso lo crítico sigue en `CLAUDE.md`.
- La fila «TAREA-24a» del §9 anterior (H8) sigue diciendo «pendiente de verificación» (textual); la nota del índice de HISTORIAL y el estado vigente dicen ✅.

## 6. Enlaces
- **Vivos:** `CLAUDE.md` y `docs/fases/` solo citan `00_ESTADO_ACTUAL.md §7.1` (FASE_5_Estados_Proyecto:237, FASE_5_Edicion_Cronograma:142, FASE_5_Edicion_Cabecera:119, FASE_5_Cronograma:400): **siguen válidos**.
- **Reportes con enlaces viejos (no se reescribieron):**
  - Citan §5, §6, §8 o §9 del estado anterior, o pendientes ahora en H5: TAREA-02, 03, 04, 05, 07, 09, 13, 15, 18b, 20, 26c y 26d-1 (los que citan §7 o §7.1, como 12, 17, 17b y 19x, siguen válidos).
  - Citan la «hoja de ruta» o la «cabecera»: TAREA-22c, 23-1, 23-2 y 23-3.
  - El índice de HISTORIAL explica dónde quedó cada sección.

## 7. Pendientes
- **Restos de graphify fuera del alcance:** `.claude/skills/graphify/` (su descripción entra en la lista de skills de cada sesión), `.claude/settings.json.graphify-bak` (550 B, con el hook antiguo), `~/.claude/CLAUDE.md` y `~/.claude/skills/graphify/`. Los borra el usuario si quiere.
- Medir la próxima Fase A frente a la de la TAREA-24 (tokens de entrada, cantidad de lecturas).
- Las instrucciones del objetivo de aprendizaje del usuario las actualiza el usuario.
