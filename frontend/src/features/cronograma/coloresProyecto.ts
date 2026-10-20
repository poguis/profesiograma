// frontend/src/features/cronograma/coloresProyecto.ts
// Código del usuario (TAREA-21). Corrección del 19/10/2026: en lugar de mover el tono al borde de la zona prohibida
// (juntaba los tonos: 10,91° entre los Id 1..12), los Id 1..12 usan 12 huecos del arco libre y el resto, el ángulo
// áureo repartido sobre ese arco. Margen interno 18,5°.
export interface Hsl { h: number; s: number; l: number }

const ANGULO_AUREO = 137.508
// Margen interno: el redondeo HSL → hex (8 bits) mueve el tono hasta ~0,3°; con 18,5° nunca queda a menos de 18°.
const MARGEN_TONO = 18.5
// Separación mínima entre los huecos de los Id 1..12 (prueba 5).
const SEPARACION_HUECOS = 20
const HUECOS = 12
const SATURACION = 62
const LUMINOSIDADES = [38, 46, 54] as const
const DELTA_E_MINIMO = 20
// Respaldo si la respuesta no trae leyenda (semillas de TipoNovedad)
export const COLORES_NOVEDAD_BASE = ['#D13438', '#0277BD', '#FFB900', '#8E44AD', '#27AE60', '#607D8B']

export function hexAHsl(hex: string): Hsl {
  const n = parseInt(hex.replace('#', ''), 16)
  const r = ((n >> 16) & 255) / 255, g = ((n >> 8) & 255) / 255, b = (n & 255) / 255
  const max = Math.max(r, g, b), min = Math.min(r, g, b), d = max - min
  const l = (max + min) / 2
  if (d === 0) return { h: 0, s: 0, l: l * 100 }
  const s = d / (1 - Math.abs(2 * l - 1))
  let h = max === r ? ((g - b) / d) % 6 : max === g ? (b - r) / d + 2 : (r - g) / d + 4
  h = (h * 60 + 360) % 360
  return { h, s: s * 100, l: l * 100 }
}

export function hslAHex({ h, s, l }: Hsl): string {
  const sl = s / 100, ll = l / 100
  const c = (1 - Math.abs(2 * ll - 1)) * sl
  const x = c * (1 - Math.abs(((h / 60) % 2) - 1))
  const m = ll - c / 2
  const [r, g, b] =
    h < 60 ? [c, x, 0] : h < 120 ? [x, c, 0] : h < 180 ? [0, c, x] :
    h < 240 ? [0, x, c] : h < 300 ? [x, 0, c] : [c, 0, x]
  const aHex = (v: number) => Math.round((v + m) * 255).toString(16).padStart(2, '0')
  return `#${aHex(r)}${aHex(g)}${aHex(b)}`.toUpperCase()
}

/** Tonos prohibidos: solo los colores de novedad con saturación (los grises no ocupan tono). */
export function tonosProhibidos(coloresNovedad: readonly string[]): number[] {
  return coloresNovedad.map(hexAHsl).filter(c => c.s >= 25).map(c => c.h)
}

/** Tramo libre de tono [inicio, inicio + largo) en grados (puede cruzar 360°). */
export interface Arco { inicio: number; largo: number }

/** Tramos libres: el círculo menos ±MARGEN_TONO alrededor de cada tono prohibido (zonas solapadas se unen). */
export function arcosLibres(prohibidos: readonly number[]): Arco[] {
  if (prohibidos.length === 0) return [{ inicio: 0, largo: 360 }]
  const zonas = prohibidos
    .map(p => ((p - MARGEN_TONO) % 360 + 360) % 360)
    .sort((a, b) => a - b)
    .map(inicio => ({ inicio, fin: inicio + 2 * MARGEN_TONO }))
  const unidas: { inicio: number; fin: number }[] = []
  for (const z of zonas) {
    const ultima = unidas.at(-1)
    if (ultima && z.inicio <= ultima.fin) ultima.fin = Math.max(ultima.fin, z.fin)
    else unidas.push({ ...z })
  }
  // La última zona puede cruzar 360° y alcanzar a la primera.
  if (unidas.length > 1 && unidas.at(-1)!.fin - 360 >= unidas[0].inicio) {
    unidas[0].inicio = unidas.at(-1)!.inicio - 360
    unidas[0].fin = Math.max(unidas[0].fin, unidas.at(-1)!.fin - 360)
    unidas.pop()
  }
  if (unidas.length === 1 && unidas[0].fin - unidas[0].inicio >= 360) return []
  return unidas.map((z, i) => {
    const siguiente = i + 1 < unidas.length ? unidas[i + 1].inicio : unidas[0].inicio + 360
    return { inicio: z.fin % 360, largo: siguiente - z.fin }
  }).filter(a => a.largo > 0)
}

/** Tono a `distancia` grados desde el inicio del arco libre total (recorriendo los tramos en orden). */
function tonoEnArco(distancia: number, arcos: readonly Arco[]): number {
  let resto = distancia
  for (const a of arcos) {
    if (resto <= a.largo) return (a.inicio + resto) % 360
    resto -= a.largo
  }
  const ultimo = arcos.at(-1)!
  return (ultimo.inicio + ultimo.largo) % 360
}

