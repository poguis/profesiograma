# TAREA-13 — Pantalla "Nuevo proyecto" (React)

**Fecha:** 2026-10-01 (Fase A, B0, B1 y B2)
**Resultado:** ✅ completada. Verificación visual del usuario: todas OK (sección 9).
- Backend (B0): `dotnet build -c Release` **0 advertencias / 0 errores**; `dotnet test` **232/232**; `has-pending-model-changes` sin cambios. F1–F3 verificados por el usuario.
- Frontend: `npm run build` sin errores ni advertencias; `npm run lint` (oxlint) sin hallazgos; `npm test` **44/44**.

No se ejecutaron `npm run dev`, la API ni SQL. No se usaron comandos git que modifiquen el repositorio. No se crearon datos en la base.

## 1. Decisiones aprobadas (Fase A)
| # | Decisión |
|---|---|
| D1 | Vitest para la lógica pura (reducer, `aSolicitud`, errores, habilitación). **Versión fijada 5.0.3** (peer `vite ^6.4.0 \|\| ^7.0.0 \|\| ^8.0.0`; el proyecto usa Vite 8.3.1). Sin jsdom (entorno node) |
| D2 | Una sola página con secciones (Cabecera, Principales, Backs, Vista previa) |
| D3 | Confirmación al salir con `useBlocker` (App usa `createBrowserRouter`) + Dialog, y `beforeunload`. Ambos se desactivan después del 201 |
| D4 | La validación del cliente solo ayuda; no bloquea el envío. El mensaje del servidor reemplaza al del cliente |
| D5 (b) / D6 | Endpoint `GET /api/proyectos/opciones-formulario` (sección 2) |
| D7 | Tras el 201: `navigate(..., { replace: true })` |
| Δ3 | `cargo: null` en los backs (el servidor usa el puesto del empleado) |
| R9 | Los límites del endpoint deshabilitan "Agregar" y limitan los días de descanso; el servidor sigue siendo la autoridad |
| — | "Agregar principal/back" deshabilitado mientras falte la fecha de inicio o fin del proyecto (con texto de ayuda) |
| Almuerzo | Clase compartida `ReglasAlmuerzo` en Application, usada por el validador y por el endpoint (respuesta del usuario a la contradicción de la sección 2) |

## 2. B0 — Backend: opciones del formulario
**Contradicción encontrada y resuelta:** el validador usaba **constantes privadas** para el almuerzo (salida 11:00–14:00, regreso 12:00–15:00), no los parámetros `ALMUERZO_SALIDA_OPCIONES` / `ALMUERZO_REGRESO_OPCIONES`. Por decisión del usuario:
- se creó `ReglasAlmuerzo` (rangos de RN09 + opciones cada hora);
- el validador la usa sin cambiar su comportamiento (los mensajes son idénticos y las pruebas existentes pasan sin modificarse);
- los parámetros quedan sin uso → pendiente 18 en `00_ESTADO_ACTUAL.md`.

Los **límites** sí salían de `Parametro` en el validador (`ObtenerLimitesAsync`); el endpoint usa la misma llamada. Los **departamentos** usan `ObtenerDepartamentosDeUsuarioAsync`, igual que P5 en el validador. **No hay consultas EF nuevas** (las dos reutilizadas ya tenían prueba de traducción de la TAREA-12).

`GET /api/proyectos/{id}` ya tenía la restricción `{id:int}`; no se modificó.

| Archivo | Cambio |
|---|---|
| `App.Application/Proyectos/Crear/ReglasAlmuerzo.cs` | Nuevo: rangos RN09, `OpcionesSalida` / `OpcionesRegreso`, `SalidaEnRango` / `RegresoEnRango`, `Formato` |
| `App.Application/Proyectos/Crear/OpcionesFormularioProyecto.cs` | Nuevo: `OpcionesFormularioProyectoDto` y `OpcionesFormularioProyectoServicio` |
| `App.Application/Proyectos/Crear/CrearProyectoValidador.cs` | Usa `ReglasAlmuerzo` en lugar de sus constantes |
| `App.Application/DependencyInjection.cs` | Registra el servicio |
| `App.Api/Endpoints/ProyectoEndpoints.cs` | `GET /api/proyectos/opciones-formulario` (política Gestor del grupo) |
| `App.Api/Profesiograma.Dev.http` | F1 gestor 200, F2 anonimo 401, F3 `/api/proyectos/4` 200 |
| `tests/App.Application.Tests/Proyectos/Crear/ReglasAlmuerzoTests.cs` | 12 pruebas (opciones generadas, bordes de los rangos) |
| `tests/App.Application.Tests/Proyectos/Crear/OpcionesFormularioProyectoServicioTests.cs` | 3 pruebas (límites, varios departamentos, sin usuario) |

