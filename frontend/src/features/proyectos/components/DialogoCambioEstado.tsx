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
  MessageBarTitle,
  Radio,
  RadioGroup,
  Spinner,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components'
import { useReducer, useRef, useState } from 'react'
import { Link } from 'react-router'
import { ErrorApi } from '../../../api/errores'
import { SelectorFecha } from '../../../components/SelectorFecha'
import { formatearFecha } from '../../../utils/formato'
import {
  type AccionDialogo,
  type ErroresCambio,
  SIN_ERRORES_CAMBIO,
  type VistaCambio,
  aSolicitudCambio,
  aSolicitudRegistroCambio,
  crearEstadoDialogo,
  interpretarErrorCambio,
  opcionesDestino,
  puedeConfirmar,
  reducerDialogo,
  vistaCambioVigente,
} from '../cambioEstado'
import { MENSAJE_CAMBIO_POR_OTRO, cambioPorOtro } from '../tokenConcurrencia'
import { useAplicarCambioEstado, usePrevisualizarCambioEstado } from '../hooks'
import type { CambioEstadoRealizado, DestinoCambioEstado } from '../tipos'
import { ImpactoCambioEstado } from './ImpactoCambioEstado'

const useEstilos = makeStyles({
  superficie: { width: 'min(640px, 100vw)', maxWidth: '100vw', maxHeight: '100dvh' },
  contenido: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM, overflowY: 'auto' },
  ayuda: { color: tokens.colorNeutralForeground3 },
  desactualizada: { opacity: 0.5 },
  acciones: { flexWrap: 'wrap' },
})

export interface ProyectoCambio {
  id: number
  /** Código del estado actual (ACTIVO | SUSPENDIDO). */
  estado: string
  fechaInicio: string
  fechaFin: string
  /** Token BASE (TAREA-19b2): versión de la última etapa del detalle; el diálogo lo fija al abrirse. */
  versionBase: number
}

export interface DialogoCambioEstadoProps {
  proyecto: ProyectoCambio
  /** Ruta del listado (enlace del 404). */
  rutaListado: string
  onCerrar: () => void
  onRealizado: (resultado: CambioEstadoRealizado) => void
  /**
   * Recarga el detalle (400/409 o cambio por otro) y devuelve la versión de su última etapa (nuevo token base). Si el
   * estado nuevo ya no admite cambios, el padre cierra el diálogo.
   */
  onRecargar: () => Promise<number | undefined>
}

type Operacion = 'impacto' | 'confirmar'

