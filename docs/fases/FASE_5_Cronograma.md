# FASE 5 — Diseño: Cronograma (consulta, TAREA-20)

La consulta alimenta la vista persona × día (TAREA-21) y el reporte Excel (TAREA-22). Solo lectura. Origen: `ControlMensualProyectos.pa.yaml` (botón «Ver cronograma», `btnCargarVista`, bloque activo desde la l. ~571; botón «REPORTE», l. 974).

## 1. Comportamiento original (resumen)
| Tema | Original | Nuevo |
|---|---|---|
| Rango | Mes actual; más de 90 días se **recorta** con aviso (l. 573–590) | Mes actual en Ecuador (D6); más de `CRONOGRAMA_MAX_DIAS` → **se recorta** a los primeros días y se informa con `recortado` / `hastaSolicitado` (D5 revisada el 19/10/2026, TAREA-21; antes, 400) |
| Visibilidad | SuperAdmin todo; los demás, por `CORREO_CREADOR` (l. 609–627) | R1: Admin todo; los demás, sus proyectos (D10) |
| Estados del proyecto | No filtra | Todos los no eliminados |
| Novedades | Todas, sin filtrar `ESTADO_NOVEDAD` (C17) | Solo no anuladas; cualquier origen (D11) |
| Personas | Con al menos una asignación en el rango (l. 682–700) | Igual (D4) |
| Precedencia | l. 746–764 | §3 |
| Novedad PROYECTO | Cualquier proyecto de la persona **en el rango** (l. 740) | Solo el proyecto con día de la persona **ese día** (D1) |
| Colores y siglas | En el cliente (l. 661–677, 768–797) | Colores de proyectos en el frontend (D8); las novedades traen su sigla y color de `TipoNovedad` |
| Filtros | Persona por nombre y proyecto por nombre, en el cliente | `proyectoId`, `departamentoId` y `codigoEkon` en el servidor; la búsqueda por nombre, en la TAREA-21 |

## 2. API (política Gestor)
| Endpoint | Respuesta |
|---|---|
| `GET /api/cronograma?desde&hasta&proyectoId&departamentoId&codigoEkon` | 200 `CronogramaDto`; 400 con el campo |
| `GET /api/proyectos/{id:int}/cronograma?desde&hasta` | 200 `CronogramaDto` solo con los días de ese proyecto; 404 si no existe, está eliminado o no es visible; 400 |

**Parámetros (todos opcionales y como texto):**
- Fechas `yyyy-MM-dd`; los Id son enteros ≥ 1.
- Un valor ilegible responde 400 en su campo, con el mismo formato que el listado: «La fecha 'desde' debe tener el formato yyyy-MM-dd.», «El valor de 'proyectoId' debe ser un número entero mayor o igual a 1.».
- **Rango:**
  - `hasta < desde` → 400 `hasta` «La fecha 'hasta' no puede ser anterior a la fecha 'desde'.»;
  - más de `CRONOGRAMA_MAX_DIAS` (Parametro Id 4, 90; 90 si falta) → **200 con el rango recortado**: `hasta` = desde + maxDias − 1, `recortado` = true y `hastaSolicitado` = el fin pedido. Así lo decide la D5 revisada (TAREA-21, 19/10/2026); antes era 400 «El rango no puede superar 90 días.», mensaje retirado.
  - Por proyecto: la misma regla con `desde`/`hasta` explícitos. Sin fechas (D12), si el proyecto pasa de maxDias también se informa el recorte, con su `FechaFin` como `hastaSolicitado`. Con un `desde` posterior al fin del proyecto se muestran maxDias días desde esa fecha, sin recorte.
- **Sin fechas:**
  - global: el mes actual en Ecuador; con una sola fecha, el mes de esa fecha (D6);
  - por proyecto: desde su `FechaInicio` hasta su `FechaFin`, como máximo 90 días (D12).
- **Filtros (global):**
  - `proyectoId` / `departamentoId`: las personas con días en ese proyecto o en proyectos de ese departamento (el del **proyecto**), con **todos** sus días visibles, como el filtro del original;
  - `codigoEkon`: solo esa persona.

**Respuesta dispersa (D7):** los días LIBRE no se envían.
```json
{
  "desde": "2026-10-01", "hasta": "2026-10-31", "maxDias": 90, "recortado": false, "hastaSolicitado": null,
  "proyectos": [{ "id": 14, "codigo": "PRY-…", "nombre": "…", "estado": "ACTIVO", "grupo": "CAMPO", "companiaId": 9001,
                  "proyectoErpId": "…", "dimensionUegpId": null,
                  "horario": { "codigo": 73, "descripcion": "…", "entrada": "17:00:00", "salida": "04:00:00", "cruzaMedianoche": true },
                  "almuerzo": { "salida": "01:00:00", "regreso": "02:00:00" } }],
  "personas": [{ "codigoEkon": "12345", "nombre": "…", "asignadoEn": [{ "proyectoId": 14, "cargo": "…" }],
                 "dias": [{ "fecha": "2026-10-02", "estado": "CHOQUE",
                            "asignaciones": [{ "proyectoId": 14, "rol": "PRINCIPAL", "actividad": "DEV.01" }],
                            "otrosProyectos": 1, "novedad": null, "novedadesExtra": 0 }] }],
  "leyendaNovedades": [{ "tipo": "VACACIONES", "sigla": "V", "color": "#0277BD" }]
}
```

