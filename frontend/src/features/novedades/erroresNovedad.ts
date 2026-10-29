// Errores de la API en los formularios de novedades (TAREA-24b; la 24c lo reutiliza en la edición y la anulación).
// Envoltorio de interpretarErrorDialogo (proyectos): el 409 se decide por `tipoConflicto` (TAREA-27) y el 404 habla de la
// novedad. Lógica pura, sin React.
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

/** Extensión `tipoConflicto` de los 409 de novedades (TAREA-27, `ProblemasConflicto` en la API; FASE_5_Novedades §1). */
export type TipoConflicto = 'duplicado' | 'cambio' | 'anulada' | 'registroConcurrente'

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
    // Solo por `tipoConflicto` (P4, sin respaldo por el texto). cambio, anulada o sin tipo: 409 genérico con «Recargar».
    switch (error.extensiones?.tipoConflicto as TipoConflicto | undefined) {
      case 'duplicado': {
        // Hay que cambiar el tipo o las fechas; `codigo` solo viene si quien registra ve la existente (R6).
        const codigo = typeof error.extensiones?.codigo === 'string' ? error.extensiones.codigo : undefined
        return { ...base, ofrecerRecarga: false, codigoDuplicado: codigo }
      }
      case 'registroConcurrente':
        // Otro registro se guardó al mismo tiempo: los datos del formulario siguen siendo válidos, basta reintentar (P3).
        return { ...base, ofrecerRecarga: false, ofrecerReintento: true }
      default:
        return base
    }
  }
  return base
}

/** Como interpretarErrorNovedad, pero acepta cualquier error (un fallo que no es de la API se puede reintentar). */
export function interpretarErrorDesconocido<C extends string>(error: unknown, campos: readonly C[]): ErroresNovedad<C> {
  return error instanceof ErrorApi
    ? interpretarErrorNovedad(error, campos)
    : { campos: {}, generales: ['Ocurrió un error inesperado.'], ofrecerRecarga: false, ofrecerReintento: true, noEncontrado: false }
}
