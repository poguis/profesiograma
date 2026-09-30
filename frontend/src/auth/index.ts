// Punto ÚNICO de identidad para el cliente HTTP.
// Fase 7: reemplazar el modo DevAuth por MSAL (@azure/msal-react) devolviendo
// { Authorization: `Bearer ${token}` } aquí; pantallas y clienteHttp no cambian.
import { devAuthActivo } from './modo'

export { devAuthActivo }

export async function obtenerEncabezadosAutenticacion(): Promise<Record<string, string>> {
  // Condición literal (no la constante importada): Vite la resuelve a `false` en el build y el
  // import dinámico se elimina antes de generar chunks (devAuth.ts no se emite en producción).
  if (import.meta.env.DEV && import.meta.env.VITE_AUTH_MODE === 'dev') {
    const { leerUsuarioDev } = await import('./devAuth')
    return { 'X-Dev-User': leerUsuarioDev() }
  }

  return {}
}