**Campos:**
- `estado`: `NOVEDAD | CHOQUE | PROYECTO | OTRO_PROYECTO | DESCANSO`.
- `actividad`: la vigente ese día en ese proyecto (`ActividadVigente.Elegir`, mayor versión).
- `asignadoEn.cargo`: el `CargoAsignado` del último día del rango en ese proyecto.
- `novedad`: `{ tipo, sigla (SiglaCronograma), color, aplicacion, origen, estadoReporte, observacionReporte }`.
- `leyendaNovedades`: solo los tipos presentes en la respuesta.
- Personas ordenadas por nombre (es-EC) y código; proyectos, por nombre.

## 3. Reglas de la celda (`ArmadoCronograma`, lógica pura)
| Orden | Condición | Estado |
|---|---|---|
| 1 | Novedad PERSONA (D9: la de Id mayor; `novedadesExtra` = las demás PERSONA de ese día) | NOVEDAD |
| 2 | Novedad GENERAL (la de Id mayor) | NOVEDAD |
| 3 | Novedad PROYECTO de un proyecto con día de la persona **ese día**, de cualquier rol (D1) | NOVEDAD |
| 4 | Días de trabajo (rol ≠ DESCANSO) en los proyectos de la respuesta + `otrosProyectos` > 1 (D2, D3: cualquier estado del proyecto) | CHOQUE |
| 5 | Exactamente 1 día de trabajo, en un proyecto de la respuesta | PROYECTO |
| 6 | Exactamente 1 día de trabajo, solo en un proyecto que no viene en la respuesta | OTRO_PROYECTO |
| 7 | Solo DESCANSO | DESCANSO |
| 8 | Nada | LIBRE (no se envía) |

**Novedades:**
- Una novedad GENERAL o PERSONA se muestra aunque ese día no haya asignación (`asignaciones` vacío), como el original.
- La persona debe aparecer en el rango (D4).

**`otrosProyectos` (D2) y OTRO_PROYECTO, por vista** (aclarado en la TAREA-21b):
- **Global (`/api/cronograma`), no Admin:** días de trabajo en proyectos de **otros propietarios** (los que el usuario no ve). OTRO_PROYECTO = ese día trabaja solo en un proyecto de otro propietario.
- **Admin global:** 0 (todo es visible) y la consulta 1b no se ejecuta.
- **Por proyecto (`/api/proyectos/{id}/cronograma`):** días de trabajo en **todos los demás proyectos**, sean o no del mismo usuario (`SalvoProyectoId`). OTRO_PROYECTO = ese día la persona no está en este proyecto y trabaja en otro.
- **CHOQUE con `asignaciones` vacío:** en el endpoint por proyecto, un día sin asignación en este proyecto y con 2 o más proyectos ajenos llega como CHOQUE con `asignaciones = []` (fila 4 de la tabla: `activos + otrosProyectos > 1`). El frontend lo muestra como **OTRO_PROYECTO** en la pestaña del detalle (`estadoEnContexto`, Q3 de la TAREA-21b); el backend no cambia.
- **Solo la cantidad**: nunca el Id, código ni nombre del proyecto ajeno.

**Turno nocturno:** el día pertenece a la fecha de entrada (pendiente 50). `horario.cruzaMedianoche` = salida < entrada (`ReglasAlmuerzo.EsNocturno`).

**Proyectos SUSPENDIDO o TERMINADO:** el recorte (E3) borra los días posteriores a F, así que no aparecen después de esa fecha. INACTIVO se muestra como cualquier otro estado.

## 4. Implementación
- **Application:**
  - `Cronograma/CronogramaContratos.cs` (solicitudes, DTO, `ICronogramaConsultas`);
  - `ArmadoCronograma.cs` (precedencia y armado);
  - `CronogramaConsultaServicio.cs` (rango, validación y visibilidad).
  - `LectorParametros.LeerId` (Id opcional).
- **Infrastructure:** `Consultas/CronogramaConsultas.cs`, con 7 consultas `internal static IQueryable` y su ToQueryString (`CronogramaSqlTests`). Sin N+1 y sin cambios de esquema:

