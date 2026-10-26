// Días de una novedad (TAREA-24a: agrupación por mes del detalle; TAREA-24b: días del rango con la regla del backend,
// N6/N17: todos los días calendario, sin excluir fines de semana, máximo 366). Lógica pura, sin React ni zona horaria:
// las fechas "yyyy-MM-dd" se leen como UTC para que el día de la semana no dependa del equipo.

/** Tope de días de una novedad (DiasNovedad.MaxDias del backend; también llega en opciones-formulario). */
export const MAX_DIAS = 366
const MS_DIA = 86_400_000

function aUtc(fecha: string): number {
  const [anio, mes, dia] = fecha.split('-').map(Number)
  return Date.UTC(anio, mes - 1, dia)
}

/** Días calendario de inicio a fin, ambos incluidos; 0 si falta una fecha o fin < inicio. */
export function contarDias(inicio: string | undefined, fin: string | undefined): number {
  if (!inicio || !fin) {
    return 0
  }
  const dias = Math.round((aUtc(fin) - aUtc(inicio)) / MS_DIA) + 1
  return dias > 0 ? dias : 0
}

/** true si el rango supera el máximo («La novedad no puede superar 366 días.»). */
export function excedeMaximo(inicio: string | undefined, fin: string | undefined, maxDias = MAX_DIAS): boolean {
  return contarDias(inicio, fin) > maxDias
}

/** Fechas "yyyy-MM-dd" de inicio a fin, ambas incluidas (vacío si fin < inicio). Quien llama revisa antes excedeMaximo. */
export function diasDelRango(inicio: string, fin: string): string[] {
  const desde = aUtc(inicio)
  return Array.from({ length: contarDias(inicio, fin) }, (_, i) => new Date(desde + i * MS_DIA).toISOString().slice(0, 10))
}

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
  return `${DIAS_SEMANA[new Date(aUtc(dia)).getUTCDay()]} ${dia.slice(8, 10)}`
}
