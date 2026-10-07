import { describe, expect, it } from 'vitest'
import { hoyEnNegocio } from './formato'

describe('hoyEnNegocio (TAREA-19c, P2)', () => {
  it('usa la fecha de Ecuador (UTC-5), no la UTC', () => {
    expect(hoyEnNegocio(new Date('2026-10-08T03:00:00Z'))).toBe('2026-10-07') // 22:00 del 07/10 en Ecuador
    expect(hoyEnNegocio(new Date('2026-10-08T05:00:00Z'))).toBe('2026-10-08') // 00:00 del 08/10 en Ecuador
  })

  it('formato yyyy-MM-dd con ceros a la izquierda', () => {
    expect(hoyEnNegocio(new Date('2026-01-05T17:00:00Z'))).toBe('2026-01-05')
  })
})
