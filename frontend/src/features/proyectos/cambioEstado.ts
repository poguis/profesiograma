// Diálogo "Cambiar estado" (TAREA-15). Lógica pura, sin React: se prueba con Vitest.
// Las opciones replican la máquina de estados SOLO para la interfaz; el servidor decide (FASE_5_Estados_Proyecto §1).
import { type ErrorRespuesta, type ErroresDialogo, interpretarErrorDialogo, sinErrores } from './erroresDialogo'
import type { DestinoCambioEstado, PrevisualizacionCambioEstado, SolicitudCambioEstado } from './tipos'
import { cambioPorOtro, conToken } from './tokenConcurrencia'

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

/**
 * Cuerpo del registro con el token BASE (TAREA-19b2): la última etapa del detalle con que se abrió el diálogo, nunca la
 * versión de la vista previa. Si el proyecto cambió desde entonces, el servidor responde 409 ("Recargar").
 */
export function aSolicitudRegistroCambio(estado: EstadoDialogo, versionBase: number): SolicitudCambioEstado {
  return conToken(aSolicitudCambio(estado), versionBase)
}

// ------------------------------------------------------------------ vista previa y confirmación

export interface VistaCambio {
  revision: number
  datos: PrevisualizacionCambioEstado
}

export function vistaCambioVigente(vista: VistaCambio | null, revision: number): boolean {
  return vista !== null && vista.revision === revision
}

/**
 * R4: vista previa vigente, sin envío en curso y, para TERMINAR, la casilla marcada. TAREA-19b2: con `versionBase`, una
 * vista previa con otra versión (alguien registró después de abrir el diálogo) bloquea la confirmación.
 */
export function puedeConfirmar(vista: VistaCambio | null, estado: EstadoDialogo, enviando: boolean, versionBase?: number): boolean {
  if (enviando || !vistaCambioVigente(vista, estado.revision)) {
    return false
  }
  if (versionBase !== undefined && cambioPorOtro(vista, versionBase)) {
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

/** Campos del diálogo; todo 400 ofrece "Recargar datos del proyecto" (lógica común en erroresDialogo.ts). */
export type ErroresCambio = ErroresDialogo<'estadoDestino' | 'fecha'>

export const SIN_ERRORES_CAMBIO: ErroresCambio = sinErrores()

export type { ErrorRespuesta }

export function interpretarErrorCambio(error: ErrorRespuesta): ErroresCambio {
  return interpretarErrorDialogo(error, { campos: ['estadoDestino', 'fecha'] })
}
