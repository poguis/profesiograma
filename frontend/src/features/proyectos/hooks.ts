import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useMemo } from 'react'
import {
  buscarEmpleados,
  clavesEmpleados,
  clavesErp,
  clavesProyectos,
  listarActividadesErp,
  listarCompaniasErp,
  listarDimensionesErp,
  listarHorariosErp,
  listarProyectos,
  listarProyectosErp,
  obtenerCatalogos,
  obtenerOpcionesFormulario,
  obtenerProyecto,
} from './api'
import type { Catalogos, FiltroEmpleados, FiltrosProyectos } from './tipos'

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
