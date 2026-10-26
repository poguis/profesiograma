import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Dropdown,
  Field,
  MessageBar,
  MessageBarActions,
  MessageBarBody,
  Option,
  Spinner,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components'
import { ArrowLeft20Regular, Save20Regular, Search20Regular } from '@fluentui/react-icons'
import { useEffect, useRef, useState } from 'react'
import { Link, useBlocker, useNavigate } from 'react-router'
import { ErrorApi } from '../../../api/errores'
import { EstadoError } from '../../../components/EstadoError'
import { BuscadorEmpleados } from '../../proyectos/components/BuscadorEmpleados'
import { claveEmpleado } from '../../proyectos/formularioProyecto'
import { CamposNovedad, type ValoresCamposNovedad } from '../components/CamposNovedad'
import { SelectorProyectoVigente } from '../components/SelectorProyectoVigente'
import { excedeMaximo } from '../diasNovedad'
import { CAMPOS_REGISTRO, type CampoRegistro, type ErroresNovedad, interpretarErrorNovedad } from '../erroresNovedad'
import { APLICACIONES } from '../filtrosUrl'
import {
  REGISTRO_VACIO,
  aSolicitudCrear,
  conAplicacion,
  tieneCambios,
  validarRegistro,
  type EstadoRegistro,
} from '../formularioNovedad'
import { useCrearNovedad, useOpcionesNovedad } from '../hooks'
import { useNombresNovedad } from '../presentacion'
import type { AplicacionNovedad } from '../tipos'
import type { EstadoNavegacionNovedades } from './ListadoNovedades'

const MOVIL = '@media (max-width: 599px)'

const useEstilos = makeStyles({
  pagina: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL, maxWidth: '720px' },
  formulario: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
  boton: { alignSelf: 'flex-start', [MOVIL]: { alignSelf: 'stretch' } },
  persona: { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: tokens.spacingHorizontalM },
  acciones: { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: tokens.spacingHorizontalM },
  secundario: { color: tokens.colorNeutralForeground3 },
})

/** Mensaje por clave del envío (cliente o servidor), pintado en su control. */
type Errores = Partial<Record<CampoRegistro, string>>

