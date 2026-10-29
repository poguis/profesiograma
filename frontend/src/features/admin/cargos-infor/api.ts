import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiPost, apiPut } from '../../../api/clienteHttp'
import type { PaginaResultado } from '../../../api/tipos'
import {
  type CargoEditado,
  type CargoInfor,
  type FiltrosCargos,
  type ResultadoImportacion,
  type SolicitudEditarCargo,
  type VistaPreviaImportacion,
  aConsultaApiCargos,
} from './cargosInfor'

export const clavesCargosInfor = {
  todos: ['admin', 'cargos-infor'] as const,
  listados: ['admin', 'cargos-infor', 'listado'] as const,
  listado: (f: FiltrosCargos) => ['admin', 'cargos-infor', 'listado', f] as const,
  vistaPrevia: ['admin', 'cargos-infor', 'importacion'] as const,
}

/** Prefijo de clavesProyectos.reporteResumen(id, consulta): el código Infor sale en el reporte (como novedades/invalidacion.ts). */
const CLAVE_REPORTE_RESUMEN = ['proyectos', 'reporte-resumen'] as const

/** Lista (la página solo se monta para un Admin, dentro de AdminRequerido). Sin reintentos automáticos. */
export function useCargosInfor(filtros: FiltrosCargos) {
  return useQuery({
    queryKey: clavesCargosInfor.listado(filtros),
    queryFn: ({ signal }) => apiGet<PaginaResultado<CargoInfor>>(`/admin/cargos-infor?${aConsultaApiCargos(filtros)}`, { signal }),
    retry: false,
    placeholderData: (anterior) => anterior,
  })
}

/**
 * Vista previa (P2): solo con el diálogo abierto, siempre fresca (staleTime 0, sin caché al cerrar) y sin reintentos; puede
 * tardar varios segundos si la caché del ERP venció.
 */
export function useVistaPreviaImportacion(habilitada: boolean) {
  return useQuery({
    queryKey: clavesCargosInfor.vistaPrevia,
    queryFn: ({ signal }) => apiGet<VistaPreviaImportacion>('/admin/cargos-infor/importacion', { signal }),
    enabled: habilitada,
    retry: false,
    staleTime: 0,
    gcTime: 0,
  })
}

function useInvalidarCargos() {
  const queryClient = useQueryClient()
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: clavesCargosInfor.listados }),
      // La vista previa queda vencida sin volver a pedirse: el diálogo abierto ya muestra el resultado (sin llamar al ERP).
      queryClient.invalidateQueries({ queryKey: clavesCargosInfor.vistaPrevia, refetchType: 'none' }),
      queryClient.invalidateQueries({ queryKey: CLAVE_REPORTE_RESUMEN }),
    ])
}

/** POST sin cuerpo (lectura fresca del ERP). Invalida listado, vista previa y el resumen del reporte. */
export function useImportarCargos() {
  const invalidar = useInvalidarCargos()
  return useMutation({
    mutationFn: () => apiPost<undefined, ResultadoImportacion>('/admin/cargos-infor/importacion', undefined),
    onSuccess: invalidar,
  })
}

/** PUT del código y el estado (compare-and-set con los anteriores). Mismas invalidaciones. */
export function useGuardarCargo(id: number) {
  const invalidar = useInvalidarCargos()
  return useMutation({
    mutationFn: (s: SolicitudEditarCargo) => apiPut<SolicitudEditarCargo, CargoEditado>(`/admin/cargos-infor/${id}`, s),
    onSuccess: invalidar,
  })
}