Respuesta verificada por el usuario (F1): `departamentos [{id 7, "UNIDAD SISTEMA INTEGRADO DE GESTION"}]`, salida `["11:00","12:00","13:00","14:00"]`, regreso `["12:00","13:00","14:00","15:00"]`, `maxPrincipales 20`, `maxBacks 20`, `backMaxDiasDescanso 20`. F2 401. F3 200.

## 3. B1 — Cabecera, personal y cliente HTTP (R1–R10, R14)
| Requisito | Implementación |
|---|---|
| R1 | Ruta `proyectos/nuevo` con `lazy`, declarada antes de `proyectos/:id`. Botón "Nuevo proyecto" en el listado |
| R2 | Compañía → Grupo → (Proyecto ERP → Actividad) o Dimensión según `requiereProyectoErp` / `requiereDimension` del catálogo |
| R3 | `reducerFormulario`: grupo y compañía limpian ERP, actividad y dimensión; el proyecto ERP limpia la actividad; elegir el mismo valor no limpia. `aSolicitud` envía `null` (nunca `""`) en los campos que el grupo no usa, aunque el estado tuviera valor |
| R4 | Fechas con `SelectorFecha`, horario (solo activos), almuerzo con las opciones del endpoint. Ayudas: fin ≥ inicio, regreso > salida |
| R5 | 1 departamento → automático (texto); varios → selector; ninguno → oculto |
| R6 | `BuscadorEmpleados` (Dialog): espera de 400 ms, paginación 5/10/20, "Ver otros departamentos", marca "Ya agregado: P1" sin bloquear (E6); queda abierto para agregar varias personas |
| R7 | Tarjetas con jornada, fechas (por defecto el rango del proyecto, P2) y cargo opcional; numeración 1..n; ↑ ↓ y quitar |
| R8 | El back referencia al principal por **clave local**: reordenar no rompe la referencia; si se quita el principal → "Sin relación" + aviso. DESCANSO oculta los días y envía 0 |
| R9 | Límites del endpoint: "Agregar" deshabilitado al máximo (y el reducer lo ignora); días de descanso entre 0 y `backMaxDiasDescanso` |
| R10 | Cambiar las fechas del proyecto no toca las del personal; advertencia en cada fila fuera del rango |
| R14 | `apiPost<TReq, TRes>`; `ErrorApi` con `extensiones` y tipos `conflicto` (409) y `noDisponible` (503); `conflicto` no se reintenta; `mutations.retry: false`. `obtenerEncabezadosAutenticacion()` sigue siendo el único punto de encabezados |

Horas: el formulario y la solicitud usan solo `HH:mm`. Las del horario del ERP (`HH:mm:ss`) solo se muestran con `formatearHora` (ya existía en `utils/formato.ts`) y no entran al estado.

## 4. B2 — Vista previa y registro (R11–R13, D3)
| Requisito | Implementación |
|---|---|
| R11 | `VistaPrevia`: resumen (personas, tramos, días), cruces con `TablaCruces` (persona · rol · proyecto · mes · días) y tramos por persona en un Accordion (días de trabajo/descanso desde `diasPorPersona`) |
| R12 | La vista previa guarda la `revision` del formulario. Si cambia → aviso "Vista previa desactualizada" y la anterior se muestra atenuada. `puedeRegistrar(vista, revision, enviando)`: vista vigente, sin cruces y sin envío en curso. Doble clic: además del botón deshabilitado, un `ref` descarta el segundo clic aunque llegue antes del siguiente render |
| R13 — 201 | `invalidateQueries(['proyectos'])` y `navigate('/proyectos/{id}', { replace: true, state: { codigoCreado } })`; el detalle muestra "Proyecto PRY-… registrado." |
| R13 — 409 | `crucesDeConflicto(error.extensiones)` reemplaza los cruces de la vista previa (misma `TablaCruces`) y "Registrar" queda deshabilitado; mensaje con título y detalle del servidor |
| R13 — 503 | Mensaje del servidor con botón "Reintentar" (también para error de red). Aplica a previsualizar y a registrar |
| R13 — 400 | `distribuirErrores(errors, clavesDelEnvio)`: cabecera por campo; `principales[i].campo` / `backs[i].campo` → la fila que tenía el índice **en el envío** (el error la sigue si luego se reordena); `principales` / `backs` → encima de la sección; claves desconocidas o índices inexistentes → arriba del formulario. Los errores de campos sin control visible (p. ej. `empleadoId`) se muestran en la tarjeta. Editar un campo borra solo su error (`limpiarErroresServidor`) |
| D3 | `useBlocker` (navegación interna a otra ruta) + Dialog "¿Salir sin registrar?" y `beforeunload`; activos solo si el formulario cambió (`revision > 0`) y desactivados después del 201 (`ref`) |

