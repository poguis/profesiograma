import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  MessageBar,
  MessageBarActions,
  MessageBarBody,
  makeStyles,
} from '@fluentui/react-components'
import { useState } from 'react'
import type { ErroresDialogo } from '../proyectos/erroresDialogo'

const MOVIL = '@media (max-width: 599px)'

const useEstilos = makeStyles({
  superficie: { [MOVIL]: { width: '100%', maxWidth: '100%' } },
  accionesDialogo: { [MOVIL]: { flexDirection: 'column', '& > button': { width: '100%' } } },
})

/**
 * Errores generales de una sección de administración (extraído de PaginaUsuario en la TAREA-25-3-2, sin cambios): 409 con
 * «Recargar» (con confirmación si hay cambios), 503 y red con «Reintentar».
 */
export function BarraErrores({
  errores,
  cambios,
  pendiente,
  onRecargar,
  onReintentar,
}: {
  errores: ErroresDialogo<string> | null
  cambios: boolean
  pendiente: boolean
  onRecargar: () => Promise<void>
  onReintentar: () => void
}) {
  const estilos = useEstilos()
  const [confirmar, setConfirmar] = useState(false)
  if (!errores || errores.generales.length === 0) {
    return null
  }
  return (
    <MessageBar intent="error">
      <MessageBarBody>
        {errores.generales.map((m) => (
          <div key={m}>{m}</div>
        ))}
      </MessageBarBody>
      {(errores.ofrecerRecarga || errores.ofrecerReintento) && (
        <MessageBarActions>
          {errores.ofrecerRecarga && <Button onClick={() => (cambios ? setConfirmar(true) : void onRecargar())}>Recargar</Button>}
          {errores.ofrecerReintento && (
            <Button disabled={pendiente} onClick={onReintentar}>
              Reintentar
            </Button>
          )}
        </MessageBarActions>
      )}
      <Dialog open={confirmar} onOpenChange={(_, d) => !d.open && setConfirmar(false)}>
        <DialogSurface className={estilos.superficie}>
          <DialogBody>
            <DialogTitle>¿Recargar?</DialogTitle>
            <DialogContent>Se perderán los cambios que no guardó en esta sección.</DialogContent>
            <DialogActions className={estilos.accionesDialogo}>
              <Button onClick={() => setConfirmar(false)}>Seguir editando</Button>
              <Button
                appearance="primary"
                onClick={() => {
                  setConfirmar(false)
                  void onRecargar()
                }}
              >
                Recargar
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </MessageBar>
  )
}
