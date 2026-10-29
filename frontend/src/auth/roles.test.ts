import { describe, expect, it } from 'vitest'
import { esAdmin } from './roles'

// Movida desde features/novedades/sincronizacion.test.ts (pendiente 70, TAREA-25-2), sin cambios.
describe('esAdmin', () => {
  it('sin distinguir mayúsculas; Gestor solo, vacío o ausente no', () => {
    expect(esAdmin({ roles: ['Admin'] })).toBe(true)
    expect(esAdmin({ roles: ['Gestor', 'admin'] })).toBe(true)
    expect(esAdmin({ roles: ['ADMIN'] })).toBe(true)
    expect(esAdmin({ roles: ['Gestor'] })).toBe(false)
    expect(esAdmin({ roles: [] })).toBe(false)
    expect(esAdmin(undefined)).toBe(false)
  })
})
