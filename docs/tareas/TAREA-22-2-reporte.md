# TAREA-22-2 — Reporte Excel por proyecto (backend)

**Fecha:** 2026-10-21 (Fase B; Fase A aprobada el 20/10/2026, Z1–Z16 con los ajustes de esta tarea).
**Resultado:** implementada; **pendiente de la verificación manual del usuario** (R1–R15, `backend/tests/manual/tarea22-2/LEEME.md`).

**Compilación** (`dotnet build Profesiograma.slnx -c Release --no-incremental`): **0 errores, 0 advertencias del compilador**.
- Hay **16 advertencias de auditoría de NuGet** (NU1902 moderada y NU1903 alta), que son 8 distintas repetidas, todas de `System.Security.Cryptography.Pkcs`.
- **No vienen de ClosedXML.** `dotnet nuget why` muestra que llegan por `Microsoft.Data.SqlClient` 6.1.6 (Infrastructure) y por `Microsoft.Identity.Web` 4.15.0 (Api), que ya estaban.
- Aparecieron en la restauración que hizo `dotnet add package`, que trajo la base de avisos actualizada. El 20/10 la compilación tenía 0.
- No cambié esas dependencias: ver §7.

**Pruebas:**
- `dotnet test --solution Profesiograma.slnx -c Release`: **801/801** = 758 anteriores + 43 nuevas, 0 ajustadas.
- `has-pending-model-changes`: sin cambios (no hay migración).

**Lo que no se hizo:** frontend (22b), 503 sin base (22c), migraciones, llamadas a la API, SQL, `dotnet run`, git que modifique el repositorio.

## 1. Documentación previa (§0 de la tarea)
- **00_ESTADO:** la TAREA-22-1 pasa a ✅ (M1–M9 OK, 20/10/2026). Línea base: `Empleado` = 18 filas (8 DEV y 10 reales), todas con cédula.
- **FASE_4 §3:** lo que midió el sondeo M1.
  - 1630 activos, con `cedula` como texto;
  - largo de 8 a 10 (2 de 8 dígitos y 1628 de 10);
  - 358 empiezan con "0";
  - sin vacíos ni repetidos.

  [PENDIENTE DE CONFIRMAR] con RR. HH. las 2 cédulas de 8 dígitos.

## 2. Comprobaciones pedidas
- **Z6 en el original** (bloque decodificado, l. 642–659): exporta el **Id** de la actividad.
  - `actividadFinal = If(!IsBlank(histAct), histAct.ACTIVITY_ID, proy.ActividadExcel)`, y `ActividadExcel` es `px.ACTIVITY_ID` (OnVisible).
  - El nuevo exporta `ProyectoActividad.ActividadCodigo`, que es ese mismo Id (el `ActivityId` del ERP).
- **API de ClosedXML, verificada en el paquete 0.105.1:**
  - el XML de documentación trae `IXLTable.ReplaceData`, `AppendData` y `Resize`, e `IXLCell.InsertData` e `InsertTable`;
  - se escribió celda por celda con `XLCellValue` y luego `Range(…).CreateTable("Tabla1")`, para controlar el tipo de cada valor sin reflexión;
  - en las pruebas, releyendo el libro: "0912345678" y "2/10/2026" quedan como **texto**, y los `TimeSpan` como duración con numFmtId 46.
- **Encabezados con espacio final:** `CreateTable` conserva `ACTIVIDAD ` y `CARGO INFOR `.
- **Anchos:** ClosedXML suma al guardar un margen de 0,710625 (fuente por defecto); al leer, la plantilla da 10,14 donde su XML dice 10,855. Se descuenta ese margen (`MargenAncho`) para que los `<col width>` del archivo sean los de la plantilla. La prueba de estructura compara el XML de los dos paquetes.
- **Licencias** (verificadas en los `.nuspec` del caché local de NuGet):
  - ClosedXML 0.105.1 → MIT;
  - `dotnet list package --include-transitive` resuelve **SixLabors.Fonts 1.0.0** → **Apache-2.0**. No resolvió 2.x (Split License): **no hay pendiente para TI o legal**;
  - transitivas: ClosedXML.Parser 2.0.0, DocumentFormat.OpenXml 3.1.1 (+ Framework 3.1.1), ExcelNumberFormat 1.1.0, RBush.Signed 4.0.0, Microsoft.Bcl.AsyncInterfaces 8.0.0, System.Memory.Data 8.0.1.

