import type { QueryClient } from '@tanstack/react-query'
import { clavesCronograma } from '../cronograma/api'
import { clavesNovedades } from './api'

/**
 * Tras crear, editar, anular o sincronizar novedades (N15): novedades, el cronograma general y el del proyecto, y el
 * resumen del reporte. No invalida ['proyectos'] completo (listados, detalle y formularios no dependen de novedades).
 */
export function invalidarTrasNovedad(queryClient: QueryClient) {
  return Promise.all([
    queryClient.invalidateQueries({ queryKey: clavesNovedades.todos }),
    queryClient.invalidateQueries({ queryKey: clavesCronograma.todos }),
    // Prefijos de clavesProyectos.cronograma(id, consulta) y clavesProyectos.reporteResumen(id, consulta).
    queryClient.invalidateQueries({ queryKey: ['proyectos', 'cronograma'] }),
    queryClient.invalidateQueries({ queryKey: ['proyectos', 'reporte-resumen'] }),
  ])
}
