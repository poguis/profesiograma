import { apiGet, apiPost } from '../../api/clienteHttp'
import type { PaginaResultado } from '../../api/tipos'
import { aConsultaApi } from './filtrosUrl'
import type {
  ActividadErp,
  CabeceraActualizada,
  CabeceraEdicion,
  CambioEstadoRealizado,
  Catalogos,
  CompaniaErp,
  DimensionErp,
  EdicionPersonal,
  EmpleadoBusqueda,
  FiltroEmpleados,
  FiltrosProyectos,
  HorarioErp,
  OpcionesFormularioProyecto,
  PersonalActualizado,
  Previsualizacion,
  PrevisualizacionCabecera,
  PrevisualizacionPersonal,
  PrevisualizacionCambioEstado,
  PrevisualizacionReactivacion,
  ProyectoCreado,
  ProyectoDetalle,
  ProyectoErp,
  ProyectoReactivado,
  ProyectoResumen,
  Reactivacion,
  SolicitudActualizarPersonal,
  SolicitudCambioEstado,
  SolicitudCrearProyecto,
  SolicitudEditarCabecera,
  SolicitudReactivar,
} from './tipos'

export const clavesProyectos = {
  todos: ['proyectos'] as const,
  listado: (filtros: FiltrosProyectos) => ['proyectos', 'listado', filtros] as const,
  detalle: (id: number) => ['proyectos', 'detalle', id] as const,
  /** Bajo 'proyectos': invalidar `todos` tras un cambio también recarga la cabecera. */
  cabecera: (id: number) => ['proyectos', 'cabecera', id] as const,
  /** GET …/edicion (TAREA-19b): bajo 'proyectos', se refresca al invalidar `todos`. */
  edicion: (id: number) => ['proyectos', 'edicion', id] as const,
  /** GET …/reactivacion (TAREA-19c): bajo 'proyectos', se refresca al invalidar `todos`. */
  reactivacion: (id: number) => ['proyectos', 'reactivacion', id] as const,
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

/** Datos del formulario "Editar datos generales": permisos, actividades y opciones de almuerzo. 404 si no es visible. */
export function obtenerCabecera(id: number, signal?: AbortSignal) {
  return apiGet<CabeceraEdicion>(`/proyectos/${id}/cabecera`, { signal })
}

/** Impacto de editar la cabecera (no guarda). 400 / 404 / 409 / 503. */
export function previsualizarCabecera(id: number, solicitud: SolicitudEditarCabecera) {
  return apiPost<SolicitudEditarCabecera, PrevisualizacionCabecera>(`/proyectos/${id}/cabecera/previsualizar`, solicitud)
}

/** Registra la edición: 200 { id, version }; 400 / 404 / 409 / 503. */
export function registrarCabecera(id: number, solicitud: SolicitudEditarCabecera) {
  return apiPost<SolicitudEditarCabecera, CabeceraActualizada>(`/proyectos/${id}/cabecera`, solicitud)
}

/** Datos de "Actualizar personal" (TAREA-17): corte, personal por clase con permisos, límites y token. 404 si no es visible. */
export function obtenerEdicionPersonal(id: number, signal?: AbortSignal) {
  return apiGet<EdicionPersonal>(`/proyectos/${id}/edicion`, { signal })
}

/** Vista previa de la actualización de personal (no guarda; 200 aunque haya cruces). 400 / 404 / 409 / 503. */
export function previsualizarPersonal(id: number, solicitud: SolicitudActualizarPersonal) {
  return apiPost<SolicitudActualizarPersonal, PrevisualizacionPersonal>(`/proyectos/${id}/personal/previsualizar`, solicitud)
}

/** Registra la actualización: 200 { id, version }; 400; 404; 409 (cruces con extensiones, o proyecto cambiado); 503. */
export function registrarPersonal(id: number, solicitud: SolicitudActualizarPersonal) {
  return apiPost<SolicitudActualizarPersonal, PersonalActualizado>(`/proyectos/${id}/personal`, solicitud)
}

// ------------------------------------------------------------------ Reactivación (TAREA-17b; pantalla TAREA-19c)

/** GET /api/proyectos/{id}/reactivacion: datos del formulario (puedeReactivar, propuesto, personal, token). */
export function obtenerReactivacion(id: number, signal?: AbortSignal) {
  return apiGet<Reactivacion>(`/proyectos/${id}/reactivacion`, { signal })
}

/** Vista previa de la reactivación (no guarda). 400 / 404 / 409 / 503. */
export function previsualizarReactivacion(id: number, solicitud: SolicitudReactivar) {
  return apiPost<SolicitudReactivar, PrevisualizacionReactivacion>(`/proyectos/${id}/reactivacion/previsualizar`, solicitud)
}

/** Registra la reactivación. 200 { id, estado, version }; 400; 404; 409 (cruces con extensiones, o proyecto cambiado); 503. */
export function registrarReactivacion(id: number, solicitud: SolicitudReactivar) {
  return apiPost<SolicitudReactivar, ProyectoReactivado>(`/proyectos/${id}/reactivacion`, solicitud)
}
