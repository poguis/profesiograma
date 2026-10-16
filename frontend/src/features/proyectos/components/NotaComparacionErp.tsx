import { Button, MessageBar, MessageBarActions, MessageBarBody, Spinner } from '@fluentui/react-components'
import { notaGeneralErp } from '../comparacionErp'
import type { ComparacionErp } from '../tipos'

export interface NotaComparacionErpProps {
  comparacion: ComparacionErp | undefined
  error: unknown
  cargando: boolean
  onReintentar: () => void
}

/**
 * TAREA-26d-3: estado de la comparación "guardado frente al ERP" (una sola nota por pantalla, D4). Informativa: nunca
 * bloquea la vista previa ni el registro.
 */
export function NotaComparacionErp({ comparacion, error, cargando, onReintentar }: NotaComparacionErpProps) {
  if (cargando && !comparacion && !error) {
    return <Spinner size="tiny" labelPosition="after" label="Comparando con el ERP…" />
  }

  const nota = notaGeneralErp(comparacion, error)
  if (!nota) {
    return null
  }

  return (
    <MessageBar intent="info">
      <MessageBarBody>{nota.texto}</MessageBarBody>
      {nota.reintentable && (
        <MessageBarActions>
          <Button disabled={cargando} onClick={onReintentar}>
            Reintentar
          </Button>
        </MessageBarActions>
      )}
    </MessageBar>
  )
}
