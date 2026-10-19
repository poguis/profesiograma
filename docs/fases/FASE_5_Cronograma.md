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

**`otrosProyectos` (D2):**
- Global, no Admin: días de trabajo en proyectos de **otros** propietarios.
- Por proyecto: días de trabajo en todos los **demás** proyectos.
- Admin global: 0 (todo es visible) y la consulta no se ejecuta.
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
| 3 | CEDULA | **Sin origen** | `Empleado.Cedula` siempre NULL (opción C). **D15 pendiente** |
| 4 | NOMBRE | `Empleado.NombreCompleto` | |
| 5 | FECHA | Fecha del día | El original enviaba el texto `d/m/yyyy`; la columna es texto |
| 6 | INGRESO DE JORNADA | `Proyecto.HoraEntrada` | `00:00:00` si el día es libre (novedad o descanso) |
| 7 | INGRESO ALMUERZO | `Proyecto.RegresoAlmuerzo` | Así lo mapeaba el original. Almuerzo nulo: **D16 pendiente** |
| 8 | SALIDA ALMUERZO | `Proyecto.SalidaAlmuerzo` | El original lo enviaba sin segundos. **D16 pendiente** |
| 9 | SALIDA DE JORNADA | `Proyecto.HoraSalida` | Nocturno: la salida es menor que el ingreso; confirmar cómo lo lee Infor (pendiente) |
| 10 | PROYECTO | `Proyecto.ProyectoErpId` | PLANTA/OFICINAS sin ProjectId: pendiente |
| 11 | `ACTIVIDAD ` (con espacio) | Actividad vigente ese día | Si no hay, la del proyecto (original) |
| 12 | ESTADO | `TipoNovedad.EstadoReporte` / LIBRE (descanso) / LABORABLE | |
| 13 | OBSERVACION | `TipoNovedad.ObservacionReporte` / "DESCANSO" / grupo | El original usaba `Upper(TipoNovedad)` |
| 14 | `CARGO INFOR ` (con espacio) | `CargoInfor.CodigoInfor` | **D14 pendiente**: cargo por `Empleado.Puesto` (la clave de `CargoInfor`) o `CargoAsignado` |

**Decisiones de la TAREA-22 (no implementadas):**
- **D14:** origen de CARGO INFOR. (Recomiendo `Empleado.Puesto`.)
- **D15:** CEDULA. (Recomiendo dejarla vacía o leerla de la API al exportar, sin guardarla.)
- **D16:** almuerzo nulo en un día laborable. (Recomiendo `00:00:00`.)

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
  - **sin reintentos automáticos (P10)**, porque con la base caída cada intento tarda ~15 s (pendiente 55);
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
