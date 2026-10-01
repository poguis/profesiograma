import { obtenerEncabezadosAutenticacion } from '../auth'
import { ErrorApi, tipoPorEstado } from './errores'
import type { ProblemDetails, ValidationProblemDetails } from './tipos'

/** Base relativa: en desarrollo la resuelve el proxy de Vite (vite.config.ts). */
const BASE_API = '/api'

interface OpcionesSolicitud {
  signal?: AbortSignal
}

export function apiGet<T>(ruta: string, opciones?: OpcionesSolicitud): Promise<T> {
  return solicitar<T>('GET', ruta, undefined, opciones)
}

/** POST con cuerpo JSON. Las mutaciones no se reintentan (queryClient: mutations.retry = false). */
export function apiPost<TReq, TRes>(ruta: string, cuerpo: TReq, opciones?: OpcionesSolicitud): Promise<TRes> {
  return solicitar<TRes>('POST', ruta, JSON.stringify(cuerpo), opciones)
}

async function solicitar<T>(
  metodo: string,
  ruta: string,
  cuerpoJson: string | undefined,
  opciones?: OpcionesSolicitud,
): Promise<T> {
  // La identidad (DevAuth hoy, MSAL en la Fase 7) se obtiene solo en src/auth.
  const encabezados = new Headers({ Accept: 'application/json', ...(await obtenerEncabezadosAutenticacion()) })
  if (cuerpoJson !== undefined) {
    encabezados.set('Content-Type', 'application/json')
  }

  let respuesta: Response
  try {
    respuesta = await fetch(`${BASE_API}${ruta}`, {
      method: metodo,
      headers: encabezados,
      body: cuerpoJson,
      signal: opciones?.signal,
    })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      throw error // cancelación de React Query: no es un error de la API
    }
    throw new ErrorApi({ tipo: 'red', estado: null })
  }

  if (!respuesta.ok) {
    throw await crearErrorApi(respuesta)
  }

  if (respuesta.status === 204) {
    return undefined as T
  }

  return (await respuesta.json()) as T
}

const MIEMBROS_ESTANDAR = new Set(['type', 'title', 'status', 'detail', 'instance', 'errors'])

/**
 * Convierte una respuesta no exitosa en ErrorApi. Soporta ProblemDetails, ValidationProblem y cuerpo vacío (401/403).
 * Los miembros no estándar (p. ej. `cruces` y `resumen` del 409) se conservan en `extensiones`.
 */
async function crearErrorApi(respuesta: Response): Promise<ErrorApi> {
  const tipo = tipoPorEstado(respuesta.status)
  const problema = await leerProblema(respuesta)
  const extensiones = problema
    ? Object.fromEntries(Object.entries(problema).filter(([clave]) => !MIEMBROS_ESTANDAR.has(clave)))
    : undefined

  return new ErrorApi({
    tipo,
    estado: respuesta.status,
    titulo: problema?.title,
    detalle: problema?.detail,
    errores: problema && 'errors' in problema ? (problema as ValidationProblemDetails).errors : undefined,
    extensiones,
  })
}

async function leerProblema(respuesta: Response): Promise<ProblemDetails | ValidationProblemDetails | null> {
  const tipoContenido = respuesta.headers.get('Content-Type') ?? ''
  if (!tipoContenido.includes('json')) {
    return null
  }

  try {
    return (await respuesta.json()) as ProblemDetails | ValidationProblemDetails
  } catch {
    return null // cuerpo vacío o JSON inválido: se usa el título por defecto del tipo
  }
}