| # | Consulta | Índice |
|---|---|---|
| 0 | `CRONOGRAMA_MAX_DIAS` | `UQ_Parametro_Clave` |
| 1 | Días del rango, proyectos no eliminados y visibles, filtros (subconsulta de personas para `proyectoId` / `departamentoId`) | `IX_ProyectoAsignacionDia_Cronograma` (Fecha) |
| 1b | D2: `COUNT` por persona y fecha en los proyectos que no vienen en la respuesta (rol ≠ DESCANSO) | `IX_ProyectoAsignacionDia_Cruces` (EmpleadoId, Fecha) |
| 2 | Novedades no anuladas: GENERAL, PERSONA de esas personas y PROYECTO de esos proyectos; LINQ sobre las tablas, sin mapear `vwNovedadDiaVigente` (D13) | `IX_NovedadDia_Fecha` |
| 3 | Actividades de esos proyectos que se solapan con el rango | `IX_ProyectoActividad_Vigencia` |
| 4 | Cabeceras de esos proyectos (horario, almuerzo, compañía, ERP y grupo) | PK |
| 5 | Rango del proyecto (no eliminado y visible), para el endpoint por proyecto | PK |

- **Api:** `Endpoints/CronogramaEndpoints.cs`.
- **Volumen:** SharePoint tiene ≈5.000 días en total; 300 personas × 90 días ≤ 27.000 celdas antes de omitir las libres. Sin paginación.

## 5. Decisiones (TAREA-20, aprobadas el 19/10/2026)
| # | Decisión |
|---|---|
| D1 | Novedad PROYECTO solo si la persona tiene día en ese proyecto ese día |
| D2 | CHOQUE cuenta los proyectos no visibles; consulta 1b, solo cantidades (`otrosProyectos`) |
| D3 | CHOQUE incluye proyectos TERMINADO e INACTIVO |
| D4 | Las personas con solo novedades no aparecen |
| D5 | ~~Rango mayor que `CRONOGRAMA_MAX_DIAS` → 400 en `hasta`~~. **Revisada el 19/10/2026 (TAREA-21):** se recorta a los primeros maxDias días y se informa (`recortado`, `hastaSolicitado`); el 400 se mantiene para `hasta < desde` y para fechas o Id ilegibles |
| D6 | Sin fechas: el mes actual en Ecuador |
| D7 | Respuesta dispersa (sin días LIBRE) |
| D8 | Colores de los proyectos en el frontend |
| D9 | Varias novedades PERSONA el mismo día: gana la de Id mayor; `novedadesExtra` cuenta las demás |
| D10 | Visibilidad CREADOR (Admin todo); `PROYECTO_VISIBILIDAD` sigue sin uso |
| D11 | Incluye las novedades de origen PERMISOS_MEDICOS |
| D12 | Endpoint por proyecto sin fechas: desde su `FechaInicio`, máximo 90 días |
| D13 | LINQ sobre las tablas (sin mapear la vista) |

Adicional de la implementación: estado `OTRO_PROYECTO` (fila 6 de §3), para el día en que la persona solo trabaja en un proyecto que no viene en la respuesta.

## 6. Plantilla del reporte (análisis para la TAREA-22)
`docs/PLANTILLA_REPORTE_PROYECTOS.xlsx`:
- hoja **`'PROYECTO '`** (con espacio final), `Tabla1` en A1:N2 y la fila de datos vacía;
- nombres definidos **`LABORABLE` y `LIBRE` → `#REF!`** (rotos);
- formatos de la fila 2: CEDULA, FECHA y ACTIVIDAD como texto (`@`); las 4 horas como `[h]:mm:ss`.

El original (l. 974) arma una fila por EKON + proyecto + día. Prevalece el trabajo sobre el descanso, la novedad PROYECTO es la de ese proyecto y ese día, y no hay CHOQUE. Ordena por empresa, proyecto, nombre y fecha.

| # | Columna | Origen | Nota |
|---|---|---|---|
| 1 | EMPRESA | `Proyecto.CompaniaId` | |
| 2 | EKON | `Empleado.CodigoEkon` | |
| 3 | CEDULA | `Empleado.Cedula` | **D15** (TAREA-22-1): se guarda en la alta puntual; texto tal cual |
| 4 | NOMBRE | `Empleado.NombreCompleto` | |
| 5 | FECHA | Fecha del día | Texto `d/M/yyyy` sin ceros a la izquierda, como el original (`Text(fecha, "d/m/yyyy")`) |
| 6 | INGRESO DE JORNADA | `Proyecto.HoraEntrada` | `00:00:00` si el día es libre (novedad o descanso) |
| 7 | INGRESO ALMUERZO | `Proyecto.RegresoAlmuerzo` | Así lo mapeaba el original. Almuerzo nulo → `00:00:00` (**D16**) |
| 8 | SALIDA ALMUERZO | `Proyecto.SalidaAlmuerzo` | El original lo enviaba sin segundos. Almuerzo nulo → `00:00:00` (**D16**) |
| 9 | SALIDA DE JORNADA | `Proyecto.HoraSalida` | Nocturno: tal cual, menor que el ingreso [SUPUESTO 54, hasta que Infor confirme] |
| 10 | PROYECTO | `Proyecto.ProyectoErpId` | PLANTA/OFICINAS sin ProjectId: vacío [SUPUESTO 53] |
| 11 | `ACTIVIDAD ` (con espacio) | Código (ACTIVITY_ID) de la actividad vigente ese día | Si no hay, la de mayor versión (el original usaba la de la cabecera) |
| 12 | ESTADO | `TipoNovedad.EstadoReporte` / LIBRE (descanso) / LABORABLE | |
| 13 | OBSERVACION | `TipoNovedad.ObservacionReporte` / "DESCANSO" / grupo | El original usaba `Upper(TipoNovedad)` |
| 14 | `CARGO INFOR ` (con espacio) | `CargoInfor.CodigoInfor` | **D14**: el activo cuyo `Cargo` = `Empleado.Puesto` (el original usaba el cargo de la asignación con 3 valores fijos) |

