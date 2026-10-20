# TAREA-22-1 — Cédula en la alta puntual (D15) y acción Admin «completar cédulas»

**Fecha:** 2026-10-20 (Fase B; la Fase A de la TAREA-22 se aprobó el mismo día, Z10 y Z11).
**Resultado:** implementada; **pendiente de la verificación manual del usuario** (M1–M9, `backend/tests/manual/tarea22-1/LEEME.md`).
- `dotnet build Profesiograma.slnx -c Release --no-incremental`: 0 advertencias, 0 errores.
- `dotnet test --solution Profesiograma.slnx -c Release`: **758/758** = 744 anteriores + 14 nuevas; **2 ajustadas** (las aprobadas en B3).
- `dotnet ef migrations has-pending-model-changes`: sin cambios (no hay migración).

**Lo que no se hizo:** el reporte Excel (TAREA-22-2), el 503 sin base (22c), cambios en el frontend, migraciones, llamadas a la API o al ERP, SQL, `dotnet run`, git que modifique el repositorio.

## 1. Decisión durante la implementación (contradicción con el plan)
El plan decía que las filas DEV del sembrador estaban en NULL. **No es así:** `EmpleadosPrueba.CrearDev` les pone una cédula ficticia (`"99999999" + n`), y por eso `tarea26d/conteos.sql` excluía a los DEV.

Si el Simulado les entregara cédulas "00…", la alta puntual las sobrescribiría (rompería la identidad DEV de la 26d), y «completar» no las contaría.

**Decisión del usuario (20/10/2026):** el Simulado entrega a DEV001–DEV008 **la cédula del sembrador**; SIM001–SIM200, 10 dígitos ficticios que empiezan en "00". Por eso M4 del LEEME dice que los DEV no cuentan en `completadas`.

## 2. Qué se hizo
**Cliente HTTP y tipos**
- **`EmpleadoJson.Cedula`:** se lee con el convertidor **`SoloTexto`**, que nunca lanza.
  - Texto → sin espacios extremos; vacío o solo espacios → null.
  - Número, null, ausente, objeto o arreglo → null. Un número no se convierte a texto.
  - `EmpleadoJson.ToString()` devuelve `EmpleadoJson(código)`, sin la cédula.
- **`CedulasEmpleados` (Application, nuevo):**
  - clase sellada, código → cédula, normalizada (sin espacios extremos, máximo 20, vacía → sin dato);
  - sin enumeración pública: `De(código)`, `Solo(códigos)` y `Cantidad`;
  - `ToString()` = `CedulasEmpleados(n)`.
- **Tipos sin cédula:** `EmpleadoErp`, `DatosEmpleado` y `EmpleadoAsignable` no la llevan.
- **`IDescargaEmpleadosErp.DescargarConCedulasAsync`:** tiene una implementación por defecto sin cédulas, así que los dobles existentes no cambian. `DescargarAsync` sigue igual, por eso las pruebas del cliente no se tocaron.
- **`ListaEmpleadosErp.Cedulas`:** vacía por defecto. `ComoAnterior()` la deja vacía.
- **`CacheEmpleadosErp`:**
  - la lectura fresca devuelve las cédulas de los registros **válidos**;
  - la lista que queda en caché va **sin** ellas, aunque la haya renovado la lectura fresca.
- **Recorrido de la cédula hasta la alta puntual:**
  - `CatalogoEmpleados.Cedulas` (solo las de los códigos pedidos y solo con la lista fresca);
  - de ahí a `ProyectoValidado.Cedulas` (creación) y `CambioPersonal.Cedulas` (personal y reactivación);
  - y al llamar a `AltaPuntualEmpleados.AplicarAsync(…, cedulas, ct)` desde los 2 repositorios.

**Alta puntual**
- `Preparar(…, cedulas)`:
  - fila nueva → la cédula del diccionario (ya recortada a 20) o NULL;
  - refresco → solo una cédula no vacía la actualiza; vacía o ausente no pisa la guardada.
- Nada más cambió.

**Modo Simulado**
- `EmpleadosPrueba.CedulasSimuladas()` (comentada como ficticia) y `EmpleadosErpSimulado.DescargarConCedulasAsync`.
- El sembrador no cambió.

