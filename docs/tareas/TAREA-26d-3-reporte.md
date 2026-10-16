# TAREA-26d-3 — Comparación "guardado frente al ERP" y retiro de `empleadoId`

**Fecha:** 2026-10-13 (Fase A y Fase B)
**Resultado:** ✅ **cerrada.** Verificación del usuario C1–C6 OK (14 y 16/10/2026; sección 8). En el cierre solo se ajustaron el script `retiro.ps1` y el `LEEME.md` de `tarea26d3` (sección 8.2), sin tocar código de la aplicación.
- `dotnet build Profesiograma.slnx -c Release`: **0 advertencias, 0 errores**.
- `dotnet test --solution Profesiograma.slnx -c Release`: **604/604** (591 + 13 nuevas; 8 pruebas de transición retiradas o reescritas, sección 4.1).
- Frontend: `npm run build` OK; `npm run lint` (oxlint) sin hallazgos; `npm test` **222/222** (217 + 5 nuevas; ninguna ajustada).
- `grep` de `X-Dev-User` fuera de `src/auth/`: 0.
- Campo de transición `empleadoId` restante en `backend/src` y `frontend/src`: **0**. Solo quedan la FK de dominio y persistencia, los Id de respuesta (tramos, días, cruces) y comentarios que dicen que se retiró o no se envía.
- **Sin cambios de esquema ni migraciones.**

**Lo que no se hizo:** ninguna llamada a una API (ni la nuestra ni `backstack.sedemi.com`), ningún SQL contra ninguna base (los `caso_a_*.sql` se escribieron, no se ejecutaron), `dotnet run`, `npm run dev`, `user-secrets` ni comandos git que modifiquen.

## 1. Decisiones del usuario (Fase A)
| # | Decisión |
|---|---|
| D1 | Solo informar. No se refresca `Empleado` ni `ProyectoPersonal.CargoAsignado` |
| D2 | Endpoint nuevo `GET /api/proyectos/{id}/personal/comparacion-erp` (misma visibilidad que `…/edicion`; 404 si no es visible) |
| D3 | "Actualizar personal": solo vigentes. "Reactivar": propuesto (R7) + históricas (insignia por fila, sin columna nueva) |
| D4 | Texto único con ERP caído: "No se pudo comparar con el ERP." (una vez por pantalla; reemplaza el de R7). Con lista anterior: "Comparado con la lista de las HH:mm; el ERP no respondió." |
| D5 | La unidad entra en `CAMBIO_DEPARTAMENTO` |
| D6 | Script SQL de prueba con reversión, solo `PROFESIOGRAMA_DEV`; no lo ejecuta Claude |

## 2. Qué se hizo
**Backend: retiro de `empleadoId`**
- `PrincipalSolicitud`, `BackSolicitud`, `PrincipalEdicionSolicitud` y `BackEdicionSolicitud`: `CodigoEkon` ocupa el lugar de `EmpleadoId` y ya no es un parámetro opcional al final.
- `CatalogoEmpleados`:
  - sin `ReferenciaEmpleado`, `filasPorId` ni `MensajeAmbos`;
  - `CargarAsync` recibe los códigos y `Validar(codigoEkon, clave, e)`;
  - los errores van siempre a `…codigoEkon`.
- `CrearProyectoValidador`, `EdicionPersonalValidador.ResolverEmpleado`, `EdicionPersonalServicio` y `ReactivacionServicio`: sin la rama de `empleadoId`. Una vigente con un `codigoEkon` distinto del suyo → 400 en `…codigoEkon`.
- Un `empleadoId` enviado se ignora como campo desconocido. Si es lo único que llega: 400 `…codigoEkon` "El empleado es obligatorio.".

**Backend: comparación**
- `Application/Empleados/ComparacionEmpleadosErp.cs` (lógica pura):
  - records `EmpleadoGuardado`, `AvisoErpDto`, `AvisosPersonaErpDto`, `ComparacionErpDto`;
  - avisos `NO_ACTIVO`, `CAMBIO_NOMBRE`, `CAMBIO_CARGO` (puesto con `NormalizarCargo`) y `CAMBIO_DEPARTAMENTO` (departamento · unidad; una unidad igual al departamento no cuenta);
  - normalización del buscador: espacios extremos e internos, vacío = null, sin mayúsculas ni tildes.
