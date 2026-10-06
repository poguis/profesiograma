// Errores de la API en los diálogos del detalle (cambio de estado, TAREA-15; edición de cabecera, TAREA-19a).
// Lógica pura, sin React: se prueba con Vitest (cambioEstado.test.ts y edicionCabecera.test.ts).
import type { TipoErrorApi } from '../../api/errores'

export interface ErroresDialogo<C extends string> {
  /** Mensaje por campo conocido (se muestra en el Field del campo). */
  campos: Partial<Record<C, string>>
  /** Mensajes sin campo identificable (se muestran arriba del diálogo). */
  generales: string[]
  /** "Recargar datos del proyecto": los datos con que se abrió el diálogo pueden estar viejos. */
  ofrecerRecarga: boolean
  /** 503 y red: botón "Reintentar". */
  ofrecerReintento: boolean
  /** 404: mensaje y enlace al listado. */
  noEncontrado: boolean
}

/** Datos mínimos de un error de la API (ErrorApi los cumple). */
export interface ErrorRespuesta {
  tipo: TipoErrorApi
  titulo: string
  errores?: Record<string, string[]>
}

export interface OpcionesInterpretacion<C extends string> {
  /** Claves del ValidationProblem que se muestran en su campo; el resto va a `generales`. */
  campos: readonly C[]
  /**
   * Un 400 ofrece "Recargar" si trae alguna de estas claves. Sin valor, todo 400 la ofrece (TAREA-15).
   */
  clavesRecarga?: readonly string[]
}

export function sinErrores<C extends string>(): ErroresDialogo<C> {
  return { campos: {}, generales: [], ofrecerRecarga: false, ofrecerReintento: false, noEncontrado: false }
}

export const MENSAJE_NO_ENCONTRADO = 'El proyecto no existe o no tiene acceso.'

export function interpretarErrorDialogo<C extends string>(
  error: ErrorRespuesta,
  opciones: OpcionesInterpretacion<C>,
): ErroresDialogo<C> {
  switch (error.tipo) {
    case 'validacion': {
      const claves = Object.keys(error.errores ?? {})
      const resultado: ErroresDialogo<C> = {
        ...sinErrores<C>(),
        ofrecerRecarga: opciones.clavesRecarga === undefined || claves.some((c) => opciones.clavesRecarga!.includes(c)),
      }
      for (const [clave, mensajes] of Object.entries(error.errores ?? {})) {
        const mensaje = mensajes.join(' ')
        if ((opciones.campos as readonly string[]).includes(clave)) {
          resultado.campos[clave as C] = mensaje
        } else {
          resultado.generales.push(mensaje)
        }
      }
      if (!error.errores) {
        resultado.generales.push(error.titulo)
      }
      return resultado
    }
    case 'conflicto':
      return { ...sinErrores<C>(), generales: [error.titulo], ofrecerRecarga: true }
    case 'noDisponible':
    case 'red':
      return { ...sinErrores<C>(), generales: [error.titulo], ofrecerReintento: true }
    case 'noEncontrado':
      return { ...sinErrores<C>(), generales: [MENSAJE_NO_ENCONTRADO], noEncontrado: true }
    default:
      return { ...sinErrores<C>(), generales: [error.titulo] }
  }
}
