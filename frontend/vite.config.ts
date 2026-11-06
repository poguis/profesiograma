import { fileURLToPath } from 'node:url'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  build: {
    rolldownOptions: {
      // TAREA-7d-4: segunda entrada, la página del puente de MSAL para las llamadas silenciosas (redirect.html).
      input: {
        main: fileURLToPath(new URL('./index.html', import.meta.url)),
        redirect: fileURLToPath(new URL('./redirect.html', import.meta.url)),
      },
    },
  },
  server: {
    // Fase 7b: la URI de redirección de Entra ID es exacta (http://localhost:5173). Si el puerto está ocupado, Vite falla
    // («Port 5173 is already in use») en lugar de usar otro puerto en el que el inicio de sesión no funcionaría.
    port: 5173,
    strictPort: true,
    // Las llamadas a /api se reenvían a la API local: mismo origen para el navegador, sin CORS.
    proxy: {
      '/api': {
        target: 'https://localhost:7180',
        secure: false, // certificado de desarrollo de ASP.NET Core
      },
    },
  },
})
