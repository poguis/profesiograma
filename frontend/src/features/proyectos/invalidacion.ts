import type { QueryClient } from '@tanstack/react-query'
import { clavesCronograma } from '../cronograma/api'
import { clavesProyectos } from './api'

/**
 * Tras una escritura (crear, cambio de estado, cabecera, personal o reactivación). TAREA-21b (Q5): además de
 * ['proyectos'] (detalle, listado, formularios y el cronograma del proyecto), el cronograma general ['cronograma'].
 */
export function invalidarTrasEscritura(queryClient: QueryClient) {
  return Promise.all([
    queryClient.invalidateQueries({ queryKey: clavesProyectos.todos }),
    queryClient.invalidateQueries({ queryKey: clavesCronograma.todos }),
  ])
}
