// Edición y anulación de novedades (TAREA-24c, FASE_5_Novedades §7): estado del formulario, días resultantes, recorte de
// excluidos al acortar el rango (A4), «sin cambios» (P3), resumen previo, solicitud y validación del motivo (N10).
// Lógica pura, sin React. Los mensajes son los del backend (el backend manda).
import { formatearFecha } from '../../utils/formato'
import { MAX_DIAS, diasDelRango, excedeMaximo } from './diasNovedad'
import { LARGO_OBSERVACION } from './formularioNovedad'
import type { NovedadDetalle, SolicitudEditarNovedad } from './tipos'

export type CampoEdicion = 'tipoNovedad' | 'fechaInicio' | 'fechaFin' | 'diasExcluidos' | 'observacion'

export const MENSAJE_SIN_DIAS = 'La novedad debe tener al menos 1 día.'
export const MENSAJE_SIN_CAMBIOS = 'No hay cambios para registrar.'
export const MENSAJE_MOTIVO = 'El motivo debe tener entre 5 y 500 caracteres.'
export const MOTIVO_MIN = 5
export const MOTIVO_MAX = 500

export interface EstadoEdicion {
  tipo?: string
  fechaInicio?: string
  fechaFin?: string
  /** Días del rango que NO se aplican (los chips apagados), ordenados. */
  excluidos: string[]
  observacion: string
}

/** Lo que cuenta para «sin cambios»: tipo, conjunto de días y observación recortada (null y vacía equivalen). */
export type NovedadOriginal = Pick<NovedadDetalle, 'tipo' | 'fechaInicio' | 'fechaFin' | 'dias' | 'observacion'>

/** Estado inicial desde el GET: rango guardado y excluidos = días del rango que no están en `dias`. */
export function estadoInicial(n: NovedadOriginal): EstadoEdicion {
  const dias = new Set(n.dias)
  return {
    tipo: n.tipo.codigo,
    fechaInicio: n.fechaInicio,
    fechaFin: n.fechaFin,
    excluidos: diasDelRango(n.fechaInicio, n.fechaFin).filter((d) => !dias.has(d)),
    observacion: n.observacion ?? '',
  }
}

/** Rango válido y dentro del máximo: sus días; si no, vacío. */
function diasValidos(e: EstadoEdicion, maxDias: number): string[] {
  if (!e.fechaInicio || !e.fechaFin || excedeMaximo(e.fechaInicio, e.fechaFin, maxDias)) {
    return []
  }
  return diasDelRango(e.fechaInicio, e.fechaFin)
}

/** Días que quedarán guardados: los del rango menos los excluidos. */
export function diasResultantes(e: EstadoEdicion, maxDias = MAX_DIAS): string[] {
  const excluidos = new Set(e.excluidos)
  return diasValidos(e, maxDias).filter((d) => !excluidos.has(d))
}

/**
 * Cambia el rango (A4): quita de los excluidos los días que quedan fuera y los devuelve para la nota. Los días nuevos al
 * ampliar entran incluidos (no están en los excluidos).
 */
export function conRango(e: EstadoEdicion, fechaInicio?: string, fechaFin?: string): { estado: EstadoEdicion; recortados: string[] } {
  const dentro = (d: string) => (!fechaInicio || d >= fechaInicio) && (!fechaFin || d <= fechaFin)
  return {
    estado: { ...e, fechaInicio, fechaFin, excluidos: e.excluidos.filter(dentro) },
    recortados: e.excluidos.filter((d) => !dentro(d)),
  }
}

/** Enciende o apaga un día (chip). */
export function alternarDia(e: EstadoEdicion, dia: string): EstadoEdicion {
  const excluidos = e.excluidos.includes(dia) ? e.excluidos.filter((d) => d !== dia) : [...e.excluidos, dia].sort()
  return { ...e, excluidos }
}

export function incluirTodos(e: EstadoEdicion): EstadoEdicion {
  return { ...e, excluidos: [] }
}

const recortada = (texto: string | null) => (texto ?? '').trim()

function mismosDias(a: readonly string[], b: readonly string[]): boolean {
  const conjunto = new Set(a)
  return conjunto.size === new Set(b).size && b.every((d) => conjunto.has(d))
}

/** P3: mismo tipo, mismo conjunto de días y misma observación recortada → sin cambios. */
export function hayCambios(original: NovedadOriginal, e: EstadoEdicion, maxDias = MAX_DIAS): boolean {
  return (
    e.tipo !== original.tipo.codigo ||
    !mismosDias(original.dias, diasResultantes(e, maxDias)) ||
    recortada(e.observacion) !== recortada(original.observacion)
  )
}

/** Errores por campo antes de enviar. Vacío = se puede guardar (si además hay cambios). */
export function validarEdicion(e: EstadoEdicion, maxDias = MAX_DIAS): Partial<Record<CampoEdicion, string>> {
  const errores: Partial<Record<CampoEdicion, string>> = {}
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
  } else if (excedeMaximo(e.fechaInicio, e.fechaFin, maxDias)) {
    errores.fechaFin = `La novedad no puede superar ${maxDias} días.`
  } else if (diasResultantes(e, maxDias).length === 0) {
    errores.diasExcluidos = MENSAJE_SIN_DIAS
  }
  if (recortada(e.observacion).length > LARGO_OBSERVACION) {
    errores.observacion = `La observación no puede superar ${LARGO_OBSERVACION} caracteres.`
  }
  return errores
}

/** Resumen previo de lo que cambiará (una línea por tema). `nombreTipo` traduce el código. */
export function resumenCambios(
  original: NovedadOriginal,
  e: EstadoEdicion,
  nombreTipo: (codigo: string) => string,
  maxDias = MAX_DIAS,
): string[] {
  const lineas: string[] = []
  if (e.tipo && e.tipo !== original.tipo.codigo) {
    lineas.push(`Tipo: ${original.tipo.nombre} → ${nombreTipo(e.tipo)}`)
  }
  if (e.fechaInicio !== original.fechaInicio || e.fechaFin !== original.fechaFin) {
    const rango = (a?: string, b?: string) => `${formatearFecha(a)}–${formatearFecha(b)}`
    lineas.push(`Rango: ${rango(original.fechaInicio, original.fechaFin)} → ${rango(e.fechaInicio, e.fechaFin)}`)
  }
  const dias = diasResultantes(e, maxDias)
  if (!mismosDias(original.dias, dias)) {
    const n = e.excluidos.length
    lineas.push(`Días: ${original.dias.length} → ${dias.length}${n ? ` (${n} ${n === 1 ? 'excluido' : 'excluidos'})` : ''}`)
  }
  const antes = recortada(original.observacion)
  const despues = recortada(e.observacion)
  if (antes !== despues) {
    lineas.push(despues ? 'Observación: cambiada' : 'Observación: borrada')
  }
  return lineas
}

/** Cuerpo del PUT. Llamar solo con validarEdicion vacío. La observación vacía viaja como null (la borra, P1). */
export function aSolicitudEditar(e: EstadoEdicion, version: string): SolicitudEditarNovedad {
  return {
    version,
    tipoNovedad: e.tipo!,
    fechaInicio: e.fechaInicio!,
    fechaFin: e.fechaFin!,
    diasExcluidos: e.excluidos,
    observacion: recortada(e.observacion) || null,
  }
}

/** N10: motivo recortado de 5 a 500 caracteres. Devuelve el mensaje del backend o undefined si es válido. */
export function validarMotivo(motivo: string): string | undefined {
  const largo = motivo.trim().length
  return largo < MOTIVO_MIN || largo > MOTIVO_MAX ? MENSAJE_MOTIVO : undefined
}
