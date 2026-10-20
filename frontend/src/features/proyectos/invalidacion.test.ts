import { QueryClient } from '@tanstack/react-query'
import { describe, expect, it } from 'vitest'
import { clavesCronograma } from '../cronograma/api'
import { clavesProyectos } from './api'
import { invalidarTrasEscritura } from './invalidacion'

describe('invalidarTrasEscritura (TAREA-21b, Q5)', () => {
  it('invalida el detalle, el cronograma del proyecto y el cronograma general', async () => {
    const queryClient = new QueryClient()
    queryClient.setQueryData(clavesProyectos.detalle(17), { id: 17 })
    queryClient.setQueryData(clavesProyectos.cronograma(17, ''), { personas: [] })
    queryClient.setQueryData(clavesCronograma.consulta('proyectoId=17'), { personas: [] })
    queryClient.setQueryData(['erp', 'horarios'], []) // fuera de las dos claves: no se toca

    await invalidarTrasEscritura(queryClient)

    const invalidada = (clave: readonly unknown[]) => queryClient.getQueryState(clave)?.isInvalidated
    expect(invalidada(clavesProyectos.detalle(17))).toBe(true)
    expect(invalidada(clavesProyectos.cronograma(17, ''))).toBe(true)
    expect(invalidada(clavesCronograma.consulta('proyectoId=17'))).toBe(true)
    expect(invalidada(['erp', 'horarios'])).toBe(false)
  })
})
