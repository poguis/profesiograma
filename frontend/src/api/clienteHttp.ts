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

/** PUT con cuerpo JSON (TAREA-24c: edición de novedades). Mismos errores que apiPost; tampoco se reintenta. */
export function apiPut<TReq, TRes>(ruta: string, cuerpo: TReq, opciones?: OpcionesSolicitud): Promise<TRes> {
  return solicitar<TRes>('PUT', ruta, JSON.stringify(cuerpo), opciones)
}

/**
 * Accept de las descargas .xlsx (Z29, TAREA-22b): también ProblemDetails, para que ASP.NET escriba el cuerpo de un error.
 */
const ACEPTA_XLSX =
  'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet, application/problem+json, application/json;q=0.9'

/**
 * GET de un archivo (TAREA-22b): misma autenticación y mismos errores que apiGet. Con fetch el estado se revisa antes de
 * leer el cuerpo, así que un error se lee como ProblemDetails (no como Blob). El blob no se guarda aquí.
 */
export async function apiDescargar(ruta: string, opciones?: OpcionesSolicitud): Promise<{ blob: Blob; encabezados: Headers }> {
  const respuesta = await enviar('GET', ruta, undefined, ACEPTA_XLSX, opciones)
  return { blob: await respuesta.blob(), encabezados: respuesta.headers }
}

async function solicitar<T>(
  metodo: string,
  ruta: string,
  cuerpoJson: string | undefined,
  opciones?: OpcionesSolicitud,
): Promise<T> {
  const respuesta = await enviar(metodo, ruta, cuerpoJson, 'application/json', opciones)

  if (respuesta.status === 204) {
    return undefined as T
  }

  return (await respuesta.json()) as T
}

/** fetch con la identidad y la conversión de errores comunes; devuelve la respuesta exitosa sin leer su cuerpo. */
async function enviar(
  metodo: string,
  ruta: string,
  cuerpoJson: string | undefined,
  aceptar: string,
  opciones?: OpcionesSolicitud,
): Promise<Response> {
  // La identidad (DevAuth hoy, MSAL en la Fase 7) se obtiene solo en src/auth.
  const encabezados = new Headers({ Accept: aceptar, ...(await obtenerEncabezadosAutenticacion()) })
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

  return respuesta
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
