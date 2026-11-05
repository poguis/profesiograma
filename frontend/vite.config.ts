import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
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
