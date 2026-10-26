import {
  Button,
  Checkbox,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Field,
  MessageBar,
  MessageBarActions,
  MessageBarBody,
  Spinner,
  Text,
  Textarea,
  makeStyles,
  tokens,
} from '@fluentui/react-components'
import { useRef, useState } from 'react'
import { MOTIVO_MAX, validarMotivo } from '../edicionNovedad'
import { CAMPOS_ANULACION, type ErroresNovedad, interpretarErrorDesconocido } from '../erroresNovedad'
import { useAnularNovedad } from '../hooks'
import type { NovedadDetalle } from '../tipos'

const useEstilos = makeStyles({
  contenido: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
  superficie: { width: 'min(560px, 100vw)', maxWidth: '100vw' },
})

export interface DialogoAnularNovedadProps {
  abierto: boolean
  /** La novedad del detalle abierto: su `version` es la que se envía. */
  novedad: Pick<NovedadDetalle, 'id' | 'codigo' | 'version'>
  onCerrar: () => void
  onAnulada: (mensaje: string) => void
  /** «Recargar» (409 o 400 `version`): recarga el detalle; el diálogo se cierra. */
  onRecargar: () => void
}

/** Anulación (N10, TAREA-24c): motivo 5–500, irreversible, con la casilla «Entiendo» y protección contra doble clic. */
export function DialogoAnularNovedad({ abierto, novedad, onCerrar, onAnulada, onRecargar }: DialogoAnularNovedadProps) {
  const estilos = useEstilos()
  const anular = useAnularNovedad(novedad.id)
  const [motivo, setMotivo] = useState('')
  const [entiendo, setEntiendo] = useState(false)
  const [intentado, setIntentado] = useState(false)
  const [errorEnvio, setErrorEnvio] = useState<ErroresNovedad<'motivo'> | null>(null)
  const enviandoRef = useRef(false)

  const errorMotivo = errorEnvio?.campos.motivo ?? (intentado || motivo.trim().length > MOTIVO_MAX ? validarMotivo(motivo) : undefined)

  const cerrar = () => {
    if (anular.isPending) {
      return
    }
    setMotivo('')
    setEntiendo(false)
    setIntentado(false)
    setErrorEnvio(null)
    onCerrar()
  }

  const confirmar = () => {
    setIntentado(true)
    if (enviandoRef.current || anular.isPending || !entiendo || validarMotivo(motivo)) {
      return
    }
    enviandoRef.current = true
    setErrorEnvio(null)
    anular.mutate(
      { version: novedad.version, motivo: motivo.trim() },
      {
        onSuccess: () => {
          setMotivo('')
          setEntiendo(false)
          setIntentado(false)
          onAnulada(`Novedad ${novedad.codigo} anulada.`)
        },
        onError: (error) => setErrorEnvio(interpretarErrorDesconocido(error, CAMPOS_ANULACION)),
        onSettled: () => {
          enviandoRef.current = false
        },
      },
    )
  }

  return (
    <Dialog open={abierto} onOpenChange={(_, d) => !d.open && cerrar()}>
      <DialogSurface className={estilos.superficie}>
        <DialogBody>
          <DialogTitle>Anular {novedad.codigo}</DialogTitle>
          <DialogContent className={estilos.contenido}>
            {errorEnvio && errorEnvio.generales.length > 0 && (
              <MessageBar intent="error">
                <MessageBarBody>
                  {errorEnvio.generales.map((mensaje) => (
                    <div key={mensaje}>{mensaje}</div>
                  ))}
                </MessageBarBody>
                {(errorEnvio.ofrecerRecarga || errorEnvio.ofrecerReintento) && (
                  <MessageBarActions>
                    {errorEnvio.ofrecerRecarga && (
                      <Button
                        onClick={() => {
                          cerrar()
                          onRecargar()
                        }}
                      >
                        Recargar
                      </Button>
                    )}
                    {errorEnvio.ofrecerReintento && (
                      <Button disabled={anular.isPending} onClick={confirmar}>
                        Reintentar
                      </Button>
                    )}
                  </MessageBarActions>
                )}
              </MessageBar>
            )}
            <Text weight="semibold">Esta acción no se puede deshacer.</Text>
            <Field label="Motivo" required validationMessage={errorMotivo} hint={`${motivo.trim().length}/${MOTIVO_MAX}`}>
              <Textarea
                resize="vertical"
                value={motivo}
                disabled={anular.isPending}
                onChange={(_, d) => {
                  setMotivo(d.value)
                  setErrorEnvio((actual) => (actual ? { ...actual, campos: {} } : actual))
                }}
              />
            </Field>
            <Checkbox
              label="Entiendo que la anulación es irreversible."
              checked={entiendo}
              disabled={anular.isPending}
              onChange={(_, d) => setEntiendo(Boolean(d.checked))}
            />
          </DialogContent>
          <DialogActions>
            <Button disabled={anular.isPending} onClick={cerrar}>
              Cancelar
            </Button>
            <Button
              appearance="primary"
              icon={anular.isPending ? <Spinner size="tiny" /> : undefined}
              disabled={anular.isPending || !entiendo}
              onClick={confirmar}
            >
              {anular.isPending ? 'Anulando…' : 'Anular'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  )
}
