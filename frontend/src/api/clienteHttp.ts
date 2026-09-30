import { obtenerEncabezadosAutenticacion } from '../auth'
import { ErrorApi, tipoPorEstado } from './errores'
import type { ProblemDetails, ValidationProblemDetails } from './tipos'

/** Base relativa: en desarrollo la resuelve el proxy de Vite (vite.config.ts). */
const BASE_API = '/api'

interface OpcionesSolicitud {
  signal?: AbortSignal
}

export function apiGet<T>(ruta: string, opciones?: OpcionesSolicitud): Promise<T> {
  return solicitar<T>('GET', ruta, opciones)
}

async function solicitar<T>(metodo: string, ruta: string, opciones?: OpcionesSolicitud): Promise<T> {
  // La identidad (DevAuth hoy, MSAL en la Fase 7) se obtiene solo en src/auth.
  const encabezados = new Headers({ Accept: 'application/json', ...(await obtenerEncabezadosAutenticacion()) })

  let respuesta: Response
  try {
    respuesta = await fetch(`${BASE_API}${ruta}`, { method: metodo, headers: encabezados, signal: opciones?.signal })
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

/** Convierte una respuesta no exitosa en ErrorApi. Soporta ProblemDetails, ValidationProblem y cuerpo vacío (401/403). */
async function crearErrorApi(respuesta: Response): Promise<ErrorApi> {
  const tipo = tipoPorEstado(respuesta.status)
  const problema = await leerProblema(respuesta)

  return new ErrorApi({
    tipo,
    estado: respuesta.status,
    titulo: problema?.title,
    detalle: problema?.detail,
    errores: problema && 'errors' in problema ? problema.errors : undefined,
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
