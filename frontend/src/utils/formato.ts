const ZONA_NEGOCIO = 'America/Guayaquil' // Ecuador (UTC-5)

const formatoFecha = new Intl.DateTimeFormat('es-EC', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  timeZone: ZONA_NEGOCIO,
})

const formatoFechaHora = new Intl.DateTimeFormat('es-EC', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
  timeZone: ZONA_NEGOCIO,
})

const PATRON_FECHA_ISO = /^(\d{4})-(\d{2})-(\d{2})$/
const PATRON_FECHA_EC = /^(\d{1,2})\/(\d{1,2})\/(\d{4})$/

/**
 * Formatea una fecha como dd/MM/yyyy (es-EC).
 * - "yyyy-MM-dd" (DateOnly de la API): se reordena el texto, sin conversión de zona (evita el desfase de un día).
 * - Date: se muestra en la zona de Ecuador.
 */
export function formatearFecha(valor: string | Date | null | undefined): string {
  if (valor == null || valor === '') {
    return ''
  }

  if (typeof valor === 'string') {
    const partes = PATRON_FECHA_ISO.exec(valor)
    if (partes) {
      return `${partes[3]}/${partes[2]}/${partes[1]}`
    }
    valor = new Date(valor)
  }

  return Number.isNaN(valor.getTime()) ? '' : formatoFecha.format(valor)
}

/** Instante UTC (ISO, p. ej. "2026-09-30T16:44:11Z") → "dd/MM/yyyy HH:mm" en hora de Ecuador. */
export function formatearFechaHora(valorIsoUtc: string | null | undefined): string {
  if (!valorIsoUtc) {
    return ''
  }
  const fecha = new Date(valorIsoUtc)
  return Number.isNaN(fecha.getTime()) ? '' : formatoFechaHora.format(fecha).replace(',', '')
}

const formatoFechaIsoNegocio = new Intl.DateTimeFormat('en-CA', {
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
  timeZone: ZONA_NEGOCIO,
})

/** Fecha de hoy en Ecuador como "yyyy-MM-dd" (TAREA-19c: fecha de reactivación por defecto). */
export function hoyEnNegocio(ahora: Date = new Date()): string {
  const partes = formatoFechaIsoNegocio.formatToParts(ahora)
  const parte = (tipo: Intl.DateTimeFormatPartTypes) => partes.find((p) => p.type === tipo)?.value ?? ''
  return `${parte('year')}-${parte('month')}-${parte('day')}`
}

/** "HH:mm:ss" (TimeOnly de la API) → "HH:mm". */
export function formatearHora(valor: string | null | undefined): string {
  return valor ? valor.slice(0, 5) : ''
}

// ------------------------------------------------ conversiones para selectores de fecha (fecha local, sin hora)

/** Date (fecha local del selector) → "yyyy-MM-dd" para la API/URL. */
export function aFechaIso(fecha: Date): string {
  const mes = String(fecha.getMonth() + 1).padStart(2, '0')
  const dia = String(fecha.getDate()).padStart(2, '0')
  return `${fecha.getFullYear()}-${mes}-${dia}`
}

/** "yyyy-MM-dd" → Date local (medianoche local). Null si el texto no es una fecha válida. */
export function desdeFechaIso(valor: string | null | undefined): Date | null {
  const partes = valor ? PATRON_FECHA_ISO.exec(valor) : null
  return partes ? crearFechaValida(Number(partes[1]), Number(partes[2]), Number(partes[3])) : null
}

/** "dd/MM/yyyy" escrito por el usuario → Date local. Null si no es válida (p. ej. 31/02/2026). */
export function desdeFechaEc(valor: string): Date | null {
  const partes = PATRON_FECHA_EC.exec(valor.trim())
  return partes ? crearFechaValida(Number(partes[3]), Number(partes[2]), Number(partes[1])) : null
}

function crearFechaValida(anio: number, mes: number, dia: number): Date | null {
  const fecha = new Date(anio, mes - 1, dia)
  return fecha.getFullYear() === anio && fecha.getMonth() === mes - 1 && fecha.getDate() === dia ? fecha : null
}
