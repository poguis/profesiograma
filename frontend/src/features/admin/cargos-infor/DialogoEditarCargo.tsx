import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Field,
  Input,
  Spinner,
  Switch,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components'
import { Save20Regular } from '@fluentui/react-icons'
import { useRef, useState } from 'react'
import type { ErroresDialogo } from '../../proyectos/erroresDialogo'
import { BarraErrores } from '../BarraErrores'
import { useGuardarCargo } from './api'
import {
  type CampoCargo,
  type CargoInfor,
  TEXTO_CODIGO_INFOR,
  TEXTO_CONFIRMAR_BAJA,
  aSolicitudCargo,
  esBajaCargo,
  estadoEdicionCargo,
  hayCambiosCargo,
  interpretarErrorCargo,
  motivoSinGuardarCargo,
  normalizarCodigo,
  resumenCargo,
  validarCodigo,
} from './cargosInfor'

const MOVIL = '@media (max-width: 599px)'

const useEstilos = makeStyles({
  superficie: { [MOVIL]: { width: '100%', maxWidth: '100%' } },
  contenido: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
  secundario: { color: tokens.colorNeutralForeground3 },
  acciones: { [MOVIL]: { flexDirection: 'column', '& > button': { width: '100%' } } },
})

interface Props {
  /** Fila del listado que abrió el diálogo: sus valores son los anteriores del PUT. */
  cargo: CargoInfor
  onCerrar: () => void
  onGuardado: () => void
  /** 409 o 404: cierra el diálogo y vuelve a leer la lista. */
  onRecargar: () => Promise<void>
}

/** Edición del código Infor y del estado de un cargo (TAREA-25-3-2). Se monta solo mientras está abierto. */
export function DialogoEditarCargo({ cargo, onCerrar, onGuardado, onRecargar }: Props) {
  const estilos = useEstilos()
  const guardar = useGuardarCargo(cargo.id)
  const [estado, setEstado] = useState(() => estadoEdicionCargo(cargo))
  const [errores, setErrores] = useState<ErroresDialogo<CampoCargo> | null>(null)
  const [confirmarBaja, setConfirmarBaja] = useState(false)
  const [confirmarCierre, setConfirmarCierre] = useState(false)
  const enviandoRef = useRef(false)
  const cambios = hayCambiosCargo(estado)
  const motivo = motivoSinGuardarCargo(estado)
  const codigo = normalizarCodigo(estado.codigo)
  const errorCodigo = validarCodigo(codigo) ?? errores?.campos.codigoInfor

  const enviar = () => {
    // El ref evita un doble envío aunque el segundo clic llegue antes de que se deshabilite el botón.
    if (enviandoRef.current || guardar.isPending || motivoSinGuardarCargo(estado)) {
      return
    }
    enviandoRef.current = true
    setErrores(null)
    guardar.mutate(aSolicitudCargo(estado), {
      onSuccess: onGuardado,
      onError: (error) => setErrores(interpretarErrorCargo(error)),
      onSettled: () => {
        enviandoRef.current = false
      },
    })
  }

  const cerrar = () => {
    if (guardar.isPending) {
      return
    }
    if (cambios) {
      setConfirmarCierre(true)
    } else {
      onCerrar()
    }
  }

  return (
    <Dialog open onOpenChange={(_, d) => !d.open && cerrar()}>
      <DialogSurface className={estilos.superficie}>
        <form
          onSubmit={(evento) => {
            evento.preventDefault()
            if (esBajaCargo(estado)) {
              setConfirmarBaja(true)
            } else {
              enviar()
            }
          }}
        >
          <DialogBody>
            <DialogTitle>Editar cargo</DialogTitle>
            <DialogContent className={estilos.contenido}>
              <div>
                <Text weight="semibold" block>
                  {cargo.cargo}
                </Text>
                <Text className={estilos.secundario} block>
                  Personas guardadas: {cargo.personasGuardadas} · Activos en el ERP: {cargo.activosErp ?? '—'}
                </Text>
              </div>
              <Text className={estilos.secundario}>{TEXTO_CODIGO_INFOR}</Text>
              <BarraErrores errores={errores} cambios={cambios} pendiente={guardar.isPending} onRecargar={onRecargar} onReintentar={enviar} />
              <Field
                label="Código Infor"
                hint={codigo === null ? 'Vacío = sin código.' : `Se guardará como: ${codigo}`}
                validationMessage={errorCodigo}
              >
                <Input
                  value={estado.codigo}
                  disabled={guardar.isPending}
                  onChange={(_, d) => {
                    setEstado({ ...estado, codigo: d.value })
                    setErrores(null)
                  }}
                />
              </Field>
              <Field validationMessage={errores?.campos.activo}>
                <Switch
                  label="Activo"
                  checked={estado.activo}
                  disabled={guardar.isPending}
                  onChange={(_, d) => {
                    setEstado({ ...estado, activo: d.checked })
                    setErrores(null)
                  }}
                />
              </Field>
              {motivo ? (
                !guardar.isPending && !errorCodigo && <Text className={estilos.secundario}>{motivo}</Text>
              ) : (
                <Text weight="semibold">{resumenCargo(estado)}</Text>
              )}
            </DialogContent>
            <DialogActions className={estilos.acciones}>
              <Button disabled={guardar.isPending} onClick={cerrar}>
                Cancelar
              </Button>
              <Button
                type="submit"
                appearance="primary"
                disabled={guardar.isPending || motivo !== undefined}
                icon={guardar.isPending ? <Spinner size="tiny" /> : <Save20Regular />}
              >
                {guardar.isPending ? 'Guardando…' : 'Guardar'}
              </Button>
            </DialogActions>
          </DialogBody>
        </form>

        <Dialog open={confirmarBaja} onOpenChange={(_, d) => !d.open && setConfirmarBaja(false)}>
          <DialogSurface className={estilos.superficie}>
            <DialogBody>
              <DialogTitle>¿Dar de baja el cargo?</DialogTitle>
              <DialogContent>{TEXTO_CONFIRMAR_BAJA}</DialogContent>
              <DialogActions className={estilos.acciones}>
                <Button onClick={() => setConfirmarBaja(false)}>Cancelar</Button>
                <Button
                  appearance="primary"
                  onClick={() => {
                    setConfirmarBaja(false)
                    enviar()
                  }}
                >
                  Dar de baja
                </Button>
              </DialogActions>
            </DialogBody>
          </DialogSurface>
        </Dialog>

        <Dialog open={confirmarCierre} onOpenChange={(_, d) => !d.open && setConfirmarCierre(false)}>
          <DialogSurface className={estilos.superficie}>
            <DialogBody>
              <DialogTitle>¿Cerrar sin guardar?</DialogTitle>
              <DialogContent>Los cambios del cargo no se han guardado. Si cierra ahora, se perderán.</DialogContent>
              <DialogActions className={estilos.acciones}>
                <Button onClick={() => setConfirmarCierre(false)}>Seguir editando</Button>
                <Button appearance="primary" onClick={onCerrar}>
                  Cerrar sin guardar
                </Button>
              </DialogActions>
            </DialogBody>
          </DialogSurface>
        </Dialog>
      </DialogSurface>
    </Dialog>
  )
}
