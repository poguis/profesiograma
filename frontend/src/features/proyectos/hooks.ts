import { keepPreviousData, useQuery } from '@tanstack/react-query'
import { useMemo } from 'react'
import { clavesProyectos, listarProyectos, obtenerCatalogos, obtenerProyecto } from './api'
import type { Catalogos, FiltrosProyectos } from './tipos'

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
