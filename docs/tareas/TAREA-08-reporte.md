# TAREA-08 — Frontend base (React + TypeScript + Vite)

**Fecha:** 2026-09-30
**Resultado:** completada. `npm run build` (tsc + vite) y `npm run lint` terminan **sin errores ni advertencias de código**. Solo queda el aviso informativo de tamaño de bundle (sección 4).
No se ejecutó `npm run dev` (lo hace el usuario). No se instaló Node ni ninguna herramienta global, no se modificó `backend/` y no se usaron `dotnet run`, SQL ni comandos git que modifiquen el repositorio.

## 1. Fase A — Verificación y decisiones

| Elemento | Versión | Requisito |
|---|---|---|
| Node | v24.16.0 | Vite 8: `^20.19.0 \|\| >=22.12.0` · React Router 8: `>=22.22.0` ✅ |
| npm | 11.13.0 | ✅ |

Versiones estables consultadas con `npm view` e instaladas: vite 8.3.1, @vitejs/plugin-react 6.1.1, react / react-dom 19.3.0, @fluentui/react-components 9.74.9, @fluentui/react-icons 2.0.343, @tanstack/react-query 5.104.0, react-router 8.4.0, typescript 6.0.3, oxlint 1.86.0, @types/react / @types/react-dom 19.3.0 y @types/node 24.19.0.
Peers verificados: Fluent 9 acepta `react <20`, React Query `^18 || ^19` y React Router 8 `react >=19.2.7`.

| Id | Decisión aprobada | Motivo |
|---|---|---|
| (a) | **TypeScript 6.0.3** (no 7.0.2, que es `latest`) | La plantilla oficial fija `~6.0.2` y typescript-eslint exige `<6.1.0`. La 7.x (nativa) todavía no la soporta el ecosistema |
| (b) | **oxlint**, con la configuración de la plantilla oficial | create-vite 9.2.1 ya no trae ESLint (`"lint": "oxlint"`, plugins `react`, `typescript`, `oxc`) |
| (c) | Incluir `@fluentui/react-icons` | Íconos del menú y de la barra |
| (d) | Pendiente 3 marcado como resuelto: "Frontend: React + Vite (decidido 2026-09-30)" | — |
| Creación | Copiar `template-react-ts` de **create-vite@9.2.1**, en lugar de `npm create` | `frontend/` no estaba vacía y `npm create` pedía confirmación interactiva |

## 2. Qué se hizo

### Creación del proyecto
1. Se descargó `create-vite@9.2.1` con `npm pack` al scratchpad de la sesión, fuera del repositorio, y se copió `template-react-ts`:
   - `_gitignore` → `.gitignore` y `_oxlintrc.json` → `.oxlintrc.json`;
   - se descartaron los archivos de demostración (`App.tsx`/`App.css` de ejemplo, `src/assets/`, `public/icons.svg`);
   - se conservó `public/favicon.svg`, el ícono de la plantilla, como marcador de posición.
2. Ajustes:
   - `package.json`: `name = profesiograma-frontend` y las versiones aprobadas;
   - `"strict": true` **explícito** en `tsconfig.app.json` y `tsconfig.node.json`, porque la plantilla no lo declara;
   - `index.html`: `lang="es-EC"` y título `PROFESIOGRAMA`.
3. `npm install`: 120 paquetes, **0 vulnerabilidades**, sin conflictos de peers.

### `.gitignore` de `frontend/` frente al de la raíz
No hay contradicciones. Ambos ignoran `node_modules` y `dist`. El de `frontend/` ignora además `*.local`, es decir `.env.*.local`, logs y carpetas de editor. `git check-ignore` confirma:
- `frontend/node_modules` y `frontend/dist` → ignorados;
- `frontend/.env.development.local` → ignorado (`*.local`);
- `frontend/.env.development` → **no** ignorado, así que es versionable, como se pidió.