**Decisiones de la TAREA-22 (tomadas el 20/10/2026; implementadas en la 22-1 y la 22-2, ver §8):**
- **D14:** CARGO INFOR por `Empleado.Puesto`.
- **D15:** la cédula se guarda en `Empleado.Cedula` en la alta puntual (TAREA-22-1).
- **D16:** almuerzo nulo en un día laborable → `00:00:00`.
- El reporte es **por proyecto** (Z1), como el original, que exigía elegir un proyecto.

## 7. Vista (TAREA-21, frontend)
`/cronograma` (carga diferida; entrada "Cronograma" en el menú lateral; la política Gestor la impone la API, un 403 se muestra con `EstadoError`). Código en `frontend/src/features/cronograma/`.

**Filtros (en la URL, como `/proyectos`):**
- `desde` y `hasta` con `SelectorFecha`. Los selectores muestran el **rango efectivo** de la respuesta: el mes actual sin fechas (D6) o el recortado (D5 revisada). La URL conserva el rango que pidió el usuario.
- **Al cambiar una sola fecha** (corrección del 19/10/2026), se escriben en la URL **las dos** (`fechasAlCambiar`). Con una sola, el backend aplicaría D6 (el mes de esa fecha) y movería la que el usuario no tocó: p. ej. cambiar solo «hasta» a noviembre hacía saltar «desde» al 01/11.
  - La fecha cambiada se escribe tal cual.
  - La otra se toma en este orden: la de la URL; la de la última respuesta (para «hasta», la **solicitada** si hubo recorte, no la recortada); y, si todavía no hay respuesta, la del mes actual en Ecuador (`hoyEnNegocio`), como D6.
  - Los accesos «Mes anterior» y «Mes siguiente» ya escribían las dos fechas. «Mes actual» quita las dos (D6).
- **Rango recortado:** una MessageBar `warning` encima de la cuadrícula: «El rango supera {maxDias} días. Se muestra del {desde} al {hasta} (solicitado hasta el {hastaSolicitado}).» (`textoAvisoRecorte`).
- Accesos "Mes anterior", "Mes actual" (quita las fechas) y "Mes siguiente".
- `proyectoId` y `departamentoId` van al servidor. Las opciones de proyecto son los de la respuesta más el seleccionado (P8); los departamentos salen de `GET /api/catalogos`.
- `persona` filtra **solo en el cliente** (P9), por nombre o código EKON, sin tildes ni mayúsculas y con espacios colapsados (`normalizarTexto`); no se envía a la API.

**Cuadrícula (P1: tabla HTML con sticky, sin virtualización):**
- Columna de la persona (240 px, sticky a la izquierda): nombre, EKON y los códigos de sus proyectos (`asignadoEn`).
- Dos filas de encabezado sticky arriba: mes (banda) y día (inicial; martes y miércoles abreviados, como el original l. 832; número en rojo en fin de semana).
- Fines de semana sombreados; hoy en Ecuador marcado (borde de acento).
- La respuesta dispersa se expande en el cliente (`expandirDias` × `indexarCeldas`).
- Filas memorizadas; celdas como `<button>` simple (sin un componente de Fluent por celda).

| Estado | Forma (P2) | Texto |
|---|---|---|
| PROYECTO | Relleno sólido del color del proyecto | Abreviatura (P7: 2 caracteres del nombre sin "PLANTA - ") |
| NOVEDAD | Ficha redondeada con el color de la novedad y borde blanco de 2 px | Sigla en negrita y "+n" (`novedadesExtra`, D9) |
| DESCANSO | Color del proyecto atenuado al 35 % (P6) | "D-" + abreviatura |
| CHOQUE | `#201F1E` | "C" + total (asignaciones visibles con rol ≠ DESCANSO + `otrosProyectos`) |
| OTRO_PROYECTO | Rayado gris | Sin texto ("Otro proyecto" en `aria-label`, detalle y leyenda; sin datos del ajeno, D2) |

