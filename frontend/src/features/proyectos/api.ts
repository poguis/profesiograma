import { apiGet, apiPost } from '../../api/clienteHttp'
import type { PaginaResultado } from '../../api/tipos'
import { aConsultaApi } from './filtrosUrl'
import type {
  ActividadErp,
  CambioEstadoRealizado,
  Catalogos,
  CompaniaErp,
  DimensionErp,
  EmpleadoBusqueda,
  FiltroEmpleados,
  FiltrosProyectos,
  HorarioErp,
  OpcionesFormularioProyecto,
  Previsualizacion,
  PrevisualizacionCambioEstado,
  ProyectoCreado,
  ProyectoDetalle,
  ProyectoErp,
  ProyectoResumen,
  SolicitudCambioEstado,
  SolicitudCrearProyecto,
} from './tipos'

export const clavesProyectos = {
  todos: ['proyectos'] as const,
  listado: (filtros: FiltrosProyectos) => ['proyectos', 'listado', filtros] as const,
  detalle: (id: number) => ['proyectos', 'detalle', id] as const,
  catalogos: ['catalogos'] as const,
  opcionesFormulario: ['proyectos', 'opciones-formulario'] as const,
}

// Claves fuera de 'proyectos': invalidar el listado tras registrar no debe recargar el ERP ni los empleados.
export const clavesErp = {
  companias: ['erp', 'companias'] as const,
  proyectos: (companiaId: number) => ['erp', 'companias', companiaId, 'proyectos'] as const,
  dimensiones: (companiaId: number) => ['erp', 'companias', companiaId, 'dimensiones'] as const,
  actividades: (companiaId: number, proyectoErpId: string) =>
    ['erp', 'companias', companiaId, 'proyectos', proyectoErpId, 'actividades'] as const,
  horarios: ['erp', 'horarios'] as const,
}

export const clavesEmpleados = {
  busqueda: (filtro: FiltroEmpleados) => ['empleados', filtro] as const,
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

export function obtenerOpcionesFormulario(signal?: AbortSignal) {
  return apiGet<OpcionesFormularioProyecto>('/proyectos/opciones-formulario', { signal })
}

export function listarCompaniasErp(signal?: AbortSignal) {
  return apiGet<CompaniaErp[]>('/erp/companias', { signal })
}

export function listarProyectosErp(companiaId: number, signal?: AbortSignal) {
  return apiGet<ProyectoErp[]>(`/erp/companias/${companiaId}/proyectos`, { signal })
}

export function listarDimensionesErp(companiaId: number, signal?: AbortSignal) {
  return apiGet<DimensionErp[]>(`/erp/companias/${companiaId}/dimensiones`, { signal })
}

export function listarActividadesErp(companiaId: number, proyectoErpId: string, signal?: AbortSignal) {
  return apiGet<ActividadErp[]>(
    `/erp/companias/${companiaId}/proyectos/${encodeURIComponent(proyectoErpId)}/actividades`,
    { signal },
  )
}

export function listarHorariosErp(signal?: AbortSignal) {
  return apiGet<HorarioErp[]>('/erp/horarios', { signal })
}

export function buscarEmpleados(filtro: FiltroEmpleados, signal?: AbortSignal) {
  const consulta = new URLSearchParams({
    soloMisDepartamentos: String(filtro.soloMisDepartamentos),
    pagina: String(filtro.pagina),
    tamano: String(filtro.tamano),
  })
  if (filtro.texto) {
    consulta.set('texto', filtro.texto)
  }
  return apiGet<PaginaResultado<EmpleadoBusqueda>>(`/empleados?${consulta.toString()}`, { signal })
}

/** 200 con tramos, días y cruces; no guarda nada. 400 validación; 503 ERP no disponible. */
export function previsualizarProyecto(solicitud: SolicitudCrearProyecto) {
  return apiPost<SolicitudCrearProyecto, Previsualizacion>('/proyectos/previsualizar', solicitud)
}

/** 201 { id, codigo }; 400 validación; 409 cruces (extensiones); 503 ERP no disponible o registro ocupado. */
export function crearProyecto(solicitud: SolicitudCrearProyecto) {
  return apiPost<SolicitudCrearProyecto, ProyectoCreado>('/proyectos', solicitud)
}

/** Impacto de suspender o terminar (no guarda). 400 / 404 / 503. */
export function previsualizarCambioEstado(id: number, solicitud: SolicitudCambioEstado) {
  return apiPost<SolicitudCambioEstado, PrevisualizacionCambioEstado>(`/proyectos/${id}/cambio-estado/previsualizar`, solicitud)
}

/** Aplica la suspensión o el cierre: 200 { id, estado, version }; 400 / 404 / 409 / 503. */
export function aplicarCambioEstado(id: number, solicitud: SolicitudCambioEstado) {
  return apiPost<SolicitudCambioEstado, CambioEstadoRealizado>(`/proyectos/${id}/cambio-estado`, solicitud)
}
