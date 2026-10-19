# FASE 5 — Diseño: Cronograma (consulta, TAREA-20)

La consulta alimenta la vista persona × día (TAREA-21) y el reporte Excel (TAREA-22). Solo lectura. Origen: `ControlMensualProyectos.pa.yaml` (botón «Ver cronograma», `btnCargarVista`, bloque activo desde la l. ~571; botón «REPORTE», l. 974).

## 1. Comportamiento original (resumen)
| Tema | Original | Nuevo |
|---|---|---|
| Rango | Mes actual; más de 90 días se **recorta** con aviso (l. 573–590) | Mes actual en Ecuador (D6); más de `CRONOGRAMA_MAX_DIAS` → 400 (D5) |
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
  - más de `CRONOGRAMA_MAX_DIAS` (Parametro Id 4, 90; 90 si falta) → 400 `hasta` «El rango no puede superar 90 días.».
- **Sin fechas:**
  - global: el mes actual en Ecuador; con una sola fecha, el mes de esa fecha (D6);
  - por proyecto: desde su `FechaInicio` hasta su `FechaFin`, como máximo 90 días (D12).
- **Filtros (global):**
  - `proyectoId` / `departamentoId`: las personas con días en ese proyecto o en proyectos de ese departamento (el del **proyecto**), con **todos** sus días visibles, como el filtro del original;
  - `codigoEkon`: solo esa persona.

**Respuesta dispersa (D7):** los días LIBRE no se envían.
```json
{
  "desde": "2026-10-01", "hasta": "2026-10-31", "maxDias": 90,
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
| D5 | Rango mayor que `CRONOGRAMA_MAX_DIAS` → 400 en `hasta` |
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
