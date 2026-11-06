// Tipado de las variables VITE_* (se fusiona con ImportMetaEnv de vite/client).
export {}

declare global {
  interface ImportMetaEnv {
    /** "dev" = DevAuth (solo en `npm run dev`); "entra" (o sin valor, y siempre en el build) = MSAL (Fase 7b). */
    readonly VITE_AUTH_MODE?: 'dev' | 'entra'
    /** Id del tenant de SEDEMI (público). */
    readonly VITE_ENTRA_TENANT_ID?: string
    /** Client ID del registro SPA (público). */
    readonly VITE_ENTRA_CLIENT_ID?: string
    /** Scope de la API: api://<ClientId de la API>/access_as_user. */
    readonly VITE_ENTRA_API_SCOPE?: string
    /** TAREA-7d-3: URL pública canónica (https). Otro origen redirige a ella; DEV no la define. */
    readonly VITE_URL_PUBLICA?: string
  }
}
