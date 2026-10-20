import { describe, expect, it } from 'vitest'
import { contrasteTexto } from './cronograma'
import {
  COLORES_NOVEDAD_BASE,
  colorProyecto,
  coloresDeRespuesta,
  deltaE,
  hexAHsl,
  hslAHex,
  arcosLibres,
  coloresEstables,
  coloresNovedadEstables,
  huecosTono,
  tonosProhibidos,
} from './coloresProyecto'

// Leyenda actual (semillas de TipoNovedad): F, V, P/PM, CD, PT, D. CHOQUE es un color del frontend.
const LEYENDA = [
  { tipo: 'FERIADO', sigla: 'F', color: '#D13438' },
  { tipo: 'VACACIONES', sigla: 'V', color: '#0277BD' },
  { tipo: 'PERMISO', sigla: 'P', color: '#FFB900' },
  { tipo: 'PERMISO_MEDICO', sigla: 'PM', color: '#FFB900' },
  { tipo: 'CALAMIDAD_DOMESTICA', sigla: 'CD', color: '#8E44AD' },
  { tipo: 'PATERNIDAD', sigla: 'PT', color: '#27AE60' },
  { tipo: 'DESCANSO', sigla: 'D', color: '#607D8B' },
]
const CHOQUE = '#201F1E'
const IDS = Array.from({ length: 200 }, (_, i) => i + 1)

const distanciaTono = (a: number, b: number) => {
  const d = Math.abs(a - b) % 360
  return d > 180 ? 360 - d : d
}

