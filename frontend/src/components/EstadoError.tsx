import { MessageBar, MessageBarBody, MessageBarTitle } from '@fluentui/react-components'
import { ErrorApi } from '../api/errores'

/** Muestra un error de consulta de forma legible (sin romper la pantalla). */
export function EstadoError({ error }: { error: unknown }) {
  const titulo = error instanceof ErrorApi ? error.titulo : 'Ocurrió un error inesperado.'
  const detalle = error instanceof ErrorApi ? error.detalle : undefined
  const estado = error instanceof ErrorApi && error.estado !== null ? ` (HTTP ${error.estado})` : ''
  const intencion = error instanceof ErrorApi && error.tipo === 'noAutenticado' ? 'warning' : 'error'

  return (
    <MessageBar intent={intencion}>
      <MessageBarBody>
        <MessageBarTitle>
          {titulo}
          {estado}
        </MessageBarTitle>
        {detalle}
      </MessageBarBody>
    </MessageBar>
  )
}
