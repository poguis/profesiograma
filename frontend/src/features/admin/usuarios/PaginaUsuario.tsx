import {
  Badge,
  Button,
  Checkbox,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Field,
  Input,
  MessageBar,
  MessageBarActions,
  MessageBarBody,
  MessageBarTitle,
  Spinner,
  Switch,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components'
import { ArrowLeft20Regular, Save20Regular } from '@fluentui/react-icons'
import { useEffect, useRef, useState } from 'react'
import { useBlocker, useLocation, useNavigate, useParams } from 'react-router'
import { AdminRequerido } from '../AdminRequerido'
import { BarraErrores } from '../BarraErrores'
import { useGuardarDatosUsuario, useGuardarDepartamentos, useUsuarioAdmin } from './api'
import type { EstadoNavegacionUsuario } from './DialogoNuevoUsuario'
import {
  AVISO_DATOS,
  AVISO_DEPARTAMENTOS,
  CAMPOS_DATOS,
  CAMPOS_DEPARTAMENTOS,
  type ErroresUsuario,
  MENSAJE_BAJA_PROPIA,
  TEXTO_CONFIRMAR_BAJA,
  TEXTO_ROLES,
  type UsuarioAdminDetalle,
  aSolicitudDatos,
  aSolicitudDepartamentos,
  advertenciaDepartamentos,
  alternarDepartamento,
  esBaja,
  estadoDatos,
  estadoDepartamentos,
  etiquetaDepartamento,
  hayCambiosDatos,
  hayCambiosDepartamentos,
  interpretarErrorUsuario,
  motivoSinGuardarDatos,
  motivoSinGuardarDepartamentos,
  resumenDatos,
  resumenDepartamentos,
} from './usuariosAdmin'

const MOVIL = '@media (max-width: 599px)'

const useEstilos = makeStyles({
  pagina: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL, maxWidth: '720px' },
  seccion: {
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalM,
    padding: tokens.spacingHorizontalL,
    border: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
  },
  lista: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXS, margin: 0, padding: 0, listStyleType: 'none' },
  acciones: { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: tokens.spacingHorizontalM },
  boton: { alignSelf: 'flex-start', [MOVIL]: { alignSelf: 'stretch', width: '100%' } },
  volver: { alignSelf: 'flex-start' },
  secundario: { color: tokens.colorNeutralForeground3 },
  insignias: { display: 'flex', flexWrap: 'wrap', gap: tokens.spacingHorizontalXS },
  error: { color: tokens.colorPaletteRedForeground1 },
  superficie: { [MOVIL]: { width: '100%', maxWidth: '100%' } },
  accionesDialogo: { [MOVIL]: { flexDirection: 'column', '& > button': { width: '100%' } } },
})

/** Ruta /admin/usuarios/:id (TAREA-25-2): «Datos» y «Departamentos», cada una con su botón. Solo Admin. */
export function PaginaUsuario() {
  return (
    <AdminRequerido>
      <DetalleUsuario />
    </AdminRequerido>
  )
}

