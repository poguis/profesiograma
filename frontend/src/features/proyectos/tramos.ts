// Unión de tramos contiguos para mostrarlos (TAREA-19b, pendiente 26). Solo presentación: el motor devuelve la base
// (días < corte) y lo regenerado como tramos separados (p. ej. PRINCIPAL 04/10–04/10 y 05/10–14/10 en el Id 10).
import type { TramoCronograma } from './tipos'

const MS_DIA = 86_400_000

/** "yyyy-MM-dd" + 1 día (en UTC: sin desfases por la zona horaria). */
function diaSiguiente(fecha: string): string {
  const [anio, mes, dia] = fecha.split('-').map(Number)
  return new Date(Date.UTC(anio, mes - 1, dia) + MS_DIA).toISOString().slice(0, 10)
}

function claveUnion(t: TramoCronograma): string {
  return [t.persona.rol, t.persona.numero, t.empleadoId, t.rol, t.tipo, t.bloque].join('|')
}

/**
 * Tramos de la misma persona (rol y número), empleado, rol, tipo y bloque con fechas consecutivas (inicio = fin
 * anterior + 1) se muestran como uno, con los días sumados. El resultado queda ordenado por persona y fecha de inicio.
 */
export function unirTramosContiguos(tramos: readonly TramoCronograma[]): TramoCronograma[] {
  const ordenados = [...tramos].sort(
    (a, b) =>
      a.persona.rol.localeCompare(b.persona.rol) ||
      a.persona.numero - b.persona.numero ||
      a.inicio.localeCompare(b.inicio) ||
      a.rol.localeCompare(b.rol),
  )

  const resultado: TramoCronograma[] = []
  const ultimoPorClave = new Map<string, number>()
  for (const tramo of ordenados) {
    const clave = claveUnion(tramo)
    const indice = ultimoPorClave.get(clave)
    const anterior = indice === undefined ? undefined : resultado[indice]
    if (anterior && diaSiguiente(anterior.fin) === tramo.inicio) {
      resultado[indice!] = { ...anterior, fin: tramo.fin, dias: anterior.dias + tramo.dias }
      continue
    }
    ultimoPorClave.set(clave, resultado.length)
    resultado.push({ ...tramo })
  }
  return resultado
}
