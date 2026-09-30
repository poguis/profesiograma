# TAREA-09 — Pantalla "Control de proyectos": listado con filtros + detalle de solo lectura (frontend)

**Fecha:** 2026-09-30
**Resultado:** completada.
- `npm run build` (`tsc -b` + `vite build`) y `npm run lint` (oxlint) terminan **sin errores ni advertencias**.
- El bundle inicial bajó de **936,21 kB** sin lazy a **578,33 kB** con lazy.
- Falta la prueba visual del usuario (sección 8).

No se modificó `backend/` ni se ejecutaron `npm run dev`, `dotnet run`, SQL o comandos git que modifiquen el repositorio. A la API solo se le hicieron GET de lectura con `X-Dev-User` para revisar el contrato.

## 1. Fase A — Contrato y decisiones

Se revisó el JSON real de `GET /api/proyectos`, `GET /api/proyectos/1`, `GET /api/catalogos` y de un 400 (`?desde=x&pagina=0`). Coincide con el reporte de la TAREA-07. **No falta nada en la API.**

| Id | Decisión |
|---|---|
| O1 | El listado trae `estado`/`grupo` como códigos. El nombre se toma de `/api/catalogos`; si no está, se muestra el código |
| O2 | `etapas[].tipoMovimiento` es un código. El nombre se toma de `catalogos.tiposMovimiento` |
| O3 | `/api/catalogos` exige la política Gestor, que admin y gestor cumplen |
| D1 = A | Se agregó **`@fluentui/react-datepicker-compat` 0.6.39**, porque `@fluentui/react-components` no incluye DatePicker. Está encapsulado en `src/components/SelectorFecha.tsx`: textos en español, formato y parseo dd/MM/yyyy, semana desde el lunes, "Ir a hoy" |
| D2 | Colores con tokens de Fluent: ACTIVO Green, SUSPENDIDO Gold, INACTIVO Red y TERMINADO **Peach, que reemplaza a Coral** porque Fluent no tiene "Coral". Referencia verificada: el encabezado de `SIG PROYECTOS (9).csv` en `docs/origen/sharepoint/muestras/` usa `sp-css-backgroundColor-BgGreen`, `-BgGold`, `-BgRed` y `-BgCoral`. **Solo se leyó la primera línea y solo se extrajeron nombres de clase**; no se copiaron datos |

## 2. Qué se hizo

### Rutas y menú
- `src/app/App.tsx`: `proyectos` y `proyectos/:id` con **`lazy`** de React Router (import dinámico del módulo de página).
- `src/components/MenuLateral.tsx`: "Proyectos" es un `NavLink` activo y se quitó "Próximamente". Sin `end`, sigue resaltado también en el detalle.

### Listado (`/proyectos`)
| Requisito | Implementación |
|---|---|
| Filtros | `FiltrosProyectos.tsx`: Estado y Grupo (Dropdown desde `/api/catalogos`, con "Todos"), Buscar (SearchBox con **espera de 400 ms**), Desde y Hasta (`SelectorFecha`, dd/MM/yyyy → `yyyy-MM-dd`), "Limpiar filtros" (deshabilitado si no hay filtros; conserva el tamaño de página) |
| Filtros en la URL | `filtrosUrl.ts` (`useFiltrosUrl`). La URL es la fuente de verdad: `?estado=&grupo=&texto=&desde=&hasta=&pagina=&tamano=`, omitiendo vacíos y valores por defecto (pagina 1, tamano 20). Cada cambio que no sea de página vuelve a la página 1. Los cambios de texto reemplazan la entrada del historial para no llenarlo; el resto la agrega. Recargar o usar atrás/adelante conserva la búsqueda |
| Tabla | `TablaProyectos.tsx` (DataGrid): Código, Nombre, Grupo, Estado (etiqueta de color), Inicio, Fin, Responsable, Backs y Horario (`HH:mm – HH:mm`) |
| Backs | `CeldaBacks.tsx`: 2 nombres y, si hay más, una insignia "+N" enfocable con Tooltip que muestra el resto |
| Paginación | `src/components/Paginacion.tsx`: Anterior/Siguiente, "Página X de Y", "N proyectos" y tamaño 10/20/50. Si la URL trae otro tamaño, se agrega a la lista |
| Abrir | Clic en la fila o **Enter** → `/proyectos/:id`, guardando `location.search` en el estado de navegación |
| Estados | Cargando: Skeleton de 5 filas (primera carga) y Spinner "Actualizando…" al cambiar filtros, conservando la página anterior (`keepPreviousData`). Vacío: "No hay proyectos con estos filtros." **400**: los mensajes de la API salen **junto al filtro** (`desde`/`hasta` en su `Field`); `pagina`/`tamano` u otros campos, en un MessageBar con "Restablecer paginación". **401/403/500/red**: `EstadoError`. Página sin resultados pero con `total > 0`: aviso con "Ir a la página 1" |
| Formato local | `SelectorFecha` detecta si el texto escrito no es una fecha dd/MM/yyyy válida y muestra "Ingrese una fecha válida con el formato dd/MM/yyyy." en el campo, sin llamar a la API |

