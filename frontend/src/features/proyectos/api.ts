import { apiGet } from '../../api/clienteHttp'
import type { PaginaResultado } from '../../api/tipos'
import { aConsultaApi } from './filtrosUrl'
import type { Catalogos, FiltrosProyectos, ProyectoDetalle, ProyectoResumen } from './tipos'

export const clavesProyectos = {
  todos: ['proyectos'] as const,
  listado: (filtros: FiltrosProyectos) => ['proyectos', 'listado', filtros] as const,
  detalle: (id: number) => ['proyectos', 'detalle', id] as const,
  catalogos: ['catalogos'] as const,
}

export function listarProyectos(filtros: FiltrosProyectos, signal?: AbortSignal) {
  return apiGet<PaginaResultado<ProyectoResumen>>(`/proyectos?${aConsultaApi(filtros)}`, { signal })
}

export function obtenerProyecto(id: number, signal?: AbortSignal) {
  return apiGet<ProyectoDetalle>(`/proyectos/${id}`, { signal })
}

export function obtenerCatalogos(signal?: AbortSignal) {
  return apiGet<Catalogos>('/catalogos', { signal })
}