**Acción Admin**
- **Ruta:** `POST /api/empleados/completar-cedulas` (Politica.Admin, sin cuerpo), en `EmpleadoEndpoints`.
- **Log:** Information con los 4 conteos y el Id del usuario. Va en el endpoint porque Application no tiene la dependencia de logging y no la agregué.
- **Servicio (`CompletarCedulasServicio`, Application):**
  - lectura fresca fuera de la transacción: si el ERP falla → 503 sin abrir la transacción;
  - luego, dentro de `ITransaccionAsignaciones`, ejecuta `CompletarCedulas.Aplicar` (lógica pura: solo llena las NULL que la API trae con cédula; cuenta `sinCedulaEnApi` y `noEncontradasEnApi`);
  - guarda solo si completó alguna. El motivo del applock está comentado en el código.
- **`CompletarCedulasRepositorio` (Infrastructure):** `ConsultaSinCedula`, con seguimiento, para que la auditoría llene ModificadoPor.
- **Respuesta:** `ResultadoCompletarCedulasDto(Revisadas, Completadas, SinCedulaEnApi, NoEncontradasEnApi)`.

## 3. Archivos
| Archivo | Cambio |
|---|---|
| `backend/src/App.Application/Empleados/CedulasEmpleados.cs` | Nuevo |
| `backend/src/App.Application/Empleados/CompletarCedulas.cs` | Nuevo (DTO, contrato del repositorio, lógica pura y servicio) |
| `backend/src/App.Application/Empleados/FuenteEmpleados.cs` | `ListaEmpleadosErp.Cedulas` |
| `backend/src/App.Application/Empleados/MapeoEmpleado.cs` | `LargoCedula`, `NormalizarCedula` |
| `backend/src/App.Application/Proyectos/Crear/CatalogoEmpleados.cs` | `Cedulas` |
| `backend/src/App.Application/Proyectos/Crear/CrearProyectoContratos.cs`, `CrearProyectoValidador.cs` | `ProyectoValidado.Cedulas` |
| `backend/src/App.Application/Proyectos/Personal/EdicionPersonalContratos.cs`, `EdicionPersonalServicio.cs`, `Reactivacion/ReactivacionServicio.cs` | `CambioPersonal.Cedulas` |
| `backend/src/App.Application/DependencyInjection.cs` | `CompletarCedulasServicio` |
| `backend/src/App.Infrastructure/Erp/FuenteEmpleadosErpHttp.cs` | `DescargarConCedulasAsync`, `EmpleadoJson.Cedula`, `SoloTexto`, `ToString` |
| `backend/src/App.Infrastructure/Erp/CacheEmpleadosErp.cs` | Cédulas solo en la lectura fresca |
| `backend/src/App.Infrastructure/Erp/EmpleadosErpSimulado.cs`, `Persistencia/DatosPrueba/EmpleadosPrueba.cs` | Cédulas ficticias |
| `backend/src/App.Infrastructure/Persistencia/Empleados/AltaPuntualEmpleados.cs` | Cédula en la fila nueva y en el refresco |
| `backend/src/App.Infrastructure/Persistencia/Empleados/CompletarCedulasRepositorio.cs` | Nuevo |
| `backend/src/App.Infrastructure/Persistencia/PersistenciaServiceCollectionExtensions.cs` | Registro del repositorio |
| `backend/src/App.Infrastructure/Persistencia/Proyectos/ProyectoRepositorio.cs`, `EdicionPersonalRepositorio.cs` | Pasan las cédulas a la alta puntual |
| `backend/src/App.Api/Endpoints/EmpleadoEndpoints.cs` | Ruta nueva |
| `backend/tests/App.Application.Tests/Empleados/CedulasEmpleadosTests.cs` | Nuevo (6 pruebas) |
| `backend/tests/App.Infrastructure.Tests/Erp/FuenteEmpleadosErpHttpTests.cs` | +2 pruebas, 1 ajustada |
| `backend/tests/App.Infrastructure.Tests/Erp/CacheEmpleadosErpTests.cs` | +2 pruebas |
| `backend/tests/App.Infrastructure.Tests/Consultas/EmpleadosApiSqlTests.cs` | +2 pruebas, 1 ajustada |
| `backend/tests/App.Api.Tests/CompletarCedulasTests.cs` | Nuevo (2 pruebas) |
| `backend/tests/manual/tarea22-1/` | `sondeo-cedula.cmd/.ps1`, `completar.cmd`, `conteos-cedula.sql`, `LEEME.md`, `simulacion/` (ficticios) |
| `backend/tests/manual/tarea26d/conteos.sql` | `NoDevConCedula` marcado como obsoleto (comentario; no se borró) |
| `CLAUDE.md` | «Datos sensibles»: regla de la cédula |
| `docs/fases/FASE_2_Modelo_Datos.md` | C31 (P10 reemplazada por D15) |
| `docs/fases/FASE_4_Empleados_API.md` | Fecha, §3, §4, §5, §6 paso 1 y §6.1 nueva |
| `docs/00_ESTADO_ACTUAL.md` | Encabezado, §6 (21 y 21b ✅, 22 dividida), §7 (51–54, 56, nota de Fase 3), §8 (22-1), §9 (21 y 21b ✅) |

