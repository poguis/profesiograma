# TAREA-25-3-1 — Reporte: cargos Infor, backend

**Fecha:** 29/10/2026 · **Fase:** B (implementación del plan de la TAREA-25-3) · **Estado:** ✅ Verificada por el usuario (29/10/2026)

Contratos, reglas, concurrencia y decisiones (F1–F5, K1–K15): `docs/fases/FASE_5_Usuarios_Departamentos.md` §10.

## 1. Qué se hizo
- **Endpoints `Politica.Admin`** bajo `/api/admin/cargos-infor`:
  - listado con filtros, `personasGuardadas` en la misma consulta y `activosErp` desde la caché (null sin lista);
  - vista previa de la importación con la caché;
  - importación con lectura fresca, dentro de `ITransaccionAsignaciones` y releyendo la tabla completa;
  - edición con compare-and-set por valores; con 0 filas se relee el Id para dar 404 o 409.
- **Reglas** (`ReglasCargoInfor`, Application):
  - se reutiliza `MapeoEmpleado.NormalizarCargo`;
  - un comparador es-EC `IgnoreCase | IgnoreKanaType | IgnoreWidth` imita `Modern_Spanish_CI_AS`;
  - conteos de `fusionados` y grupos de `variantes` (tildes y espacios internos);
  - código Infor (F2).