- El color del texto lo calcula `contrasteTexto`: negro o blanco, el de mayor contraste (≥ 4,5 con cualquier fondo; mínimo medido 4,64).
- **Detalle (P4):** un único Popover de Fluent, anclado a la celda pulsada (clic o Enter). Muestra fecha, estado, persona, novedad (tipo, aplicación, origen, "+n"), asignaciones (código, nombre, rol, actividad) y "Asignado además en n proyecto(s) de otros usuarios.". Al cerrarlo, el foco vuelve a la celda.
- **Leyenda:** proyectos con color, abreviatura, código, nombre, horario y la marca **"Nocturno"** si `cruzaMedianoche` (pendiente 50); las novedades de la respuesta; Choque, Descanso del proyecto y Otro proyecto.
- **Estados de la consulta:**
  - 400 en su filtro, sin reintento (`hasta < desde` y fechas o Id ilegibles; el rango largo ya no es 400);
  - 403 con `EstadoError`;
  - 5xx o red con `EstadoError` y "Reintentar";
  - **sin reintentos automáticos (P10)**, porque con la base caída cada intento espera la conexión (hasta ~5 s y luego 503 «Base de datos no disponible» desde la TAREA-22c; antes ~15 s y 500, pendiente 55);
  - vacío: "No hay asignaciones en el rango.".