/** Registro de novedades PERSONA, PROYECTO y GENERAL (TAREA-24b). La API valida todo de nuevo y manda (§2). */
export function NuevaNovedad() {
  const estilos = useEstilos()
  const navigate = useNavigate()
  const opciones = useOpcionesNovedad()
  const nombres = useNombresNovedad()
  const crear = useCrearNovedad()

  const [estado, setEstado] = useState<EstadoRegistro>(REGISTRO_VACIO)
  const [intentado, setIntentado] = useState(false)
  const [errorEnvio, setErrorEnvio] = useState<ErroresNovedad<CampoRegistro> | null>(null)
  const [buscadorAbierto, setBuscadorAbierto] = useState(false)
  const enviandoRef = useRef(false)
  const registradoRef = useRef(false)

  // ---------------------------------------------------------------- confirmar al salir con cambios (patrón de la 19b)
  const sucio = tieneCambios(estado)
  const bloqueo = useBlocker(
    ({ currentLocation, nextLocation }) =>
      sucio && !registradoRef.current && currentLocation.pathname !== nextLocation.pathname,
  )
  useEffect(() => {
    if (!sucio) {
      return
    }
    const alSalir = (evento: BeforeUnloadEvent) => {
      if (!registradoRef.current) {
        evento.preventDefault()
      }
    }
    window.addEventListener('beforeunload', alSalir)
    return () => window.removeEventListener('beforeunload', alSalir)
  }, [sucio])

  const maxDias = opciones.data?.maxDias
  const erroresCliente = validarRegistro(estado, maxDias)
  // Antes del primer intento solo se muestran las reglas que el usuario ya puede ver (rango y largo de la observación).
  const visiblesCliente: Errores = intentado
    ? erroresCliente
    : {
        ...(estado.fechaInicio && estado.fechaFin ? { fechaFin: erroresCliente.fechaFin } : {}),
        observacion: erroresCliente.observacion,
      }
  const errores: Errores = { ...visiblesCliente, ...errorEnvio?.campos } // el mensaje del servidor tiene prioridad
  const excedido = excedeMaximo(estado.fechaInicio, estado.fechaFin, maxDias)

  /** Aplica un cambio y borra el error del servidor de los campos tocados. */
  const cambiar = (siguiente: EstadoRegistro, campos: CampoRegistro[]) => {
    setEstado(siguiente)
    setErrorEnvio((actual) => {
      if (!actual) {
        return actual
      }
      const restantes = { ...actual.campos }
      for (const campo of campos) {
        delete restantes[campo]
      }
      return { ...actual, campos: restantes }
    })
  }

  const cambiarCampos = (cambios: Partial<ValoresCamposNovedad>) => {
    const campos: CampoRegistro[] = []
    if ('tipo' in cambios) campos.push('tipoNovedad')
    if ('fechaInicio' in cambios || 'fechaFin' in cambios) campos.push('fechaInicio', 'fechaFin')
    if ('observacion' in cambios) campos.push('observacion')
    cambiar({ ...estado, ...cambios }, campos)
  }

  const registrar = () => {
    // El ref evita un doble envío aunque el segundo clic llegue antes de que se deshabilite el botón.
    if (enviandoRef.current || crear.isPending) {
      return
    }
    setIntentado(true)
    if (Object.keys(erroresCliente).length > 0) {
      return
    }
    enviandoRef.current = true
    setErrorEnvio(null)
    crear.mutate(aSolicitudCrear(estado), {
      onSuccess: (creada) => {
        registradoRef.current = true // desactiva la confirmación al salir
        const estadoNavegacion: EstadoNavegacionNovedades = { aviso: `Novedad ${creada.codigo} registrada.` }
        void navigate(`/novedades/${creada.id}`, { replace: true, state: estadoNavegacion })
      },
      onError: (error) => {
        setErrorEnvio(
          error instanceof ErrorApi
            ? interpretarErrorNovedad(error, CAMPOS_REGISTRO)
            : { campos: {}, generales: ['Ocurrió un error inesperado.'], ofrecerRecarga: false, ofrecerReintento: true, noEncontrado: false },
        )
      },
      onSettled: () => {
        enviandoRef.current = false
      },
    })
  }

  if (opciones.isPending) {
    return <Spinner label="Cargando el formulario…" labelPosition="after" />
  }
  if (opciones.error) {
    return (
      <section className={estilos.pagina}>
        <EstadoError error={opciones.error} />
        <Button className={estilos.boton} onClick={() => void opciones.refetch()}>
          Reintentar
        </Button>
      </section>
    )
  }

  const { puedeRegistrarGeneral, tiposPorAplicacion } = opciones.data
  const aplicaciones = APLICACIONES.filter((a) => a !== 'GENERAL' || puedeRegistrarGeneral)
  const tipos = estado.aplicacion ? (tiposPorAplicacion[estado.aplicacion] ?? []) : []
  const etiquetasSeleccion = new Map(estado.empleado ? [[claveEmpleado(estado.empleado.codigoEkon), ['Seleccionado']]] : [])

  return (
    <section className={estilos.pagina}>
      <Button className={estilos.boton} icon={<ArrowLeft20Regular />} onClick={() => void navigate('/novedades')}>
        Volver al listado
      </Button>
      <Text as="h2" size={600} weight="semibold">
        Nueva novedad
      </Text>

      {errorEnvio && errorEnvio.generales.length > 0 && (
        <MessageBar intent="error" data-errores-generales tabIndex={-1}>
          <MessageBarBody>
            {errorEnvio.generales.map((mensaje) => (
              <div key={mensaje}>{mensaje}</div>
            ))}
            {errorEnvio.codigoDuplicado && (
              <Link to={`/novedades?texto=${encodeURIComponent(errorEnvio.codigoDuplicado)}`}>
                Ver {errorEnvio.codigoDuplicado}
              </Link>
            )}
          </MessageBarBody>
          {errorEnvio.ofrecerReintento && (
            <MessageBarActions>
              <Button disabled={crear.isPending} onClick={registrar}>
                Reintentar
              </Button>
            </MessageBarActions>
          )}
        </MessageBar>
      )}

      <div className={estilos.formulario}>
        <Field label="Aplicación" required validationMessage={errores.tipoAplicacion}>
          <Dropdown
            placeholder="Elija la aplicación"
            value={estado.aplicacion ? nombres.aplicacion(estado.aplicacion) : ''}
            selectedOptions={estado.aplicacion ? [estado.aplicacion] : []}
            onOptionSelect={(_, datos) =>
              cambiar(conAplicacion(estado, datos.optionValue as AplicacionNovedad), [
                'tipoAplicacion',
                'codigoEkon',
                'proyectoId',
                'tipoNovedad',
              ])
            }
          >
            {aplicaciones.map((a) => (
              <Option key={a} value={a}>
                {nombres.aplicacion(a)}
              </Option>
            ))}
          </Dropdown>
        </Field>

        {estado.aplicacion === 'PERSONA' && (
          <Field label="Persona" required validationMessage={errores.codigoEkon}>
            <div className={estilos.persona}>
              {estado.empleado ? (
                <Text>
                  {estado.empleado.codigoEkon} · {estado.empleado.nombreCompleto}
                  {estado.empleado.cargo && <span className={estilos.secundario}> · {estado.empleado.cargo}</span>}
                </Text>
              ) : (
                <Text className={estilos.secundario}>Sin persona elegida</Text>
              )}
              <Button icon={<Search20Regular />} onClick={() => setBuscadorAbierto(true)}>
                {estado.empleado ? 'Cambiar persona' : 'Buscar persona'}
              </Button>
            </div>
          </Field>
        )}

        {estado.aplicacion === 'PROYECTO' && (
          <Field label="Proyecto" required validationMessage={errores.proyectoId}>
            <SelectorProyectoVigente
              valor={estado.proyecto}
              invalido={Boolean(errores.proyectoId)}
              onCambiar={(proyecto) => cambiar({ ...estado, proyecto }, ['proyectoId'])}
            />
          </Field>
        )}

        <CamposNovedad valores={estado} tipos={tipos} errores={errores} onCambiar={cambiarCampos} />

        <div className={estilos.acciones}>
          <Button
            className={estilos.boton}
            appearance="primary"
            icon={crear.isPending ? <Spinner size="tiny" /> : <Save20Regular />}
            disabled={crear.isPending || excedido}
            onClick={registrar}
          >
            {crear.isPending ? 'Registrando…' : 'Registrar'}
          </Button>
          {intentado && Object.keys(erroresCliente).length > 0 && !crear.isPending && (
            <Text className={estilos.secundario}>Revise los campos marcados.</Text>
          )}
        </div>
      </div>

      <BuscadorEmpleados
        abierto={buscadorAbierto}
        titulo="Seleccionar persona"
        etiquetas={etiquetasSeleccion}
        puedeAgregar
        verOtrosInicial
        textoAccion="Seleccionar"
        onSeleccionar={(empleado) => {
          cambiar({ ...estado, empleado }, ['codigoEkon'])
          setBuscadorAbierto(false)
        }}
        onCerrar={() => setBuscadorAbierto(false)}
      />

      <Dialog open={bloqueo.state === 'blocked'} onOpenChange={(_, d) => !d.open && bloqueo.reset?.()}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>¿Salir sin registrar?</DialogTitle>
            <DialogContent>La novedad no se ha registrado. Si sale ahora, se perderán los datos ingresados.</DialogContent>
            <DialogActions>
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
