// Errores de la API en los formularios de novedades (TAREA-24b; la 24c lo reutiliza en la edición y la anulación).
// Envoltorio de interpretarErrorDialogo (proyectos): un 409 de duplicado NO ofrece «Recargar» (hay que cambiar el tipo o
// las fechas) y el 404 habla de la novedad. Lógica pura, sin React.
import { ErrorApi } from '../../api/errores'
import { type ErrorRespuesta, type ErroresDialogo, interpretarErrorDialogo } from '../proyectos/erroresDialogo'

/** Claves del ValidationProblem de POST /api/novedades que van a su control (FASE_5_Novedades §2); `general` va arriba. */
export const CAMPOS_REGISTRO = [
  'tipoAplicacion',
  'tipoNovedad',
  'codigoEkon',
  'proyectoId',
  'fechaInicio',
  'fechaFin',
  'observacion',
] as const
export type CampoRegistro = (typeof CAMPOS_REGISTRO)[number]

/** TAREA-24c: claves del PUT que van a su control (`diasExcluidos` → zona de chips); `general`, `origen` y `version` arriba. */
export const CAMPOS_EDICION = ['tipoNovedad', 'fechaInicio', 'fechaFin', 'diasExcluidos', 'observacion'] as const
/** TAREA-24c: clave de la anulación que va bajo su campo. */
export const CAMPOS_ANULACION = ['motivo'] as const

export const MENSAJE_NOVEDAD_NO_ENCONTRADA = 'La novedad no existe o no tiene acceso.'
/**
 * title del 409 de duplicado (ResultadoCrearNovedad.MensajeDuplicado, N7). Acoplamiento: el ProblemDetails del backend no
 * trae un discriminador (`type`) para sus 409; el duplicado se reconoce por `codigo` (si viene, R6) o por este title exacto.
 */
export const MENSAJE_DUPLICADO = 'Ya existe una novedad de ese tipo para ese sujeto en esas fechas.'

export interface ErroresNovedad<C extends string> extends ErroresDialogo<C> {
  /** R6: código de la novedad existente, solo si el backend lo envía (quien registra la ve). */
  codigoDuplicado?: string
}

export function interpretarErrorNovedad<C extends string>(
  error: ErrorRespuesta & { extensiones?: Record<string, unknown> },
  campos: readonly C[],
): ErroresNovedad<C> {
  // Un 400 solo ofrece «Recargar» si es por la versión (24c: «Falta la versión…» / «no es válida»).
  const base = interpretarErrorDialogo(error, { campos, clavesRecarga: ['version'] })
  if (error.tipo === 'noEncontrado') {
    return { ...base, generales: [MENSAJE_NOVEDAD_NO_ENCONTRADA] }
  }
  if (error.tipo === 'conflicto') {
    const codigo = typeof error.extensiones?.codigo === 'string' ? error.extensiones.codigo : undefined
    const duplicado = codigo !== undefined || error.titulo === MENSAJE_DUPLICADO
    return duplicado ? { ...base, ofrecerRecarga: false, codigoDuplicado: codigo } : base
  }
  return base
}

/** Como interpretarErrorNovedad, pero acepta cualquier error (un fallo que no es de la API se puede reintentar). */
export function interpretarErrorDesconocido<C extends string>(error: unknown, campos: readonly C[]): ErroresNovedad<C> {
  return error instanceof ErrorApi
    ? interpretarErrorNovedad(error, campos)
    : { campos: {}, generales: ['Ocurrió un error inesperado.'], ofrecerRecarga: false, ofrecerReintento: true, noEncontrado: false }
}
