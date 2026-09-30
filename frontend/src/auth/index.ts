// Punto ÚNICO de identidad para el cliente HTTP.
// Fase 7: reemplazar el modo DevAuth por MSAL (@azure/msal-react) devolviendo
// { Authorization: `Bearer ${token}` } aquí; pantallas y clienteHttp no cambian.
import { devAuthActivo, leerUsuarioDev } from './devAuth'

export { devAuthActivo }

export async function obtenerEncabezadosAutenticacion(): Promise<Record<string, string>> {
  if (devAuthActivo) {
    return { 'X-Dev-User': leerUsuarioDev() }
  }

  return {}
}
