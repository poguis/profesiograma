import { apiGet, apiPost, apiPut } from '../../api/clienteHttp'
import type { PaginaResultado } from '../../api/tipos'
import { aConsultaApi } from './filtrosUrl'
import type {
  FiltrosNovedades,
  NovedadCreada,
  NovedadDetalle,
  NovedadEditada,
  NovedadResumen,
  OpcionesFormularioNovedad,
  SolicitudAnularNovedad,
  SolicitudCrearNovedad,
  SolicitudEditarNovedad,
} from './tipos'

export const clavesNovedades = {
  /** Todo lo de novedades: lo invalida invalidarTrasNovedad (N15). */
  todos: ['novedades'] as const,
  listado: (filtros: FiltrosNovedades) => ['novedades', 'listado', filtros] as const,
  detalle: (id: number) => ['novedades', 'detalle', id] as const,
  /** GET /api/novedades/opciones-formulario (se usa desde la 24b). */
  opciones: ['novedades', 'opciones-formulario'] as const,
}

export function listarNovedades(filtros: FiltrosNovedades, signal?: AbortSignal) {
  return apiGet<PaginaResultado<NovedadResumen>>(`/novedades?${aConsultaApi(filtros)}`, { signal })
}

export function obtenerNovedad(id: number, signal?: AbortSignal) {
  return apiGet<NovedadDetalle>(`/novedades/${id}`, { signal })
}

export function obtenerOpcionesNovedad(signal?: AbortSignal) {
  return apiGet<OpcionesFormularioNovedad>('/novedades/opciones-formulario', { signal })
}

export function crearNovedad(solicitud: SolicitudCrearNovedad) {
  return apiPost<SolicitudCrearNovedad, NovedadCreada>('/novedades', solicitud)
}

export function editarNovedad(id: number, solicitud: SolicitudEditarNovedad) {
  return apiPut<SolicitudEditarNovedad, NovedadEditada>(`/novedades/${id}`, solicitud)
}

export function anularNovedad(id: number, solicitud: SolicitudAnularNovedad) {
  return apiPost<SolicitudAnularNovedad, NovedadEditada>(`/novedades/${id}/anular`, solicitud)
}
