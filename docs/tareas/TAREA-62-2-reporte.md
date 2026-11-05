# TAREA-62-2 — Reporte: criterio de personal elegible, frontend

**Fecha:** 28/10/2026 · **Fase:** B (implementación del plan de la TAREA-62) · **Estado:** ✅ Verificada por el usuario el 28/10/2026 (A1–A11 correctos; 362/362; la limpieza dejó el criterio en ADMINISTRATIVO y cero novedades activas de 2070)

## 1. Qué se hizo
- **Menú «Administración»** (icono de configuración) visible solo si `esAdmin` (de `novedades/sincronizacion.ts`, sin cambios), con la misma consulta `useUsuarioActual` de la barra superior. Respeta la fila del menú por debajo de 768 px (U4).
- **Ruta `/admin/personal-elegible`** (carga diferida). Un gestor que entra por URL ve «No tienes permiso para ver esta página.»: el GET solo se habilita con `esAdmin`, así que no llama al backend.
- **Pantalla «Personal elegible»** (`features/admin/personal-elegible/`):
  - una casilla por familia con sus activos (orden del backend);
  - los dos textos fijos y el aviso «Sin criterio…» cuando corresponde;
  - «Guardar» deshabilitado con «No hay cambios para registrar.» o «Seleccione al menos una familia.»; con cambios, «Se guardará: A → B».
  - El PUT lleva el `valorActual` del GET que abrió la pantalla (consulta con `staleTime: Infinity` y sin reintentos). Mutación sin reintentos, con `isPending` y un ref contra el doble clic.
  - 200 → «Criterio actualizado.», se invalidan el criterio y `['empleados']` (todas las búsquedas) y el formulario se vuelve a montar con el valor nuevo.
  - Errores: 400 bajo la lista (varios mensajes), 409 con «Recargar» (con confirmación si hay cambios), 403 sin «Reintentar», 503 y red con «Reintentar» (también en el GET).
  - `useBlocker` + `beforeunload` mientras haya cambios. Una columna, con el botón a todo el ancho por debajo de 600 px.
- **Buscador:** `textoGruposAsignables` (función pura en `buscadorEmpleados.ts`) y una línea «Solo grupos asignables: A, B.» en `BuscadorEmpleados` cuando `familiasAsignables` no está vacío. Tipo `familiasAsignables?` en `ResultadoBusquedaEmpleados`.
- **400 «no pertenece a un grupo asignable»:** confirmado que ya se muestra junto a la persona en Nuevo proyecto (`distribuirErrores` → tarjeta con `mensajesSinCampo`), Actualizar personal y Reactivar (`interpretarErrorPersonal` / `interpretarErrorReactivacion`), y bajo «Persona» en Nueva novedad (`CAMPOS_REGISTRO`). **No hizo falta corregir ninguna pantalla**; se agregaron pruebas que lo fijan.
- **Documentación:**
  - `FASE_4_Empleados_API.md` §10: fila «Frontend (TAREA-62-2)».
  - `00_ESTADO_ACTUAL.md`: 62-1 ✅ y 62-2 implementada, en **24 903 B**. Lo reemplazado va textual a `HISTORIAL.md` H12, con 0 líneas perdidas.
  - `TAREA-62-1-reporte.md` marcada verificada, con la nota de las corridas 100–101.

## 2. Archivos tocados
| Archivo | Cambio |
|---|---|
| `frontend/src/features/admin/personal-elegible/criterioPersonal.ts` (+test) | Nuevo: estado, cambios, resumen, cuerpo y errores |
| `…/personal-elegible/api.ts` | Nuevo: GET/PUT, `useCriterioPersonal`, `useGuardarCriterioPersonal` (invalida `['admin','criterio-personal']` y `['empleados']`) |
| `…/personal-elegible/PaginaPersonalElegible.tsx` | Nuevo |
| `frontend/src/components/MenuLateral.tsx`, `frontend/src/app/App.tsx` | Entrada «Administración» y ruta |
| `frontend/src/features/proyectos/buscadorEmpleados.ts`, `components/BuscadorEmpleados.tsx`, `tipos.ts` | Texto y línea de grupos; tipo |
| `frontend/src/features/proyectos/personalElegible.test.ts` | Nuevo: texto de grupos y 400 por persona en las 4 pantallas |
| `docs/fases/FASE_4_Empleados_API.md`, `docs/00_ESTADO_ACTUAL.md`, `docs/HISTORIAL.md`, `docs/tareas/TAREA-62-1-reporte.md` | Documentación |