## 5. Archivos (frontend)
**Nuevos**
| Archivo | Contenido |
|---|---|
| `src/features/proyectos/formularioProyecto.ts` | Estado, reducer, `aSolicitud`, avisos R10, ayudas R4, errores 400, habilitación de "Registrar", lectura del 409 |
| `src/features/proyectos/formularioProyecto.test.ts` | 28 pruebas (B1) |
| `src/features/proyectos/formularioProyecto.envio.test.ts` | 16 pruebas (B2) |
| `src/features/proyectos/pages/NuevoProyecto.tsx` | Página, mutaciones, bloqueo al salir |
| `src/features/proyectos/components/SeccionCabecera.tsx` | Cabecera |
| `src/features/proyectos/components/ListaPrincipales.tsx` / `ListaBacks.tsx` | Tarjetas de personal |
| `src/features/proyectos/components/TarjetaPersona.tsx` | Tarjeta común (encabezado, errores, advertencias, campos en grilla) |
| `src/features/proyectos/components/AyudaAgregarPersonal.tsx` | Por qué "Agregar" está deshabilitado |
| `src/features/proyectos/components/BuscadorEmpleados.tsx` | Búsqueda de empleados |
| `src/features/proyectos/components/VistaPrevia.tsx` / `TablaCruces.tsx` | Vista previa y cruces |
| `src/hooks/useValorConRetardo.ts` | Debounce genérico |

**Modificados:** `src/api/{clienteHttp,errores,tipos}.ts`, `src/app/{App.tsx,queryClient.ts}`, `src/features/proyectos/{api,hooks,tipos}.ts`, `pages/ListadoProyectos.tsx` (botón y `EstadoNavegacionProyectos` con `codigoCreado`), `pages/DetalleProyecto.tsx` (aviso del código), `package.json` (`vitest` 5.0.3 exacta, script `test`), `package-lock.json`.

**Documentación:** `docs/00_ESTADO_ACTUAL.md` (§7 pendiente 14 resuelto y 18–21 nuevos; §8 fila B0; §9 fila TAREA-13), `docs/tareas/TAREA-12-reporte.md` (§9 pendiente "✅ Cerrado en TAREA-12b (§10)").

## 6. Comandos ejecutados y resultado
| Comando | Resultado |
|---|---|
| `npm view vitest version peerDependencies` | 5.0.3; peer `vite ^6.4.0 \|\| ^7.0.0 \|\| ^8.0.0` |
| `npm install --save-dev --save-exact vitest@5.0.3` | Instalada; 0 vulnerabilidades |
| `dotnet build Profesiograma.slnx -c Release` | 0 advertencias, 0 errores |
| `dotnet test --solution Profesiograma.slnx -c Release` | 232/232 (217 + 15 nuevas) |
| `dotnet ef migrations has-pending-model-changes … --configuration Release` | "No changes have been made to the model since the last migration." |
| `npm run build` | Sin errores ni advertencias |
| `npm run lint` / `npx oxlint` | Sin hallazgos (código de salida 0) |
| `npm test` | B1: 28/28 · B2: **44/44** |

## 7. Bundle (`npm run build`)
| Chunk | Tamaño | gzip |
|---|---|---|
| Inicial: `index` + chunk compartido (`formato`) + runtime | 277,2 + 302,4 + 0,6 = **580,2 kB** | 85,4 + 89,8 + 0,4 ≈ 175,6 kB |
| **Nuevo: `NuevoProyecto`** (carga diferida) | **99,2 kB** | 26,2 kB |
| Referencia TAREA-09: inicial | 578 kB | — |

