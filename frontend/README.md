# PROFESIOGRAMA — Frontend

React 19 + TypeScript (strict) + Vite 8 · Fluent UI React v9 · TanStack Query v5 · React Router v8.

## Requisitos
- Node.js `^20.19.0 || >=22.12.0` (Vite 8) y `>=22.22.0` (React Router 8). Verificado con Node 24.16.0 y npm 11.13.0.
- La API corriendo en `https://localhost:7180` (ver `backend/README.md`).

## Uso (desde `frontend/`)
```bash
npm install        # primera vez o tras cambiar package.json
npm run dev        # servidor de desarrollo (http://localhost:5173)
npm run build      # tsc -b (verificación de tipos) + vite build → dist/
npm run lint       # oxlint
npm run preview    # sirve dist/ localmente
```

## Conexión con la API
El proxy de Vite (`vite.config.ts`) reenvía `/api/*` a `https://localhost:7180` (`secure: false` por el certificado
de desarrollo). El navegador ve un solo origen, así que la API no necesita CORS.

## Autenticación
- **Desarrollo (DevAuth):** con `VITE_AUTH_MODE=dev` (archivo `.env.development`) y `npm run dev`, la barra superior
  muestra un selector de usuario (`admin` | `gestor` | `anonimo`). La elección se guarda en `localStorage`
  (`profesiograma.devUsuario`) y el cliente HTTP envía el encabezado `X-Dev-User`. Al cambiar el usuario se vacía
  la caché de React Query y se vuelven a pedir los datos.
- **Producción:** `import.meta.env.DEV` es `false` en `vite build`, así que **`X-Dev-User` nunca se envía** y el
  código de DevAuth no llega al bundle. Los `import()` de DevAuth usan la condición literal
  `import.meta.env.DEV && import.meta.env.VITE_AUTH_MODE === 'dev'` para que Vite no genere sus chunks.
- **Fase 7 (Entra ID):** la identidad está aislada en `src/auth/`. `obtenerEncabezadosAutenticacion()`
  (`src/auth/index.ts`) pasará a devolver `Authorization: Bearer <token>` con MSAL (`@azure/msal-react`),
  sin cambiar pantallas ni `src/api/clienteHttp.ts`.

## Estructura
```
src/
  app/         App (rutas), Proveedores (Fluent + React Query), queryClient
  api/         clienteHttp (fetch "/api" + errores tipados), errores (ErrorApi), tipos (DTOs)
  auth/        modo (devAuthActivo), index (encabezados de identidad), devAuth y SelectorUsuarioDev (solo dev,
               import dinámico), useUsuarioActual
  components/  DisenoPrincipal (barra + menú + contenido), MenuLateral, EstadoError, Paginacion,
               SelectorFecha (DatePicker de Fluent en español, dd/MM/yyyy)
  features/
    proyectos/ Control de proyectos: listado con filtros en la URL y detalle de solo lectura (rutas con carga diferida)
  pages/       Inicio, NoAutorizado (403), NoEncontrado (404)
  utils/       formato (fechas dd/MM/yyyy, es-EC)
```

## Convenciones
- Textos de la interfaz en español (Ecuador). Fechas `dd/MM/yyyy` con `formatearFecha` (`src/utils/formato.ts`):
  las fechas `yyyy-MM-dd` de la API se reordenan sin conversión de zona horaria.
- Errores HTTP → `ErrorApi` con `tipo`: `validacion` (400), `noAutenticado` (401), `prohibido` (403),
  `noEncontrado` (404), `servidor` (5xx) y `red` (sin respuesta). React Query no reintenta 400/401/403/404.