/** R2–R5: diálogo "Cambiar estado". Se monta al abrirse, así que cada apertura empieza vacía. */
export function DialogoCambioEstado({ proyecto, rutaListado, onCerrar, onRealizado, onRecargar }: DialogoCambioEstadoProps) {
  const estilos = useEstilos()
  const [estado, despachar] = useReducer(reducerDialogo, undefined, crearEstadoDialogo)
  const [vista, setVista] = useState<VistaCambio | null>(null)
  const [errores, setErrores] = useState<ErroresCambio>(SIN_ERRORES_CAMBIO)
  const [ultimaOperacion, setUltimaOperacion] = useState<Operacion>('impacto')
  const [recargando, setRecargando] = useState(false)
  const [errorFormatoFecha, setErrorFormatoFecha] = useState<string>()
  const previsualizar = usePrevisualizarCambioEstado(proyecto.id)
  const aplicar = useAplicarCambioEstado(proyecto.id)
  const enviandoRef = useRef(false)
  // TAREA-19b2: token base fijado al abrir; solo cambia con "Recargar datos del proyecto".
  const [versionBase, setVersionBase] = useState(proyecto.versionBase)

  const enviando = previsualizar.isPending || aplicar.isPending || recargando
  const opciones = opcionesDestino(proyecto.estado)
  const vigente = vistaCambioVigente(vista, estado.revision)
  const habilitadoConfirmar =
    puedeConfirmar(vista, estado, aplicar.isPending, versionBase) && !previsualizar.isPending && !recargando
  const esCierre = estado.destino === 'TERMINADO'

  /** Cambiar destino o fecha borra el error del servidor de ese campo. */
  const dispatch = (accion: AccionDialogo) => {
    despachar(accion)
    if (accion.tipo === 'destino' || accion.tipo === 'fecha') {
      const campo = accion.tipo === 'destino' ? 'estadoDestino' : 'fecha'
      setErrores((actuales) => ({ ...actuales, campos: { ...actuales.campos, [campo]: undefined } }))
    }
  }

  const registrarError = (error: unknown) =>
    setErrores(
      error instanceof ErrorApi
        ? interpretarErrorCambio(error)
        : { ...SIN_ERRORES_CAMBIO, generales: ['Ocurrió un error inesperado.'] },
    )

  const verImpacto = () => {
    const revision = estado.revision
    setUltimaOperacion('impacto')
    setErrores(SIN_ERRORES_CAMBIO)
    previsualizar.mutate(aSolicitudCambio(estado), {
      onSuccess: (datos) => setVista({ revision, datos }),
      onError: (error) => {
        setVista(null)
        registrarError(error)
      },
    })
  }

  const confirmar = () => {
    // El ref evita un doble envío aunque el segundo clic llegue antes de que se deshabilite el botón.
    if (enviandoRef.current || !puedeConfirmar(vista, estado, aplicar.isPending, versionBase)) {
      return
    }
    enviandoRef.current = true
    setUltimaOperacion('confirmar')
    setErrores(SIN_ERRORES_CAMBIO)
    aplicar.mutate(aSolicitudRegistroCambio(estado, versionBase), {
      onSuccess: onRealizado,
      onError: registrarError,
      onSettled: () => {
        enviandoRef.current = false
      },
    })
  }

  /** 400/409 o cambio por otro: recargar el detalle y empezar de nuevo con los datos actuales (y su token base). */
  const recargar = async () => {
    setRecargando(true)
    try {
      const nueva = await onRecargar()
      if (nueva !== undefined) {
        setVersionBase(nueva)
      }
    } finally {
      setRecargando(false)
      despachar({ tipo: 'reiniciar' })
      setVista(null)
      setErrores(SIN_ERRORES_CAMBIO)
    }
  }

  return (
    <Dialog open onOpenChange={(_, d) => !d.open && !enviando && onCerrar()}>
      <DialogSurface className={estilos.superficie}>
        <DialogBody>
          <DialogTitle>Cambiar estado</DialogTitle>
          <DialogContent className={estilos.contenido}>
            {errores.generales.length > 0 && (
              <MessageBar intent="error">
                <MessageBarBody>
                  {errores.generales.map((m) => (
                    <div key={m}>{m}</div>
                  ))}
                  {errores.noEncontrado && <Link to={rutaListado}>Ir al listado de proyectos</Link>}
                </MessageBarBody>
                {errores.ofrecerReintento && (
                  <MessageBarActions>
                    <Button disabled={enviando} onClick={ultimaOperacion === 'confirmar' ? confirmar : verImpacto}>
                      Reintentar
                    </Button>
                  </MessageBarActions>
                )}
              </MessageBar>
            )}

            {errores.ofrecerRecarga && (
              <MessageBar intent="warning">
                <MessageBarBody>
                  <MessageBarTitle>Los datos del proyecto pueden haber cambiado.</MessageBarTitle>
                  Recargue para continuar con la información actual.
                </MessageBarBody>
                <MessageBarActions>
                  <Button disabled={enviando} onClick={() => void recargar()}>
                    Recargar datos del proyecto
                  </Button>
                </MessageBarActions>
              </MessageBar>
            )}

            <Field label="Nuevo estado" required validationMessage={errores.campos.estadoDestino}>
              <RadioGroup
                value={estado.destino ?? ''}
                onChange={(_, d) =>
                  dispatch({
                    tipo: 'destino',
                    destino: d.value as DestinoCambioEstado,
                    estadoProyecto: proyecto.estado,
                    fechaFin: proyecto.fechaFin,
                  })
                }
              >
                {opciones.map((o) => (
                  <Radio
                    key={o.codigo}
                    value={o.codigo}
                    disabled={!o.habilitada || enviando}
                    label={o.ayuda ? `${o.etiqueta} (${o.ayuda})` : o.etiqueta}
                  />
                ))}
              </RadioGroup>
            </Field>

            <Field
              label="Fecha del movimiento"
              required
              hint={`Entre ${formatearFecha(proyecto.fechaInicio)} y ${formatearFecha(proyecto.fechaFin)} (inclusive).`}
              validationMessage={errorFormatoFecha ?? errores.campos.fecha}
            >
              <SelectorFecha
                valor={estado.fecha ?? undefined}
                fechaMinima={proyecto.fechaInicio}
                fechaMaxima={proyecto.fechaFin}
                onCambiar={(valor) => dispatch({ tipo: 'fecha', fecha: valor ?? null })}
                onErrorFormato={setErrorFormatoFecha}
              />
            </Field>

            <div>
              <Button disabled={estado.destino === null || enviando} onClick={verImpacto}>
                {previsualizar.isPending ? 'Calculando…' : 'Ver impacto'}
              </Button>
            </div>

            {/* TAREA-19b2: la vista previa trae otra versión que la de los datos con que se abrió: no se registra. */}
            {vigente && cambioPorOtro(vista, versionBase) && (
              <MessageBar intent="warning">
                <MessageBarBody>
                  <MessageBarTitle>{MENSAJE_CAMBIO_POR_OTRO}</MessageBarTitle>
                </MessageBarBody>
                <MessageBarActions>
                  <Button disabled={enviando} onClick={() => void recargar()}>
                    Recargar datos del proyecto
                  </Button>
                </MessageBarActions>
              </MessageBar>
            )}

            {vista !== null && !vigente && (
              <MessageBar intent="warning">
                <MessageBarBody>
                  <MessageBarTitle>Vista previa desactualizada.</MessageBarTitle>
                  Cambió el estado o la fecha. Pulse "Ver impacto" otra vez para poder confirmar.
                </MessageBarBody>
              </MessageBar>
            )}

            {vista !== null && (
              <div className={vigente ? undefined : estilos.desactualizada}>
                <ImpactoCambioEstado datos={vista.datos} />
              </div>
            )}

            {vigente && (
              <>
                <Text weight="semibold">Esta acción no se puede deshacer.</Text>
                {esCierre && (
                  <Checkbox
                    checked={estado.entiendeTerminado}
                    disabled={enviando}
                    label="Entiendo que el proyecto quedará TERMINADO"
                    onChange={(_, d) => dispatch({ tipo: 'entiende', valor: d.checked === true })}
                  />
                )}
              </>
            )}
          </DialogContent>

          <DialogActions className={estilos.acciones}>
            <Button disabled={enviando} onClick={onCerrar}>
              Cancelar
            </Button>
            <Button
              appearance="primary"
              icon={aplicar.isPending ? <Spinner size="tiny" /> : undefined}
              disabled={!habilitadoConfirmar}
              onClick={confirmar}
            >
              {esCierre ? 'Confirmar cierre' : 'Confirmar suspensión'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  )
}
