import { keepPreviousData, useMutation, useQueries, useQuery, useQueryClient } from '@tanstack/react-query'
import { clavesProyectos, listarProyectos } from '../proyectos/api'
import { useCatalogos } from '../proyectos/hooks'
import { clavesNovedades, crearNovedad, listarNovedades, obtenerNovedad, obtenerOpcionesNovedad } from './api'
import { invalidarTrasNovedad } from './invalidacion'
import type { FiltrosNovedades } from './tipos'

/**
 * GET /api/novedades. Sin reintentos automáticos (H9, como el cronograma): con la base caída cada intento espera la
 * conexión y luego 503; el usuario reintenta con el botón. Mientras llega la página nueva se conserva la anterior.
 */
export function useNovedades(filtros: FiltrosNovedades) {
  return useQuery({
    queryKey: clavesNovedades.listado(filtros),
    queryFn: ({ signal }) => listarNovedades(filtros, signal),
    placeholderData: keepPreviousData,
    retry: false,
  })
}

/** Detalle. No consulta si el id de la ruta no es un entero positivo; sin reintentos automáticos (H9). */
export function useNovedad(id: number) {
  return useQuery({
    queryKey: clavesNovedades.detalle(id),
    queryFn: ({ signal }) => obtenerNovedad(id, signal),
    enabled: Number.isInteger(id) && id > 0,
    retry: false,
  })
}

/** GET /api/novedades/opciones-formulario (TAREA-24b): tipos por aplicación, GENERAL y máximo de días. */
export function useOpcionesNovedad() {
  return useQuery({
    queryKey: clavesNovedades.opciones,
    queryFn: ({ signal }) => obtenerOpcionesNovedad(signal),
    retry: false,
  })
}

/** Proyectos que trae cada consulta del selector; con esta cantidad se pide afinar la búsqueda (U6). */
export const TAMANO_PROYECTOS_VIGENTES = 20

/**
 * Proyectos vigentes del usuario para una novedad PROYECTO (U6): una consulta GET /api/proyectos por cada estado con
 * `esVigente` del catálogo (ACTIVO y SUSPENDIDO; el backend acepta los dos, N5), unidas y ordenadas por código.
 * La visibilidad (R1) la decide la API.
 */
export function useProyectosVigentes(texto: string, habilitada: boolean) {
  const catalogos = useCatalogos()
  const estados = (catalogos.data?.estadosProyecto ?? []).filter((e) => e.esVigente).map((e) => e.codigo)
  const consultas = useQueries({
    queries: estados.map((estado) => {
      const filtros = { estado, texto: texto || undefined, pagina: 1, tamano: TAMANO_PROYECTOS_VIGENTES }
      return {
        queryKey: clavesProyectos.listado(filtros),
        queryFn: ({ signal }: { signal: AbortSignal }) => listarProyectos(filtros, signal),
        enabled: habilitada,
        placeholderData: keepPreviousData,
        retry: false,
      }
    }),
  })

  const proyectos = consultas
    .flatMap((c) => c.data?.items ?? [])
    .sort((a, b) => a.codigo.localeCompare(b.codigo))
  return {
    proyectos,
    cargando: catalogos.isPending || consultas.some((c) => c.isFetching),
    error: catalogos.error ?? consultas.find((c) => c.error)?.error ?? null,
    /** Alguna consulta llegó al tope (20 coincidencias en un estado): se pide afinar la búsqueda. */
    hayMas: consultas.some((c) => (c.data?.total ?? 0) >= TAMANO_PROYECTOS_VIGENTES),
    reintentar: () => {
      void catalogos.refetch()
      for (const c of consultas) {
        void c.refetch()
      }
    },
  }
}

/** POST /api/novedades. Sin reintentos (queryClient); al crear invalida novedades, cronogramas y reporte (N15). */
export function useCrearNovedad() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: crearNovedad,
    onSuccess: () => invalidarTrasNovedad(queryClient),
  })
}
