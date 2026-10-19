import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { apiGet } from '../../api/clienteHttp'
import { aConsultaApi, type FiltrosCronograma } from './cronograma'
import type { Cronograma } from './tipos'

export const clavesCronograma = {
  consulta: (consulta: string) => ['cronograma', consulta] as const,
}

export function obtenerCronograma(consulta: string, signal?: AbortSignal) {
  return apiGet<Cronograma>(consulta ? `/cronograma?${consulta}` : '/cronograma', { signal })
}

/**
 * GET /api/cronograma. Sin reintentos automáticos (P10): con la base caída cada intento tarda ~15 s (pendiente 55);
 * el usuario reintenta con el botón. Mientras llega el rango nuevo se conserva el anterior (sin parpadeo).
 */
export function useCronograma(filtros: FiltrosCronograma) {
  const consulta = aConsultaApi(filtros)
  return useQuery({
    queryKey: clavesCronograma.consulta(consulta),
    queryFn: ({ signal }) => obtenerCronograma(consulta, signal),
    placeholderData: keepPreviousData,
    retry: false,
  })
}