function DetalleUsuario() {
  const estilos = useEstilos()
  const navigate = useNavigate()
  const location = useLocation()
  const { id: textoId } = useParams()
  const id = Number(textoId)
  const valido = Number.isInteger(id) && id > 0
  const detalle = useUsuarioAdmin(id, valido)
  const [aviso, setAviso] = useState((location.state as EstadoNavegacionUsuario | null)?.aviso)
  const [recargasDatos, setRecargasDatos] = useState(0)
  const [recargasDepartamentos, setRecargasDepartamentos] = useState(0)
  const [cambiosDatos, setCambiosDatos] = useState(false)
  const [cambiosDepartamentos, setCambiosDepartamentos] = useState(false)
  const hayCambios = cambiosDatos || cambiosDepartamentos

  // Confirmar al salir con cambios sin guardar (patrón de la 62-2). Tras guardar, la sección se vuelve a montar sin cambios.
  const bloqueo = useBlocker(({ currentLocation, nextLocation }) => hayCambios && currentLocation.pathname !== nextLocation.pathname)
  useEffect(() => {
    if (!hayCambios) {
      return
    }
    const alSalir = (evento: BeforeUnloadEvent) => evento.preventDefault()
    window.addEventListener('beforeunload', alSalir)
    return () => window.removeEventListener('beforeunload', alSalir)
  }, [hayCambios])

  /** Vuelve a leer el usuario y monta la sección con el estado nuevo. */
  const recargar = async (seccion: 'datos' | 'departamentos') => {
    setAviso(undefined)
    await detalle.refetch()
    if (seccion === 'datos') {
      setRecargasDatos((n) => n + 1)
    } else {
      setRecargasDepartamentos((n) => n + 1)
    }
  }

  const errorGet = !valido
    ? interpretarErrorUsuario(new Error('Id inválido'), [])
    : detalle.error
      ? interpretarErrorUsuario(detalle.error, [])
      : null
  const noExiste = !valido || errorGet?.noEncontrado

  return (
    <section className={estilos.pagina}>
      <Button className={estilos.volver} appearance="subtle" icon={<ArrowLeft20Regular />} onClick={() => void navigate('/admin/usuarios')}>
        Usuarios y departamentos
      </Button>

      {aviso && (
        <MessageBar intent="success">
          <MessageBarBody>
            <MessageBarTitle>{aviso}</MessageBarTitle>
          </MessageBarBody>
        </MessageBar>
      )}

      {errorGet ? (
        <MessageBar intent="error">
          <MessageBarBody>{noExiste ? 'El usuario no existe.' : errorGet.generales.map((m) => <div key={m}>{m}</div>)}</MessageBarBody>
          {!noExiste && errorGet.ofrecerReintento && (
            <MessageBarActions>
              <Button disabled={detalle.isFetching} onClick={() => void detalle.refetch()}>
                Reintentar
              </Button>
            </MessageBarActions>
          )}
        </MessageBar>
      ) : !detalle.data ? (
        <Spinner label="Cargando el usuario…" labelPosition="after" />
      ) : (
        <>
          <div>
            <Text as="h2" size={600} weight="semibold" block>
              {detalle.data.nombreMostrar}
            </Text>
            <Text block>{detalle.data.email}</Text>
            <span className={estilos.insignias}>
              <Badge appearance="tint" color={detalle.data.activo ? 'success' : 'danger'}>
                {detalle.data.activo ? 'Activo' : 'Inactivo'}
              </Badge>
              {!detalle.data.conIdentidad && (
                <Badge appearance="tint" color="informative">
                  Aún no ha ingresado
                </Badge>
              )}
            </span>
          </div>
          <Text className={estilos.secundario}>{TEXTO_ROLES}</Text>

          <SeccionDatos
            key={`datos-${recargasDatos}`}
            usuario={detalle.data}
            onGuardado={() => {
              setAviso(AVISO_DATOS)
              setRecargasDatos((n) => n + 1) // la mutación ya volvió a leer el usuario
            }}
            onRecargar={() => recargar('datos')}
            onCambios={setCambiosDatos}
            onEditar={() => setAviso(undefined)}
          />
          <SeccionDepartamentos
            key={`departamentos-${recargasDepartamentos}`}
            usuario={detalle.data}
            onGuardado={() => {
              setAviso(AVISO_DEPARTAMENTOS)
              setRecargasDepartamentos((n) => n + 1)
            }}
            onRecargar={() => recargar('departamentos')}
            onCambios={setCambiosDepartamentos}
            onEditar={() => setAviso(undefined)}
          />
        </>
      )}

      <Dialog open={bloqueo.state === 'blocked'} onOpenChange={(_, d) => !d.open && bloqueo.reset?.()}>
        <DialogSurface className={estilos.superficie}>
          <DialogBody>
            <DialogTitle>¿Salir sin guardar?</DialogTitle>
            <DialogContent>Los cambios del usuario no se han guardado. Si sale ahora, se perderán.</DialogContent>
            <DialogActions className={estilos.accionesDialogo}>
              <Button onClick={() => bloqueo.reset?.()}>Seguir editando</Button>
              <Button appearance="primary" onClick={() => bloqueo.proceed?.()}>
                Salir sin guardar
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </section>
  )
}

interface SeccionProps {
  usuario: UsuarioAdminDetalle
  onGuardado: () => void
  onRecargar: () => Promise<void>
  onCambios: (hay: boolean) => void
  onEditar: () => void
}

