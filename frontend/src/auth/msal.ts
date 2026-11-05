// Fase 7b: instancia única de MSAL (modo Entra). La crea inicializarMsal() en main.tsx antes del primer render.
import { PublicClientApplication } from '@azure/msal-browser'
import { encabezadosEntra, leerConfigEntra, type ConfigEntra } from './entra'

let cliente: PublicClientApplication | null = null
let config: ConfigEntra | null = null

const CLAVE_PISTA = 'profesiograma.loginHint' // solo el nombre de usuario, para ssoSilent; nunca el token

/** VITE_ENTRA_* que faltan (vacío = completa). No crea MSAL. */
export function faltantesEntra(): string[] {
  const leida = leerConfigEntra(import.meta.env)
  return 'faltan' in leida ? leida.faltan : []
}

/** Crea e inicializa MSAL y procesa la vuelta de loginRedirect. Devuelve las VITE_ENTRA_* que faltan (vacío = listo). */
export async function inicializarMsal(): Promise<string[]> {
  const leida = leerConfigEntra(import.meta.env)
  if ('faltan' in leida) {
    return leida.faltan
  }
  config = leida.config
  cliente = new PublicClientApplication({
    auth: {
      clientId: config.clientId,
      authority: `https://login.microsoftonline.com/${config.tenantId}`,
      redirectUri: window.location.origin,
      postLogoutRedirectUri: window.location.origin,
    },
    cache: { cacheLocation: 'sessionStorage' }, // ajuste 2: la sesión no sobrevive al cierre del navegador
  })
  await cliente.initialize()
  const resultado = await cliente.handleRedirectPromise()
  const cuenta = resultado?.account ?? cliente.getAllAccounts()[0] ?? null
  if (cuenta) {
    cliente.setActiveAccount(cuenta)
    guardarPista(cuenta.username)
  }
  return []
}

export function clienteMsal(): PublicClientApplication {
  if (!cliente) {
    throw new Error('MSAL no está inicializado.')
  }
  return cliente
}

export function scopeApi(): string {
  return config?.scopeApi ?? ''
}

export function encabezadosMsal(): Promise<Record<string, string>> {
  return encabezadosEntra(clienteMsal(), scopeApi())
}

export function leerPista(): string | undefined {
  try {
    return localStorage.getItem(CLAVE_PISTA) ?? undefined
  } catch {
    return undefined
  }
}

export function guardarPista(usuario: string): void {
  try {
    localStorage.setItem(CLAVE_PISTA, usuario)
  } catch {
    // localStorage no disponible: ssoSilent irá sin loginHint
  }
}

export function borrarPista(): void {
  try {
    localStorage.removeItem(CLAVE_PISTA)
  } catch {
    // sin almacenamiento
  }
}
