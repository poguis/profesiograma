// Avisos "guardado frente al ERP" por persona (TAREA-26d-3). Lógica pura, sin React: se prueba con Vitest.
// Solo informan (D1): nunca bloquean la vista previa ni el registro. Ver docs/fases/FASE_4_Empleados_API.md §8.
import { claveEmpleado } from './formularioProyecto'
import type { AvisoErp, ComparacionErp, PersonaEdicion } from './tipos'

/** D4: único texto con el ERP caído (el mismo que trae la advertencia R7 de la reactivación). */
export const MENSAJE_NO_COMPARADO = 'No se pudo comparar con el ERP.'

export interface InsigniaErp {
  texto: string
  color: 'danger' | 'warning'
}

/** Insignia y detalle de una fila. */
export interface AvisosErpFila {
  insignia: InsigniaErp
  mensajes: string[]
}

/** NO_ACTIVO pesa más que los cambios de datos. */
export function insigniaErp(avisos: readonly AvisoErp[]): InsigniaErp | null {
  if (avisos.length === 0) {
    return null
  }
  return avisos.some((a) => a.tipo === 'NO_ACTIVO')
    ? { texto: 'No activo en el ERP', color: 'danger' }
    : { texto: 'Datos distintos en el ERP', color: 'warning' }
}

/** Avisos de un código (sin distinguir mayúsculas ni espacios extremos), o null si no tiene. */
export function avisosErpDe(comparacion: ComparacionErp | undefined, codigoEkon: string): AvisosErpFila | null {
  const clave = claveEmpleado(codigoEkon)
  const persona = comparacion?.personas.find((p) => claveEmpleado(p.codigoEkon) === clave)
  const insignia = persona ? insigniaErp(persona.avisos) : null
  return persona && insignia ? { insignia, mensajes: persona.avisos.map((a) => a.mensaje) } : null
}

/** Filas del formulario (clave → avisos). Quien llama decide qué filas entran (D3). */
export function avisosErpFilas(
  filas: readonly { clave: string; empleado: { codigoEkon: string } }[],
  comparacion: ComparacionErp | undefined,
): Record<string, AvisosErpFila> {
  const resultado: Record<string, AvisosErpFila> = {}
  for (const f of filas) {
    const avisos = avisosErpDe(comparacion, f.empleado.codigoEkon)
    if (avisos) {
      resultado[f.clave] = avisos
    }
  }
  return resultado
}

/** Personas históricas (Id de la persona → avisos), para la tabla de solo lectura de la reactivación (D3). */
export function avisosErpHistoricas(
  historicas: readonly Pick<PersonaEdicion, 'id' | 'empleado'>[],
  comparacion: ComparacionErp | undefined,
): Record<number, AvisosErpFila> {
  const resultado: Record<number, AvisosErpFila> = {}
  for (const h of historicas) {
    const avisos = avisosErpDe(comparacion, h.empleado.codigoEkon)
    if (avisos) {
      resultado[h.id] = avisos
    }
  }
  return resultado
}

export interface NotaErp {
  texto: string
  /** ERP caído o consulta fallida: se ofrece "Reintentar". La lista anterior no se reintenta (sí se comparó). */
  reintentable: boolean
}

/**
 * Nota general de la pantalla (una sola vez, D4): la consulta falló o el servidor no pudo comparar → "No se pudo
 * comparar con el ERP."; con lista anterior → su aviso con la hora. null si se comparó con la lista vigente.
 */
export function notaGeneralErp(comparacion: ComparacionErp | undefined, error: unknown): NotaErp | null {
  if (error) {
    return { texto: MENSAJE_NO_COMPARADO, reintentable: true }
  }
  const aviso = comparacion?.avisoGeneral
  return aviso ? { texto: aviso, reintentable: aviso === MENSAJE_NO_COMPARADO } : null
}

/** Advertencias del GET de la reactivación sin la nota del ERP: la pantalla la muestra una sola vez (D4). */
export function sinNotaErp(advertencias: readonly string[]): string[] {
  return advertencias.filter((a) => a !== MENSAJE_NO_COMPARADO)
}