## 3. Qué se hizo
**Application (`Cronograma/Reporte/`)**
- **`ReporteContratos.cs`:**
  - `ReporteProyectoSolicitud`;
  - `FilaReporte` (14 columnas; su `ToString` no muestra la cédula);
  - `ResumenReporteDto`, `ReporteProyecto`;
  - `EmpleadoReporte` (su `ToString` tampoco muestra la cédula);
  - el puerto `IEscritorReporteExcel`.
- **`ArmadoReporte.cs`** (lógica pura):
  - una fila por persona + día;
  - precedencia novedad > trabajo > descanso (PERSONA > GENERAL > PROYECTO de este proyecto, Id mayor);
  - horas en `00:00:00` en los días libres y con almuerzo nulo (D16);
  - nocturno tal cual (supuesto 54); PROYECTO vacío sin ProjectId (supuesto 53);
  - ACTIVIDAD vigente o la de mayor versión (Z6); D14;
  - orden (Z15); `EsNumeroExportable` (Z7); `Fecha` con `d/M/yyyy`; `NombreArchivo` (Z14) y el resumen.
- **`ReporteProyectoServicio.cs`:**
  - R1 y 404 antes de validar, como el cronograma por proyecto;
  - fechas obligatorias; rango intersectado con el del proyecto;
  - reutiliza las consultas 1 (`SoloProyectoId`), 1b (`SalvoProyectoId`), 2, 3 (todas las versiones del proyecto), 4 y 5, más la 6 nueva.
  - La regla R1 se escribe igual que en los otros servicios: cada uno tiene su `TryObtenerVisibilidad`.
- **`ICronogramaConsultas.ObtenerEmpleadosReporteAsync`** (nuevo método) y el registro de `ReporteProyectoServicio`.

**Infrastructure**
- `App.Infrastructure.csproj`: **ClosedXML 0.105.1** (solo aquí).
- `Consultas/CronogramaConsultas.cs`: consulta (6) `ConsultaEmpleadosReporte`.
  - `LEFT JOIN CargoInfor` activo con `Puesto = Cargo`, sin `UPPER`;
  - un código Infor vacío se toma como null.
- `Reportes/EscritorReporteExcel.cs` (nuevo):
  - hoja `'PROYECTO '`, `Tabla1` con `TableStyleMedium2` y autofiltro, y los 14 encabezados exactos;
  - formatos por columna: `@` en C, E y K; numFmtId 46 en F–I;
  - los anchos de la plantilla;
  - sin autor, `LastModifiedBy` ni compañía;
  - sin filas, A1:N2 con la fila vacía;
  - todo en `MemoryStream`.
  - Se registra como singleton, porque no tiene estado.

**Api (`Endpoints/CronogramaEndpoints.cs`, junto al cronograma por proyecto)**
- `GET /api/proyectos/{id:int}/reporte`:
  - `TypedResults.File`, que pone `Content-Disposition` con `filename` y `filename*` UTF-8;
  - `Cache-Control: no-store` y `X-Reporte-Filas`;
  - log Information con el Id del proyecto, el Id del usuario, el rango y la cantidad de filas.
- `GET /api/proyectos/{id:int}/reporte/resumen`: solo conteos.