## 4. Pruebas
**Nuevas (14):**
- **Application (6):**
  - los tipos que se pueden loguear o serializar no tienen `Cedula` (reflexión sobre `EmpleadoErp`, `DatosEmpleado`, `EmpleadoAsignable` y `ListaEmpleadosErp`);
  - `CedulasEmpleados`: `ToString` sin valores, normalización, recorte a 20, `Solo`;
  - el catálogo fresco trae solo las cédulas pedidas; la vista previa, ninguna;
  - `Aplicar`: solo llena las NULL, no toca las no nulas y cuenta `sinCedulaEnApi` y `noEncontradasEnApi`;
  - el servicio completa dentro de la transacción y es idempotente (la segunda vez no guarda);
  - con el ERP caído: excepción de 503 sin abrir la transacción, sin leer ni escribir.
- **Infrastructure (6):**
  - convertidor: texto, número, null, ausente, "   ", objeto y arreglo → nunca lanza; los 7 empleados siguen válidos; solo el texto da una cédula;
  - `DescargarAsync` sin cédulas y `EmpleadoJson.ToString` sin cédula;
  - la caché no contiene cédulas tras una lectura fresca (dentro del TTL, vencida y `ComoAnterior`), y no salen las de registros no válidos;
  - Simulado: DEV con la cédula del sembrador, SIM `^00[0-9]{8}$`, todas únicas;
  - alta puntual: recorte a 20, refresco con valor la actualiza, sin dato en el diccionario queda NULL;
  - ToQueryString de `ConsultaSinCedula` (contiene `[Cedula] IS NULL`).
- **Api (2):**
  - gestor → 403 sin leer la API;
  - admin → 200 con el JSON exacto de conteos, sin la cédula ni el código ficticios.
  - Usan la autorización real (DevAuth + `AddSeguridadProfesiograma`) en un pipeline en memoria.

**Ajustadas (las 2 aprobadas en B3):**
- `FuenteEmpleadosErpHttpTests.DatosSensibles_…`: "cedula" sale de la lista prohibida **solo** para `EmpleadoJson`; sigue prohibida en `EmpleadoErp` y `DatosEmpleado`.
- `EmpleadosApiSqlTests.Preparar_…_SinCedula` → `Preparar_…_Cedula`: la fila nueva recibe la cédula de la API (sin espacios); la existente, con una cédula de solo espacios en la API, conserva la guardada.
- Manual: `tarea26d/conteos.sql`, comentario de obsoleto.

**Sin tocar:**
- las aserciones `DoesNotContain("[Cedula]")` / `"Cedula"` de las consultas existentes y las 3 de `SnapshotPersonal`;
- `git diff` de las pruebas solo elimina las líneas de los 2 ajustes.

