import { describe, expect, it } from 'vitest'
import { unirTramosContiguos } from './tramos'
import type { TramoCronograma } from './tipos'

const tramo = (inicio: string, fin: string, dias: number, cambios: Partial<TramoCronograma> = {}): TramoCronograma => ({
  rol: 'PRINCIPAL',
  tipo: 'AUTO',
  bloque: 1,
  persona: { rol: 'PRINCIPAL', numero: 1 },
  empleadoId: 7,
  codigoEkon: 'DEV007',
  nombreEmpleado: 'EMPLEADO PRUEBA 07',
  inicio,
  fin,
  dias,
  ...cambios,
})

describe('unirTramosContiguos (pendiente 26)', () => {
  it('une los contiguos y suma los días (caso del Id 10: base + regenerado)', () => {
    expect(unirTramosContiguos([tramo('2026-10-05', '2026-10-14', 10), tramo('2026-10-04', '2026-10-04', 1)])).toEqual([
      tramo('2026-10-04', '2026-10-14', 11),
    ])
  })

  it('une una cadena de tres y cruza el fin de mes', () => {
    expect(
      unirTramosContiguos([
        tramo('2026-10-29', '2026-10-31', 3),
        tramo('2026-11-01', '2026-11-02', 2),
        tramo('2026-11-03', '2026-11-03', 1),
      ]),
    ).toEqual([tramo('2026-10-29', '2026-11-03', 6)])
  })

  it('con un hueco no se unen', () => {
    const tramos = [tramo('2026-10-01', '2026-10-03', 3), tramo('2026-10-05', '2026-10-06', 2)]
    expect(unirTramosContiguos(tramos)).toEqual(tramos)
  })

  it.each([
    ['tipo', { tipo: 'MANUAL' }],
    ['bloque', { bloque: 2 }],
    ['rol', { rol: 'DESCANSO' }],
    ['empleado', { empleadoId: 8 }],
    ['persona', { persona: { rol: 'PRINCIPAL', numero: 2 } }],
  ] as const)('con distinto %s no se unen', (_, cambio) => {
    const resultado = unirTramosContiguos([tramo('2026-10-01', '2026-10-03', 3), tramo('2026-10-04', '2026-10-06', 3, cambio)])
    expect(resultado).toHaveLength(2)
  })

  it('no modifica la lista recibida', () => {
    const tramos = [tramo('2026-10-01', '2026-10-01', 1), tramo('2026-10-02', '2026-10-02', 1)]
    unirTramosContiguos(tramos)
    expect(tramos[0].fin).toBe('2026-10-01')
  })
})

describe('TAREA-26d-2: personas sin fila (empleadoId negativo)', () => {
  it('un Id temporal negativo se une como cualquier otro y no se mezcla con otro temporal', () => {
    const nuevo = { empleadoId: -1, codigoEkon: '900001', nombreEmpleado: 'PERSONA FICTICIA 1' }
    expect(unirTramosContiguos([tramo('2026-10-01', '2026-10-03', 3, nuevo), tramo('2026-10-04', '2026-10-05', 2, nuevo)])).toEqual([
      tramo('2026-10-01', '2026-10-05', 5, nuevo),
    ])
    const otro = { ...nuevo, empleadoId: -2, codigoEkon: '900002' }
    expect(unirTramosContiguos([tramo('2026-10-01', '2026-10-03', 3, nuevo), tramo('2026-10-04', '2026-10-05', 2, otro)])).toHaveLength(2)
  })
})
