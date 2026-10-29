import { describe, expect, it } from 'vitest'
import { formatearHoraUtc, hoyEnNegocio } from './formato'

describe('formatearHoraUtc (TAREA-25-3-2)', () => {
  it('convierte a la hora de Ecuador (UTC-5), en 24 h con ceros', () => {
    expect(formatearHoraUtc('2026-10-29T17:53:00Z')).toBe('12:53')
    expect(formatearHoraUtc('2026-10-29T04:05:59.9+00:00')).toBe('23:05') // día anterior en Ecuador
    expect(formatearHoraUtc('2026-10-29T05:00:00+00:00')).toBe('00:00')
  })

  it('vacío o inválido → texto vacío', () => {
    expect(formatearHoraUtc(null)).toBe('')
    expect(formatearHoraUtc('')).toBe('')
    expect(formatearHoraUtc('no es fecha')).toBe('')
  })
})

describe('hoyEnNegocio (TAREA-19c, P2)', () => {
  it('usa la fecha de Ecuador (UTC-5), no la UTC', () => {
    expect(hoyEnNegocio(new Date('2026-10-08T03:00:00Z'))).toBe('2026-10-07') // 22:00 del 07/10 en Ecuador
    expect(hoyEnNegocio(new Date('2026-10-08T05:00:00Z'))).toBe('2026-10-08') // 00:00 del 08/10 en Ecuador
  })

  it('formato yyyy-MM-dd con ceros a la izquierda', () => {
    expect(hoyEnNegocio(new Date('2026-01-05T17:00:00Z'))).toBe('2026-01-05')
  })
})
