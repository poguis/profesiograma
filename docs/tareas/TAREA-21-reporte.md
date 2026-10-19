# TAREA-21 — Vista del cronograma (frontend)

**Fecha:** 2026-10-19 (Fase A: plan; Fase B: implementación)
**Resultado:** implementada; **pendiente de la verificación visual del usuario** (§7: V1–V15).
- `npm run build` (tsc -b + vite build): OK. La página va en un chunk aparte (`PaginaCronograma`, 32 kB, 12 kB gzip).
- `npm run lint` (oxlint): OK, 0 avisos.
- `npm test`: **264/264** = 238 anteriores sin cambios + 26 nuevas. Ninguna con tolerancia ni `it.fails`, tras la corrección de los colores (§6.1).

**Lo que no se hizo:**
- `npm install` (sin dependencias nuevas), `npm run dev`;
- llamadas a la API, SQL, `dotnet run`;
- git que modifique el repositorio;
- cambios en `backend/`.

La pestaña del detalle del proyecto queda para la TAREA-21b (P3).

## 1. Decisiones aplicadas (Fase A → B)
Las P1–P10, detalladas en `docs/fases/FASE_5_Cronograma.md` §7:
- P1: tabla HTML con sticky.
- **P2: colores dinámicos por Id**, con el código del usuario; reemplaza la paleta de 12 y la prueba lineal del plan.
- P3: TAREA-21b.
- P4: un Popover compartido.
- P5: se mantienen las semillas.
- P6: DESCANSO atenuado al 35 %.
- P7: abreviatura sin "PLANTA - ".
- P8: opciones de proyecto = respuesta + seleccionado.
- P9: persona en el cliente.
- P10: `retry: false`.

**P7, prefijos:** el único prefijo del código es `"PLANTA - "` (`NombreVisualProyecto.PrefijoDimension`), que también se usa para OFICINAS ADMINISTRATIVAS. No se revisaron los nombres reales (no se consultan datos del usuario): si aparecen otros prefijos, se agregan a `PREFIJOS_NOMBRE` en `cronograma.ts`.

## 2. Qué se hizo
**`frontend/src/features/cronograma/`:**
- **`tipos.ts`:** `Cronograma` y sus partes, copiados de `CronogramaContratos.cs`, con nulabilidad.
- **`coloresProyecto.ts`:** copiado tal cual del bloque del usuario y **corregido después a pedido suyo** (§6.1): 12 huecos del arco libre para los Id 1..12, ángulo áureo sobre el arco libre para los siguientes y margen de 18,5°.
- **`cronograma.ts`** (lógica pura):
  - fechas: `expandirDias`, `esFinDeSemana`, `inicialDia`, `mesesDelRango`, `mesDe` / `mesAnterior` / `mesSiguiente`;
  - celdas: `indexarCeldas`, `normalizarTexto`, `filtrarPersonas`, `abreviatura`, `textoCelda`, `totalTrabajo`;
  - colores: `contrasteTexto`, `atenuar`, `aparienciaCelda`;
  - detalle: `resumenCelda`, `etiquetaCelda`;
  - URL: `leerFiltrosCronograma`, `aParametrosUrl`, `aConsultaApi`, `opcionesProyecto`.
- **`api.ts`:** `useCronograma(filtros)`, con `keepPreviousData` y `retry: false`.
- **`components/`:**
  - `FiltrosCronograma` (fechas, accesos de mes, proyecto, departamento, persona con espera de 300 ms, limpiar);
  - `CuadriculaCronograma` (tabla semántica con `caption`, encabezados de mes y día con `scope`, `th scope="row"` por persona, filas `memo`, celdas `<button>` con `aria-label` y un Popover compartido);
  - `DetalleCelda` (Popover anclado a la celda; `trapFocus`; el foco vuelve a la celda al cerrar);
  - `LeyendaCronograma` (proyectos con horario y "Nocturno", novedades y estados fijos).
- **`pages/PaginaCronograma.tsx`:** filtros en la URL; errores 400 en su campo; 403/5xx/red con "Reintentar"; vacío; filtro de persona sin coincidencias.

