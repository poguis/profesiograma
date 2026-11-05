// Fase 7b: reglas de la sesión con Entra ID (MSAL). Lógica pura, sin MSAL ni React: se prueba con dobles.
import { ErrorApi } from '../api/errores'

export const TITULO_SIN_ACCESO = 'No tienes acceso a PROFESIOGRAMA.'
export const DETALLE_SIN_ACCESO = 'Pide a TI que te asigne el rol Gestor o Admin.'
export const TITULO_SESION_VENCIDA = 'La sesión venció; vuelva a iniciar sesión.'
const TITULO_SIN_ROL_API = 'Sin rol asignado' // 403 de UsuarioActualMiddleware (P1)

export interface ConfigEntra {
  tenantId: string
  clientId: string
  scopeApi: string
}

type EnvEntra = Partial<Record<'VITE_ENTRA_TENANT_ID' | 'VITE_ENTRA_CLIENT_ID' | 'VITE_ENTRA_API_SCOPE', string>>

/** Valida las VITE_ENTRA_*; si falta alguna, devuelve sus nombres (la pantalla avisa en lugar de fallar en MSAL). */
export function leerConfigEntra(env: EnvEntra): { config: ConfigEntra } | { faltan: string[] } {
  const valores = {
    VITE_ENTRA_TENANT_ID: env.VITE_ENTRA_TENANT_ID?.trim() ?? '',
    VITE_ENTRA_CLIENT_ID: env.VITE_ENTRA_CLIENT_ID?.trim() ?? '',
    VITE_ENTRA_API_SCOPE: env.VITE_ENTRA_API_SCOPE?.trim() ?? '',
  }
  const faltan = Object.entries(valores)
    .filter(([, v]) => v === '')
    .map(([k]) => k)
  if (faltan.length > 0) {
    return { faltan }
  }
  return {
    config: { tenantId: valores.VITE_ENTRA_TENANT_ID, clientId: valores.VITE_ENTRA_CLIENT_ID, scopeApi: valores.VITE_ENTRA_API_SCOPE },
  }
}

/**
 * Ajuste 2: al cargar sin cuenta, UN solo intento de ssoSilent; si falla, el botón «Iniciar sesión». Nunca un redirect
 * automático (evita bucles de redirección).
 */
export function decidirInicio(estado: { hayCuenta: boolean; ssoIntentado: boolean }): 'listo' | 'ssoSilent' | 'boton' {
  if (estado.hayCuenta) {
    return 'listo'
  }
  return estado.ssoIntentado ? 'boton' : 'ssoSilent'
}

const CODIGOS_INTERACCION = new Set(['interaction_required', 'login_required', 'consent_required', 'no_account_error'])

/** sinAcceso: AADSTS501051 (asignación requerida en la API, P5); interaccion: hay que volver a iniciar sesión. */
export function clasificarErrorMsal(error: unknown): 'sinAcceso' | 'interaccion' | 'otro' {
  const e = (error ?? {}) as { errorCode?: unknown; errorMessage?: unknown; message?: unknown; name?: unknown }
  const texto = `${String(e.errorMessage ?? '')} ${String(e.message ?? '')}`
  if (texto.includes('AADSTS501051')) {
    return 'sinAcceso'
  }
  if (e.name === 'InteractionRequiredAuthError' || (typeof e.errorCode === 'string' && CODIGOS_INTERACCION.has(e.errorCode))) {
    return 'interaccion'
  }
  return 'otro'
}

/** Lo mínimo de PublicClientApplication que usa el cliente HTTP (permite dobles en las pruebas). */
export interface ClienteTokens<C> {
  getActiveAccount(): C | null
  acquireTokenSilent(solicitud: { scopes: string[]; account: C }): Promise<{ accessToken: string }>
}

/** Encabezado Bearer para la API. Sin cuenta o sin token: ErrorApi (nunca se registra el token). */
export async function encabezadosEntra<C>(cliente: ClienteTokens<C>, scopeApi: string): Promise<Record<string, string>> {
  const cuenta = cliente.getActiveAccount()
  if (!cuenta) {
    throw new ErrorApi({ tipo: 'noAutenticado', estado: null })
  }
  try {
    const { accessToken } = await cliente.acquireTokenSilent({ scopes: [scopeApi], account: cuenta })
    return { Authorization: `Bearer ${accessToken}` }
  } catch (error) {
    if (clasificarErrorMsal(error) === 'sinAcceso') {
      throw new ErrorApi({ tipo: 'prohibido', estado: null, titulo: TITULO_SIN_ACCESO, detalle: DETALLE_SIN_ACCESO })
    }
    throw new ErrorApi({ tipo: 'noAutenticado', estado: null, titulo: TITULO_SESION_VENCIDA })
  }
}

/** Qué pantalla corresponde a un error de GET /api/usuarios/me en modo Entra. */
export function pantallaPorErrorUsuario(error: unknown): 'sinAcceso' | 'iniciarSesion' | 'ninguna' {
  if (!(error instanceof ErrorApi)) {
    return 'ninguna'
  }
  if (error.tipo === 'prohibido' && (error.titulo === TITULO_SIN_ACCESO || error.titulo === TITULO_SIN_ROL_API)) {
    return 'sinAcceso'
  }
  return error.tipo === 'noAutenticado' ? 'iniciarSesion' : 'ninguna'
}
