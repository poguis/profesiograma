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
  ResultadoSincronizacion,
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

/**
 * TAREA-24d: SIN `escenario` (P1: solo lo usa el script manual de la 23-3) y sin cuerpo (`JSON.stringify(undefined)` es
 * undefined: no viaja cuerpo ni Content-Type).
 */
export function sincronizarPermisosMedicos() {
  return apiPost<undefined, ResultadoSincronizacion>('/admin/novedades/sincronizar-permisos-medicos', undefined)
}

export function anularNovedad(id: number, solicitud: SolicitudAnularNovedad) {
  return apiPost<SolicitudAnularNovedad, NovedadEditada>(`/novedades/${id}/anular`, solicitud)
}
