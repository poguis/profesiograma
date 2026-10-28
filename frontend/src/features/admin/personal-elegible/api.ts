import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiPut } from '../../../api/clienteHttp'
import type { CriterioPersonal, SolicitudGuardarCriterio } from './criterioPersonal'

export const clavesCriterioPersonal = {
  criterio: ['admin', 'criterio-personal'] as const,
}

/** Prefijo de clavesEmpleados.busqueda (proyectos/api.ts): todas las búsquedas del buscador de empleados. */
const CLAVE_EMPLEADOS = ['empleados'] as const

export function obtenerCriterio(signal?: AbortSignal) {
  return apiGet<CriterioPersonal>('/admin/empleados/familias', { signal })
}

export function guardarCriterio(solicitud: SolicitudGuardarCriterio) {
  return apiPut<SolicitudGuardarCriterio, { valorActual: string }>('/admin/empleados/familias-asignables', solicitud)
}

/**
 * GET del criterio (solo Admin: `habilitada` = esAdmin, así un gestor no llama al backend). Sin reintentos automáticos
 * (como el listado de novedades) y sin refresco en segundo plano: el formulario edita sobre el GET que lo abrió.
 */
export function useCriterioPersonal(habilitada: boolean) {
  return useQuery({
    queryKey: clavesCriterioPersonal.criterio,
    queryFn: ({ signal }) => obtenerCriterio(signal),
    enabled: habilitada,
    retry: false,
    staleTime: Infinity,
  })
}

/** PUT del criterio. Sin reintentos; al guardar invalida el criterio y todas las búsquedas de empleados (efecto inmediato). */
export function useGuardarCriterioPersonal() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: guardarCriterio,
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: clavesCriterioPersonal.criterio }),
        queryClient.invalidateQueries({ queryKey: CLAVE_EMPLEADOS }),
      ]),
  })
}