**Colores de proyectos (P2, `coloresProyecto.ts`, código del usuario corregido el 19/10/2026):**
- **Zonas prohibidas:** ±18,5° (margen interno) alrededor de los tonos de las novedades con saturación ≥ 25 %. Son `COLORES_NOVEDAD_BASE` (semillas de `TipoNovedad`) unidas a `leyendaNovedades`; el gris de D (`#607D8B`) no ocupa tono. Con las semillas actuales quedan **5 tramos libres que suman 175°**.
- **Id 1..12:** 12 **huecos** calculados desde el arco libre (`huecosTono`).
  - Se reparten por tramo según su largo (D'Hondt), sin pasar de la capacidad con 20° de separación: un tramo de largo L admite ⌊L/20⌋ + 1 huecos.
  - Dentro de un tramo: 1 hueco al centro; 2 o más, de borde a borde y equidistantes.
  - Los Id recorren los huecos con un salto de 5, para que Id consecutivos queden lejos.
- **Id 13 en adelante:** el ángulo áureo (id × 137,508°) repartido sobre el arco libre: la fracción `(id × 137,508 / 360) mod 1` se recorre a lo largo de los tramos. Ningún tono se mueve al borde de una zona.
- **Saturación** 62 %. **Luminosidad**: 38, 46 o 54 %, empezando por `id mod 3`; se toma la primera con ΔE (CIE76) ≥ 20 frente a todos los colores de novedad. Si ninguna llega, la más lejana.
- **Estabilidad:** el color depende solo del Id y de la lista de colores de novedad. Con las semillas actuales es el mismo en cualquier rango o filtro; un color de novedad nuevo cambia las zonas y, con ellas, los colores de los proyectos.
- **Sin paleta fija ni prueba de colisión.**
- **Medido (Id 1..200):**
  - ΔE mínimo 20,14 (Id 78 frente a CD);
  - distancia de tono mínima a una novedad 18,32°;
  - separación entre los Id 1..12 de 20,28°;
  - contraste mínimo del texto 4,72;
  - 198 colores distintos.
- **Historia:** la versión entregada movía el tono al borde de la zona (`ajustarTono`) y juntaba los tonos (10,91° entre los Id 1..12; 125 colores distintos). Se reemplazó por los huecos y el arco libre (TAREA-21 §6.1).

**Decisiones de la vista (19/10/2026):**

| # | Decisión |
|---|---|
| P1 | Tabla HTML con sticky, sin virtualización |
| P2 | Colores dinámicos por Id (12 huecos del arco libre para los Id 1..12 y ángulo áureo sobre el arco libre para el resto; zonas de ±18,5°, S 62 %, 3 luminosidades, ΔE ≥ 20); forma por estado (tabla de arriba) |
| P3 | Pestaña "Cronograma" en el detalle del proyecto → TAREA-21b |
| P4 | Un Popover compartido, por clic o Enter |
| P5 | Se mantienen las semillas de `TipoNovedad` |
| P6 | DESCANSO: color del proyecto atenuado al 35 % |
| P7 | Abreviatura de 2 caracteres sin el prefijo "PLANTA - " (el único prefijo del código: `NombreVisualProyecto.PrefijoDimension`, también para OFICINAS) |
| P8 | Opciones de proyecto: los de la respuesta + el seleccionado |
| P9 | Persona por nombre o código, solo en el cliente, en la URL |
| P10 | `useCronograma` con `retry: false` |

### 7.1 Pestaña "Cronograma" del detalle del proyecto (TAREA-21b, 20/10/2026)
`/proyectos/:id`, pestaña **Cronograma** después de Personal e Historial (`PestanaCronograma`).

**Consulta:**
- `GET /api/proyectos/{id}/cronograma` con `useCronogramaProyecto(id, rango, activa)`.
- Solo con la pestaña activa: el panel se monta solo entonces y el hook lleva `enabled`.
- `retry: false` (P10) y `keepPreviousData`.

**Rango local, sin URL (Q2):**
- Al abrir, sin fechas: D12, desde el inicio del proyecto hasta su fin, como máximo 90 días. Si el proyecto es más largo, se avisa del recorte.
- `SelectorRango` con `fechasAlCambiar` sobre el estado local, "Mes anterior", "Mes siguiente" y **"Todo el proyecto"** (vuelve a D12).
- Filtro local de persona, por nombre o código y sin tildes (Q6).
- La pestaña activa sigue siendo estado local (`location.state.pestana` admite `'cronograma'`), sin `?tab=`.

**Vista:**
- `VistaCronograma` con `contexto="proyecto"` y `proyectoContexto={id}`:
  - las celdas sin asignación en este proyecto (incluido el CHOQUE entre ajenos) se ven como OTRO_PROYECTO (Q3), con el mismo rayado;
  - el detalle dice "Asignado además en n otro(s) proyecto(s)." (Q7);
  - la leyenda dice "Otro proyecto (cualquier otro)".
- La leyenda no cambia: un elemento con el horario y "Nocturno" (Q8).
- **Aviso de recorte** + "Use los accesos de mes o abra el cronograma general."
- **Enlace "Abrir en el cronograma general"**: `/cronograma?desde&hasta&proyectoId={id}` con el rango **efectivo** de la respuesta (Q9). El general muestra a esas personas con todos sus proyectos visibles.
- **Estados:** cargando; vacío ("El proyecto no tiene asignaciones en el rango."); 400 en su selector; 404 ("Proyecto no encontrado o sin acceso"); 5xx o red con "Reintentar".

**Colores (Q4):**
- En **las dos vistas**, `coloresEstables(ids, catalogos.tiposNovedad)` calcula con `COLORES_NOVEDAD_BASE` ∪ los `colorHex` del catálogo de `TipoNovedad` (`GET /api/catalogos`), no con la `leyendaNovedades` de cada respuesta.
- Así un proyecto tiene el mismo color en `/cronograma` y en la pestaña. Sin catálogo (no cargó), solo la base.

**Componentes extraídos (Q1):**
- `VistaCronograma`: resumen, aviso de recorte, vacío, cuadrícula y leyenda; además, `ErrorCronograma` con "Reintentar".
- `SelectorRango`: desde/hasta, meses y botón de reinicio configurable ("Mes actual" en `/cronograma`, "Todo el proyecto" en la pestaña).
- `/cronograma` se comporta igual.

**Invalidación (Q5):**
- La clave por proyecto es `['proyectos', 'cronograma', id, consulta]`.
- `invalidarTrasEscritura(queryClient)` invalida `['proyectos']` y `['cronograma']`. La usan los 5 `onSuccess`: crear, cambio de estado, cabecera, personal y reactivación.
- Así, tras cualquier escritura se refrescan el detalle, la pestaña y `/cronograma`.

| # | Decisión (20/10/2026) |
|---|---|
| Q1 | Extraer `VistaCronograma` y `SelectorRango` |
| Q2 | Pestaña como estado local + `location.state.pestana`; sin `?tab=` |
| Q3 | Sin asignación en este proyecto (incluido el CHOQUE entre ajenos) → OTRO_PROYECTO, en el frontend (`estadoEnContexto`) |
| Q4 | Colores con la base ∪ `catalogos.tiposNovedad`, en las dos vistas (`coloresNovedadEstables`) |
| Q5 | Clave por proyecto bajo `['proyectos']`; `invalidarTrasEscritura` también invalida `['cronograma']` |
| Q6 | Filtro local de persona en la pestaña |
| Q7 | Contexto `'proyecto'` → "Asignado además en n otro(s) proyecto(s)."; por defecto `'general'` |
| Q8 | Leyenda sin cambios |
| Q9 | Enlace al cronograma general con el rango efectivo |

## 8. Reporte Excel (TAREA-22-2, backend; 21/10/2026)
**Origen:**
- el botón «REPORTE» (`ControlMensualProyectos.pa.yaml` l. 974; bloque activo del `OnSelect` decodificado, l. 541–737);
- el flujo `ReporteProyectosSIG` (`docs/origen/sharepoint/esquemas/ReporteProyectoSIG.json`): copiaba la plantilla, ejecutaba un Office Script (su código no está en el repo, pendiente 4) y enviaba el archivo por correo.

Aquí el libro se descarga.

**Contrato (política Gestor, visibilidad R1):**

| Ruta | Respuesta |
|---|---|
| `GET /api/proyectos/{id:int}/reporte?desde&hasta` | 200 `.xlsx` (`application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`); 400 en su campo; 404 |
| `GET /api/proyectos/{id:int}/reporte/resumen?desde&hasta` | 200 `{ filas, personas, personasSinCedula, personasSinCodigoInfor, filasSinActividad, diasConOtroProyecto }`; 400; 404 |

- **Parámetros** (texto, `yyyy-MM-dd`, **obligatorios**, sin tope de días):
  - «La fecha 'desde' es obligatoria.» (igual para 'hasta');
  - «La fecha 'desde' debe tener el formato yyyy-MM-dd.» (igual para 'hasta');
  - `hasta < desde` → 400 en `hasta`.
- **404:** inexistente, eliminado o no visible. Como en el cronograma por proyecto, se comprueba antes que los parámetros.
- **Rango:** el pedido se intersecta con [FechaInicio, FechaFin] del proyecto antes de consultar (mismo resultado, menos lectura).
- **Archivo:** `Reporte_{CódigoProyecto}_{yyyyMMdd}_{yyyyMMdd}.xlsx`, con las fechas pedidas. Los caracteres que no son letra, dígito, `-`, `_` o `.` pasan a `_`.
- **Encabezados de la respuesta:** `Content-Disposition` con `filename*` UTF-8, `Cache-Control: no-store` y `X-Reporte-Filas`.
- **Sin filas:** 200, `X-Reporte-Filas: 0` y la tabla vacía (A1:N2).
- **Log del endpoint:** Id del proyecto, Id del usuario, rango y cantidad de filas. Nunca nombres ni cédulas.

**Reglas de fila (`ArmadoReporte`, Application, lógica pura):**
- **Una fila por persona + día con asignación** en el proyecto, de cualquier rol (Z4).
  - Una novedad sin asignación no genera fila.
  - El día de un turno nocturno es el de la entrada (pendiente 50).
- **Prioridad:**
  1. novedad → LIBRE por novedad. Orden PERSONA > GENERAL > PROYECTO de este proyecto; la de Id mayor (D9); solo no anuladas, de cualquier origen (D11);
  2. si no hay novedad y hay trabajo → LABORABLE;
  3. si solo hay descanso → LIBRE por descanso.

  Dentro del proyecto, el trabajo gana al descanso.
- **Columnas:** las de la tabla de §6.
  - En los días libres, las 4 horas van en `00:00:00`.
  - ESTADO/OBSERVACION: `TipoNovedad.EstadoReporte`/`ObservacionReporte` con novedad; LIBRE/DESCANSO con descanso; LABORABLE/código del grupo con trabajo.
- **ACTIVIDAD (Z6):** el código de la actividad (ACTIVITY_ID, como el original) vigente ese día (`ActividadVigente`).
  - Si no hay vigente, la de mayor versión.
  - Si no hay ninguna, vacío; cuenta en `filasSinActividad`.
- **Tipos (Z7) [SUPUESTO mientras falte el Office Script]:**
  - CEDULA, FECHA y ACTIVIDAD, texto;
  - EMPRESA, EKON y PROYECTO, **número** solo si tienen únicamente dígitos, no empiezan en "0" y tienen 15 dígitos o menos; si no, texto;
  - la cédula va tal como está en `Empleado.Cedula`.
- **Orden (Z15):** EMPRESA, PROYECTO (ordinal, vacíos al final), NOMBRE (es-EC, sin distinguir mayúsculas), FECHA; desempate por EKON.
- **CARGO INFOR (D14):** consulta (6) `ConsultaEmpleadosReporte`.
  - `LEFT JOIN CargoInfor` activo con `Cargo = Empleado.Puesto`, por la collation de la base (sin `UPPER`).
  - Sin código → vacío; cuenta en `personasSinCodigoInfor`.
  - Es la única consulta del cronograma que lee `[Cedula]`.
- **`diasConOtroProyecto`:** días-persona del reporte en que la persona también trabaja en otro proyecto. Sale de la consulta 1b con `SalvoProyectoId`; es informativo.

**Libro (`EscritorReporteExcel`, Infrastructure, ClosedXML 0.105.1; Z8):**
- Se arma en código y en memoria: no se carga ni se distribuye la plantilla.
- Hoja `'PROYECTO '`, `Tabla1` con `TableStyleMedium2` y autofiltro, y los 14 encabezados exactos.
- Formatos por columna: `@` en C, E y K; `[h]:mm:ss` (numFmtId 46, valores `TimeSpan`) en F–I.
- Los `<col width>` son los de la plantilla. ClosedXML suma un margen de 0,710625 al guardar y se descuenta.
- Sin autor, correo ni los nombres definidos rotos. Una prueba compara la estructura con `docs/PLANTILLA_REPORTE_PROYECTOS.xlsx`.
- **Licencias:** ClosedXML es MIT; SixLabors.Fonts resuelve a **1.0.0**, que es Apache-2.0.
- **Z7 validada (R1, 21/10/2026):** la regla número/texto quedó validada con los datos de desarrollo (`conteos-tipos.sql`).

**Frontend (TAREA-22b, 21/10/2026):** `/proyectos/:id` → «Exportar a Excel» (acciones del encabezado, cualquier estado).
- **Diálogo `DialogoExportarReporte`:**
  - título «Exportar reporte a Excel · {código}» y el nombre;
  - Desde y Hasta obligatorios, con `SelectorFecha`, que al abrir valen el inicio y el fin del proyecto y no tienen tope;
  - aviso fijo «El archivo contiene cédulas. Manéjelo con cuidado.».
  - Se monta solo al abrirlo: cada apertura empieza con el rango inicial, sin errores ni resultados anteriores.
- **Validación (`reporteExcel.ts`):** los mismos textos que el backend; el error de formato del selector gana; `hasta < desde` va bajo Hasta.
- **Resumen previo:** `useResumenReporte`, con la clave `['proyectos', 'reporte-resumen', id, consulta]`, así que se refresca con cualquier escritura del proyecto.
  - `enabled` solo con el diálogo abierto y el rango válido; `retry: false`; `keepPreviousData`.
  - Muestra «{filas} fila(s) · {personas} persona(s)» y solo los avisos con n > 0.
  - Sin filas: «No hay asignaciones en el rango.» y «Descargar» deshabilitado.
  - Si falla: `EstadoError`, «Reintentar» y «Descargar» deshabilitado.
- **Descarga:** `useDescargarReporte`, una mutación que no invalida nada (es de solo lectura).
  1. `apiDescargar` (`clienteHttp.ts`) usa la misma autenticación y los mismos errores que `apiGet`. Accept: xlsx, `application/problem+json` y `application/json;q=0.9`. Con `fetch`, un error se lee como ProblemDetails, no como Blob.
  2. El nombre sale de `filename*` (UTF-8; si está mal codificado, se pasa al siguiente criterio), luego de `filename` y, si no hay, del nombre por defecto. Una ruta incluida se reduce al nombre.
  3. Se guarda con un `<a download>` temporal; la URL se revoca a los ~1000 ms.
  4. El blob solo vive dentro de `mutationFn`, que devuelve `{ filas }` (de `X-Reporte-Filas`).
  - Sin `localStorage`, `sessionStorage` ni `console`.
- **Errores de la descarga:**
  - 400 bajo su selector;
  - 404 «Proyecto no encontrado o sin acceso» con enlace al listado;
  - 503, 5xx y red con «Reintentar». Con la base caída llega un 503 «Base de datos no disponible» en ~5 s (TAREA-22c; antes un 500, pendiente 55) y el diálogo muestra también su detail.
- **Doble clic:** `enviandoRef` + `isPending`.
- **Cancelación (Z28):** cerrar (Cancelar, Esc o la X), o desmontar el diálogo, cancela la petición con `AbortController`, sin error ni descarga.
- **Éxito:** se cierra el diálogo y el detalle muestra «Reporte generado ({n} filas).», o «Reporte generado.» si no se pudo leer `X-Reporte-Filas`. No cambia de pestaña.
- Sin botón en `/cronograma` (Z27).
- **Fase 7 (pendiente 59):** si la SPA y la API quedan en orígenes distintos, exponer `Content-Disposition` y `X-Reporte-Filas` (`Access-Control-Expose-Headers`).

| # | Decisión del frontend (21/10/2026) |
|---|---|
| Z17 | «Exportar a Excel» en las acciones del encabezado del detalle, en cualquier estado |
| Z18 | Rango inicial = inicio y fin del proyecto; editable; sin tope |
| Z19 | Resumen como query (`enabled`, `retry: false`, `keepPreviousData`, clave bajo `['proyectos']`); si falla, «Descargar» deshabilitado |
| Z20 | Descarga como mutación que guarda el archivo y devuelve solo `{ filas }` |
| Z21 | `apiDescargar` en `clienteHttp.ts` |
| Z22 | Nombre: `filename*` → `filename` → por defecto; `decodeURIComponent` dentro de `try/catch` |
| Z23 | Éxito con la MessageBar del detalle, sin cambiar de pestaña |
| Z24 | Diálogo sin carga diferida propia (la página ya es diferida) |
| Z25 | URL revocada ~1000 ms después del `click()`; el blob no se conserva |
| Z26 | Textos del resumen |
| Z27 | Sin botón en `/cronograma` |
| Z28 | Cerrar durante la generación cancela la petición (`AbortController`) |
| Z29 | Accept de la descarga con xlsx y ProblemDetails |


| # | Decisión (20/10/2026) |
|---|---|
| Z1 | Reporte por proyecto; sin reporte global ni filtros por departamento o persona |
| Z2 | Sin tope de días ni parámetro nuevo; `desde` y `hasta` obligatorios |
| Z3 | Sin filas → 200, tabla vacía y `X-Reporte-Filas: 0` |
| Z4 | Una fila por EKON + día con asignación; dentro del proyecto el trabajo gana al descanso |
| Z5 | Novedades PERSONA > GENERAL > PROYECTO, Id mayor; 4 horas en 00:00:00 en los días libres |
| Z6 | ACTIVIDAD vigente; si no hay, la de mayor versión; si no hay ninguna, vacío |
| Z7 | Número solo con dígitos, sin "0" inicial y ≤ 15 dígitos; si no, texto [SUPUESTO] |
| Z8 | Libro armado en código |
| Z9 | Endpoint de resumen (solo conteos) |
| Z10–Z11 | Cédula (D15) y «completar cédulas»: TAREA-22-1 |
| Z12 | 503 sin base: TAREA-22c (implementada el 21/10/2026; ver `00_ESTADO_ACTUAL.md` §7.1 regla 4) |
| Z13 | División 22-1 / 22-2 / 22b (frontend) / 22c |
| Z14 | Nombre del archivo con las fechas pedidas |
| Z15 | Orden de las filas |
| Z16 | ClosedXML 0.105.1 solo en Infrastructure |
