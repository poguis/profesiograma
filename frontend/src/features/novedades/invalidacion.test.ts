import { QueryClient } from '@tanstack/react-query'
import { describe, expect, it } from 'vitest'
import { clavesCronograma } from '../cronograma/api'
import { clavesProyectos } from '../proyectos/api'
import { clavesNovedades } from './api'
import { invalidarTrasNovedad } from './invalidacion'

describe('invalidarTrasNovedad (TAREA-24a, N15)', () => {
  it('invalida novedades, los dos cronogramas y el resumen del reporte; no el resto de proyectos ni el ERP', async () => {
    const queryClient = new QueryClient()
    const sujetas = [
      clavesNovedades.listado({ incluirAnuladas: false, pagina: 1, tamano: 20 }),
      clavesNovedades.detalle(5),
      clavesCronograma.consulta('proyectoId=17'),
      clavesProyectos.cronograma(17, 'desde=2026-10-01'),
      clavesProyectos.reporteResumen(17, 'desde=2026-10-01&hasta=2026-10-31'),
    ]
    const intactas = [
      clavesProyectos.listado({ pagina: 1, tamano: 20 }),
      clavesProyectos.detalle(17),
      ['erp', 'horarios'],
    ]
    for (const clave of [...sujetas, ...intactas]) {
      queryClient.setQueryData(clave, {})
    }

    await invalidarTrasNovedad(queryClient)

    const invalidada = (clave: readonly unknown[]) => queryClient.getQueryState(clave)?.isInvalidated
    for (const clave of sujetas) {
      expect(invalidada(clave), JSON.stringify(clave)).toBe(true)
    }
    for (const clave of intactas) {
      expect(invalidada(clave), JSON.stringify(clave)).toBe(false)
    }
  })
})