## 4. Archivos
| Archivo | Cambio |
|---|---|
| `backend/src/App.Application/Cronograma/Reporte/ReporteContratos.cs`, `ArmadoReporte.cs`, `ReporteProyectoServicio.cs` | Nuevos |
| `backend/src/App.Application/Cronograma/CronogramaContratos.cs` | +`ObtenerEmpleadosReporteAsync` |
| `backend/src/App.Application/DependencyInjection.cs` | `ReporteProyectoServicio` |
| `backend/src/App.Infrastructure/App.Infrastructure.csproj` | ClosedXML 0.105.1 |
| `backend/src/App.Infrastructure/Consultas/CronogramaConsultas.cs` | Consulta (6) y `FilaEmpleadoReporte` |
| `backend/src/App.Infrastructure/Reportes/EscritorReporteExcel.cs` | Nuevo |
| `backend/src/App.Infrastructure/Persistencia/PersistenciaServiceCollectionExtensions.cs` | Registro del escritor |
| `backend/src/App.Api/Endpoints/CronogramaEndpoints.cs` | 2 rutas |
| `backend/tests/App.Application.Tests/Cronograma/ReporteProyectoTests.cs` | Nuevo (31 pruebas) |
| `backend/tests/App.Application.Tests/Cronograma/DoblesCronograma.cs` | Doble: **solo se agregó** el método nuevo de la interfaz (necesario para compilar; ninguna prueba cambió) |
| `backend/tests/App.Infrastructure.Tests/Reportes/EscritorReporteExcelTests.cs` | Nuevo (4 pruebas) |
| `backend/tests/App.Infrastructure.Tests/Consultas/CronogramaReporteSqlTests.cs` | Nuevo (2 pruebas) |
| `backend/tests/App.Api.Tests/ReporteProyectoTests.cs` | Nuevo (6 pruebas) |
| `backend/tests/manual/tarea22-2/` | `resumen.cmd/.ps1`, `reporte.cmd/.ps1`, `conteos-tipos.sql`, `LEEME.md`, `simulacion/` (ficticios) |
| `docs/fases/FASE_5_Cronograma.md` | §6 corregida (CEDULA, D14–D16, reporte por proyecto) y §8 nueva |
| `docs/fases/FASE_4_Empleados_API.md` | §3: lo que midió el sondeo M1 |
| `docs/00_ESTADO_ACTUAL.md` | Encabezado, §6 (22-2), §7 (4, 52–54), §8 (22-1 ✅ y 22-2) |

## 5. Pruebas nuevas (43)
- **Application (31):**
  - LABORABLE; libre por novedad; libre por descanso;
  - almuerzo nulo + nocturno + PLANTA sin ProjectId;
  - sin cédula, sin código Infor y sin actividad, con el resumen;
  - actividad vigente o de mayor versión;
  - precedencia PERSONA/GENERAL/PROYECTO con Id mayor, y la novedad de otro proyecto no cuenta;
  - trabajo > descanso y novedad sin asignación → sin fila;
  - orden es-EC (Ñ y minúsculas) y `diasConOtroProyecto`;
  - Z7 (12 casos: "0123", "0", 15 y 16 dígitos, letras, espacio, vacío, null);
  - fecha y nombre de archivo saneado; `ToString` sin la cédula;
  - servicio: 400 (5 casos), 404 (inexistente o eliminado, no visible; el admin sí lo ve), rango intersectado con las fechas pedidas en el nombre y el resumen, y un rango fuera del proyecto sin consultar los días.
- **Infrastructure (6):**
  - libro releído en memoria: hoja, tabla, encabezados exactos, tipos (CEDULA y FECHA texto; EKON 900001 número y DEV001 texto; EMPRESA número), horas `TimeSpan` con 46, `@` en C, E y K, orden, anchos del XML y sin autor;
  - tabla vacía A1:N2;
  - **estructura contra la plantilla** (hoja, tabla, rango, tema, encabezados, formatos de la fila 2 y `<col width>`);
  - rendimiento con 30.000 filas;
  - ToQueryString de la consulta 6 (`[Cedula]`, `LEFT JOIN CargoInfor`, `Activo`, igualdad `[e].[Puesto] = [c].[Cargo]` sin `UPPER`/`LOWER`);
  - las 7 consultas de la TAREA-20 sin `[Cedula]`.
- **Api (6):**
  - 200 con content-type, `Content-Disposition` (`filename` y `filename*`), `no-store` y `X-Reporte-Filas`;
  - 400 en su campo (3 casos, también en el resumen);
  - 404 de un proyecto ajeno (reporte y resumen) y 401 sin usuario;
  - el resumen da el JSON exacto de conteos, sin la cédula ni nombres.

**Índice `UQ_CargoInfor_Cargo`:** `ToQueryString` no puede mostrar el plan de ejecución. La prueba verifica que el predicado es una igualdad directa sobre la columna, sin funciones; eso es lo que permite usar el índice. Confirmarlo con el plan real requiere ejecutar la consulta en SSMS (no se hizo).

