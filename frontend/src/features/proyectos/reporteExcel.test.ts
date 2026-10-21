import { describe, expect, it } from 'vitest'
import {
  avisosResumen,
  consultaReporte,
  esCancelacion,
  filasDesdeEncabezado,
  nombreDesdeContentDisposition,
  nombrePorDefecto,
  ofreceReintento,
  puedeDescargar,
  rangoInicial,
  textoExito,
  validarRango,
} from './reporteExcel'
import type { ResumenReporte } from './tipos'

const RESUMEN: ResumenReporte = {
  filas: 40, personas: 2, personasSinCedula: 0, personasSinCodigoInfor: 0, filasSinActividad: 0, diasConOtroProyecto: 0,
}

describe('rangoInicial', () => {
  it('inicio y fin del proyecto; sin fin (salvaguarda), el de inicio', () => {
    expect(rangoInicial({ fechaInicio: '2026-10-01', fechaFin: '2026-12-31' })).toEqual({ desde: '2026-10-01', hasta: '2026-12-31' })
    expect(rangoInicial({ fechaInicio: '2026-10-01', fechaFin: '' })).toEqual({ desde: '2026-10-01', hasta: '2026-10-01' })
    expect(rangoInicial({ fechaInicio: '2026-10-01', fechaFin: null })).toEqual({ desde: '2026-10-01', hasta: '2026-10-01' })
  })
})

describe('validarRango', () => {
  it('obligatorias, con los textos del backend', () => {
    expect(validarRango(undefined, undefined)).toEqual({
      desde: "La fecha 'desde' es obligatoria.",
      hasta: "La fecha 'hasta' es obligatoria.",
    })
  })

  it('el error de formato del selector gana; hasta < desde va bajo Hasta; válido sin errores', () => {
    expect(validarRango(undefined, '2026-10-31', { desde: 'Fecha no válida.' })).toEqual({ desde: 'Fecha no válida.', hasta: undefined })
    expect(validarRango('2026-10-31', '2026-10-01')).toEqual({
      desde: undefined,
      hasta: "La fecha 'hasta' no puede ser anterior a la fecha 'desde'.",
    })
    expect(validarRango('2026-10-01', '2026-10-01')).toEqual({ desde: undefined, hasta: undefined })
    expect(validarRango('2024-01-01', '2027-12-31')).toEqual({ desde: undefined, hasta: undefined }) // sin tope
  })
})

describe('consulta y nombres', () => {
  it('consulta codificada y nombre por defecto como el backend', () => {
    expect(consultaReporte('2026-10-01', '2026-10-31')).toBe('desde=2026-10-01&hasta=2026-10-31')
    expect(nombrePorDefecto('PRY-2026/1 a', '2026-10-01', '2026-10-31')).toBe('Reporte_PRY-2026_1_a_20261001_20261031.xlsx')
  })

  it('Content-Disposition: filename* (con tildes) → filename → por defecto', () => {
    const def = 'Reporte_X_20261001_20261031.xlsx'
    expect(nombreDesdeContentDisposition(
      "attachment; filename=Reporte_A.xlsx; filename*=UTF-8''Reporte_Peri%C3%B3dico.xlsx", def)).toBe('Reporte_Periódico.xlsx')
    expect(nombreDesdeContentDisposition('attachment; filename="Reporte con espacio.xlsx"', def)).toBe('Reporte con espacio.xlsx')
    expect(nombreDesdeContentDisposition('attachment; filename=Reporte_B.xlsx', def)).toBe('Reporte_B.xlsx')
    expect(nombreDesdeContentDisposition(null, def)).toBe(def)
    expect(nombreDesdeContentDisposition(undefined, def)).toBe(def)
    expect(nombreDesdeContentDisposition('attachment', def)).toBe(def)
  })

  it('mal codificado cae al siguiente criterio; una ruta se reduce al nombre', () => {
    const def = 'Reporte_X.xlsx'
    expect(nombreDesdeContentDisposition("attachment; filename*=UTF-8''Reporte_%E0%A4%A.xlsx; filename=Reporte_C.xlsx", def)).toBe('Reporte_C.xlsx')
    expect(nombreDesdeContentDisposition("attachment; filename*=UTF-8''%E0%A4%A", def)).toBe(def)
    expect(nombreDesdeContentDisposition('attachment; filename="..\\..\\carpeta/Reporte_D.xlsx"', def)).toBe('Reporte_D.xlsx')
    expect(nombreDesdeContentDisposition("attachment; filename*=UTF-8''..%2Fotra%2FReporte_E.xlsx", def)).toBe('Reporte_E.xlsx')
  })
})

describe('filas, éxito y resumen', () => {
  it('X-Reporte-Filas: número o undefined', () => {
    expect(filasDesdeEncabezado('40')).toBe(40)
    expect(filasDesdeEncabezado(' 0 ')).toBe(0)
    expect([null, undefined, '', 'x', '-1', '1.5'].map(filasDesdeEncabezado)).toEqual([undefined, undefined, undefined, undefined, undefined, undefined])
    expect(textoExito(40)).toBe('Reporte generado (40 filas).')
    expect(textoExito(undefined)).toBe('Reporte generado.')
  })

  it('totales siempre y solo los avisos con n > 0', () => {
    expect(avisosResumen(RESUMEN)).toEqual({ totales: '40 fila(s) · 2 persona(s)', avisos: [] })
    expect(avisosResumen({ ...RESUMEN, personasSinCedula: 1, personasSinCodigoInfor: 2, filasSinActividad: 3, diasConOtroProyecto: 4 }).avisos).toEqual([
      '1 persona(s) sin cédula: la columna CEDULA irá vacía.',
      '2 persona(s) sin código Infor: la columna CARGO INFOR irá vacía.',
      '3 fila(s) sin actividad: la columna ACTIVIDAD irá vacía.',
      'En 4 caso(s) la persona también tiene asignación en otro proyecto el mismo día.',
    ])
  })
})

describe('puedeDescargar, reintento y cancelación', () => {
  const valido = { desde: undefined, hasta: undefined }

  it('solo con rango válido, resumen listo con filas y sin otra descarga', () => {
    expect(puedeDescargar(valido, RESUMEN, 'listo', false)).toBe(true)
    expect(puedeDescargar({ desde: "La fecha 'desde' es obligatoria." }, RESUMEN, 'listo', false)).toBe(false)
    expect(puedeDescargar(valido, RESUMEN, 'cargando', false)).toBe(false)
    expect(puedeDescargar(valido, undefined, 'error', false)).toBe(false)
    expect(puedeDescargar(valido, { ...RESUMEN, filas: 0 }, 'listo', false)).toBe(false)
    expect(puedeDescargar(valido, RESUMEN, 'listo', true)).toBe(false)
  })

  it('«Reintentar» con 503, 5xx y red; la cancelación no es un error', () => {
    expect((['noDisponible', 'servidor', 'red', 'validacion', 'noEncontrado'] as const).map(ofreceReintento)).toEqual([true, true, true, false, false])
    expect(esCancelacion(new DOMException('cancelado', 'AbortError'))).toBe(true)
    expect(esCancelacion(new Error('x'))).toBe(false)
  })
})
