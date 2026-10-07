import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { useMemo } from 'react'
import {
  aplicarCambioEstado,
  buscarEmpleados,
  clavesEmpleados,
  clavesErp,
  clavesProyectos,
  crearProyecto,
  listarActividadesErp,
  listarCompaniasErp,
  listarDimensionesErp,
  listarHorariosErp,
  listarProyectos,
  listarProyectosErp,
  obtenerCabecera,
  obtenerEdicionPersonal,
  obtenerCatalogos,
  obtenerOpcionesFormulario,
  obtenerProyecto,
  obtenerReactivacion,
  previsualizarCabecera,
  previsualizarCambioEstado,
  previsualizarPersonal,
  previsualizarProyecto,
  previsualizarReactivacion,
  registrarCabecera,
  registrarPersonal,
  registrarReactivacion,
} from './api'
import type {
  Catalogos,
  FiltroEmpleados,
  FiltrosProyectos,
  SolicitudActualizarPersonal,
  SolicitudCambioEstado,
  SolicitudEditarCabecera,
  SolicitudReactivar,
} from './tipos'

/** Datos del ERP y del formulario: cambian poco; el servidor ya los cachea 5–10 min. */
const VIGENCIA_DATOS_FORMULARIO_MS = 5 * 60_000

/** Listado paginado. Mientras llega la página nueva se conserva la anterior (sin parpadeo). */
export function useProyectos(filtros: FiltrosProyectos) {
  return useQuery({
    queryKey: clavesProyectos.listado(filtros),
    queryFn: ({ signal }) => listarProyectos(filtros, signal),
    placeholderData: keepPreviousData,
  })
}

/** Detalle. No consulta si el id de la ruta no es un entero positivo. */
export function useProyecto(id: number) {
  return useQuery({
    queryKey: clavesProyectos.detalle(id),
    queryFn: ({ signal }) => obtenerProyecto(id, signal),
    enabled: Number.isInteger(id) && id > 0,
  })
}

/** Catálogos (cambian poco): se cachean 10 minutos. */
export function useCatalogos() {
  return useQuery({
    queryKey: clavesProyectos.catalogos,
    queryFn: ({ signal }) => obtenerCatalogos(signal),
    staleTime: 10 * 60_000,
  })
}

/** Código → nombre de estados, grupos y tipos de movimiento (si faltan, se muestra el código). */
export function useNombresCatalogo() {
  const { data } = useCatalogos()
  return useMemo(() => crearNombres(data), [data])
}

function crearNombres(catalogos: Catalogos | undefined) {
  const mapa = (items: { codigo: string; nombre: string }[] | undefined) =>
    new Map((items ?? []).map((i) => [i.codigo, i.nombre]))
  const estados = mapa(catalogos?.estadosProyecto)
  const grupos = mapa(catalogos?.gruposProyecto)
  const movimientos = mapa(catalogos?.tiposMovimiento)

  return {
    estado: (codigo: string) => estados.get(codigo) ?? codigo,
    grupo: (codigo: string) => grupos.get(codigo) ?? codigo,
    movimiento: (codigo: string) => movimientos.get(codigo) ?? codigo,
  }
}

// ------------------------------------------------------------------ Nuevo proyecto

/** Departamentos del usuario, opciones de almuerzo y límites (GET /api/proyectos/opciones-formulario). */
export function useOpcionesFormulario() {
  return useQuery({
    queryKey: clavesProyectos.opcionesFormulario,
    queryFn: ({ signal }) => obtenerOpcionesFormulario(signal),
    staleTime: VIGENCIA_DATOS_FORMULARIO_MS,
  })
}

export function useCompaniasErp() {
  return useQuery({
    queryKey: clavesErp.companias,
    queryFn: ({ signal }) => listarCompaniasErp(signal),
    staleTime: VIGENCIA_DATOS_FORMULARIO_MS,
  })
}

export function useProyectosErp(companiaId: number | null) {
  return useQuery({
    queryKey: clavesErp.proyectos(companiaId ?? 0),
    queryFn: ({ signal }) => listarProyectosErp(companiaId!, signal),
    enabled: companiaId !== null,
    staleTime: VIGENCIA_DATOS_FORMULARIO_MS,
  })
}

export function useDimensionesErp(companiaId: number | null) {
  return useQuery({
    queryKey: clavesErp.dimensiones(companiaId ?? 0),
    queryFn: ({ signal }) => listarDimensionesErp(companiaId!, signal),
    enabled: companiaId !== null,
    staleTime: VIGENCIA_DATOS_FORMULARIO_MS,
  })
}

export function useActividadesErp(companiaId: number | null, proyectoErpId: string | null) {
  return useQuery({
    queryKey: clavesErp.actividades(companiaId ?? 0, proyectoErpId ?? ''),
    queryFn: ({ signal }) => listarActividadesErp(companiaId!, proyectoErpId!, signal),
    enabled: companiaId !== null && proyectoErpId !== null,
    staleTime: VIGENCIA_DATOS_FORMULARIO_MS,
  })
}