### Detalle (`/proyectos/:id`)
| Requisito | Implementación |
|---|---|
| Volver | "Volver al listado" → `/proyectos` + la búsqueda guardada, que conserva los filtros. Si se entró directo por URL, va a `/proyectos` |
| Cabecera | `CabeceraProyecto.tsx`: código · nombre + etiqueta de estado. Tarjetas "General" (grupo, compañía con RUC, inicio, fin, departamento, propietario con correo), "Horario" (descripción, entrada–salida, almuerzo, tipo si existe), "Actividad vigente" (o "Sin actividad registrada.") y **"ERP" solo si algún campo tiene datos** |
| Pestañas | TabList "Personal (N)" / "Historial (N)" |
| Personal | `TablaPersonal.tsx`: Rol (insignia), N°, EKON, Nombre, Cargo, Jornada, Días trabajo / descanso, Inicio, Fin y Principal inicial (✓) |
| Historial | `TablaHistorial.tsx`: Versión, Movimiento (nombre de catálogo), Estado (etiqueta), Inicio, Fin, Corte, Actividad y Registro (`fechaRegistroUtc` → **dd/MM/yyyy HH:mm en hora de Ecuador**) |
| 404 | "Proyecto no encontrado o sin acceso" con enlace al listado. No distingue entre "no existe" y "sin permiso". Un `:id` no numérico se trata igual, sin llamar a la API |
| Otros errores | `EstadoError` |

### Arquitectura
```
src/features/proyectos/
  api.ts            listarProyectos, obtenerProyecto, obtenerCatalogos + clavesProyectos (React Query)
  tipos.ts          DTOs según el JSON real (TAREA-07)
  hooks.ts          useProyectos (keepPreviousData), useProyecto (solo con id válido), useCatalogos (10 min),
                    useNombresCatalogo (código → nombre)
  filtrosUrl.ts     leerFiltros / aParametrosUrl / aConsultaApi + useFiltrosUrl
  estadoColores.ts  código → tono (con la referencia BgGreen/BgGold/BgRed/BgCoral y la nota Peach = Coral)
  components/       FiltrosProyectos, TablaProyectos, CeldaBacks, EtiquetaEstado, CabeceraProyecto,
                    TablaPersonal, TablaHistorial
  pages/            ListadoProyectos, DetalleProyecto
src/components/     Paginacion, SelectorFecha (reutilizables)
src/utils/formato.ts + formatearFechaHora, formatearHora, aFechaIso, desdeFechaIso, desdeFechaEc
src/api/tipos.ts    + PaginaResultado<T>
```
- **Diferencia con el plan:** `EtiquetaEstado` quedó en `features/proyectos/components/` y no en `src/components/`. Depende del mapa de colores de estado del proyecto, y ponerlo en `src/components` habría creado una dependencia de `components` hacia `features`.
- `CeldaBacks` es un componente adicional.
- **Solo presentación:** el frontend no filtra ni calcula reglas. Envía los filtros a la API (incluso valores fuera de rango, para que responda el 400) y la visibilidad la decide la API. Solo traduce códigos a nombres con los catálogos y da formato a fechas y horas.

