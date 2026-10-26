import { describe, expect, it } from 'vitest'
import { agruparPorMes, contarDias, diasDelRango, etiquetaDia, excedeMaximo } from './diasNovedad'

describe('diasDelRango y contarDias (TAREA-24b, N6/N17)', () => {
  it('inicio = fin es un día', () => {
    expect(diasDelRango('2070-03-10', '2070-03-10')).toEqual(['2070-03-10'])
    expect(contarDias('2070-03-10', '2070-03-10')).toBe(1)
  })

  it('incluye los fines de semana (viernes a lunes = 4 días)', () => {
    expect(diasDelRango('2070-01-03', '2070-01-06')).toEqual(['2070-01-03', '2070-01-04', '2070-01-05', '2070-01-06'])
  })

  it('cruza el cambio de mes con el 29 de febrero de un año bisiesto y el cambio de año', () => {
    expect(diasDelRango('2068-02-28', '2068-03-01')).toEqual(['2068-02-28', '2068-02-29', '2068-03-01'])
    expect(diasDelRango('2069-12-31', '2070-01-01')).toEqual(['2069-12-31', '2070-01-01'])
  })

  it('fin anterior al inicio o fecha faltante: sin días', () => {
    expect(diasDelRango('2070-03-10', '2070-03-09')).toEqual([])
    expect(contarDias(undefined, '2070-03-09')).toBe(0)
  })

  it('366 días se permiten y 367 no', () => {
    expect(contarDias('2070-01-01', '2071-01-01')).toBe(366)
    expect(excedeMaximo('2070-01-01', '2071-01-01')).toBe(false)
    expect(excedeMaximo('2070-01-01', '2071-01-02')).toBe(true)
    expect(diasDelRango('2068-01-01', '2068-12-31')).toHaveLength(366) // bisiesto
  })
})

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
