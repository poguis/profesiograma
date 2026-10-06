// Textos de presentación compartidos por formularios y diálogos de proyectos (funciones puras).
import { formatearFecha, formatearHora } from '../../utils/formato'
import type { EmpleadoCambio, HorarioErp } from './tipos'

const NOMBRE_ROL: Readonly<Record<string, string>> = { PRINCIPAL: 'Principal', BACK: 'Back', DESCANSO: 'Descanso' }

/** "DEV001 EMPLEADO PRUEBA 01" */
export function textoPersona(e: EmpleadoCambio): string {
  return `${e.codigoEkon} ${e.nombreCompleto}`
}

/** PRINCIPAL → "Principal"; un código desconocido se muestra tal cual. */
export function textoRol(codigo: string): string {
  return NOMBRE_ROL[codigo] ?? codigo
}

/** "dd/MM/yyyy" o "dd/MM/yyyy – dd/MM/yyyy". */
export function textoRango(desde: string, hasta: string): string {
  return desde === hasta ? formatearFecha(desde) : `${formatearFecha(desde)} – ${formatearFecha(hasta)}`
}

/** Las horas del ERP llegan "HH:mm:ss": solo se muestran (formatearHora), no entran al formulario. */
export function textoHorario(h: Pick<HorarioErp, 'descripcion' | 'horaEntrada' | 'horaSalida'>): string {
  const entrada = formatearHora(h.horaEntrada)
  const salida = formatearHora(h.horaSalida)
  return entrada && salida ? `${h.descripcion} · ${entrada}–${salida}` : h.descripcion
}