/**
 * HUECOS tonos para los Id 1..12, repartidos por tramo según su largo (D'Hondt) sin pasar de su capacidad con
 * SEPARACION_HUECOS (bordes incluidos). En cada tramo: 1 hueco al centro; 2 o más, de borde a borde y equidistantes.
 * Orden de los Id: salto de 5 huecos (5 y 12 coprimos), para que Id consecutivos queden lejos.
 */
export function huecosTono(arcos: readonly Arco[]): number[] {
  if (arcos.length === 0) return []
  const capacidad = arcos.map(a => Math.floor(a.largo / SEPARACION_HUECOS) + 1)
  const asignados = arcos.map(() => 0)
  for (let n = 0; n < HUECOS; n++) {
    // Si la capacidad total no alcanza (leyenda con más colores), se reparte igual: la separación será menor.
    const conCupo = arcos.map((_, i) => i).filter(i => asignados[i] < capacidad[i])
    const candidatos = conCupo.length > 0 ? conCupo : arcos.map((_, i) => i)
    const elegido = candidatos.reduce((mejor, i) =>
      arcos[i].largo / (asignados[i] + 1) > arcos[mejor].largo / (asignados[mejor] + 1) ? i : mejor)
    asignados[elegido]++
  }
  const tonos = arcos.flatMap((a, i) => {
    const k = asignados[i]
    if (k === 0) return []
    if (k === 1) return [(a.inicio + a.largo / 2) % 360]
    return Array.from({ length: k }, (_, j) => (a.inicio + (a.largo * j) / (k - 1)) % 360)
  })
  tonos.sort((x, y) => x - y)
  return Array.from({ length: tonos.length }, (_, i) => tonos[(i * 5) % tonos.length])
}

/** Tono estable del Id: Id 1..12 en los huecos; desde el 13, ángulo áureo repartido sobre el arco libre. */
export function tonoProyecto(id: number, prohibidos: readonly number[]): number {
  const arcos = arcosLibres(prohibidos)
  if (arcos.length === 0) return (id * ANGULO_AUREO) % 360
  const huecos = huecosTono(arcos)
  if (id >= 1 && id <= huecos.length) return huecos[id - 1]
  const total = arcos.reduce((s, a) => s + a.largo, 0)
  const fraccion = ((id * ANGULO_AUREO) / 360) % 1
  return tonoEnArco(fraccion * total, arcos)
}

function hexALab(hex: string): [number, number, number] {
  const n = parseInt(hex.replace('#', ''), 16)
  const lin = (v: number) => { v /= 255; return v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4 }
  const [r, g, b] = [lin((n >> 16) & 255), lin((n >> 8) & 255), lin(n & 255)]
  const f = (t: number) => (t > 0.008856 ? Math.cbrt(t) : 7.787 * t + 16 / 116)
  const x = f((r * 0.4124 + g * 0.3576 + b * 0.1805) / 0.95047)
  const y = f(r * 0.2126 + g * 0.7152 + b * 0.0722)
  const z = f((r * 0.0193 + g * 0.1192 + b * 0.9505) / 1.08883)
  return [116 * y - 16, 500 * (x - y), 200 * (y - z)]
}

export function deltaE(a: string, b: string): number {
  const [l1, a1, b1] = hexALab(a), [l2, a2, b2] = hexALab(b)
  return Math.hypot(l1 - l2, a1 - a2, b1 - b2)
}

/** Color estable por Id, lejos de los colores de novedad. */
export function colorProyecto(id: number, coloresNovedad: readonly string[] = COLORES_NOVEDAD_BASE): string {
  const tono = tonoProyecto(id, tonosProhibidos(coloresNovedad))
  const inicio = id % LUMINOSIDADES.length
  let mejor = '', mejorDistancia = -1
  for (let k = 0; k < LUMINOSIDADES.length; k++) {
    const hex = hslAHex({ h: tono, s: SATURACION, l: LUMINOSIDADES[(inicio + k) % LUMINOSIDADES.length] })
    const minimo = Math.min(...coloresNovedad.map(c => deltaE(hex, c)))
    if (minimo >= DELTA_E_MINIMO) return hex
    if (minimo > mejorDistancia) { mejor = hex; mejorDistancia = minimo }
  }
  return mejor
}

/** Mapa Id → color para una respuesta. La leyenda del backend se une a las semillas. */
export function coloresDeRespuesta(ids: readonly number[], leyenda: readonly { color: string }[]): Map<number, string> {
  const novedad = [...new Set([...COLORES_NOVEDAD_BASE, ...leyenda.map(l => l.color.toUpperCase())])]
  return new Map(ids.map(id => [id, colorProyecto(id, novedad)]))
}

/**
 * Q4 (TAREA-21b): colores de novedad que no dependen de la respuesta: la base ∪ el catálogo de TipoNovedad
 * (GET /api/catalogos). Así un proyecto tiene el mismo color en /cronograma y en la pestaña del detalle, aunque sus
 * leyendas difieran. Sin catálogo (no cargó), solo la base.
 */
export function coloresNovedadEstables(tiposNovedad?: readonly { colorHex: string }[]): string[] {
  return [...new Set([...COLORES_NOVEDAD_BASE, ...(tiposNovedad ?? []).map(t => t.colorHex.toUpperCase())])]
}

/** Mapa Id → color con los colores de novedad estables (Q4): el mismo en las dos vistas. */
export function coloresEstables(ids: readonly number[], tiposNovedad?: readonly { colorHex: string }[]): Map<number, string> {
  return coloresDeRespuesta(ids, coloresNovedadEstables(tiposNovedad).map(color => ({ color })))
}
