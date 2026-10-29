import {
  Button,
  Checkbox,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  MessageBar,
  MessageBarActions,
  MessageBarBody,
  MessageBarTitle,
  Spinner,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components'
import { Save20Regular } from '@fluentui/react-icons'
import { useEffect, useRef, useState } from 'react'
import { useBlocker } from 'react-router'
import { esAdmin } from '../../../auth/roles'
import { useUsuarioActual } from '../../../auth/useUsuarioActual'
import { useCriterioPersonal, useGuardarCriterioPersonal } from './api'
import {
  MENSAJE_SIN_CRITERIO,
  type CriterioPersonal,
  type ErroresCriterio,
  aSolicitud,
  alternarFamilia,
  estaMarcada,
  estadoInicial,
  hayCambios,
  interpretarErrorCriterio,
  motivoSinGuardar,
  resumenCambio,
} from './criterioPersonal'

const MOVIL = '@media (max-width: 599px)'

const useEstilos = makeStyles({
  pagina: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL, maxWidth: '720px' },
  lista: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXS, margin: 0, padding: 0, listStyleType: 'none' },
  acciones: { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: tokens.spacingHorizontalM },
  boton: { alignSelf: 'flex-start', [MOVIL]: { alignSelf: 'stretch', width: '100%' } },
  secundario: { color: tokens.colorNeutralForeground3 },
  error: { color: tokens.colorPaletteRedForeground1 },
})

/**
 * Ruta /admin/personal-elegible (TAREA-62-2): criterio de personal elegible (Parametro EMPLEADO_FAMILIAS_ASIGNABLES).
 * Solo Admin: el gestor ve «No tienes permiso…» sin llamar al backend (la API exige la política Admin igual).
 */
export function PaginaPersonalElegible() {
  const estilos = useEstilos()
  const usuario = useUsuarioActual()
  const admin = esAdmin(usuario.data)
  const criterio = useCriterioPersonal(admin)
  const [recargas, setRecargas] = useState(0)
  const [aviso, setAviso] = useState<string>()

  if (usuario.isPending) {
    return <Spinner label="Cargando…" labelPosition="after" />
  }
  if (!admin) {
    return (
      <MessageBar intent="warning">
        <MessageBarBody>No tienes permiso para ver esta página.</MessageBarBody>
      </MessageBar>
    )
  }

  /** Vuelve a leer el criterio y monta el formulario con el valor nuevo. */
  const recargar = async () => {
    await criterio.refetch()
    setRecargas((n) => n + 1)
  }

  const errorGet = criterio.error ? interpretarErrorCriterio(criterio.error) : null

  return (
    <section className={estilos.pagina}>
      <Text as="h2" size={600} weight="semibold">
        Personal elegible
      </Text>
      <Text>Grupos (familias de puesto del ERP) que se pueden elegir como personal nuevo en proyectos y novedades.</Text>
      <Text className={estilos.secundario}>Solo afecta a las asignaciones nuevas: las personas ya asignadas no cambian.</Text>
      <Text className={estilos.secundario}>La sincronización de permisos médicos no aplica este criterio.</Text>

      {aviso && (
        <MessageBar intent="success">
          <MessageBarBody>
            <MessageBarTitle>{aviso}</MessageBarTitle>
          </MessageBarBody>
        </MessageBar>
      )}

      {errorGet ? (
        <MessageBar intent="error">
          <MessageBarBody>
            {errorGet.generales.map((m) => (
              <div key={m}>{m}</div>
            ))}
          </MessageBarBody>
          {errorGet.ofrecerReintento && (
            <MessageBarActions>
              <Button disabled={criterio.isFetching} onClick={() => void criterio.refetch()}>
                Reintentar
              </Button>
            </MessageBarActions>
          )}
        </MessageBar>
      ) : criterio.data ? (
        <FormularioCriterio
          key={recargas}
          criterio={criterio.data}
          onGuardado={async () => {
            setAviso('Criterio actualizado.')
            setRecargas((n) => n + 1) // la mutación ya volvió a leer el criterio: estado desde el nuevo valorActual
          }}
          onRecargar={async () => {
            setAviso(undefined)
            await recargar()
          }}
          onCambio={() => setAviso(undefined)}
        />
      ) : (
        <Spinner label="Cargando el criterio…" labelPosition="after" />
      )}
    </section>
  )
}

interface FormularioCriterioProps {
  criterio: CriterioPersonal
  onGuardado: () => Promise<void>
  onRecargar: () => Promise<void>
  onCambio: () => void
}