## 5. Comandos y resultados
| Comando | Resultado |
|---|---|
| `dotnet build Profesiograma.slnx -c Release` (durante el trabajo) | OK; un error de ambigüedad del constructor `CedulasEmpleados.Vacio` (corregido) y uno de una expresión de colección sin tipo en una prueba (corregido) |
| `dotnet test --solution Profesiograma.slnx -c Release` (antes de las pruebas nuevas) | 743/744: falló solo `DatosSensibles_…`, la prueba que se esperaba ajustar |
| `dotnet test --project tests/App.Api.Tests` | Las 2 pruebas nuevas fallaron porque en Development se valida la construcción de los servicios (`EmpleadoConsultaServicio` sin dependencias). Se registró con una fábrica que no se llama → 13/13 |
| `dotnet build Profesiograma.slnx -c Release --no-incremental` | 0 advertencias, 0 errores |
| `dotnet test --solution Profesiograma.slnx -c Release` | 758/758 (Domain, Application, Infrastructure y Api correctos) |
| `dotnet ef migrations has-pending-model-changes … --configuration Release` | «No changes have been made to the model since the last migration.» |
| Scripts manuales con `SIMULAR=1` en una **copia** en el scratchpad (con `tarea26a/comun.ps1`) | `sondeo-cedula`: MODO SIMULACION, agregados correctos sobre el JSON ficticio (5 textos, 1 vacío, largo 10, 3 con 10 dígitos, 1 con no dígitos, 4 que empiezan en "0", 1 valor repetido en 2 registros). `completar.cmd`: MODO SIMULACION, imprime solo el JSON ficticio. Ninguna llamada de red; nada se escribió en `backend/tests/manual/` |
| `grep` de logs, excepciones y mensajes en los 21 archivos fuente tocados | Ninguno contiene la cédula (§6) |

## 6. Revisión de exposición (grep sobre el código fuente)
**Archivos revisados:**
- `App.Api/Endpoints/EmpleadoEndpoints.cs`;
- `App.Application/DependencyInjection.cs`;
- `App.Application/Empleados/`: `FuenteEmpleados.cs`, `MapeoEmpleado.cs`, `CedulasEmpleados.cs`, `CompletarCedulas.cs`;
- `App.Application/Proyectos/Crear/`: `CatalogoEmpleados.cs`, `CrearProyectoContratos.cs`, `CrearProyectoValidador.cs`;
- `App.Application/Proyectos/Personal/`: `EdicionPersonalContratos.cs`, `EdicionPersonalServicio.cs`;
- `App.Application/Proyectos/Reactivacion/ReactivacionServicio.cs`;
- `App.Infrastructure/Erp/`: `CacheEmpleadosErp.cs`, `EmpleadosErpSimulado.cs`, `FuenteEmpleadosErpHttp.cs`;
- `App.Infrastructure/Persistencia/`: `DatosPrueba/EmpleadosPrueba.cs`, `Empleados/AltaPuntualEmpleados.cs`, `Empleados/CompletarCedulasRepositorio.cs`, `PersistenciaServiceCollectionExtensions.cs`, `Proyectos/EdicionPersonalRepositorio.cs`, `Proyectos/ProyectoRepositorio.cs`.

**Resultado:**
- Los únicos logs son el de la acción (4 conteos y el Id del usuario), el de la lista anterior (minutos) y los del cliente HTTP (operación, código, milisegundos y el mensaje de `ErpNoDisponibleException`, todos textos fijos).
- Las excepciones tienen textos fijos o Id y códigos de estado.
- Un duplicado en la alta puntual solo puede venir de `UQ_Empleado_CodigoEkon` o `UQ_CargoInfor_Cargo` (el índice de la cédula no es único).
- La cédula se recorta a 20 antes de guardarla, así que no puede haber un error de truncamiento con su valor.
- Los records que llevan la referencia a `CedulasEmpleados` (`ProyectoValidado`, `CambioPersonal`) la imprimirían como `CedulasEmpleados(n)`.

## 7. Pendientes
- **Verificación manual del usuario:** M1–M9 (`backend/tests/manual/tarea22-1/LEEME.md`). M1 da los agregados reales de `cedula` que faltaban en la TAREA-26a.
- **TAREA-22-2:** reporte Excel (CEDULA desde `Empleado.Cedula`; D14, D16 y los supuestos 53 y 54); FASE_5_Cronograma §6 se corrige ahí.
- **Fase 3:** decidir si la `CEDULA` de `SIG_ASIGNACIONES_PERSONAL`/`DETALLE` se carga en `Empleado.Cedula` [PENDIENTE] (00_ESTADO, pendiente 51).
- **Observación:** `simulacion/completar_gestor.json` trae un ProblemDetails ficticio. Con la API real, el 403 de DevAuth puede llegar sin cuerpo: lo que vale es la línea `HTTP 403`.
