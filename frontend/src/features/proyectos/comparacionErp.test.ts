import { describe, expect, it } from 'vitest'
import { ErrorApi } from '../../api/errores'
import {
  MENSAJE_NO_COMPARADO,
  avisosErpDe,
  avisosErpFilas,
  avisosErpHistoricas,
  insigniaErp,
  notaGeneralErp,
  sinNotaErp,
} from './comparacionErp'
import type { AvisoErp, ComparacionErp } from './tipos'

// TAREA-26d-3: avisos "guardado frente al ERP" (datos ficticios).
const cambioCargo: AvisoErp = { tipo: 'CAMBIO_CARGO', mensaje: 'El cargo en el ERP cambió: TECNICO → SUPERVISOR.', guardado: 'TECNICO', erp: 'SUPERVISOR' }
const noActivo: AvisoErp = { tipo: 'NO_ACTIVO', mensaje: 'Ya no está activo en el ERP.', guardado: null, erp: null }

const comparacion = (cambios: Partial<ComparacionErp> = {}): ComparacionErp => ({
  comparadoUtc: '2026-10-13T15:30:00Z',
  avisoGeneral: null,
  personas: [
    { codigoEkon: 'DEV006', avisos: [cambioCargo] },
    { codigoEkon: '900001', avisos: [cambioCargo, noActivo] },
  ],
  ...cambios,
})

describe('comparación con el ERP (TAREA-26d-3)', () => {
  it('insignia: "No activo" pesa más que los cambios; sin avisos, ninguna', () => {
    expect(insigniaErp([cambioCargo])).toEqual({ texto: 'Datos distintos en el ERP', color: 'warning' })
    expect(insigniaErp([cambioCargo, noActivo])).toEqual({ texto: 'No activo en el ERP', color: 'danger' })
    expect(insigniaErp([])).toBeNull()
  })

  it('avisos por código, sin distinguir mayúsculas ni espacios', () => {
    expect(avisosErpDe(comparacion(), ' dev006 ')).toEqual({
      insignia: { texto: 'Datos distintos en el ERP', color: 'warning' },
      mensajes: [cambioCargo.mensaje],
    })
    expect(avisosErpDe(comparacion(), 'DEV007')).toBeNull()
    expect(avisosErpDe(undefined, 'DEV006')).toBeNull()
  })

  it('filas por clave y históricas por Id: solo las que tienen avisos', () => {
    const filas = [
      { clave: 'p91', empleado: { codigoEkon: 'DEV006' } },
      { clave: 'k93', empleado: { codigoEkon: 'DEV007' } },
      { clave: 'pn1', empleado: { codigoEkon: '900001' } },
    ]
    expect(Object.keys(avisosErpFilas(filas, comparacion()))).toEqual(['p91', 'pn1'])
    expect(avisosErpFilas(filas, comparacion()).pn1.insignia.color).toBe('danger')

    const historicas = [
      { id: 31, empleado: { id: 6, codigoEkon: 'DEV006', nombreCompleto: 'EMPLEADO PRUEBA 06' } },
      { id: 32, empleado: { id: 1, codigoEkon: 'DEV001', nombreCompleto: 'EMPLEADO PRUEBA 01' } },
    ]
    expect(Object.keys(avisosErpHistoricas(historicas, comparacion()))).toEqual(['31'])
  })

  it('nota general: ERP caído o consulta fallida con Reintentar; lista anterior sin Reintentar; comparada, ninguna', () => {
    expect(notaGeneralErp(comparacion({ avisoGeneral: MENSAJE_NO_COMPARADO, personas: [] }), null)).toEqual({
      texto: 'No se pudo comparar con el ERP.',
      reintentable: true,
    })
    expect(notaGeneralErp(undefined, new ErrorApi({ tipo: 'red', estado: null }))).toEqual({
      texto: MENSAJE_NO_COMPARADO,
      reintentable: true,
    })
    const anterior = 'Comparado con la lista de las 10:30; el ERP no respondió.'
    expect(notaGeneralErp(comparacion({ avisoGeneral: anterior }), null)).toEqual({ texto: anterior, reintentable: false })
    expect(notaGeneralErp(comparacion(), null)).toBeNull()
    expect(notaGeneralErp(undefined, null)).toBeNull()
  })

  it('D4: la nota del ERP se quita de las advertencias del GET para mostrarla una sola vez', () => {
    const advertencias = ['El proyecto no tiene principal inicial; se propone el último principal.', MENSAJE_NO_COMPARADO]
    expect(sinNotaErp(advertencias)).toEqual([advertencias[0]])
  })
})