### Funcionalidad
| Punto | Implementación |
|---|---|
| Proxy | `vite.config.ts`: `server.proxy['/api'] = { target: 'https://localhost:7180', secure: false }`. Sin CORS en la API |
| Variables | `.env.development`: `VITE_AUTH_MODE=dev`. Tipado en `src/vite-env.d.ts` con `declare global` (necesario con `moduleDetection: "force"`) |
| Cliente HTTP | `src/api/clienteHttp.ts`: `apiGet<T>(ruta)` con base `/api`, encabezados obtenidos **solo** de `src/auth`. Las respuestas no exitosas se convierten en `ErrorApi` (`src/api/errores.ts`), con `tipo` `validacion` (400), `noAutenticado` (401), `prohibido` (403), `noEncontrado` (404), `servidor` (otros o 5xx) y `red` (fetch sin respuesta). Lee ProblemDetails/ValidationProblem (`title`, `detail`, `errors`) si el cuerpo es JSON y soporta cuerpo vacío (401/403 actuales). La cancelación (`AbortError`) no se convierte en error |
| DevAuth | `src/auth/devAuth.ts`: `devAuthActivo = import.meta.env.DEV && VITE_AUTH_MODE === 'dev'`; usuario en `localStorage` (`profesiograma.devUsuario`, por defecto `admin`). `src/auth/index.ts`: `obtenerEncabezadosAutenticacion()`, **único punto** que agrega `X-Dev-User`; en la Fase 7 devolverá `Authorization: Bearer` con MSAL |
| Selector | `SelectorUsuarioDev.tsx` (Fluent `Dropdown`: admin, gestor, anonimo). Solo se muestra si `devAuthActivo`. Al cambiar: guarda, actualiza el estado y ejecuta `queryClient.resetQueries()`, que descarta la caché y vuelve a pedir `usuarios/me` |
| React Query | `src/app/queryClient.ts`: no reintenta 400/401/403/404; red o 5xx, hasta 2 veces; `refetchOnWindowFocus: false`; `staleTime` 30 s |
| Layout | `DisenoPrincipal.tsx`: barra superior (PROFESIOGRAMA, usuario actual —"No autenticado" si 401— y selector dev), menú lateral y `<Outlet/>` |
| Menú | `MenuLateral.tsx`: "Inicio" (`NavLink`, activo) y "Proyectos" deshabilitado, con la insignia "Próximamente" |
| Rutas | `App.tsx` (`createBrowserRouter` + `RouterProvider` de `react-router/dom`): `/` Inicio, `/no-autorizado` (403), `*` NoEncontrado (404) |
| Inicio | `GET /api/usuarios/me`. Muestra nombre, correo y roles (insignias). Con 401 muestra un `MessageBar` de advertencia "No autenticado" y, en dev, la indicación de elegir admin o gestor, sin romper la app. Otros errores se muestran con `EstadoError`. Muestra "Hoy: dd/MM/yyyy" |
| Fechas | `src/utils/formato.ts`: `formatearFecha()`. Las cadenas `yyyy-MM-dd` de la API se reordenan como texto, sin conversión de zona (evita el desfase de un día). Los `Date` se formatean con `Intl.DateTimeFormat('es-EC', { timeZone: 'America/Guayaquil' })`. Verificado: 2026-09-30T03:00Z → `29/09/2026` y 2026-01-05T15:00Z → `05/01/2026` |
| Idioma | Todos los textos de la interfaz en español |

## 3. Archivos tocados

| Acción | Archivo |
|---|---|
| Nuevo (plantilla ajustada) | `frontend/.gitignore`, `.oxlintrc.json`, `index.html`, `package.json`, `package-lock.json`, `tsconfig.json`, `tsconfig.app.json`, `tsconfig.node.json`, `public/favicon.svg` |
| Nuevo | `frontend/vite.config.ts` (plantilla + proxy), `frontend/.env.development` |
| Nuevo | `frontend/src/main.tsx`, `index.css`, `vite-env.d.ts` |
| Nuevo | `frontend/src/app/App.tsx`, `Proveedores.tsx`, `queryClient.ts` |
| Nuevo | `frontend/src/api/clienteHttp.ts`, `errores.ts`, `tipos.ts` |
| Nuevo | `frontend/src/auth/devAuth.ts`, `index.ts`, `useUsuarioActual.ts`, `SelectorUsuarioDev.tsx` |
| Nuevo | `frontend/src/components/DisenoPrincipal.tsx`, `MenuLateral.tsx`, `EstadoError.tsx` |
| Nuevo | `frontend/src/pages/Inicio.tsx`, `NoAutorizado.tsx`, `NoEncontrado.tsx` |
| Nuevo | `frontend/src/utils/formato.ts`, `frontend/src/features/.gitkeep` |
| Reemplazado | `frontend/README.md` (requisitos, scripts, proxy, autenticación, estructura, convenciones) |
| Modificado | `CLAUDE.md` (sección "Frontend (desde frontend/)") |
| Modificado | `docs/00_ESTADO_ACTUAL.md` (decisión de frontend en la sección 3, fila Frontend en la sección 4, pendiente 3 resuelto y sección 9 nueva) |
| Creado | `docs/tareas/TAREA-08-reporte.md` |

