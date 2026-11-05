import { describe, expect, it } from 'vitest'
import { ErrorApi } from '../api/errores'
import {
  TITULO_SESION_VENCIDA,
  TITULO_SIN_ACCESO,
  clasificarErrorMsal,
  decidirArranque,
  decidirInicio,
  encabezadosEntra,
  leerConfigEntra,
  pantallaPorErrorUsuario,
} from './entra'

// Fase 7b: MSAL SIMULADO (dobles); nunca se llama a Entra ID. Valores ficticios.
const SCOPE = 'api://bbbbbbbb-0000-0000-0000-000000000002/access_as_user'

function clienteFalso(opciones: { cuenta?: { username: string } | null; token?: string; error?: unknown }) {
  const pedidos: { scopes: string[] }[] = []
  return {
    pedidos,
    getActiveAccount: () => (opciones.cuenta === undefined ? { username: 'persona@sedemi.com' } : opciones.cuenta),
    acquireTokenSilent: async (s: { scopes: string[]; account: { username: string } | null }) => {
      pedidos.push({ scopes: s.scopes })
      if (opciones.error) {
        throw opciones.error
      }
      return { accessToken: opciones.token ?? 'token-falso' }
    },
  }
}

describe('leerConfigEntra', () => {
  it('completa → config recortada; faltante o en blanco → sus nombres', () => {
    expect(
      leerConfigEntra({ VITE_ENTRA_TENANT_ID: ' t ', VITE_ENTRA_CLIENT_ID: 'c', VITE_ENTRA_API_SCOPE: SCOPE }),
    ).toEqual({ config: { tenantId: 't', clientId: 'c', scopeApi: SCOPE } })
    expect(leerConfigEntra({ VITE_ENTRA_CLIENT_ID: '  ' })).toEqual({
      faltan: ['VITE_ENTRA_TENANT_ID', 'VITE_ENTRA_CLIENT_ID', 'VITE_ENTRA_API_SCOPE'],
    })
  })
})

describe('decidirInicio (ajuste 2: un solo ssoSilent, nunca redirect automático)', () => {
  it('con cuenta → listo; sin cuenta → ssoSilent una vez y luego el botón', () => {
    expect(decidirInicio({ hayCuenta: true, ssoIntentado: false })).toBe('listo')
    expect(decidirInicio({ hayCuenta: true, ssoIntentado: true })).toBe('listo')
    expect(decidirInicio({ hayCuenta: false, ssoIntentado: false })).toBe('ssoSilent')
    expect(decidirInicio({ hayCuenta: false, ssoIntentado: true })).toBe('boton')
  })
})

describe('clasificarErrorMsal', () => {
  it('AADSTS501051 → sinAcceso; interacción requerida → interaccion; otro → otro', () => {
    expect(clasificarErrorMsal({ errorCode: 'invalid_grant', errorMessage: 'AADSTS501051: Application … is not assigned to a role' })).toBe(
      'sinAcceso',
    )
    expect(clasificarErrorMsal({ name: 'InteractionRequiredAuthError', errorCode: 'x' })).toBe('interaccion')
    expect(clasificarErrorMsal({ errorCode: 'login_required' })).toBe('interaccion')
    expect(clasificarErrorMsal(new Error('timed_out'))).toBe('otro')
    expect(clasificarErrorMsal(undefined)).toBe('otro')
  })
})

describe('encabezadosEntra', () => {
  it('con cuenta → Bearer con el scope de la API', async () => {
    const cliente = clienteFalso({ token: 'abc' })
    await expect(encabezadosEntra(cliente, SCOPE)).resolves.toEqual({ Authorization: 'Bearer abc' })
    expect(cliente.pedidos).toEqual([{ scopes: [SCOPE] }])
  })

  it('sin cuenta → noAutenticado sin pedir token', async () => {
    const cliente = clienteFalso({ cuenta: null })
    await expect(encabezadosEntra(cliente, SCOPE)).rejects.toMatchObject({ tipo: 'noAutenticado' })
    expect(cliente.pedidos).toEqual([])
  })

  it('AADSTS501051 → prohibido «No tienes acceso…»; interacción → sesión vencida', async () => {
    await expect(encabezadosEntra(clienteFalso({ error: { errorMessage: 'AADSTS501051' } }), SCOPE)).rejects.toMatchObject({
      tipo: 'prohibido',
      titulo: TITULO_SIN_ACCESO,
    })
    await expect(
      encabezadosEntra(clienteFalso({ error: { name: 'InteractionRequiredAuthError' } }), SCOPE),
    ).rejects.toMatchObject({ tipo: 'noAutenticado', titulo: TITULO_SESION_VENCIDA })
  })
})

describe('pantallaPorErrorUsuario', () => {
  it('403 «Sin rol asignado» o sin acceso de Entra → sinAcceso; 401 → iniciarSesion; el resto → ninguna', () => {
    expect(pantallaPorErrorUsuario(new ErrorApi({ tipo: 'prohibido', estado: 403, titulo: 'Sin rol asignado' }))).toBe('sinAcceso')
    expect(pantallaPorErrorUsuario(new ErrorApi({ tipo: 'prohibido', estado: null, titulo: TITULO_SIN_ACCESO }))).toBe('sinAcceso')
    expect(pantallaPorErrorUsuario(new ErrorApi({ tipo: 'prohibido', estado: 403, titulo: 'Usuario no habilitado' }))).toBe('ninguna')
    expect(pantallaPorErrorUsuario(new ErrorApi({ tipo: 'noAutenticado', estado: 401 }))).toBe('iniciarSesion')
    expect(pantallaPorErrorUsuario(new ErrorApi({ tipo: 'red', estado: null }))).toBe('ninguna')
    expect(pantallaPorErrorUsuario(null)).toBe('ninguna')
  })
})

// TAREA-7d-1 (J4): orden devAuth → requiereHttps → configIncompleta → app.
describe('decidirArranque', () => {
  const TODAS = ['VITE_ENTRA_TENANT_ID', 'VITE_ENTRA_CLIENT_ID', 'VITE_ENTRA_API_SCOPE']

  // Contexto seguro según el navegador: https, o http solo en localhost (el resto de http, p. ej. con IP, no lo es).
  function contextoSeguro(url: string): boolean {
    const u = new URL(url)
    return u.protocol === 'https:' || ['localhost', '127.0.0.1', '[::1]'].includes(u.hostname)
  }

  it('DevAuth gana a todo', () => {
    expect(decidirArranque({ devAuth: true, contextoSeguro: false, faltan: TODAS })).toBe('devAuth')
  })

  it('http con IP y sin IDs: requiere HTTPS (antes que la configuración incompleta)', () => {
    expect(decidirArranque({ devAuth: false, contextoSeguro: contextoSeguro('http://192.168.1.77:7082/'), faltan: TODAS })).toBe(
      'requiereHttps',
    )
  })

  it('https sin IDs: configuración incompleta', () => {
    expect(decidirArranque({ devAuth: false, contextoSeguro: contextoSeguro('https://profesiograma.sedemi.local/'), faltan: TODAS })).toBe(
      'configIncompleta',
    )
  })

  it('https completo: la aplicación', () => {
    expect(decidirArranque({ devAuth: false, contextoSeguro: contextoSeguro('https://profesiograma.sedemi.local/'), faltan: [] })).toBe('app')
  })

  it('http://localhost es contexto seguro (DEV en modo Entra no cambia)', () => {
    expect(decidirArranque({ devAuth: false, contextoSeguro: contextoSeguro('http://localhost:5173/'), faltan: [] })).toBe('app')
  })
})
