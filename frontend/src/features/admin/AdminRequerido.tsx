import { MessageBar, MessageBarBody, Spinner } from '@fluentui/react-components'
import type { ReactNode } from 'react'
import { esAdmin } from '../../auth/roles'
import { useUsuarioActual } from '../../auth/useUsuarioActual'

export const MENSAJE_SIN_PERMISO = 'No tienes permiso para ver esta página.'

/**
 * Páginas /admin/* (TAREA-25-2): un gestor ve «No tienes permiso…» y la página no se monta, así que no llama al backend
 * (la API exige la política Admin igual). Usa la misma consulta (y caché) de /api/usuarios/me que la barra superior.
 */
export function AdminRequerido({ children }: { children: ReactNode }) {
  const usuario = useUsuarioActual()
  if (usuario.isPending) {
    return <Spinner label="Cargando…" labelPosition="after" />
  }
  if (!esAdmin(usuario.data)) {
    return (
      <MessageBar intent="warning">
        <MessageBarBody>{MENSAJE_SIN_PERMISO}</MessageBarBody>
      </MessageBar>
    )
  }
  return <>{children}</>
}