- `Application/Proyectos/Personal/ComparacionErpServicio.cs`:
  - visibilidad como `…/edicion`; compara todas las personas guardadas del proyecto con `ObtenerActivosAsync` (caché), **nunca** con una lectura fresca;
  - `ErpNoDisponibleException` → `NoComparado()` (200 con aviso general);
  - sin personas guardadas, no llama a la API.
- `IDatosReferenciaProyecto.ObtenerEmpleadosPorIdsAsync` (solo servía a la transición) → `ObtenerEmpleadosGuardadosAsync`. `ConsultaEmpleadosPorIds` se reutiliza y amplía (código, nombre, puesto, departamento, unidad; sin cédula, correo ni filtro de estado).
- `ProyectoEndpoints`: `GET /{id:int}/personal/comparacion-erp` → 200 o 404.
- `ReactivacionServicio.AdvertenciaErpNoVerificado` = `ComparacionEmpleadosErp.MensajeNoComparado` (D4).

**Frontend**
- `tipos.ts`: `AvisoErp`, `AvisosPersonaErp`, `ComparacionErp`. `api.ts`: `obtenerComparacionErp` y la clave `comparacionErp` bajo `proyectos`. `hooks.ts`: `useComparacionErp` (consulta aparte, `staleTime` de 5 min).
- `comparacionErp.ts` (lógica pura): `insigniaErp`, `avisosErpDe`, `avisosErpFilas`, `avisosErpHistoricas`, `notaGeneralErp` y `sinNotaErp`.
- `components/NotaComparacionErp.tsx`:
  - "Comparando con el ERP…" mientras carga;
  - `MessageBar` informativa con la nota general y "Reintentar" si el ERP no respondió o la consulta falló.
- `ListaPersonalEdicion.tsx`:
  - props `erpFilas` y `erpHistoricas`;
  - insignia "No activo en el ERP" (roja) o "Datos distintos en el ERP" (amarilla) en la tarjeta, con los mensajes entre sus advertencias;
  - en la tabla de históricas, insignia y detalle en la celda de la persona (sin columna nueva).
- `ActualizarPersonal.tsx`: la comparación se muestra solo en las vigentes (`id !== null`).
- `ReactivarProyecto.tsx`:
  - insignias en las históricas y en las filas nuevas cuyo código es de una persona guardada (así las recibe el propuesto);
  - la nota del ERP se quita de las advertencias del GET (`sinNotaErp`) y se muestra una sola vez.
- Nada de esto deshabilita la vista previa ni el registro.

## 3. Archivos
- **Backend nuevos:** `src/App.Application/Empleados/ComparacionEmpleadosErp.cs`, `src/App.Application/Proyectos/Personal/ComparacionErpServicio.cs`, `tests/App.Application.Tests/Empleados/ComparacionEmpleadosErpTests.cs`.
- **Backend modificados:**
  - `src`: `CatalogoEmpleados.cs`, `CrearProyectoSolicitud.cs`, `CrearProyectoValidador.cs`, `CrearProyectoContratos.cs`, `EdicionPersonalContratos.cs`, `EdicionPersonalValidador.cs`, `EdicionPersonalServicio.cs`, `ReactivacionServicio.cs`, `DependencyInjection.cs`, `Infrastructure/Consultas/DatosReferenciaProyecto.cs`, `Api/Endpoints/ProyectoEndpoints.cs`;
  - pruebas: `Dobles.cs`, `DoblesPersonal.cs`, `DoblesReactivacion.cs`, `EmpleadosApiTests.cs`, `CrearProyectoServicioTests.cs`, `CrearProyectoValidadorTests.cs`, `EdicionPersonalServicioTests.cs`, `EdicionPersonalH12Tests.cs`, `PrincipalOpcionalTests.cs`, `ReactivacionServicioTests.cs`, `Infrastructure.Tests/Consultas/EmpleadosApiSqlTests.cs`, `Api.Tests/SolicitudIlegibleTests.cs`.