`git status` (solo lectura): 33 archivos de `frontend/` versionables. `node_modules/` y `dist/` no aparecen.

## 4. Comandos ejecutados y resultado

| Comando | Resultado |
|---|---|
| `node -v` / `npm -v` | v24.16.0 / 11.13.0 |
| `npm view <paquete> version / engines / peerDependencies` | Ver sección 1 |
| `npm pack create-vite@9.2.1` (en el scratchpad) | Plantilla `template-react-ts` obtenida |
| `npm install` (en `frontend/`) | 120 paquetes, **0 vulnerabilidades** |
| `npm run build` (1.ª vez) | `tsc -b` sin errores y `vite build` correcto. Aviso informativo: chunk de 646,58 kB (194,08 kB gzip) mayor a 500 kB |
| `npm run lint` (1.ª vez) | 0 errores, **1 advertencia**: `react(purity)` en `Inicio.tsx`, porque `new Date()` se llamaba durante el render |
| Corrección | `const [hoy] = useState(() => formatearFecha(new Date()))`: la fecha se calcula una sola vez |
| `npm run lint` (2.ª vez) | **0 errores, 0 advertencias** |
| `npm run build` (2.ª vez) | **Correcto** |
| Búsqueda de `X-Dev-User`, `devUsuario` y `Usuario (dev)` en `dist/` | **Sin coincidencias**: el código de DevAuth no llega al bundle de producción |
| `git status --short -uall frontend`, `git check-ignore` | Solo lectura. Ver sección 2 |

### Aviso de tamaño del bundle
`vite build` avisa que el único chunk pesa 646,58 kB (194,08 kB gzip), por encima del umbral de 500 kB. El tamaño se debe principalmente a Fluent UI. No es un error. Se resolverá con carga diferida por ruta (`lazy`) cuando existan las pantallas de proyectos. No se cambió `chunkSizeWarningLimit` para no ocultar el aviso.

## 5. Errores y correcciones
- Advertencia `react(purity)` (`Inicio.tsx`): corregida como se describe arriba.
- `vite-env.d.ts`: se escribió desde el inicio con `export {}` + `declare global`, porque la plantilla usa `moduleDetection: "force"` (cada archivo es un módulo) y una interfaz sin `declare global` no se fusionaría con `ImportMetaEnv`.

## 6. Pendientes
- **Prueba visual del usuario** con `npm run dev` y la API corriendo:
  1. Inicio muestra ADMIN DESARROLLO, `admin.dev@profesiograma.local` y el rol Admin;
  2. al cambiar a `gestor` se recargan los datos (rol Gestor);
  3. con `anonimo` se muestra "No autenticado" en la barra y en Inicio, sin errores en la consola;
  4. "Proyectos" aparece deshabilitado con "Próximamente";
  5. una ruta inexistente (p. ej. `/xyz`) muestra "Página no encontrada".
- **Error de red a través del proxy:** si la API está apagada, el proxy de Vite responde con un error HTTP (normalmente 500 sin cuerpo JSON), no con un fallo de `fetch`. En ese caso la app muestra "Error del servidor o la API no está disponible." (tipo `servidor`). El tipo `red` solo aparece si falla el propio servidor de Vite o la red del navegador.
- Carga diferida por ruta cuando se agreguen las pantallas de proyectos (tamaño del bundle).
- Fase 7: implementar MSAL en `src/auth/` (`obtenerEncabezadosAutenticacion` y el estado del usuario).
- `CLAUDE.md` sigue describiendo el frontend como "Frontend (futuro)" en la sección "Stack". No se cambió porque no estaba pedido.
