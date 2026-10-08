# TAREA-26d-2 — Frontend: empleados por `codigoEkon` (opción C)

**Fecha:** 2026-10-08
**Resultado:** implementada. Falta la verificación visual del usuario (W1–W6, sección 6).
- `npm run build`: compila.
- `npm run lint` (oxlint): sin errores ni advertencias.
- `npm test`: **217/217**. Son las 205 de la 19c/26d-1: 6 con aserciones ajustadas de forma mecánica (sección 4.1) y el resto sin cambios. Más 12 nuevas.
- `grep` de `X-Dev-User` fuera de `src/auth/`: **0**.
- `grep` de `empleadoId` / `empleado.id` en el envío de personas: **0**. Solo quedan en comentarios, en los tipos de respuesta (tramos, días y cruces, donde el servidor devuelve el Id real o temporal) y en las claves de React de `ImpactoCabecera` / `ImpactoRecorte` (personas guardadas).
- Backend: solo el paso 0 (corrección del 500, documentada en `TAREA-26d-1-reporte.md` §8). `dotnet build -c Release` 0/0; `dotnet test` **591/591**.

**Lo que no se hizo:** ninguna llamada a una API, `npm run dev`, `dotnet run`, SQL ni comandos git que modifiquen. La 26d-3 queda fuera.

## 1. Paso 0 — Defecto 500 de la vista previa
No estaba corregido: la corrección anterior se revirtió a pedido del usuario. Se rehízo así:
1. **Prueba primero.** `tests/App.Api.Tests/SolicitudIlegibleTests.cs` arma un pipeline en memoria, sin red. Con el cuerpo de V3 (`"actividadId": 20`), 8 de las 10 pruebas dieron **500**: el defecto quedó reproducido.
2. **Corrección.** `SolicitudIlegibleExceptionHandler` + `AddErroresApi`, con `ThrowOnBadRequest` en todos los entornos. Ahora responde 400 con el mensaje en español en su campo; por ejemplo, `actividadId`: "Debe ser texto entre comillas (p. ej. "20")".
3. **Resultado.** 10/10.

El detalle (causa, corrección y rutas revisadas) está en `TAREA-26d-1-reporte.md` §8. El `LEEME.md` de `tarea26d` indica ahora el tipo y el origen de cada valor.

## 2. Qué se hizo (sección G del plan de la 26d)
| Punto | Cambio |
|---|---|
| 1. Tipos | `EmpleadoBusqueda` sin `id`, con `codigoEkon` y `unidad`. `ResultadoBusquedaEmpleados` con `avisoErp`. `PrincipalSolicitud`, `BackSolicitud`, `PrincipalEdicionSolicitud` y `BackEdicionSolicitud` con `codigoEkon`; **sin** `empleadoId`. Comentarios: `empleadoId` negativo en tramos y `EmpleadoEdicion` = persona sin fila |
| 2. Buscador | Lógica pura en `buscadorEmpleados.ts`: `aEmpleadoFila`, `detalleEmpleado` (con la unidad si es distinta del departamento), `marcasEmpleado` y `errorBuscador`. En el componente: clave y `key` por código, marcas "Ya agregado" por código, `avisoErp` como aviso no bloqueante y 503 "Servicio de empleados no disponible" con "Reintentar" (`refetch`), sin romper la pantalla |
| 3. Formularios | `EmpleadoFila` = `{ codigoEkon, nombreCompleto, cargo }`. `claveEmpleado` (sin espacios extremos, en mayúsculas) para comparar y marcar. `etiquetasPorEmpleado` y `etiquetasPorEmpleadoEdicion` son `Map<string, …>` por código. `cambiarEmpleado` compara códigos. `aSolicitud` envía `codigoEkon`. `aSolicitudPersonal` envía `codigoEkon` solo en las nuevas (`id === null`) y `null` en las vigentes, que van solo con `id`. Los Id negativos en tramos y vista previa no se tratan como error (solo forman claves). El 503 al registrar ya ofrecía "Reintentar" en las tres pantallas |
| 4. Reactivación | El propuesto (R7) es una fila nueva: viaja por `codigoEkon`. El aviso "No se pudo verificar los datos en el ERP." llega en `advertencias` del GET y la pantalla ya lo muestra |