describe('coloresProyecto', () => {
  it('1. Ids 1..200: ningún color a ΔE < 20 de las novedades ni del choque', () => {
    const colores = coloresDeRespuesta(IDS, LEYENDA)
    const referencia = [...LEYENDA.map((l) => l.color), CHOQUE]
    const minimo = Math.min(...[...colores.values()].flatMap((c) => referencia.map((r) => deltaE(c, r))))
    console.info(`ΔE mínimo (Ids 1..200 frente a novedades y choque): ${minimo.toFixed(2)}`)
    expect(minimo).toBeGreaterThanOrEqual(20)
  })

  it('2. Ningún tono a menos de 18° de un tono de novedad saturado', () => {
    const prohibidos = tonosProhibidos(COLORES_NOVEDAD_BASE)
    const minimo = Math.min(...IDS.flatMap((id) => prohibidos.map((p) => distanciaTono(hexAHsl(colorProyecto(id)).h, p))))
    console.info(`Distancia de tono mínima a una novedad: ${minimo.toFixed(2)}°`)
    expect(minimo).toBeGreaterThanOrEqual(18)
  })

  it('3. Estable: colorProyecto(17) no depende de los demás Id de la respuesta', () => {
    const solo = coloresDeRespuesta([17], LEYENDA).get(17)
    const conOtros = coloresDeRespuesta([3, 17, 25, 140], LEYENDA).get(17)
    expect(conOtros).toBe(solo)
    expect(solo).toBe(colorProyecto(17, [...new Set([...COLORES_NOVEDAD_BASE, ...LEYENDA.map((l) => l.color)])]))
  })

  it('4. Un color nuevo en la leyenda (#00B7C3): los colores también se alejan de él', () => {
    const conNuevo = coloresDeRespuesta(IDS, [...LEYENDA, { color: '#00B7C3' }])
    const minimo = Math.min(...[...conNuevo.values()].map((c) => deltaE(c, '#00B7C3')))
    const minimoTono = Math.min(...[...conNuevo.values()].map((c) => distanciaTono(hexAHsl(c).h, hexAHsl('#00B7C3').h)))
    console.info(`Con #00B7C3: ΔE mínimo ${minimo.toFixed(2)}, tono mínimo ${minimoTono.toFixed(2)}°`)
    expect(minimo).toBeGreaterThanOrEqual(20)
    expect(minimoTono).toBeGreaterThanOrEqual(18)
  })

  it('5. Ids consecutivos 1..12: tonos separados ≥ 20° entre sí', () => {
    const tonos = Array.from({ length: 12 }, (_, i) => hexAHsl(colorProyecto(i + 1)).h)
    let minimo = 360
    for (let i = 0; i < tonos.length; i++) {
      for (let j = i + 1; j < tonos.length; j++) {
        minimo = Math.min(minimo, distanciaTono(tonos[i], tonos[j]))
      }
    }
    console.info(`Separación de tono mínima entre los Id 1..12: ${minimo.toFixed(2)}°`)
    expect(minimo).toBeGreaterThanOrEqual(20)
  })

  it('6. Contraste del texto ≥ 4,5 sobre los Id 1..200, las novedades y el choque', () => {
    const fondos = [...coloresDeRespuesta(IDS, LEYENDA).values(), ...LEYENDA.map((l) => l.color), CHOQUE]
    const minimo = Math.min(...fondos.map((f) => contrasteTexto(f).contraste))
    console.info(`Contraste mínimo del texto: ${minimo.toFixed(2)}`)
    expect(minimo).toBeGreaterThanOrEqual(4.5)
  })

  it('7. hexAHsl / hslAHex: ida y vuelta con ±1 por canal', () => {
    const canal = (hex: string, i: number) => parseInt(hex.slice(1 + i * 2, 3 + i * 2), 16)
    for (const hex of [...LEYENDA.map((l) => l.color), CHOQUE, '#000000', '#FFFFFF', '#00B7C3', '#4F6BED', ...IDS.slice(0, 50).map((id) => colorProyecto(id))]) {
      const vuelta = hslAHex(hexAHsl(hex))
      for (let i = 0; i < 3; i++) {
        expect(Math.abs(canal(vuelta, i) - canal(hex, i))).toBeLessThanOrEqual(1)
      }
    }
  })

  it('Arco libre con margen de 18,5°: 5 tramos (175°) y 12 huecos repartidos por largo', () => {
    const arcos = arcosLibres(tonosProhibidos(COLORES_NOVEDAD_BASE))
    const total = arcos.reduce((suma, a) => suma + a.largo, 0)
    expect(arcos).toHaveLength(5)
    expect(total).toBeCloseTo(360 - 5 * 37, 0)
    const huecos = huecosTono(arcos)
    expect(huecos).toHaveLength(12)
    expect(new Set(huecos.map((h) => h.toFixed(3))).size).toBe(12)
  })

  it('Sin colores saturados en la leyenda: todo el círculo es libre', () => {
    expect(arcosLibres([])).toEqual([{ inicio: 0, largo: 360 }])
    expect(tonosProhibidos(['#607D8B'])).toEqual([])
  })

  it('coloresNovedadEstables (Q4): base ∪ catálogo, sin repetir y en mayúsculas; sin catálogo, la base', () => {
    expect(coloresNovedadEstables()).toEqual(COLORES_NOVEDAD_BASE)
    expect(coloresNovedadEstables([{ colorHex: '#d13438' }, { colorHex: '#00b7c3' }])).toEqual([...COLORES_NOVEDAD_BASE, '#00B7C3'])
  })

  it('Q4: colorProyecto(17) es igual en /cronograma y en la pestaña aunque las leyendas de sus respuestas difieran', () => {
    const catalogo = [...LEYENDA.map((l) => ({ colorHex: l.color })), { colorHex: '#00B7C3' }]
    // Leyendas de respuesta distintas (una con una novedad que la otra no trae): ya no intervienen.
    const general = coloresEstables([3, 17, 25], catalogo).get(17)
    const pestana = coloresEstables([17], catalogo).get(17)
    expect(pestana).toBe(general)
    // Con la leyenda de cada respuesta (como antes) podían diferir:
    const antesGeneral = coloresDeRespuesta([17], [...LEYENDA, { color: '#00B7C3' }]).get(17)
    const antesPestana = coloresDeRespuesta([17], LEYENDA).get(17)
    expect(antesGeneral).not.toBe(antesPestana)
  })
})