## 3. Comandos y resultado (desde `frontend/`)
| Comando | Resultado |
|---|---|
| `npm run build` | OK, sin errores ni advertencias |
| `npm run lint` (oxlint) | Sin hallazgos (exit 0) |
| `npm test` | **362/362** (349 + 13: `criterioPersonal` 8, `personalElegible` 5). `buscadorEmpleados.test.ts` y las demás, sin cambios |

## 4. Desviaciones y decisiones
- La estructura sugerida tenía «hooks» y «components» aparte; los hooks quedaron en `api.ts` y la pantalla en un solo archivo (es una sola página pequeña).
- `MenuLateral` importa `esAdmin` desde `features/novedades/sincronizacion.ts`, para no duplicarlo. Arrastra al paquete inicial módulos pequeños de errores de novedades.
- Las familias se comparan sin mayúsculas ni espacios extremos para «sin cambios», como el backend agrupa.
- La línea del buscador también aparece en «Nuevo proyecto», «Actualizar personal» y «Reactivar» (es el mismo componente).
- Sin cambios de backend (sin B0).

## 5. Verificación para el usuario (`npm run dev`; empleados en Simulado; DevAuth admin y gestor; novedades con fechas de 2070)
| # | Caso | Esperado |
|---|---|---|
| A1 | gestor: menú y `/admin/personal-elegible` por URL | Sin «Administración»; «No tienes permiso para ver esta página.» (en Red, sin llamada a `/api/admin/…`) |
| A2 | admin: «Administración» | ADMINISTRATIVO marcado (188 activos) y OPERATIVO sin marcar (20); los dos textos fijos |
| A3 | Buscador en Nueva novedad (PERSONA) y en Nuevo proyecto | «Solo grupos asignables: ADMINISTRATIVO.»; buscar SIM010 no lo encuentra |
| A4 | Marcar OPERATIVO | «Se guardará: ADMINISTRATIVO → ADMINISTRATIVO, OPERATIVO»; «Guardar» → «Criterio actualizado.»; el buscador ya encuentra SIM010 sin recargar el navegador |
| A5 | Nueva novedad PERSONA de SIM010 (2070) | Se registra; anúlela después con `tarea23-2\limpiar.cmd` (`IDS` = su Id, `CONFIRMAR=1`) |
| A6 | Desmarcar todo | «Guardar» deshabilitado con «Seleccione al menos una familia.» |
| A7 | Dos pestañas en la pantalla: guardar en A y luego en B | B: «Otro usuario cambió el criterio; recargue.» con «Recargar» (pide confirmación si B tiene cambios) |
| A8 | Cambiar una casilla y salir por el menú o con F5 | «¿Salir sin guardar?» / aviso del navegador |
| A9 | Al terminar, dejar solo ADMINISTRATIVO desde la pantalla | «Criterio actualizado.». Comprobación de solo lectura en SSMS: `SELECT Clave, Valor, ModificadoPorId, FechaModificacion FROM dbo.Parametro WHERE Clave = 'EMPLEADO_FAMILIAS_ASIGNABLES';` → `Valor` = `ADMINISTRATIVO` |
| A10 | Regresión: Nueva novedad con DEV001; Nuevo proyecto con SIM011 hasta la vista previa (sin registrar) | Funcionan igual |
| A11 | 375 px | Menú en fila con «Administración» (admin); pantalla en una columna, «Guardar» a todo el ancho |

## 6. Pendientes
- Verificación visual (§5) y limpieza de la novedad de A5.
- Pendientes 68 (Fase 3) y 69 (comparación con el ERP).
