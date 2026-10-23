import { describe, expect, it } from 'vitest'
import { agruparPorMes, etiquetaDia } from './diasNovedad'

describe('agruparPorMes (TAREA-24a)', () => {
  it('agrupa por mes en orden, con cambio de mes y de año', () => {
    const grupos = agruparPorMes(['2027-01-02', '2026-12-30', '2026-12-31', '2027-01-01', '2026-11-30'])
    expect(grupos.map((g) => g.clave)).toEqual(['2026-11', '2026-12', '2027-01'])
    expect(grupos.map((g) => g.titulo)).toEqual(['noviembre 2026', 'diciembre 2026', 'enero 2027'])
    expect(grupos[1].dias).toEqual(['2026-12-30', '2026-12-31'])
    expect(grupos[2].dias).toEqual(['2027-01-01', '2027-01-02'])
  })

  it('conserva el 29 de febrero de un año bisiesto en su mes', () => {
    const grupos = agruparPorMes(['2028-03-01', '2028-02-29', '2028-02-28'])
    expect(grupos).toEqual([
      { clave: '2028-02', titulo: 'febrero 2028', dias: ['2028-02-28', '2028-02-29'] },
      { clave: '2028-03', titulo: 'marzo 2028', dias: ['2028-03-01'] },
    ])
  })

  it('quita repetidos y devuelve vacío sin días', () => {
    expect(agruparPorMes(['2026-10-05', '2026-10-05'])[0].dias).toEqual(['2026-10-05'])
    expect(agruparPorMes([])).toEqual([])
  })
})

describe('etiquetaDia', () => {
  it('día de la semana en UTC, sin depender de la zona del equipo', () => {
    expect(etiquetaDia('2026-10-05')).toBe('lun 05')
    expect(etiquetaDia('2028-02-29')).toBe('mar 29')
    expect(etiquetaDia('2027-01-03')).toBe('dom 03')
  })
})
