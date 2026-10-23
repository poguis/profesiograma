import { describe, expect, it } from 'vitest'
import { aConsultaApi, aParametrosUrl, leerFiltros } from './filtrosUrl'
import type { FiltrosNovedades } from './tipos'

describe('filtrosUrl de novedades (TAREA-24a)', () => {
  it('valores por defecto con la URL vacía', () => {
    expect(leerFiltros(new URLSearchParams())).toEqual({ incluirAnuladas: false, pagina: 1, tamano: 20 })
    expect(aParametrosUrl({ incluirAnuladas: false, pagina: 1, tamano: 20 }).toString()).toBe('')
  })

  it('ida y vuelta con todos los filtros', () => {
    const filtros: FiltrosNovedades = {
      aplicacion: 'PERSONA',
      tipo: 'VACACIONES',
      origen: 'PROFESIOGRAMA',
      texto: 'NOV-DEV',
      desde: '2026-10-01',
      hasta: '2026-10-31',
      incluirAnuladas: true,
      pagina: 3,
      tamano: 50,
    }
    expect(leerFiltros(aParametrosUrl(filtros))).toEqual(filtros)
  })

  it('valores ilegibles vuelven al valor por defecto', () => {
    const filtros = leerFiltros(
      new URLSearchParams('aplicacion=OTRA&desde=05/10/2026&hasta=2026-1-1&incluirAnuladas=si&pagina=0&tamano=30'),
    )
    expect(filtros).toEqual({ incluirAnuladas: false, pagina: 1, tamano: 20 })
  })

  it('tamaño y página inválidos (texto o decimal) vuelven al valor por defecto', () => {
    expect(leerFiltros(new URLSearchParams('pagina=abc&tamano=10.5'))).toMatchObject({ pagina: 1, tamano: 20 })
    expect(leerFiltros(new URLSearchParams('pagina=2&tamano=10'))).toMatchObject({ pagina: 2, tamano: 10 })
  })

  it('recorta espacios y omite textos vacíos', () => {
    expect(leerFiltros(new URLSearchParams('texto=%20%20&tipo=%20FERIADO%20'))).toEqual({
      tipo: 'FERIADO',
      incluirAnuladas: false,
      pagina: 1,
      tamano: 20,
    })
  })

  it('la consulta a la API lleva siempre pagina y tamano', () => {
    expect(aConsultaApi({ incluirAnuladas: true, pagina: 1, tamano: 20 })).toBe('incluirAnuladas=true&pagina=1&tamano=20')
  })
})
