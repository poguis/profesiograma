import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { clavesNovedades, listarNovedades, obtenerNovedad } from './api'
import type { FiltrosNovedades } from './tipos'

/**
 * GET /api/novedades. Sin reintentos automáticos (H9, como el cronograma): con la base caída cada intento espera la
 * conexión y luego 503; el usuario reintenta con el botón. Mientras llega la página nueva se conserva la anterior.
 */
export function useNovedades(filtros: FiltrosNovedades) {
  return useQuery({
    queryKey: clavesNovedades.listado(filtros),
    queryFn: ({ signal }) => listarNovedades(filtros, signal),
    placeholderData: keepPreviousData,
    retry: false,
  })
}

/** Detalle. No consulta si el id de la ruta no es un entero positivo; sin reintentos automáticos (H9). */
export function useNovedad(id: number) {
  return useQuery({
    queryKey: clavesNovedades.detalle(id),
    queryFn: ({ signal }) => obtenerNovedad(id, signal),
    enabled: Number.isInteger(id) && id > 0,
    retry: false,
  })
}