function SeccionDatos({ usuario, onGuardado, onRecargar, onCambios, onEditar }: SeccionProps) {
  const estilos = useEstilos()
  const guardar = useGuardarDatosUsuario(usuario.id, usuario.esUsuarioActual)
  // Valores del GET que abrió la sección: son nombreAnterior y activoAnterior del PUT.
  const [estado, setEstado] = useState(() => estadoDatos(usuario))
  const [errores, setErrores] = useState<ErroresUsuario<(typeof CAMPOS_DATOS)[number]> | null>(null)
  const [confirmarBaja, setConfirmarBaja] = useState(false)
  const enviandoRef = useRef(false)
  const cambios = hayCambiosDatos(estado)
  const motivo = motivoSinGuardarDatos(estado)

  useEffect(() => onCambios(cambios), [cambios, onCambios])

  const enviar = () => {
    // El ref evita un doble envío aunque el segundo clic llegue antes de que se deshabilite el botón.
    if (enviandoRef.current || guardar.isPending || motivoSinGuardarDatos(estado)) {
      return
    }
    enviandoRef.current = true
    setErrores(null)
    guardar.mutate(aSolicitudDatos(estado), {
      onSuccess: onGuardado,
      onError: (error) => setErrores(interpretarErrorUsuario(error, CAMPOS_DATOS)),
      onSettled: () => {
        enviandoRef.current = false
      },
    })
  }

  return (
    <section className={estilos.seccion} aria-label="Datos">
      <Text as="h3" size={500} weight="semibold">
        Datos
      </Text>
      <BarraErrores errores={errores} cambios={cambios} pendiente={guardar.isPending} onRecargar={onRecargar} onReintentar={enviar} />
      <Field label="Nombre" required validationMessage={errores?.campos.nombreMostrar}>
        <Input
          value={estado.nombre}
          disabled={guardar.isPending}
          onChange={(_, d) => {
            setEstado({ ...estado, nombre: d.value })
            onEditar()
          }}
        />
      </Field>
      <Field
        validationMessage={errores?.campos.activo ?? (estado.esUsuarioActual ? MENSAJE_BAJA_PROPIA : undefined)}
        validationState={errores?.campos.activo ? 'error' : 'none'}
      >
        <Switch
          label="Activo"
          checked={estado.activo}
          disabled={guardar.isPending || estado.esUsuarioActual}
          onChange={(_, d) => {
            setEstado({ ...estado, activo: d.checked })
            onEditar()
          }}
        />
      </Field>
      {!motivo && <Text weight="semibold">{resumenDatos(estado)}</Text>}
      <div className={estilos.acciones}>
        <Button
          className={estilos.boton}
          appearance="primary"
          icon={guardar.isPending ? <Spinner size="tiny" /> : <Save20Regular />}
          disabled={guardar.isPending || motivo !== undefined}
          onClick={() => (esBaja(estado) ? setConfirmarBaja(true) : enviar())}
        >
          {guardar.isPending ? 'Guardando…' : 'Guardar datos'}
        </Button>
        {motivo && !guardar.isPending && <Text className={estilos.secundario}>{motivo}</Text>}
      </div>

      <Dialog open={confirmarBaja} onOpenChange={(_, d) => !d.open && setConfirmarBaja(false)}>
        <DialogSurface className={estilos.superficie}>
          <DialogBody>
            <DialogTitle>¿Dar de baja al usuario?</DialogTitle>
            <DialogContent>{TEXTO_CONFIRMAR_BAJA}</DialogContent>
            <DialogActions className={estilos.accionesDialogo}>
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
    </section>
  )
}

function SeccionDepartamentos({ usuario, onGuardado, onRecargar, onCambios, onEditar }: SeccionProps) {
  const estilos = useEstilos()
  const guardar = useGuardarDepartamentos(usuario.id)
  // Asignados según el GET que abrió la sección: son los `anteriores` del PUT.
  const [estado, setEstado] = useState(() => estadoDepartamentos(usuario))
  const [errores, setErrores] = useState<ErroresUsuario<(typeof CAMPOS_DEPARTAMENTOS)[number]> | null>(null)
  const enviandoRef = useRef(false)
  const cambios = hayCambiosDepartamentos(estado)
  const motivo = motivoSinGuardarDepartamentos(estado)
  const advertencia = advertenciaDepartamentos(estado.originales)

  useEffect(() => onCambios(cambios), [cambios, onCambios])

  const enviar = () => {
    if (enviandoRef.current || guardar.isPending || motivoSinGuardarDepartamentos(estado)) {
      return
    }
    enviandoRef.current = true
    setErrores(null)
    guardar.mutate(aSolicitudDepartamentos(estado), {
      onSuccess: onGuardado,
      onError: (error) => setErrores(interpretarErrorUsuario(error, CAMPOS_DEPARTAMENTOS)),
      onSettled: () => {
        enviandoRef.current = false
      },
    })
  }

  return (
    <section className={estilos.seccion} aria-label="Departamentos">
      <Text as="h3" size={500} weight="semibold">
        Departamentos
      </Text>
      {advertencia && (
        <MessageBar intent="warning">
          <MessageBarBody>{advertencia}</MessageBarBody>
        </MessageBar>
      )}
      <BarraErrores errores={errores} cambios={cambios} pendiente={guardar.isPending} onRecargar={onRecargar} onReintentar={enviar} />
      <ul className={estilos.lista} aria-label="Departamentos del usuario">
        {estado.disponibles.map((d) => (
          <li key={d.id}>
            <Checkbox
              label={etiquetaDepartamento(d)}
              checked={estado.marcados.includes(d.id)}
              disabled={guardar.isPending}
              onChange={() => {
                setEstado(alternarDepartamento(estado, d.id))
                setErrores(null)
                onEditar()
              }}
            />
          </li>
        ))}
      </ul>
      {errores?.campos.departamentoIds && (
        <Text className={estilos.error} role="alert">
          {errores.campos.departamentoIds}
        </Text>
      )}
      {!motivo && <Text weight="semibold">{resumenDepartamentos(estado)}</Text>}
      <div className={estilos.acciones}>
        <Button
          className={estilos.boton}
          appearance="primary"
          icon={guardar.isPending ? <Spinner size="tiny" /> : <Save20Regular />}
          disabled={guardar.isPending || motivo !== undefined}
          onClick={enviar}
        >
          {guardar.isPending ? 'Guardando…' : 'Guardar departamentos'}
        </Button>
        {motivo && !guardar.isPending && <Text className={estilos.secundario}>{motivo}</Text>}
      </div>
    </section>
  )
}