## 3. Corrección adicional: DevAuth fuera del bundle de producción

Al revisar `dist/` después de agregar las rutas diferidas se encontró lo siguiente:
- El encabezado `X-Dev-User` **no** estaba en el bundle (0 coincidencias).
- Pero las funciones auxiliares de `devAuth.ts` (lista de usuarios, clave `profesiograma.devUsuario`) quedaban como **código muerto** en el chunk compartido: exportadas, pero sin que ningún chunk las importara.

La causa es que, al dividir el código en chunks, el empaquetador (Rolldown) conserva exportaciones de módulos compartidos antes de descartar las ramas muertas. Esto contradecía lo afirmado en la TAREA-08 ("el código de DevAuth no llega al bundle").

**Corrección**, verificada con `dist/` sin ninguna coincidencia:
1. `src/auth/modo.ts` (nuevo): solo `devAuthActivo`. No importa `devAuth.ts`.
2. `src/auth/index.ts`: `devAuth.ts` se carga con `await import('./devAuth')` **solo** dentro de la rama de desarrollo.
3. `src/components/DisenoPrincipal.tsx`: `SelectorUsuarioDev` se carga con `lazy(() => import(...))` **solo** en desarrollo, con `<Suspense fallback={null}>`.
4. En los dos `import()` se usa la **condición literal** `import.meta.env.DEV && import.meta.env.VITE_AUTH_MODE === 'dev'`, no la constante importada. Con la constante, el empaquetador seguía generando `devAuth-*.js` y `SelectorUsuarioDev-*.js` como archivos huérfanos (nunca cargados, pero publicados). Con la condición literal, Vite la resuelve a `false` en el mismo archivo y esos chunks no se generan. Está documentado en `modo.ts`.

| Búsqueda en `dist/` (build final) | Coincidencias |
|---|---|
| `X-Dev-User` | 0 |
| `devUsuario` | 0 |
| `Usuario (dev)` | 0 |
| `anonimo (sin identidad)` | 0 |
| `SelectorUsuarioDev` | 0 |
| `devAuth` | 0 |

Comportamiento en desarrollo: sin cambios funcionales. El selector aparece tras una carga diferida mínima y la primera petición espera la importación de `devAuth.ts`, que queda en caché.

## 4. Tamaño del bundle

| Escenario | JS que se carga al abrir la app | gzip |
|---|---|---|
| Antes de esta tarea (TAREA-08, sin proyectos) | 646,60 kB (1 chunk) | 194,10 kB |
| **Con proyectos, SIN lazy** (build temporal en el scratchpad con importaciones estáticas; `App.tsx` restaurado después) | **936,21 kB** (1 chunk) | **268,00 kB** |
| **Con proyectos, CON lazy (final)** | **578,33 kB** = `index` 280,21 + chunk compartido 297,54 + runtime 0,58 | **175,14 kB** |

Chunks diferidos (se cargan al entrar a proyectos): `ListadoProyectos` 265,42 kB (75,50 gzip), `DetalleProyecto` 38,20 kB (10,64 gzip) y `hooks` 58,34 kB (15,90 gzip, compartido entre ambas páginas).
- **Reducción del bundle inicial:** −357,88 kB (−38 %) frente a la versión sin lazy, y −68,27 kB frente a la TAREA-08. Esto último se debe a que el selector DevAuth y su `Dropdown` ya no forman parte del bundle de producción.
- El aviso de Vite "chunk > 500 kB" **ya no aparece**.
- El chunk compartido se llama `formato-*.js` porque Rolldown lo nombra según uno de sus módulos. Contiene sobre todo el núcleo de Fluent UI.

## 5. Archivos tocados

