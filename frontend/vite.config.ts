import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    // Las llamadas a /api se reenvían a la API local: mismo origen para el navegador, sin CORS.
    proxy: {
      '/api': {
        target: 'https://localhost:7180',
        secure: false, // certificado de desarrollo de ASP.NET Core
      },
    },
  },
})
