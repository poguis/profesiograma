// Punto ÚNICO de identidad para el cliente HTTP: X-Dev-User en DevAuth, Authorization: Bearer con MSAL (Fase 7b).
// Pantallas y clienteHttp no cambian.
import { devAuthActivo } from './modo'
import { encabezadosMsal } from './msal'

export { devAuthActivo }

export async function obtenerEncabezadosAutenticacion(): Promise<Record<string, string>> {
  // Condición literal (no la constante importada): Vite la resuelve a `false` en el build y el
  // import dinámico se elimina antes de generar chunks (devAuth.ts no se emite en producción).
  if (import.meta.env.DEV && import.meta.env.VITE_AUTH_MODE === 'dev') {
    const { leerUsuarioDev } = await import('./devAuth')
    return { 'X-Dev-User': leerUsuarioDev() }
  }

  return encabezadosMsal()
}