| Acción | Archivo |
|---|---|
| Modificado | `frontend/package.json`, `package-lock.json` (+ `@fluentui/react-datepicker-compat` ^0.6.39, que trae `@fluentui/react-calendar-compat` 0.4.7) |
| Nuevo | `frontend/src/features/proyectos/api.ts`, `tipos.ts`, `hooks.ts`, `filtrosUrl.ts`, `estadoColores.ts` |
| Nuevo | `frontend/src/features/proyectos/components/FiltrosProyectos.tsx`, `TablaProyectos.tsx`, `CeldaBacks.tsx`, `EtiquetaEstado.tsx`, `CabeceraProyecto.tsx`, `TablaPersonal.tsx`, `TablaHistorial.tsx` |
| Nuevo | `frontend/src/features/proyectos/pages/ListadoProyectos.tsx`, `DetalleProyecto.tsx` |
| Nuevo | `frontend/src/components/Paginacion.tsx`, `SelectorFecha.tsx` |
| Nuevo | `frontend/src/auth/modo.ts` |
| Modificado | `frontend/src/app/App.tsx` (rutas lazy), `src/components/MenuLateral.tsx` (Proyectos habilitado), `src/components/DisenoPrincipal.tsx` (selector lazy solo en dev), `src/auth/index.ts` y `src/auth/devAuth.ts` (import dinámico), `src/api/tipos.ts` (`PaginaResultado<T>`), `src/utils/formato.ts` (funciones nuevas) |
| Eliminado | `frontend/src/features/.gitkeep` (la carpeta ya tiene contenido) |
| Modificado | `frontend/README.md` (estructura y nota de DevAuth) |
| Documentación | `docs/tareas/TAREA-09-reporte.md`, `docs/00_ESTADO_ACTUAL.md` (sección 9) |

## 6. Comandos ejecutados y resultado

| Comando | Resultado |
|---|---|
| `curl -k -sS -H "X-Dev-User: admin" https://localhost:7180/api/...` (solo GET) | 200 listado / detalle / catálogos; 400 con `errors.pagina` y `errors.desde` |
| `head -1` de los CSV de `muestras/`, extrayendo solo `sp-css-backgroundColor-Bg*` | `SIG PROYECTOS (9).csv`: BgCoral, BgGold, BgGreen, BgRed |
| `npm install @fluentui/react-datepicker-compat@^0.6.39` | Instalado; **0 vulnerabilidades** |
| `npm run build` (1.ª) | **2 errores de tipos** (ver sección 7) |
| `npm run build` / `npm run lint` (tras las correcciones) | Correcto / 0 errores y 0 advertencias |
| `npx vite build --outDir <scratchpad>/dist-sin-lazy` (con importaciones estáticas temporales) | 936,21 kB (268,00 gzip). `App.tsx` restaurado y verificado con `cmp` |
| `npm run build`, `npm run lint`, `npx tsc -b` (finales) | **Correcto, 0 errores y 0 advertencias** |
| Búsqueda de DevAuth en `dist/` | 0 coincidencias (sección 3) |

## 7. Errores y correcciones
| Archivo | Error | Causa | Corrección |
|---|---|---|---|
| `EtiquetaEstado.tsx` | TS2322 `Type 'string' is not assignable to type 'undefined'` (×5) | Griffel (`makeStyles`) no admite la propiedad abreviada `borderColor` | `...shorthands.borderColor(token)` |
| `TablaProyectos.tsx` | TS7006 `Parameter 'evento' implicitly has an 'any' type` | El `onKeyDown` de `DataGridRow<T>` genérico no infiere el tipo del evento | `(evento: KeyboardEvent<HTMLDivElement>)` |
| `TablaProyectos.tsx` | (accesibilidad; no es un error de build) | `aria-label="Abrir …"` en la fila ocultaba el contenido de las celdas al lector de pantalla | Se quitó el `aria-label` |
| `dist/` | Código muerto de DevAuth y chunks huérfanos | Ver sección 3 | Ver sección 3 |

## 8. Lista de verificación para el navegador (usuario: API corriendo + `npm run dev`)

**Menú y navegación**
- [ ] 1. En el menú lateral, "Proyectos" está habilitado (sin "Próximamente") y abre `/proyectos` con el título "Control de proyectos".
- [ ] 2. En DevTools › Red, el JS de proyectos (`ListadoProyectos…`) se descarga al entrar a Proyectos, no al abrir Inicio.

