const ZONA_NEGOCIO = 'America/Guayaquil' // Ecuador (UTC-5)

const formatoFecha = new Intl.DateTimeFormat('es-EC', {
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  timeZone: ZONA_NEGOCIO,
})

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
    const partes = /^(\d{4})-(\d{2})-(\d{2})$/.exec(valor)
    if (partes) {
      return `${partes[3]}/${partes[2]}/${partes[1]}`
    }
    valor = new Date(valor)
  }

  return Number.isNaN(valor.getTime()) ? '' : formatoFecha.format(valor)
}
