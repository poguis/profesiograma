import { useQuery } from '@tanstack/react-query'
import { apiGet } from '../api/clienteHttp'
import type { UsuarioActualDto } from '../api/tipos'

export const claveUsuarioActual = ['usuarios', 'me'] as const

/** Usuario autenticado según la API (GET /api/usuarios/me). 401 → error tipo "noAutenticado". */
export function useUsuarioActual() {
  return useQuery({
    queryKey: claveUsuarioActual,
    queryFn: ({ signal }) => apiGet<UsuarioActualDto>('/usuarios/me', { signal }),
  })
}
