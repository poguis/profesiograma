// Diálogo "Cambiar estado" (TAREA-15). Lógica pura, sin React: se prueba con Vitest.
// Las opciones replican la máquina de estados SOLO para la interfaz; el servidor decide (FASE_5_Estados_Proyecto §1).
import type { TipoErrorApi } from '../../api/errores'
import type { DestinoCambioEstado, PrevisualizacionCambioEstado, SolicitudCambioEstado } from './tipos'

export interface OpcionDestino {
  codigo: DestinoCambioEstado | 'ACTIVO'
  etiqueta: string
  habilitada: boolean
  ayuda?: string
}

/** R1: solo ACTIVO y SUSPENDIDO admiten un cambio de estado. */
export function puedeCambiarEstado(estado: string): boolean {
  return estado === 'ACTIVO' || estado === 'SUSPENDIDO'
}

/** R2: ACTIVO → Suspender / Terminar; SUSPENDIDO → Terminar y "Reactivar" deshabilitado (TAREA-17). */
export function opcionesDestino(estado: string): OpcionDestino[] {
  switch (estado) {
    case 'ACTIVO':
      return [
        { codigo: 'SUSPENDIDO', etiqueta: 'Suspender', habilitada: true },
        { codigo: 'TERMINADO', etiqueta: 'Terminar', habilitada: true },
      ]
    case 'SUSPENDIDO':
      return [
        { codigo: 'TERMINADO', etiqueta: 'Terminar', habilitada: true },
        { codigo: 'ACTIVO', etiqueta: 'Reactivar', habilitada: false, ayuda: 'Disponible próximamente' },
      ]
    default:
      return []
  }
}

/** Fecha sugerida: vacía, salvo CIERRE desde SUSPENDIDO, que propone la fecha fin actual (H1). */
export function fechaPorDefecto(estado: string, destino: DestinoCambioEstado | null, fechaFin: string): string | null {
  return estado === 'SUSPENDIDO' && destino === 'TERMINADO' ? fechaFin : null
}

// ------------------------------------------------------------------ estado del diálogo

export interface EstadoDialogo {
  /** Aumenta al cambiar destino o fecha: una vista previa solo vale para su revisión. */
  revision: number
  destino: DestinoCambioEstado | null
  /** "yyyy-MM-dd" */
  fecha: string | null
  /** Casilla "Entiendo que el proyecto quedará TERMINADO" (no invalida la vista previa). */
  entiendeTerminado: boolean
}

export type AccionDialogo =
  | { tipo: 'destino'; destino: DestinoCambioEstado; estadoProyecto: string; fechaFin: string }
  | { tipo: 'fecha'; fecha: string | null }
  | { tipo: 'entiende'; valor: boolean }
  | { tipo: 'reiniciar' }

export function crearEstadoDialogo(): EstadoDialogo {
  return { revision: 0, destino: null, fecha: null, entiendeTerminado: false }
}

export function reducerDialogo(estado: EstadoDialogo, accion: AccionDialogo): EstadoDialogo {
  switch (accion.tipo) {
    case 'destino':
      if (accion.destino === estado.destino) {
        return estado
      }
      // D1: otro destino → fecha por defecto de ese destino y casilla desmarcada.
      return {
        revision: estado.revision + 1,
        destino: accion.destino,
        fecha: fechaPorDefecto(accion.estadoProyecto, accion.destino, accion.fechaFin),
        entiendeTerminado: false,
      }
    case 'fecha':
      return accion.fecha === estado.fecha ? estado : { ...estado, revision: estado.revision + 1, fecha: accion.fecha }
    case 'entiende':
      return { ...estado, entiendeTerminado: accion.valor }
    case 'reiniciar':
      return { ...crearEstadoDialogo(), revision: estado.revision + 1 }
  }
}

/** Cuerpo de la solicitud: el servidor valida destino y fecha (400 bajo `estadoDestino` / `fecha`). */
export function aSolicitudCambio(estado: EstadoDialogo): SolicitudCambioEstado {
  return { estadoDestino: estado.destino, fecha: estado.fecha }
}

// ------------------------------------------------------------------ vista previa y confirmación

export interface VistaCambio {
  revision: number
  datos: PrevisualizacionCambioEstado
}

export function vistaCambioVigente(vista: VistaCambio | null, revision: number): boolean {
  return vista !== null && vista.revision === revision
}

/** R4: vista previa vigente, sin envío en curso y, para TERMINAR, la casilla marcada. */
export function puedeConfirmar(vista: VistaCambio | null, estado: EstadoDialogo, enviando: boolean): boolean {
  if (enviando || !vistaCambioVigente(vista, estado.revision)) {
    return false
  }
  return estado.destino !== 'TERMINADO' || estado.entiendeTerminado
}

/** R5: "Proyecto suspendido (versión 2)." / "Proyecto terminado (versión 3).". */
export function mensajeExito(estado: string, version: number): string {
  const verbo = estado === 'TERMINADO' ? 'terminado' : estado === 'SUSPENDIDO' ? 'suspendido' : `en estado ${estado}`
  return `Proyecto ${verbo} (versión ${version}).`
}

// ------------------------------------------------------------------ errores (R5)

export interface ErroresCambio {
  campos: { estadoDestino?: string; fecha?: string }
  /** Mensajes sin campo identificable (se muestran arriba del diálogo). */
  generales: string[]
  /** 400 y 409: botón "Recargar datos del proyecto" (los datos con que se abrió el diálogo pueden estar viejos). */
  ofrecerRecarga: boolean
  /** 503 y red: botón "Reintentar". */
  ofrecerReintento: boolean
  /** 404: mensaje y enlace al listado. */
  noEncontrado: boolean
}

export const SIN_ERRORES_CAMBIO: ErroresCambio = {
  campos: {},
  generales: [],
  ofrecerRecarga: false,
  ofrecerReintento: false,
  noEncontrado: false,
}

/** Datos mínimos de un error de la API (ErrorApi los cumple). */
export interface ErrorRespuesta {
  tipo: TipoErrorApi
  titulo: string
  errores?: Record<string, string[]>
}

export function interpretarErrorCambio(error: ErrorRespuesta): ErroresCambio {
  switch (error.tipo) {
    case 'validacion': {
      const resultado: ErroresCambio = { ...SIN_ERRORES_CAMBIO, campos: {}, generales: [], ofrecerRecarga: true }
      for (const [clave, mensajes] of Object.entries(error.errores ?? {})) {
        const mensaje = mensajes.join(' ')
        if (clave === 'estadoDestino' || clave === 'fecha') {
          resultado.campos[clave] = mensaje
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
      return { ...SIN_ERRORES_CAMBIO, generales: [error.titulo], ofrecerRecarga: true }
    case 'noDisponible':
    case 'red':
      return { ...SIN_ERRORES_CAMBIO, generales: [error.titulo], ofrecerReintento: true }
    case 'noEncontrado':
      return { ...SIN_ERRORES_CAMBIO, generales: ['El proyecto no existe o no tiene acceso.'], noEncontrado: true }
    default:
      return { ...SIN_ERRORES_CAMBIO, generales: [error.titulo] }
  }
}
