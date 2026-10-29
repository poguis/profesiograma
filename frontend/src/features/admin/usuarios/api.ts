import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { apiGet, apiPost, apiPut } from '../../../api/clienteHttp'
import type { PaginaResultado } from '../../../api/tipos'
import { claveUsuarioActual } from '../../../auth/useUsuarioActual'
import {
  type DepartamentoUsuario,
  type FiltrosUsuarios,
  type SolicitudCrearUsuario,
  type SolicitudDepartamentos,
  type SolicitudEditarUsuario,
  type UsuarioAdmin,
  type UsuarioAdminDetalle,
  aConsultaApiUsuarios,
} from './usuariosAdmin'

export const clavesUsuariosAdmin = {
  todos: ['admin', 'usuarios'] as const,
  listado: (f: FiltrosUsuarios) => ['admin', 'usuarios', 'listado', f] as const,
  detalle: (id: number) => ['admin', 'usuarios', 'detalle', id] as const,
}

/** Afectados por los departamentos del usuario: el buscador de empleados y las opciones de «Nuevo proyecto» (P5). */
const CLAVES_DEPARTAMENTOS = [['empleados'], ['proyectos', 'opciones-formulario']] as const

/** Lista (solo Admin: `habilitada` = esAdmin, así un gestor no llama al backend). Sin reintentos automáticos. */
export function useUsuariosAdmin(filtros: FiltrosUsuarios, habilitada: boolean) {
  return useQuery({
    queryKey: clavesUsuariosAdmin.listado(filtros),
    queryFn: ({ signal }) => apiGet<PaginaResultado<UsuarioAdmin>>(`/admin/usuarios?${aConsultaApiUsuarios(filtros)}`, { signal }),
    enabled: habilitada,
    retry: false,
    placeholderData: (anterior) => anterior,
  })
}

/** Detalle. Sin refresco en segundo plano: cada sección edita sobre el GET que la abrió (sus valores anteriores). */
export function useUsuarioAdmin(id: number, habilitada: boolean) {
  return useQuery({
    queryKey: clavesUsuariosAdmin.detalle(id),
    queryFn: ({ signal }) => apiGet<UsuarioAdminDetalle>(`/admin/usuarios/${id}`, { signal }),
    enabled: habilitada,
    retry: false,
    staleTime: Infinity,
  })
}

export function useCrearUsuario() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (s: SolicitudCrearUsuario) => apiPost<SolicitudCrearUsuario, { id: number }>('/admin/usuarios', s),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: clavesUsuariosAdmin.todos }),
  })
}

/** PUT de «Datos». Invalida lista y detalle; y /api/usuarios/me si el editado es el usuario actual (su nombre). */
export function useGuardarDatosUsuario(id: number, esUsuarioActual: boolean) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (s: SolicitudEditarUsuario) => apiPut<SolicitudEditarUsuario, UsuarioAdmin>(`/admin/usuarios/${id}`, s),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: clavesUsuariosAdmin.todos }),
        esUsuarioActual ? queryClient.invalidateQueries({ queryKey: claveUsuarioActual }) : undefined,
      ]),
  })
}

/** PUT de «Departamentos». Invalida lista, detalle, el buscador y las opciones de proyecto (efecto inmediato). */
export function useGuardarDepartamentos(id: number) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (s: SolicitudDepartamentos) =>
      apiPut<SolicitudDepartamentos, { departamentos: DepartamentoUsuario[] }>(`/admin/usuarios/${id}/departamentos`, s),
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: clavesUsuariosAdmin.todos }),
        ...CLAVES_DEPARTAMENTOS.map((queryKey) => queryClient.invalidateQueries({ queryKey })),
      ]),
  })
}