## 6. Comandos y resultados
| Comando | Resultado |
|---|---|
| `dotnet add src/App.Infrastructure/… package ClosedXML --version 0.105.1` | Agregado; restauración OK |
| `dotnet list … package --include-transitive` | ClosedXML 0.105.1; SixLabors.Fonts **1.0.0** (Apache-2.0) |
| `dotnet nuget why … System.Security.Cryptography.Pkcs` | Llega por Microsoft.Data.SqlClient y Microsoft.Identity.Web (no por ClosedXML) |
| `dotnet build …` (durante el trabajo) | 2 errores corregidos: `using` de `ActividadCorte` (está en `App.Domain.Proyectos.Estados`) y el doble sin el método nuevo |
| `dotnet test` (Application, 1.ª vez) | 1 fallo en mi prueba: el doble no trae actividades, así que hay 3 filas sin actividad (comportamiento correcto). Se corrigió la expectativa |
| `dotnet test` (Escritor, 1.ª vez) | 2 fallos por los anchos (margen de ClosedXML). Se resta el margen y se compara el XML |
| Pruebas de Api | 19/19 |
| `dotnet build Profesiograma.slnx -c Release --no-incremental` | 0 errores; 0 advertencias del compilador; 16 de auditoría NuGet (§7) |
| `dotnet test --solution Profesiograma.slnx -c Release` | 801/801 |
| `has-pending-model-changes` | Sin cambios |
| Scripts manuales con `SIMULAR=1` en una **copia** en el scratchpad | MODO SIMULACION; el resumen imprime el JSON ficticio; el reporte imprime estado, `X-Reporte-Filas`, `Cache-Control`, tipo y nombre, y **no descarga** |

**Rendimiento:** 30.000 filas × 14 columnas en **4,2–4,7 s**, archivo de **1,7 MB** (prueba en este equipo, Release). Sin aserción.

**Revisión de exposición** (grep en los 9 archivos `.cs` fuente tocados):
- **Archivos revisados:**
  - `App.Api/Endpoints/CronogramaEndpoints.cs`;
  - `App.Application/Cronograma/`: `CronogramaContratos.cs`, `Reporte/ArmadoReporte.cs`, `Reporte/ReporteContratos.cs`, `Reporte/ReporteProyectoServicio.cs`;
  - `App.Application/DependencyInjection.cs`;
  - `App.Infrastructure/`: `Consultas/CronogramaConsultas.cs`, `Persistencia/PersistenciaServiceCollectionExtensions.cs`, `Reportes/EscritorReporteExcel.cs`.
- **Logs:** el único log nuevo es el del endpoint (Id del proyecto, Id del usuario, rango y filas).
- **Excepciones:** ninguna nueva.
- **`[Cedula]`:** en todo `backend/src` solo se lee en la consulta 6 (`CronogramaConsultas.cs`) y en las de la 22-1 (`CompletarCedulasRepositorio` y la carga con seguimiento de la alta puntual).

## 7. Pendientes
- **Verificación manual del usuario:** R1–R15 (`backend/tests/manual/tarea22-2/LEEME.md`).
- **Auditoría NuGet** [PENDIENTE DE DECISIÓN DEL USUARIO]: `System.Security.Cryptography.Pkcs` 9.0.11 / 10.0.10 tiene avisos moderado y alto. Opciones:
  - fijar una versión corregida con una `PackageReference` directa en Infrastructure y Api;
  - o esperar versiones nuevas de SqlClient e Identity.Web.

  Cualquiera de las dos cambia dependencias fuera del alcance de esta tarea.
- **Desviación de §6 de la tarea:** `reporte.cmd` usa `curl -D` (encabezados a un temporal) y `-o Reporte.xlsx` en lugar de `-i -o`, porque con `-i` los encabezados quedarían dentro del `.xlsx` y Excel pediría repararlo.
- **`.gitignore`:** cubre `backend/tests/manual/**/resultado-*.txt`, pero **no** `Reporte.xlsx`. El LEEME pide no subirlo; conviene agregar `backend/tests/manual/**/Reporte.xlsx` (no lo cambié).
- **Supuestos que siguen abiertos:**
  - Z7, mientras falte el Office Script (pendiente 4);
  - el 53 (PROYECTO vacío) y el 54 (salida nocturna tal cual), hasta que Infor confirme.
- **Otras tareas:** 22b (botón «Exportar a Excel» y descarga autenticada) y 22c (503 sin base, pendiente 55).
