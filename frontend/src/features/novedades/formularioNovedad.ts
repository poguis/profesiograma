// Formulario de registro de novedades (TAREA-24b): estado, validación previa con los mensajes del backend
// (FASE_5_Novedades §2; el backend manda) y armado de la solicitud. Lógica pura, sin React.
import type { EmpleadoFila } from '../proyectos/formularioProyecto'
import { MAX_DIAS, contarDias } from './diasNovedad'
import type { CampoRegistro } from './erroresNovedad'
import type { AplicacionNovedad, SolicitudCrearNovedad } from './tipos'

export const LARGO_OBSERVACION = 500

export interface ProyectoElegido {
  id: number
  codigo: string
  nombre: string
}

export interface EstadoRegistro {
  aplicacion?: AplicacionNovedad
  empleado: EmpleadoFila | null
  proyecto: ProyectoElegido | null
  tipo?: string
  fechaInicio?: string
  fechaFin?: string
  observacion: string
}

export const REGISTRO_VACIO: EstadoRegistro = { empleado: null, proyecto: null, observacion: '' }

/** Cambiar la aplicación limpia el sujeto y el tipo (los tipos dependen de la aplicación, RN16). */
export function conAplicacion(estado: EstadoRegistro, aplicacion: AplicacionNovedad): EstadoRegistro {
  return estado.aplicacion === aplicacion ? estado : { ...estado, aplicacion, empleado: null, proyecto: null, tipo: undefined }
}

/** Algo escrito o elegido: activa la confirmación al salir. */
export function tieneCambios(e: EstadoRegistro): boolean {
  return Boolean(e.aplicacion || e.tipo || e.fechaInicio || e.fechaFin || e.observacion.trim() || e.empleado || e.proyecto)
}

/** Errores por campo antes de enviar, con los mensajes del backend. Vacío = se puede registrar. */
export function validarRegistro(e: EstadoRegistro, maxDias = MAX_DIAS): Partial<Record<CampoRegistro, string>> {
  const errores: Partial<Record<CampoRegistro, string>> = {}
  if (!e.aplicacion) {
    errores.tipoAplicacion = 'El tipo de aplicación es obligatorio.'
  } else if (e.aplicacion === 'PERSONA' && !e.empleado) {
    errores.codigoEkon = 'El empleado es obligatorio.'
  } else if (e.aplicacion === 'PROYECTO' && !e.proyecto) {
    errores.proyectoId = 'El proyecto es obligatorio.'
  }
  if (!e.tipo) {
    errores.tipoNovedad = 'El tipo de novedad es obligatorio.'
  }
  if (!e.fechaInicio) {
    errores.fechaInicio = 'La fecha de inicio es obligatoria.'
  }
  if (!e.fechaFin) {
    errores.fechaFin = 'La fecha fin es obligatoria.'
  } else if (e.fechaInicio && e.fechaFin < e.fechaInicio) {
    errores.fechaFin = 'La fecha fin debe ser mayor o igual a la fecha inicio.'
  } else if (contarDias(e.fechaInicio, e.fechaFin) > maxDias) {
    errores.fechaFin = `La novedad no puede superar ${maxDias} días.`
  }
  if (e.observacion.trim().length > LARGO_OBSERVACION) {
    errores.observacion = `La observación no puede superar ${LARGO_OBSERVACION} caracteres.`
  }
  return errores
}

/** Cuerpo de POST /api/novedades. Llamar solo con validarRegistro vacío. Observación recortada; vacía no se envía. */
export function aSolicitudCrear(e: EstadoRegistro): SolicitudCrearNovedad {
  const observacion = e.observacion.trim()
  return {
    tipoAplicacion: e.aplicacion!,
    tipoNovedad: e.tipo!,
    ...(e.aplicacion === 'PERSONA' && e.empleado ? { codigoEkon: e.empleado.codigoEkon } : {}),
    ...(e.aplicacion === 'PROYECTO' && e.proyecto ? { proyectoId: e.proyecto.id } : {}),
    fechaInicio: e.fechaInicio!,
    fechaFin: e.fechaFin!,
    ...(observacion ? { observacion } : {}),
  }
}