La carga inicial prácticamente no cambia (+2 kB por los tipos de error y `apiPost`).

## 8. Diferencias con el plan
1. D3 se implementó en la B2 (no en la B1) porque debe desactivarse tras el 201.
2. Componentes adicionales: `TarjetaPersona` y `AyudaAgregarPersonal`.
3. Con una sola compañía en el ERP, queda elegida al abrir el formulario.
4. El buscador queda abierto tras agregar (botón "Listo").
5. Las pruebas de la B2 están en un archivo aparte (`formularioProyecto.envio.test.ts`).
6. La barra de botones ("Generar vista previa", "Registrar") es fija al pie (`position: sticky`) e indica por qué "Registrar" está deshabilitado.

## 9. Verificación visual (usuario `gestor`, 2026-10-01)
**Resultado: todas OK, sin fallas.** V2–V12, V6b, V13, V13b, V14+V16 (combinados), V15, V17, V18 y V19 los ejecutó el usuario. V20 (máximo de principales) está cubierta por la prueba del reducer "R9 / V20: máximos".

| # | Caso | Resultado |
|---|---|---|
| V2–V12, V19 | Navegación, cascada ERP, limpieza CAMPO→PLANTA→CAMPO, almuerzo inválido, departamento único, buscador, principales ↑↓, quitar principal con back relacionado, back DESCANSO, advertencias de fechas, ancho 375 px | OK |
| V6b | Variante del almuerzo inválido (definida por el usuario) | OK |
| V13 | C1/C2 en diciembre 2026 → cruces EXTERNOS con PRY-20261001-4ede0f; "Registrar" deshabilitado | OK |
| V13b | 409 por carrera con dos pestañas | OK: B registró **PRY-20261001-454325**; A recibió el 409 con la tabla de cruces |
| V14+V16 | Registro sin cruces con doble clic; "Atrás" no vuelve al formulario | OK: **PRY-20261001-669d73**, creado una sola vez |
| V15 | Vista previa invalidada al editar | OK |
| V17 | Error 400 por fila que sigue a la persona al reordenar | OK |
| V18 | Confirmación al salir | OK |
| V20 | Máximo de principales | Cubierta por prueba (Vitest) |

**Evidencia en la API** (consulta GET de solo lectura con `gestor`, después de las pruebas): **6 proyectos**, no existe ningún Id mayor que 8.

| Id | Código | Fechas | Personal | Registro (UTC) | Prueba |
|---|---|---|---|---|---|
| 7 | PRY-20261001-454325 | 01/03–30/04/2027 | P1 DEV004 TIPO_3 (01/03–30/04) | 17:13:50 | V13b (pestaña B) |
| 8 | PRY-20261001-669d73 | 01/04–30/04/2027 | P1 DEV005 TIPO_3 | 17:16:08 | V14+V16: **un solo** proyecto de abril con DEV005 → sin doble registro |

Diferencias menores con los datos propuestos, que no afectan el resultado:
- V13b se registró hasta el **30/04/2027**, no hasta el 31/03.
- V14 se registró con almuerzo **11:00–12:00**, no 13:00–14:00.
- DEV004 (marzo–abril) y DEV005 (abril) son personas distintas, así que no se cruzan.

**Otros proyectos creados en `PROFESIOGRAMA_DEV` durante las pruebas manuales** (se dejan como datos de prueba; borrarlos requiere SQL con aprobación):
- Id 5 PRY-20261001-4c34f3: propietario **admin**, 01/10–31/12/2026, DEV005 y DEV008.
- Id 6 PRY-20261001-294792: gestor, 01/01–28/02/2027, DEV003. El usuario indicó que fue un intento equivocado.

**No verificables en desarrollo** (pendientes 19–20): selector con varios departamentos y 503 con "Reintentar" (el ERP Simulado no falla).

## 10. Pendientes
- 18: parámetros `ALMUERZO_*_OPCIONES` sin uso (decidir si se eliminan o si `ReglasAlmuerzo` los lee).
- 19: caso de varios departamentos sin prueba visual.
- 20: 503 / "Reintentar" a probar en QA con el modo `Http`.
- 21: el 409 desde la pantalla solo ocurre por carrera (V13b).
- Proyectos de prueba Id 5–8 en `PROFESIOGRAMA_DEV` (sección 9).

**Cierre:** `npm test` se agregó a la sección "Frontend" de `CLAUDE.md` (aprobado por el usuario).