function FormularioCriterio({ criterio, onGuardado, onRecargar, onCambio }: FormularioCriterioProps) {
  const estilos = useEstilos()
  const guardar = useGuardarCriterioPersonal()
  // Estado del GET que abrió el formulario: su valorActual es el valorAnterior del PUT.
  const [estado, setEstado] = useState(() => estadoInicial(criterio))
  const [errores, setErrores] = useState<ErroresCriterio | null>(null)
  const [confirmarRecarga, setConfirmarRecarga] = useState(false)
  const enviandoRef = useRef(false)
  const guardadoRef = useRef(false)

  const cambios = hayCambios(estado)
  const motivo = motivoSinGuardar(estado)

  // Confirmar al salir con cambios sin guardar (patrón de la 19b); no se activa tras guardar.
  const bloqueo = useBlocker(
    ({ currentLocation, nextLocation }) => cambios && !guardadoRef.current && currentLocation.pathname !== nextLocation.pathname,
  )
  useEffect(() => {
    if (!cambios) {
      return
    }
    const alSalir = (evento: BeforeUnloadEvent) => {
      if (!guardadoRef.current) {
        evento.preventDefault()
      }
    }
    window.addEventListener('beforeunload', alSalir)
    return () => window.removeEventListener('beforeunload', alSalir)
  }, [cambios])

  const enviar = () => {
    // El ref evita un doble envío aunque el segundo clic llegue antes de que se deshabilite el botón.
    if (enviandoRef.current || guardar.isPending || motivoSinGuardar(estado)) {
      return
    }
    enviandoRef.current = true
    setErrores(null)
    guardar.mutate(aSolicitud(estado), {
      onSuccess: () => {
        guardadoRef.current = true // desactiva la confirmación al salir
        void onGuardado()
      },
      onError: (error) => setErrores(interpretarErrorCriterio(error)),
      onSettled: () => {
        enviandoRef.current = false
      },
    })
  }

  const pedirRecarga = () => (cambios ? setConfirmarRecarga(true) : void onRecargar())

  return (
    <>
      {criterio.sinCriterio && (
        <MessageBar intent="warning">
          <MessageBarBody>{MENSAJE_SIN_CRITERIO}</MessageBarBody>
        </MessageBar>
      )}

      {errores && errores.generales.length > 0 && (
        <MessageBar intent="error">
          <MessageBarBody>
            {errores.generales.map((m) => (
              <div key={m}>{m}</div>
            ))}
          </MessageBarBody>
          {(errores.ofrecerRecarga || errores.ofrecerReintento) && (
            <MessageBarActions>
              {errores.ofrecerRecarga && <Button onClick={pedirRecarga}>Recargar</Button>}
              {errores.ofrecerReintento && (
                <Button disabled={guardar.isPending} onClick={enviar}>
                  Reintentar
                </Button>
              )}
            </MessageBarActions>
          )}
        </MessageBar>
      )}

      <ul className={estilos.lista} aria-label="Familias de puesto">
        {estado.familias.map((f) => (
          <li key={f.familia}>
            <Checkbox
              label={`${f.familia} (${f.activos} ${f.activos === 1 ? 'activo' : 'activos'})`}
              checked={estaMarcada(estado, f.familia)}
              disabled={guardar.isPending}
              onChange={() => {
                setEstado(alternarFamilia(estado, f.familia))
                setErrores(null)
                onCambio()
              }}
            />
          </li>
        ))}
      </ul>
      {errores?.lista.map((m) => (
        <Text key={m} className={estilos.error} role="alert">
          {m}
        </Text>
      ))}

      {!motivo && <Text weight="semibold">{resumenCambio(estado)}</Text>}

      <div className={estilos.acciones}>
        <Button
          className={estilos.boton}
          appearance="primary"
          icon={guardar.isPending ? <Spinner size="tiny" /> : <Save20Regular />}
          disabled={guardar.isPending || motivo !== undefined}
          onClick={enviar}
        >
          {guardar.isPending ? 'Guardando…' : 'Guardar'}
        </Button>
        {motivo && !guardar.isPending && <Text className={estilos.secundario}>{motivo}</Text>}
      </div>

      <Dialog open={confirmarRecarga} onOpenChange={(_, d) => !d.open && setConfirmarRecarga(false)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>¿Recargar el criterio?</DialogTitle>
            <DialogContent>Se perderán los cambios que no guardó.</DialogContent>
            <DialogActions>
              <Button onClick={() => setConfirmarRecarga(false)}>Seguir editando</Button>
              <Button
                appearance="primary"
                onClick={() => {
                  setConfirmarRecarga(false)
                  void onRecargar()
                }}
              >
                Recargar
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>

      <Dialog open={bloqueo.state === 'blocked'} onOpenChange={(_, d) => !d.open && bloqueo.reset?.()}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>¿Salir sin guardar?</DialogTitle>
            <DialogContent>Los cambios del criterio no se han guardado. Si sale ahora, se perderán.</DialogContent>
            <DialogActions>
              <Button onClick={() => bloqueo.reset?.()}>Seguir editando</Button>
              <Button appearance="primary" onClick={() => bloqueo.proceed?.()}>
                Salir sin guardar
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </>
  )
}