export function useHorariosErp() {
  return useQuery({
    queryKey: clavesErp.horarios,
    queryFn: ({ signal }) => listarHorariosErp(signal),
    staleTime: VIGENCIA_DATOS_FORMULARIO_MS,
  })
}

/** Búsqueda paginada de empleados. Mientras llega la página nueva se conserva la anterior. */
export function useBusquedaEmpleados(filtro: FiltroEmpleados, habilitada: boolean) {
  return useQuery({
    queryKey: clavesEmpleados.busqueda(filtro),
    queryFn: ({ signal }) => buscarEmpleados(filtro, signal),
    enabled: habilitada,
    placeholderData: keepPreviousData,
  })
}

/** POST /api/proyectos/previsualizar (no guarda; sin reintentos). */
export function usePrevisualizarProyecto() {
  return useMutation({ mutationFn: previsualizarProyecto })
}

/** POST /api/proyectos. Al crear, invalida los listados y detalles de proyectos. */
export function useCrearProyecto() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: crearProyecto,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: clavesProyectos.todos }),
  })
}

// ------------------------------------------------------------------ Cambio de estado

/** POST /api/proyectos/{id}/cambio-estado/previsualizar (no guarda; sin reintentos). */
export function usePrevisualizarCambioEstado(id: number) {
  return useMutation({ mutationFn: (solicitud: SolicitudCambioEstado) => previsualizarCambioEstado(id, solicitud) })
}

/** POST /api/proyectos/{id}/cambio-estado. Al aplicar, invalida el detalle y los listados de proyectos. */
export function useAplicarCambioEstado(id: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (solicitud: SolicitudCambioEstado) => aplicarCambioEstado(id, solicitud),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: clavesProyectos.todos }),
  })
}

// ------------------------------------------------------------------ Edición de cabecera (TAREA-19a)

/** GET /api/proyectos/{id}/cabecera: se consulta con el detalle para decidir el botón "Editar datos generales" (P3). */
export function useCabecera(id: number) {
  return useQuery({
    queryKey: clavesProyectos.cabecera(id),
    queryFn: ({ signal }) => obtenerCabecera(id, signal),
    enabled: Number.isInteger(id) && id > 0,
  })
}

/** POST /api/proyectos/{id}/cabecera/previsualizar (no guarda; sin reintentos). */
export function usePrevisualizarCabecera(id: number) {
  return useMutation({ mutationFn: (solicitud: SolicitudEditarCabecera) => previsualizarCabecera(id, solicitud) })
}

/** POST /api/proyectos/{id}/cabecera. Al registrar, invalida detalle, cabecera y listados de proyectos. */
export function useRegistrarCabecera(id: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (solicitud: SolicitudEditarCabecera) => registrarCabecera(id, solicitud),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: clavesProyectos.todos }),
  })
}

// ------------------------------------------------------------------ Actualización de personal (TAREA-19b)

/** GET /api/proyectos/{id}/edicion: decide "Actualizar personal" en el detalle y alimenta la pantalla. */
export function useEdicionPersonal(id: number) {
  return useQuery({
    queryKey: clavesProyectos.edicion(id),
    queryFn: ({ signal }) => obtenerEdicionPersonal(id, signal),
    enabled: Number.isInteger(id) && id > 0,
  })
}

/** POST /api/proyectos/{id}/personal/previsualizar (no guarda; sin reintentos). */
export function usePrevisualizarPersonal(id: number) {
  return useMutation({ mutationFn: (solicitud: SolicitudActualizarPersonal) => previsualizarPersonal(id, solicitud) })
}

/** POST /api/proyectos/{id}/personal. Al registrar, invalida detalle, edición y listados de proyectos. */
export function useRegistrarPersonal(id: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (solicitud: SolicitudActualizarPersonal) => registrarPersonal(id, solicitud),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: clavesProyectos.todos }),
  })
}

// ------------------------------------------------------------------ Reactivación (TAREA-19c)

/** GET /api/proyectos/{id}/reactivacion: alimenta la pantalla "Reactivar proyecto". */
export function useReactivacion(id: number) {
  return useQuery({
    queryKey: clavesProyectos.reactivacion(id),
    queryFn: ({ signal }) => obtenerReactivacion(id, signal),
    enabled: Number.isInteger(id) && id > 0,
  })
}

/** POST /api/proyectos/{id}/reactivacion/previsualizar (no guarda; sin reintentos). */
export function usePrevisualizarReactivacion(id: number) {
  return useMutation({ mutationFn: (solicitud: SolicitudReactivar) => previsualizarReactivacion(id, solicitud) })
}

/** POST /api/proyectos/{id}/reactivacion. Al registrar, invalida detalle, listado, cabecera, edición y reactivación. */
export function useRegistrarReactivacion(id: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (solicitud: SolicitudReactivar) => registrarReactivacion(id, solicitud),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: clavesProyectos.todos }),
  })
}