Los errores 400 del servidor en `…codigoEkon` llegan a la tarjeta de la fila con `mensajesSinCampo`, como antes `…empleadoId`.

## 3. Archivos
- **Nuevos:** `frontend/src/features/proyectos/buscadorEmpleados.ts` y `buscadorEmpleados.test.ts`.
- **Modificados:** `tipos.ts`, `api.ts`, `formularioProyecto.ts`, `edicionPersonal.ts`, `reactivacion.ts`, `camposVisibles.ts` (comentario), `components/BuscadorEmpleados.tsx`.
- **Pruebas modificadas:** `formularioProyecto.test.ts`, `formularioProyecto.envio.test.ts`, `edicionPersonal.test.ts`, `reactivacion.test.ts`, `tramos.test.ts`.
- **Backend (paso 0):** `src/App.Api/Program.cs`, `src/App.Api/Configuracion/ErroresApiExtensions.cs` (nuevo), `src/App.Api/Configuracion/SolicitudIlegibleExceptionHandler.cs` (nuevo), `tests/App.Api.Tests/` (proyecto nuevo, en `Profesiograma.slnx`) y `tests/manual/tarea26d/LEEME.md`.
- **Documentación:** `TAREA-26d-1-reporte.md`, `00_ESTADO_ACTUAL.md` y este reporte.

## 4. Pruebas
### 4.1 Ajustes mecánicos (aprobados de antemano: `id` / `empleadoId` → `codigoEkon`)
| Archivo | Prueba o fixture | Antes | Ahora |
|---|---|---|---|
| `formularioProyecto.test.ts` | fixture `empleado(n)` | `{ id, codigoEkon, … }` | `{ codigoEkon, … }` |
| `formularioProyecto.test.ts` | "al reordenar, la referencia sigue a la misma persona" | `p.empleadoId` → `[7, 6]` | `p.codigoEkon` → `['DEV007', 'DEV006']` |
| `formularioProyecto.test.ts` | "la misma persona puede ser principal y back (E6)…" | `etiquetasPorEmpleado(…).get(6)` | `.get('DEV006')` |
| `formularioProyecto.envio.test.ts` | fixture `empleado(n)` | `{ id, … }` | sin `id` |
| `edicionPersonal.test.ts` | fixture `empleado(n)` | `{ id, … }` | sin `id` |
| `edicionPersonal.test.ts` | fixture `vistaDe` (vista previa, `EmpleadoEdicion` con `id`) | `empleado: empleado(7)` | `empleado: { id: 7, ...empleado(7) }` |
| `edicionPersonal.test.ts` | "cambiar el empleado de una nueva conserva el resto…" | `nuevo.empleado.id` → `2` | `nuevo.empleado.codigoEkon` → `'DEV002'` |
| `edicionPersonal.test.ts` | "sin históricas; eliminadas omitidas; principalClave o principalId…" | `empleadoId: 7` y `4` (vigentes), `8` (nueva) | `codigoEkon: null` (vigentes), `'DEV008'` (nueva) |
| `reactivacion.test.ts` | fixture `empleado(n)` | `{ id, … }` | sin `id` |
| `reactivacion.test.ts` | "todo el personal guardado es histórico; el propuesto (R7)…" | `empleado: { id: 7, cargo: null }` | `empleado: { codigoEkon: 'DEV007', cargo: null }` |
| `reactivacion.test.ts` | "R, fecha fin y solo las nuevas (sin id)…" | `empleadoId: 7 / 3 / 4` | `codigoEkon: 'DEV007' / 'DEV003' / 'DEV004'` |

En total son 6 pruebas y 4 fixtures, sin cambiar lo que comprueban.

**Para revisar:** en "sin históricas; eliminadas omitidas…", las **vigentes** pasan de llevar su `empleadoId` a `codigoEkon: null`. Es lo que pide el punto 1 ("las vigentes siguen solo con `id`"; el servidor las identifica por `id` y no exige el empleado). La prueba sigue comprobando las filas enviadas, las relaciones y el cargo.

