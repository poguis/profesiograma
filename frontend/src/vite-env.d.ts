// Tipado de las variables VITE_* (se fusiona con ImportMetaEnv de vite/client).
export {}

declare global {
  interface ImportMetaEnv {
    /** "dev" = DevAuth (solo en `npm run dev`). En producción no se define. */
    readonly VITE_AUTH_MODE?: 'dev'
  }
}