**Otros archivos:**
- `app/App.tsx`: ruta `cronograma` con carga diferida.
- `components/MenuLateral.tsx`: entrada "Cronograma" (`CalendarLtr24Regular`).
- `features/proyectos/tipos.ts`: `Catalogos.departamentos?`. `GET /api/catalogos` ya lo devolvía, pero el tipo del frontend no lo tenía. Es opcional, para no romper los dobles de prueba existentes.

## 3. Archivos
| Archivo | Cambio |
|---|---|
| `frontend/src/features/cronograma/tipos.ts` | Nuevo |
| `frontend/src/features/cronograma/coloresProyecto.ts` + `.test.ts` | Nuevo (código del usuario, corregido) + 9 pruebas |
| `frontend/src/features/cronograma/cronograma.ts` + `.test.ts` | Nuevo + 17 pruebas |
| `frontend/src/features/cronograma/api.ts` | Nuevo |
| `frontend/src/features/cronograma/components/FiltrosCronograma.tsx`, `CuadriculaCronograma.tsx`, `DetalleCelda.tsx`, `LeyendaCronograma.tsx` | Nuevos |
| `frontend/src/features/cronograma/pages/PaginaCronograma.tsx` | Nuevo |
| `frontend/src/app/App.tsx` | Ruta `/cronograma` |
| `frontend/src/components/MenuLateral.tsx` | Entrada "Cronograma" |
| `frontend/src/features/proyectos/tipos.ts` | `Catalogos.departamentos?` |
| `docs/fases/FASE_5_Cronograma.md` | §7 "Vista (TAREA-21)" |
| `docs/00_ESTADO_ACTUAL.md` | Encabezado, §6 (21 y 21b nueva), §7 (pendientes 56 y 57), §9 |
| `docs/tareas/TAREA-21-reporte.md` | Este reporte |

## 4. Pruebas nuevas (26)
**`coloresProyecto.test.ts` (9): las 7 obligatorias, sin tolerancia, más 2.** Valores con la versión corregida:
1. Ids 1..200 con la leyenda actual: ΔE ≥ 20 frente a F, V, P/PM, CD, PT, D y CHOQUE. **Mínimo 20,14** (Id 78 frente a CD `#8E44AD`).
2. Ningún tono a menos de 18° de una novedad saturada: **mínimo 18,32°**.
3. Estabilidad: `colorProyecto(17)` es igual con y sin otros Id.
4. Leyenda con `#00B7C3`: ΔE mínimo 26,72 y tono ≥ 18°.
5. Ids 1..12 separados ≥ 20°: **mínimo 20,28°**.
6. Contraste del texto ≥ 4,5: **mínimo 4,72**.
7. `hexAHsl` / `hslAHex` ida y vuelta ±1 por canal.
8. (Nueva) Arco libre con margen de 18,5°: 5 tramos, 175° y 12 huecos distintos.
9. (Nueva) Sin colores saturados en la leyenda, todo el círculo es libre; el gris de D no ocupa tono.

**`cronograma.test.ts` (17):**
- `expandirDias` (mes, cruce de mes y de año, un día, bisiesto, rango invertido);
- `esFinDeSemana` e `inicialDia`; `mesesDelRango`; `mesDe` / `mesAnterior` / `mesSiguiente` (enero → diciembre, 31 de marzo → febrero, febrero bisiesto);
- `indexarCeldas`; `normalizarTexto`; `filtrarPersonas` (nombre sin tildes, código, vacío);
- `abreviatura` (P7);
- `textoCelda` por estado ("V", "+2", "C2", "TO", "D-EN", vacío);
- `contrasteTexto`; `atenuar`; `aparienciaCelda` por estado;
- `resumenCelda` (D2: solo la cantidad del ajeno); `etiquetaCelda`;
- URL: ida y vuelta, `persona` fuera de la API, Id no válido;
- `opcionesProyecto` (P8).

## 5. Comandos y resultados
| Comando | Resultado |
|---|---|
| `npm run build` | OK |
| `npm run lint` | OK (0) |
| `npm test` | 14 archivos; 264/264 |
| Pruebas de diagnóstico y prototipo temporales (creadas en `src/features/cronograma/` y borradas al terminar) | Valores mínimos, la causa del defecto y la medición de la propuesta del arco libre (9,93°) |