- **Frontend nuevos:** `comparacionErp.ts`, `comparacionErp.test.ts`, `components/NotaComparacionErp.tsx`.
- **Frontend modificados:** `tipos.ts`, `api.ts`, `hooks.ts`, `camposVisibles.ts` (comentario), `components/ListaPersonalEdicion.tsx`, `pages/ActualizarPersonal.tsx`, `pages/ReactivarProyecto.tsx`.
- **Scripts:** `backend/tests/manual/tarea26d3/` (nuevo: `comparacion.cmd/.ps1`, `retiro.cmd/.ps1`, `caso_a_preparar.sql`, `caso_a_revertir.sql`, `simulacion\`, `.gitignore`, `LEEME.md`); `tarea26d/LEEME.md` y `tarea26d/transicion.ps1` (V10 marcado como obsoleto).
- **Docs:** `FASE_4_Empleados_API.md` (§5, §8 nueva, §9), `FASE_5_Crear_Proyecto.md`, `FASE_5_Edicion_Cronograma.md`, `FASE_5_Estados_Proyecto.md`, `00_ESTADO_ACTUAL.md` y este reporte.

## 4. Pruebas
### 4.1 Pruebas de transición retiradas o reescritas (aprobado)
| # | Prueba (antes) | Reemplazo |
|---|---|---|
| 1 | `EmpleadosApiTests.Catalogo_Transicion_EmpleadoIdOCodigo_AmbosONinguno400` (ambos → `MensajeAmbos`; ninguno → `…empleadoId`; inactivo/inexistente por Id) | **Retirada.** Reemplazo: `Catalogo_SoloCodigoEkon_ObligatorioEInactivo400_EnCodigoEkon` (null o vacío → "obligatorio" en `…codigoEkon`; inactivo e inexistente por código; ninguna clave `…empleadoId`) |
| 2 | `CrearProyectoValidadorTests` "empleado obligatorio": `EmpleadoId = null` → `principales[0].empleadoId` | `CodigoEkon = null` → `principales[0].codigoEkon` |
| 3 | `CrearProyectoValidadorTests` "empleado inactivo o inexistente": `EmpleadoId = 9` → "El empleado 9…" en `…empleadoId` | `CodigoEkon = "DEV009"` → "El empleado DEV009…" en `…codigoEkon` |
| 4 | `CrearProyectoValidadorTests` "back empleado inexistente": `EmpleadoId = 99` | `CodigoEkon = "X99"` → `backs[0].codigoEkon` |
| 5 | `EdicionPersonalServicioTests.P9_CambioDeEmpleadoDeUnVigente_400`: empleado 7 → `…empleadoId` | `"DEV007"` → `principales[0].codigoEkon` (mismo mensaje) |
| 6 | `EdicionPersonalServicioTests.ClavesRepetidas_YEmpleadoNuevoInactivo_400`: "El empleado 9…" en `backs[1].empleadoId` | "El empleado DEV009…" en `backs[1].codigoEkon` |
| 7 | `ReactivacionServicioTests.EmpleadoInactivo_400`: "El empleado 9…" en `principales[0].empleadoId` | "El empleado DEV009…" en `principales[0].codigoEkon` |
| 8 | `EmpleadosApiTests.Crear_Registro_LecturaFresca_YAltaConDatosDeLaApi`: principal 2 "por empleadoId (transición)" | Principal 2 por código `"DEV007"` (con fila): sigue comprobando el Id real 7 |

**Ajustes mecánicos** (sin cambiar lo que se comprueba): las fixtures pasan del Id posicional al código.
- `Dobles.SolicitudCampo`: 6/7/8 → "DEV006"/"DEV007"/"DEV008".
- `DoblesPersonal.SolP1`/`SolK1`/`BackNuevo` y `DoblesReactivacion.Principal`/`Back`: el `int` se convierte a `$"DEV{n:000}"`.
- Las construcciones de `EdicionPersonalH12Tests`, `EdicionPersonalServicioTests` (P8 ×2, `IdDeLaOtraLista`, `Relaciones`), `PrincipalOpcionalTests` (envío de guardadas con su `CodigoEkon`), `ReactivacionServicioTests` (`IdPresente`), `CrearProyectoServicioTests` (`EmpleadoId = 6` → `CodigoEkon = "DEV006"`) y `EmpleadosApiTests` (helper `Catalogo` por códigos).
- `DatosFalsos` implementa `ObtenerEmpleadosGuardadosAsync` (con `Guardados` y `LecturasGuardados`).

### 4.2 Pruebas nuevas
- **Backend (13):**
  - `ComparacionEmpleadosErpTests` (11). Lógica pura (7): sin diferencias con tildes, mayúsculas y espacios; `NO_ACTIVO`; los tres cambios con valores; unidad dentro de departamento (y unidad = departamento no cuenta); vacío frente a valor; `Comparar` (solo con avisos, sin repetir, orden, `comparadoUtc`); lista anterior con la hora de Ecuador, `NoComparado` y texto único D4. Servicio (4): sin diferencias (caché, nunca fresca); cambio de puesto guardado sin escribir; ERP caído → aviso general, no 503; no visible o inexistente → null (404).
  - `EmpleadosApiSqlTests.EmpleadosGuardadosPorIds_…` (1): `ToQueryString` de `ConsultaEmpleadosPorIds` ampliada (columnas laborales; sin `EstadoErp`, `Cedula` ni `CorreoEmpresa`).
  - `SolicitudIlegibleTests.EmpleadoIdRetirado_SeIgnora_LaPersonaQuedaSinCodigo` (1).
- **Frontend (5):** `comparacionErp.test.ts` — insignias; avisos por código sin distinguir mayúsculas ni espacios; filas por clave e históricas por Id; nota general (ERP caído, consulta fallida, lista anterior, comparada); `sinNotaErp` (D4).

## 5. Comandos
| Comando | Resultado |
|---|---|
| `dotnet build Profesiograma.slnx -c Release` (tras el retiro, antes de ajustar pruebas) | Errores solo en pruebas: constructores posicionales y `ReferenciaEmpleado` → sección 4.1 |
| `dotnet build Profesiograma.slnx -c Release` | 0 advertencias, 0 errores |
| `dotnet test --solution Profesiograma.slnx -c Release` | 604/604 |
| `npx tsc -b`, `npm run build`, `npm run lint`, `npm test` | OK, OK, OK, 222/222 |
| `comparacion.cmd` y `retiro.cmd` con `SIMULAR=1` (lanzador en el scratchpad) | 2/2 OK (15 con avisos, 17 con aviso general, 99 → 404; retiro a) 400 en `codigoEkon`, b) 200) |

**Errores y cómo se resolvieron:**
1. **Heredoc de bash con comillas mezcladas:** los scripts de edición se escribieron como archivos `.py` en el scratchpad.
2. **Barras invertidas convertidas en tabuladores** en `tarea26d/LEEME.md` (`..\tarea26d3`): se corrigieron y se verificó que no quedan tabuladores.
3. **`retiro.ps1` leía el archivo del usuario en simulación:** en el primer ensayo con `SIMULAR=1` leyó `..\tarea26d\crear-proyecto-1.json`. Falló al indexar `principales` y **no mostró su contenido**. Ahora en simulación usa siempre `crear-proyecto.ejemplo.json`, y con un archivo sin `principales` da un mensaje claro en lugar de un error de PowerShell.

## 6. Verificación del usuario (gestor, modo Http salvo indicación)
Requisitos: API reiniciada con este código y `npm run dev`. Scripts en `backend/tests/manual/tarea26d3/` (ver su `LEEME.md`).

| # | Caso | Pasos | Esperado | ¿Escribe? | Resultado |
|---|---|---|---|---|---|
| X1 | No activo (b) en pantalla | Abrir "Actualizar personal" de un proyecto ACTIVO con personas DEV vigentes (no están en la API en Http) | Insignia roja "No activo en el ERP" y "Ya no está activo en el ERP." en esas tarjetas; vista previa y registro habilitados | No | |
| X2 | Sin diferencias | "Actualizar personal" del Id 15 (personas reales) | Sin insignias ni nota; nada bloqueado | No | |
| X3 | Cambio de cargo y departamento (a) | `caso_a_preparar.sql` (SSMS) con `@CodigoEkon` de una vigente del Id 15 → abrir "Actualizar personal" del Id 15 (o `comparacion.cmd`) → `caso_a_revertir.sql` | Insignia amarilla "Datos distintos en el ERP" con los dos mensajes; tras revertir y recargar, sin avisos | **Sí (1 fila de `Empleado`, revertida)** | |
| X4 | Reactivar | Suspender un proyecto con personas guardadas → "Reactivar" | Insignias en las históricas con diferencias y en el propuesto si corresponde; sin columna nueva | Sí (suspensión) | |
| X5 | ERP caído (c) | User-secrets con la base 7048 inválida, reiniciar, sin lista previa (o más de 60 min) → "Actualizar personal" y "Reactivar" | Una sola nota "No se pudo comparar con el ERP." con "Reintentar"; sin insignias; la pantalla carga normal (nunca 503). Restaurar los user-secrets | No | |
| X6 | Scripts | `comparacion.cmd` (C1, C2, C6) y `retiro.cmd` (C5) | Según `tarea26d3/LEEME.md` | No | |

## 7. Pendientes
- **Pendiente 46 (opcional, D1):** acción "Actualizar datos guardados del empleado".
- **Supuestos:**
  - en "Reactivar", una fila nueva recibe avisos si su código es de una persona guardada del proyecto (así los recibe el propuesto R7);
  - en "Actualizar personal" solo se muestran en las vigentes (D3).

## 8. Verificación del usuario (14 y 16/10/2026)
### 8.1 Resultados
| # | Caso | Resultado |
|---|---|---|
| C1 | (b) No activo (proyectos con DEV001–DEV008 en modo Http) | ✅ OK |
| C2 | Sin diferencias (Id 15 y 17) | ✅ OK |
| C3 | (a) Cambio de cargo y departamento (`caso_a_preparar.sql` → comparación → `caso_a_revertir.sql`) | ✅ OK |
| C4 | (c) ERP caído: 200 con "No se pudo comparar con el ERP.", nunca 503 | ✅ OK |
| C5 | Retiro de `empleadoId` (`retiro.cmd`) | ✅ OK |
| C6 | No visible → 404 | ✅ OK |

**Nota de C3:** la primera ejecución de `caso_a_revertir.sql` se hizo con un valor original equivocado. Se corrigió con los valores del SELECT "ANTES" y se verificó con `comparacion.cmd`: sin avisos.

### 8.2 Hallazgos de los scripts (corregidos en el cierre)
1. **`retiro.ps1`:** cuando un caso no obtenía el código esperado, solo se veía el código HTTP. Ahora muestra:
   - "NO ESPERADO";
   - el `title` y el `detail` del ProblemDetails;
   - **todas** las claves de `errors` con sus mensajes.
   - Nunca muestra valores de la solicitud.
   - Distingue el 400 de cuerpo ilegible (sin errores por campo).
   - Validado solo con `SIMULAR=1`: `simulacion/retiro_b.json` + `retiro_b.codigo` es ahora un 400 de cuerpo ilegible ficticio, y el caso a) sigue con su 400 esperado.
2. **`LEEME.md` de `tarea26d3`:**
   - requisito: en `crear-proyecto-1.json`, `proyectoErpId` y `actividadId` son texto entre comillas;
   - C4: usar `https://localhost:1` como URL inválida de `ServiciosExternos:ErpBase7048` y restaurar con `dotnet user-secrets remove "ServiciosExternos:ErpBase7048"`, porque el valor real viene de `appsettings.json`.