### 4.2 Pruebas nuevas (12)
- `buscadorEmpleados.test.ts` (5): fila sin `id`; detalle con unidad; marcas por código sin distinguir mayúsculas ni espacios; 503 con "Reintentar" (con y sin detalle del servidor); red y 5xx reintentables, 400 no.
- `formularioProyecto.test.ts` (2): solicitud con `codigoEkon` y sin `empleadoId`; etiquetas por código.
- `edicionPersonal.test.ts` (3): nuevas con `codigoEkon` y vigentes con `null`, sin `empleadoId`; cambiar al mismo código con otra capitalización no hace nada; etiquetas por código con las históricas.
- `reactivacion.test.ts` (1): el propuesto viaja por `codigoEkon`, sin `empleadoId`.
- `tramos.test.ts` (1): `unirTramosContiguos` une los tramos de un Id temporal negativo y no los mezcla con otro temporal.

## 5. Comandos
| Comando | Resultado |
|---|---|
| `npx tsc -b` (tras los cambios, antes de ajustar pruebas) | 5 errores, solo en pruebas (fixtures con `id` y aserciones con `empleadoId`) → ajustes de la sección 4.1 |
| `npx vitest run` (antes de los ajustes de aserción) | 3 fallos, todos de `empleadoId` → `codigoEkon` (sección 4.1) |
| `npm run build` | OK |
| `npm run lint` | OK, sin hallazgos |
| `npm test` | 217/217 |
| `dotnet build Profesiograma.slnx -c Release` | 0 advertencias, 0 errores |
| `dotnet test --solution Profesiograma.slnx -c Release` | 591/591 |

## 6. Verificación del usuario (usuario gestor, modo Http salvo W5)
Antes de empezar: reinicie la API (incluye el paso 0) y `npm run dev`.

| # | Caso | Pasos | Esperado | ¿Escribe? | Resultado |
|---|---|---|---|---|---|
| W1 | Nuevo proyecto con empleados reales | "Nuevo proyecto" → "Agregar principal": buscar un apellido con y sin tildes; sin texto, SIG ≈ 85; "Ver otros departamentos" (≈ 1629). Agregar un principal y un back reales → vista previa → Registrar → detalle | Mismo total con y sin tildes; ítems con "código · cargo · departamento"; "Ya agregado: P1" por código; vista previa sin errores con personas sin fila; 201 → detalle con el código | **Sí** (proyecto nuevo + alta puntual) | |
| W2 | Actualizar personal del Id 15 | Agregar una persona real nueva y otra que ya tenga fila (9940 o 11358 en otro rol, si no cruza) → vista previa → Registrar | Vista previa con la nueva (Id temporal) y la existente (Id real); registro OK, "Personal actualizado (versión n)." | **Sí** | |
| W3 | Reactivar con un principal real nuevo | Suspender un proyecto desde la pantalla → "Reactivar" → cambiar el principal propuesto por un empleado real nuevo → vista previa → Registrar | "Proyecto reactivado (versión n)."; el nuevo principal queda dado de alta | **Sí** | |
| W4 | ERP caído | User-secrets con la base 7048 inválida y reiniciar la API: abrir el buscador antes de 60 min de la última lista (aviso amarillo "Lista de empleados de las HH:mm; el ERP no respondió.") o después (503 "Servicio de empleados no disponible" + "Reintentar"); abrir detalle y "Actualizar personal" de un proyecto; registrar una persona nueva | Aviso o 503 sin romper la pantalla; detalle y edición sin errores; registrar → 503 con "Reintentar", **sin escribir**. Restaurar los user-secrets después | No (el registro debe fallar) | |
| W5 | Modo Simulado | Quitar `ServiciosExternos:Modo` y reiniciar → buscador con DEV y SIM → crear con SIM, sin cruces | DEV001–DEV008 y SIM001…; registro 201 | **Sí** | |
| W6 | Listado y detalle | Abrir el listado y el detalle de los Id 1–15 | Sin errores | No | |

## 7. Pendientes
- **Verificación del usuario** (W1–W6).
- **TAREA-26d-3:** avisos "guardado frente al ERP" por persona en edición y reactivación, y retiro de `empleadoId` en el backend.