## 6. Hallazgos en `coloresProyecto.ts` y corrección
### 6.1 Versión entregada y corrección (19/10/2026)
**Versión entregada (copiada tal cual):**
- `ajustarTono` movía al borde de la zona prohibida los tonos que caían dentro.
- **Prueba 5: 10,91°** (se pedía ≥ 20°). El ángulo áureo puro daba 20,06°, pero el Id 3 pasaba de 52,5° a 61,5° (borde de P/PM), a 11,06° del Id 11.
- **Prueba 2: 17,71°** (pasaba solo con una tolerancia de 0,5°, por el redondeo a hex).
- **125 colores distintos** entre los Id 1..200.

**Propuesta del usuario medida antes de implementarla:**
- Repartir el ángulo áureo sobre el arco libre con margen de 18,5° daba prueba 2 = 18,70°, pero **prueba 5 = 9,93°**.
- La causa: con 5 tonos saturados quedan 175° libres en 5 tramos, y el reparto comprime la separación en esa proporción.

**Corrección aplicada (pedida por el usuario):**
- `arcosLibres(prohibidos)`: el círculo menos ±18,5° por tono prohibido, uniendo las zonas solapadas, incluso las que cruzan 360°.
- `huecosTono(arcos)`: 12 huecos repartidos por tramo según su largo (D'Hondt), sin pasar de su capacidad con 20° (⌊L/20⌋ + 1, bordes incluidos).
  - Con las semillas, las capacidades suman exactamente 12: tramos de 8,1°, 64,8°, 20,1°, 42,8° y 39,2° → 1, 4, 2, 3 y 2 huecos.
  - Los Id 1..12 los recorren con un salto de 5.
- `tonoProyecto(id, prohibidos)`: Id 1..12 en los huecos; desde el 13, ángulo áureo sobre el arco libre.
- Se retiraron `ajustarTono` y `distanciaTono` (sin uso).
- El resto de la función del usuario no cambió: saturación, luminosidades, ΔE, `coloresDeRespuesta` y las conversiones.

**Resultado:** prueba 2 = 18,32° y prueba 5 = 20,28°, sin tolerancia ni `it.fails`; 198 colores distintos entre los Id 1..200.

**Límites que siguen:**
- **Leyendas con más colores:** si `leyendaNovedades` trae más colores saturados, las capacidades pueden sumar menos de 12; los huecos se reparten igual y la separación baja de 20°.
- **CHOQUE fuera de la base:** sigue sin estar en `COLORES_NOVEDAD_BASE`; hoy la prueba 1 lo cubre.
- **Estabilidad atada a la leyenda:** sigue igual que en la versión entregada (prueba 4).
- **Ids reales mayores que 12:** los proyectos reales tienen Id mayores que 12 (p. ej. 14, 17), así que usan el ángulo áureo sobre el arco libre. La separación de 20° está garantizada solo para los Id 1..12; para el resto, la distribución es la del ángulo áureo comprimido (≈ 9,9° entre 12 Id consecutivos).

### 6.2 Otras observaciones
- **Ficha de novedad:** el borde blanco de 2 px se ve poco sobre el fondo blanco de los días laborables; en la leyenda se agregó un contorno gris de 1 px.
- **D-:** la leyenda muestra un gris atenuado de ejemplo, porque cada proyecto usa su propio color atenuado.

## 7. Verificación visual (la hace usted: VPN, API y `npm run dev`)
| # | Caso | Esperado |
|---|---|---|
| V1 | `/cronograma` como gestor, sin parámetros | Selectores 01/10/2026–31/10/2026; "12 de 12 persona(s) · 8 proyecto(s)"; 4 celdas rayadas (Otro proyecto) |
| V2 | Como admin | 13 personas, 9 proyectos; ninguna celda rayada |
| V3 | Proyecto 17 en el filtro | Solo las personas con días en el 17, con todos sus proyectos visibles; `proyectoId=17` en la URL |
| V4 | Persona "7255" y luego un nombre en minúsculas sin tildes | Una sola fila con 7255; el nombre encuentra a la persona; `persona=` en la URL |
| V5 | Desde el 01/10 hasta el 31/12 (D5 revisada) | Se cargan del 01/10 al 29/12, con la advertencia «El rango supera 90 días. Se muestra del 01/10/2026 al 29/12/2026 (solicitado hasta el 31/12/2026).»; los selectores muestran 01/10–29/12 y la URL conserva `hasta=2026-12-31` |
| V5b | Rango de exactamente 90 días (01/10–29/12) | Sin advertencia |
| V6 | Hasta anterior a desde | Mensaje bajo "Hasta" |
| V7 | Fines de semana y hoy | Columnas de sábado y domingo sombreadas, con el número en rojo; la columna de hoy marcada |
| V8 | Mes anterior, actual y siguiente | Cambian el rango y la URL; atrás y adelante del navegador restauran |
| V9 | Clic en CHOQUE, NOVEDAD (y "+n" si hay) y Otro proyecto | Detalle con proyecto, rol, actividad, novedad y "Asignado además en n proyecto(s) de otros usuarios.", sin el nombre del ajeno |
| V10 | Leyenda | Proyecto nocturno (p. ej. el horario 73) con la marca "Nocturno"; novedades presentes; Choque, Descanso y Otro proyecto |
| V11 | VPN desconectada y "Mes siguiente" | Error y "Reintentar"; sin reintentos automáticos (una sola espera de ~15 s, pendiente 55) |
| V12 | Rango de 90 días como admin | El scroll fluye. Si se traba, anotarlo para evaluar la virtualización |
| V13 | Teclado | Tab recorre las celdas con datos; Enter abre el detalle; Escape lo cierra y el foco vuelve a la celda |
| V14 | Celdas de un proyecto junto a una V y una F | Ningún color de proyecto se confunde con una novedad (la novedad es una ficha redondeada con borde blanco) |
| V15 | Cambiar de mes o de filtro | El mismo proyecto conserva su color |
| V16 | Entrar sin parámetros y cambiar solo Hasta a noviembre; después cambiar solo Desde | Desde sigue en 01/10 (URL con `desde=2026-10-01&hasta=2026-11-…`); al cambiar Desde, Hasta se mantiene |

## 9. Ajuste D5: recortar en lugar de 400 (19/10/2026)
**Decisión del usuario (D5 revisada):**
- Un rango mayor que `CRONOGRAMA_MAX_DIAS` responde 200 con los primeros maxDias días (`hasta` = desde + maxDias − 1), en lugar de 400.
- Se mantienen los 400 de `hasta < desde` y de fechas o Id ilegibles.
- El endpoint por proyecto aplica lo mismo con fechas explícitas. Sin fechas sigue D12, y si el proyecto es largo también informa el recorte.

**Backend:**
- `CronogramaDto` tiene ahora `Recortado` (bool) y `HastaSolicitado` (`DateOnly?`, null sin recorte). Son los dos últimos parámetros, con valor por defecto, así que `ArmadoCronograma` no cambió.
- `CronogramaConsultaServicio`:
  - `Recortar(desde, hasta, maxDias)` calcula el fin efectivo y el pedido; `Recorte(dto, …)` marca la respuesta;
  - `HastaAnterior()` mantiene el 400 de `hasta < desde`;
  - se retiraron `ValidarRango` y el mensaje «El rango no puede superar N días.»;
  - las consultas usan el rango recortado (`FiltroCronograma.Hasta`).
- Por proyecto con D12: el fin pedido es el `FechaFin` del proyecto, o desde + maxDias − 1 si el `desde` es posterior al fin (en ese caso no hay recorte).
- **Sin consultas EF nuevas ni modificadas** (los ToQueryString no cambian) y **sin cambios de esquema**.

**Pruebas del backend (`CronogramaConsultaServicioTests`):**
- **Cambiadas (las de D5):**
  - `D5_Limite_90DiasValido_91Invalido` → reemplazada por `D5_91Dias_SeRecortaA90_ConElHastaSolicitado` (200: hasta = desde + 89, recortado, hastaSolicitado; la consulta usa el rango recortado) y `D5_LimiteExacto90_SinRecorte`;
  - `D5_UsaElParametro` → `D5_UsaElParametro_7DiasConUnRangoDe10` (recorta a 7, y sin parámetro vale 90).
- **Ampliadas (aserciones nuevas, sin cambiar lo que ya comprobaban):**
  - `D12_ProyectoLargo_Maximo90Dias` (+ recortado y hastaSolicitado = 31/12);
  - `D12_SinFechas_DesdeElInicio_HastaElFin` (+ sin recorte);
  - `D12_DesdeDespuesDelFin_90DiasDesdeEsaFecha` (+ sin recorte).
- **Nuevas:**
  - `D5_MesActualSinFechas_SinRecorte`;
  - `PorProyecto_FechasExplicitasDe120Dias_SeRecortan`;
  - `PorProyecto_HastaAnteriorADesde_400`.
- **Total: 744/744** (740 anteriores − 2 reemplazadas + 6 nuevas).

**Script `tarea20/cronograma.ps1`:**
- El caso «91 días» espera 200 con `recortado` = true, `hasta` = desde + 89 y `hastaSolicitado` = desde + 90, calculados desde el `desde` de la respuesta.
- La salida muestra «RECORTADO: solicitado hasta …».
- `Caso` acepta una verificación opcional de la respuesta.
- La simulación incluye los campos nuevos.
- `LEEME.md` actualizado.
- Simulado en una copia en el scratchpad: 8/8 y 0 nombres. Una variante sin recorte se marca «NO ESPERADO (recortado no es true)».

**Frontend:**
- `tipos.ts`: `recortado` y `hastaSolicitado`.
- `cronograma.ts`: `textoAvisoRecorte(cronograma)`, con 3 pruebas (con recorte, sin recorte y maxDias = 7).
- `PaginaCronograma`: MessageBar `warning` encima de la cuadrícula cuando hay recorte.
- `FiltrosCronograma`: los selectores muestran el rango efectivo de la respuesta (con prioridad sobre la URL, salvo mientras llega una respuesta nueva, `isPlaceholderData`); la URL conserva el `hasta` pedido.
- No había un manejo propio del 400 de rango excedido que quitar: el 400 de `hasta` se sigue mostrando en su campo para `hasta < desde`.
- **Total: 267/267** (264 + 3).

## 10. Corrección: el «desde» se perdía al cambiar solo el «hasta» (19/10/2026)
**Defecto (verificado por el usuario):**
- Sin fechas en la URL, los selectores mostraban 01/10–31/10, la respuesta del backend (D6).
- Al cambiar solo «hasta» a noviembre, la URL quedaba sin «desde». El backend aplicaba D6 con una sola fecha (el mes de esa fecha) y «desde» saltaba al 01/11.
- Lo mismo pasaba al cambiar solo «desde».

**Corrección (solo frontend, D6 del backend sin cambios):**
- `fechasAlCambiar(campo, valor, url, ultimaRespuesta, hoy)` en `cronograma.ts` devuelve las dos fechas.
- La otra fecha sale de la URL; si no está, de la última respuesta (para «hasta», `hastaSolicitado` si hubo recorte); y si aún no hay respuesta, del mes actual en Ecuador.
- `FiltrosCronograma` la usa en los dos selectores, con la nueva prop `ultimaRespuesta`, que `PaginaCronograma` le pasa.
- Los accesos de mes ya escribían las dos fechas («Mes actual» quita las dos).

**Pruebas nuevas (6):**
1. Sin URL y respuesta de octubre, cambiar hasta al 30/11 → desde 01/10.
2. Sin URL, cambiar desde al 15/10 → hasta 31/10.
3. URL con las dos fechas: solo cambia la tocada.
4. Con recorte (29/12 efectivo, 31/12 solicitado), cambiar desde → hasta 31/12; la URL tiene prioridad.
5. Sin respuesta todavía → mes actual en Ecuador.
6. Los accesos de mes escriben las dos fechas.

**Total: 273/273** (267 anteriores sin ajustes + 6 nuevas). Lint y build OK. Caso visual nuevo: **V16** (§7).

## 8. Pendientes
- **56:** TAREA-21b, la pestaña del detalle del proyecto.
- **57:** resuelto (§6.1).
- **55:** 503 rápido cuando la base no está disponible (backend).
- **50:** turno nocturno; aquí solo la marca "Nocturno".
