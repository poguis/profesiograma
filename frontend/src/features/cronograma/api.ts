import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { apiGet } from '../../api/clienteHttp'
import { clavesProyectos } from '../proyectos/api'
import { aConsultaApi, aParametrosUrl, type FiltrosCronograma } from './cronograma'
import type { Cronograma } from './tipos'

export const clavesCronograma = {
  /** Todas las consultas de /cronograma: las invalida invalidarTrasEscritura (TAREA-21b, Q5). */
  todos: ['cronograma'] as const,
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

/** Rango de la pestaña del proyecto: sin fechas = D12 (desde el inicio, máximo CRONOGRAMA_MAX_DIAS días). */
export interface RangoCronograma {
  desde?: string
  hasta?: string
}

export function obtenerCronogramaProyecto(proyectoId: number, consulta: string, signal?: AbortSignal) {
  const ruta = `/proyectos/${proyectoId}/cronograma`
  return apiGet<Cronograma>(consulta ? `${ruta}?${consulta}` : ruta, { signal })
}

/**
 * GET /api/proyectos/{id}/cronograma (TAREA-21b). Solo consulta con la pestaña activa (`habilitada`), sin reintentos
 * automáticos (P10) y conservando la respuesta anterior mientras llega la nueva.
 */
export function useCronogramaProyecto(proyectoId: number, rango: RangoCronograma, habilitada: boolean) {
  const consulta = aParametrosUrl(rango).toString()
  return useQuery({
    queryKey: clavesProyectos.cronograma(proyectoId, consulta),
    queryFn: ({ signal }) => obtenerCronogramaProyecto(proyectoId, consulta, signal),
    enabled: habilitada && Number.isInteger(proyectoId) && proyectoId > 0,
    placeholderData: keepPreviousData,
    retry: false,
  })
}
