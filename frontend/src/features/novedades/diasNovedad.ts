// Días de una novedad (TAREA-24a: agrupación por mes del detalle). Lógica pura, sin React ni zona horaria:
// las fechas "yyyy-MM-dd" se leen como UTC para que el día de la semana no dependa del equipo.

const MESES = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre']
const DIAS_SEMANA = ['dom', 'lun', 'mar', 'mié', 'jue', 'vie', 'sáb']

export interface MesDias {
  /** "yyyy-MM" */
  clave: string
  /** "octubre 2026" */
  titulo: string
  dias: string[]
}

/** Agrupa fechas "yyyy-MM-dd" por mes, en orden cronológico (ordena y quita repetidos). */
export function agruparPorMes(dias: readonly string[]): MesDias[] {
  const meses: MesDias[] = []
  for (const dia of [...new Set(dias)].sort()) {
    const clave = dia.slice(0, 7)
    let mes = meses.at(-1)
    if (mes?.clave !== clave) {
      mes = { clave, titulo: `${MESES[Number(dia.slice(5, 7)) - 1]} ${dia.slice(0, 4)}`, dias: [] }
      meses.push(mes)
    }
    mes.dias.push(dia)
  }
  return meses
}

/** "2026-10-05" → "lun 05". */
export function etiquetaDia(dia: string): string {
  const [anio, mes, numero] = dia.split('-').map(Number)
  return `${DIAS_SEMANA[new Date(Date.UTC(anio, mes - 1, numero)).getUTCDay()]} ${dia.slice(8, 10)}`
}