- **409 de la importación** con el mensaje propio del ajuste 2, y un log Warning con la cantidad de filas, sin nombres de puestos.
- **Script manual** `backend/tests/manual/tarea25-3/cargos.cmd` (+ `.ps1`, `LEEME.md` y `simulacion\`, 22 archivos ficticios) con el patrón de la tarea62. No lo ejecuté.
- **Documentación:**
  - `FASE_5_Usuarios_Departamentos.md` §10 y la tabla de subtareas;
  - `FASE_2_Modelo_Datos.md`: §0 punto 7, §2.7 y la ruta del `.sql`;
  - `00_ESTADO_ACTUAL.md`: fila TAREA-25, índice, siguiente tarea, pendiente 2 y el nuevo 73.

## 2. Archivos tocados
| Archivo | Cambio |
|---|---|
| `src/App.Application/Empleados/CargosInfor.cs` | Nuevo: DTO, repositorio, reglas y servicio |
| `src/App.Infrastructure/Persistencia/Empleados/CargosInforRepositorio.cs` | Nuevo: consultas, inserción, `ExecuteUpdate` |
| `src/App.Api/Endpoints/CargoInforEndpoints.cs`, `Program.cs` | Nuevos endpoints y su registro |
| `src/App.Application/DependencyInjection.cs`, `src/App.Infrastructure/Persistencia/PersistenciaServiceCollectionExtensions.cs` | Registro |
| `tests/App.Application.Tests/Empleados/CargosInforTests.cs`, `tests/App.Infrastructure.Tests/Consultas/CargosInforSqlTests.cs`, `tests/App.Api.Tests/CargosInforEndpointsTests.cs` | Nuevos (35 pruebas) |
| `backend/tests/manual/tarea25-3/*` | Script manual |
| `docs/fases/FASE_5_Usuarios_Departamentos.md`, `docs/fases/FASE_2_Modelo_Datos.md`, `docs/00_ESTADO_ACTUAL.md`, `docs/HISTORIAL.md` (H16) | Documentación |

## 3. Comandos y resultado (desde `backend/`)
| Comando | Resultado |
|---|---|
| `dotnet build Profesiograma.slnx -c Release` | Compilación correcta, 0 errores |
| `dotnet test --solution Profesiograma.slnx -c Release` | **1158/1158** (1123 + 35); ninguna prueba existente se ajustó |
| Análisis sintáctico de `cargos.ps1` (sin ejecutarlo) | 0 errores; archivo 100 % ASCII |
| `00_ESTADO_ACTUAL.md` | **24 544 B**; lo reemplazado va textual a `HISTORIAL.md` H16 (0 líneas perdidas frente a `HEAD`) |
| Frontend | Sin cambios |

## 4. Desviaciones, errores y decisiones
- **A3 (corrección de la Fase A):** `CargoInforConfiguracion` **no** define `RowVer`, y `ConAuditoria` tampoco lo agrega. Mi Fase A dijo «Hay `RowVer`»: era un error. Coincide con `FASE_2_modelo_profesiograma.sql`, así que no hay contradicción. Se usa compare-and-set por valores, sin migración.
- **Orden del script (ajuste del 29/10/2026):** c00 lista, c01 vista previa, c02 importar, c03 importar otra vez; después elige el candidato (sin código y sin personas guardadas); luego c04–c09, c10 gestor (403) y r00 restauración. Sin candidato tras c03, se detiene antes de c04 sin escribir nada más y sin archivo de estado (la importación no se revierte). Se quitó la opción `IMPORTAR_SIN_CANDIDATO`.
- **Archivo de estado presente al empezar:** el script no restaura solo (no tiene el texto del cargo para releerlo). Se detiene y pide el SQL del LEEME.
- **Rutas:** se registraron con `MapCargoInforEndpoints` en un archivo propio, no dentro de `EmpleadoEndpoints`.
- **Respuestas:** el PUT devuelve `{ id, codigoInfor, activo }`, no la fila completa. El 404 trae un ProblemDetails con «El cargo no existe.».
- **Variantes:** se cuentan entre los cargos de la API y los guardados, siempre que el grupo tenga al menos uno de la API. Un grupo formado solo por cargos guardados no se informa. La respuesta trae hasta 20 grupos, más su total (`gruposVariantes`).
- **`fusionados`** cuenta también los puestos que coinciden tras el recorte a 200, no solo los que difieren en mayúsculas o ancho.
- **Filtro `sinCodigo`:** también considera vacío un `CodigoInfor` en blanco, coherente con el reporte, que lo recorta.
- **Pendiente 39 movido a `HISTORIAL.md`:** ya estaba resuelto; lo moví para mantener el vigente por debajo de 25 KB.

## 5. Verificación para el usuario
Requisitos: **reiniciar la API** (endpoints nuevos), en el modo actual (Http). Instrucciones en `backend/tests/manual/tarea25-3/LEEME.md`.

| # | Caso | Esperado |
|---|---|---|
| V1 | `cargos.cmd` | «Casos con FALLA: 0; OMITIDOS: 0» y sin «RESTAURACIÓN PENDIENTE». Si se detiene antes de c04 por falta de candidato, pegue la salida |
| V2 | c01 frente a c02 | `nuevos` de la vista previa ≈ `insertados` de c02 (puede variar si la caché tenía otra lista); c03 con `insertados = 0` |
| V3 | SQL de solo lectura del LEEME | El cargo elegido con `CodigoInfor` NULL, `Activo = 1` y `ModificadoPorId` = el admin; las filas nuevas con `CreadoPorId` = el admin |
| V4 | Log de la API | Un Information por importación (conteos) y por PUT (Id y sí/no de código); sin nombres de puestos ni códigos |
| V5 | Regresión: «Reporte» de un proyecto (descarga Excel) | Igual que antes: la columna CARGO INFOR sigue con los códigos de las semillas |

## Verificación del usuario
29/10/2026, `cargos.cmd` en modo Http: 0 FALLA y 0 OMITIDOS (c00–c10d y r00).
- **Vista previa:** puestosApi 498, nuevos 491, yaExistentes 7, existentesInactivos 0, fusionados 0, variantes 0.
- **Importación:** insertados 491; la reimportación, insertados 0.
- **SSMS:** `CargoInfor` pasó de 8 a 499 filas; las que no tienen código, de 5 a 496; inactivos 0.
- **Cargo de prueba:** el Id 193 (ABOGADO CORPORATIVO) quedó restaurado (sin código y activo), con auditoría a nombre del admin.
- **Cuadre:** 499 = 498 + 1, porque uno de los 8 cargos previos no está entre los puestos de los activos del ERP.

## 6. Pendientes
- Verificación (§5).
- 25-3-2: frontend, tarjeta «Cargos Infor» en `/admin`.
- Pendientes 2 (Excel, opcional) y 73 (join del reporte sensible a tildes y espacios internos).