**Listado como admin**
- [ ] 3. Aparecen 3 proyectos, en orden PRY-DEV-0001, 0002, 0003, con las columnas Código, Nombre, Grupo, Estado, Inicio, Fin, Responsable, Backs y Horario.
- [ ] 4. Etiquetas de estado: ACTIVO verde, SUSPENDIDO dorado y TERMINADO coral/durazno.
- [ ] 5. Las fechas se ven en formato dd/MM/yyyy; por ejemplo, el inicio de PRY-DEV-0001 es 20/09/2026. El horario se ve como 07:00 – 18:00. PRY-DEV-0001 tiene 1 back ("EMPLEADO PRUEBA 03"). El "+N" no se puede probar con los datos actuales (máximo 1 back).
- [ ] 6. Pie de tabla: "3 proyectos", "Página 1 de 1" y "Por página: 20".

**Filtros**
- [ ] 7. Estado = Activo → solo PRY-DEV-0001; la URL muestra `?estado=ACTIVO`.
- [ ] 8. Grupo = OFICINAS ADMINISTRATIVAS → solo PRY-DEV-0003.
- [ ] 9. Escribir `0002` en Buscar → tras unos 400 ms queda solo PRY-DEV-0002. Mientras se escribe no hay una petición por cada tecla.
- [ ] 10. Desde 21/08/2026 y Hasta 26/08/2026 (con el calendario o escribiendo) → solo PRY-DEV-0003. El calendario está en español y empieza en lunes.
- [ ] 11. Desde 30/09/2026 y Hasta 20/09/2026 → el mensaje de la API ("La fecha 'desde' no puede ser posterior…") aparece debajo del campo Desde.
- [ ] 12. Escribir `31/02/2026` en Desde → "Ingrese una fecha válida con el formato dd/MM/yyyy." en el campo, sin error de la API.
- [ ] 13. "Limpiar filtros" → vuelven los 3 proyectos y la URL queda sin filtros.

**URL y paginación**
- [ ] 14. Con un filtro aplicado, recargar (F5) conserva los filtros y los resultados.
- [ ] 15. Por página = 10 y luego pegar en la URL `?tamano=1&pagina=2` → 1 proyecto, "Página 2 de 3"; Anterior y Siguiente funcionan.
- [ ] 16. `?tamano=500` en la URL → MessageBar con "El tamaño de página debe ser…" y el botón "Restablecer paginación".
- [ ] 17. Atrás y Adelante del navegador recorren los cambios de filtro.

**Detalle**
- [ ] 18. Clic en PRY-DEV-0001, o foco en la fila + Enter → `/proyectos/1`. Tarjetas General, Horario, Actividad vigente (DEV.01 – ACTIVIDAD DE PRUEBA) y ERP (DEV-ERP-001).
- [ ] 19. PRY-DEV-0002 muestra la tarjeta ERP **solo** con Dimensión DEV-DIM-01 y "PLANTA DE PRUEBA" (sin Proyecto ERP, que está vacío).
- [ ] 20. Pestaña "Personal (3)": 2 Principal + 1 Back, EKON DEV001–DEV003; ✓ de "Principal inicial" solo en DEV001; días 11 / 4, 5 / 2 y — / 0.
- [ ] 21. Pestaña "Historial (1)": v1 · Creación · Activo · registro en hora de Ecuador (p. ej. 30/09/2026 11:44).
- [ ] 22. "Volver al listado" regresa con los filtros que había.
- [ ] 23. `/proyectos/999999` y `/proyectos/abc` → "Proyecto no encontrado o sin acceso" con enlace al listado.

**Usuarios (selector dev)**
- [ ] 24. gestor → el listado muestra solo PRY-DEV-0001 y 0002; `/proyectos/3` → "Proyecto no encontrado o sin acceso".
- [ ] 25. anonimo → en Proyectos aparece el aviso de EstadoError "No autenticado. (HTTP 401)" y la app no se rompe.
- [ ] 26. La consola del navegador no muestra errores ni advertencias de React.

## 9. Pendientes
- Prueba visual del usuario (sección 8).
- Los datos de prueba no tienen proyectos con más de 2 backs, así que "+N" solo se verificó por código.
- Si el catálogo de estados se amplía, los estados nuevos se muestran con el tono neutro hasta agregarlos en `estadoColores.ts`.
