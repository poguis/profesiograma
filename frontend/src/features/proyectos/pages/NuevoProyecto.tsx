import {
  Button,
  Card,
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
import { ArrowLeft20Regular, Eye20Regular, Save20Regular } from '@fluentui/react-icons'
import { useCallback, useEffect, useMemo, useReducer, useRef, useState } from 'react'
import { useBlocker, useNavigate } from 'react-router'
import { ErrorApi } from '../../../api/errores'
import { EstadoError } from '../../../components/EstadoError'
import { BuscadorEmpleados } from '../components/BuscadorEmpleados'
import { ListaBacks } from '../components/ListaBacks'
import { ListaPrincipales } from '../components/ListaPrincipales'
import { SeccionCabecera } from '../components/SeccionCabecera'
import { VistaPrevia } from '../components/VistaPrevia'
import {
  type AccionFormulario,
  type ClavesEnvio,
  type ErroresFila,
  type ErroresServidor,
  SIN_ERRORES_SERVIDOR,
  type VistaPreviaFormulario,
  aSolicitud,
  ayudaPrincipales,
  advertenciasFechas,
  clavesDelEnvio,
  crearEstadoInicial,
  crucesDeConflicto,
  distribuirErrores,
  etiquetasPorEmpleado,
  limpiarErroresServidor,
  puedeGenerarVistaPrevia,
  puedeRegistrar,
  reducerFormulario,
  tieneRangoProyecto,
  validarCliente,
  vistaVigente,
} from '../formularioProyecto'
import {
  useCatalogos,
  useCompaniasErp,
  useCrearProyecto,
  useHorariosErp,
  useOpcionesFormulario,
  usePrevisualizarProyecto,
} from '../hooks'
import type { Catalogos, CompaniaErp, HorarioErp, OpcionesFormularioProyecto } from '../tipos'
import type { EstadoNavegacionProyectos } from './ListadoProyectos'

const useEstilos = makeStyles({
  pagina: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL },
  volver: { alignSelf: 'flex-start' },
  seccion: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
  barra: {
    position: 'sticky',
    bottom: 0,
    zIndex: 1,
    display: 'flex',
    flexWrap: 'wrap',
    alignItems: 'center',
    gap: tokens.spacingHorizontalM,
    padding: `${tokens.spacingVerticalS} 0`,
    backgroundColor: tokens.colorNeutralBackground1,
    borderTop: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke2}`,
  },
  desactualizada: { opacity: 0.5 },
})

/** Ruta /proyectos/nuevo. Carga los datos de apoyo y luego monta el formulario (estado inicial con P5). */
export function NuevoProyecto() {
  const estilos = useEstilos()
  const navigate = useNavigate()
  const catalogos = useCatalogos()
  const opciones = useOpcionesFormulario()
  const companias = useCompaniasErp()
  const horarios = useHorariosErp()

  const error = catalogos.error ?? opciones.error ?? companias.error ?? horarios.error

  return (
    <section className={estilos.pagina}>
      <Button
        className={estilos.volver}
        appearance="subtle"
        icon={<ArrowLeft20Regular />}
        onClick={() => void navigate('/proyectos')}
      >
        Volver al listado
      </Button>

      <Text as="h2" size={600} weight="semibold">
        Nuevo proyecto
      </Text>

      {error ? (
        <EstadoError error={error} />
      ) : catalogos.data && opciones.data && companias.data && horarios.data ? (
        <FormularioNuevoProyecto
          catalogos={catalogos.data}
          opciones={opciones.data}
          companias={companias.data}
          horarios={horarios.data}
        />
      ) : (
        <Spinner label="Cargando datos del formulario…" />
      )}
    </section>
  )
}

type Buscador = 'principal' | 'back' | null

/** Error de un envío que no se asigna a campos (503, red, 409…). `reintentar` solo para 503 y red. */
interface ErrorEnvio {
  error: unknown
  reintentar?: 'previsualizar' | 'registrar'
}

interface FormularioNuevoProyectoProps {
  catalogos: Catalogos
  opciones: OpcionesFormularioProyecto
  companias: CompaniaErp[]
  horarios: HorarioErp[]
}

function FormularioNuevoProyecto({ catalogos, opciones, companias, horarios }: FormularioNuevoProyectoProps) {
  const estilos = useEstilos()
  const navigate = useNavigate()
  const [estado, despacharFormulario] = useReducer(
    reducerFormulario,
    { departamentos: opciones.departamentos, companias },
    crearEstadoInicial,
  )
  const [buscador, setBuscador] = useState<Buscador>(null)
  const [vista, setVista] = useState<VistaPreviaFormulario | null>(null)
  const [erroresServidor, setErroresServidor] = useState<ErroresServidor>(SIN_ERRORES_SERVIDOR)
  const [errorEnvio, setErrorEnvio] = useState<ErrorEnvio | null>(null)
  const previsualizar = usePrevisualizarProyecto()
  const crear = useCrearProyecto()
  // Refs: se leen en el mismo instante del clic o de la navegación, antes del siguiente render.
  const enviandoRef = useRef(false)
  const registradoRef = useRef(false)

  /** Cada cambio actualiza el formulario y borra el error del servidor de ese campo. */
  const dispatch = useCallback((accion: AccionFormulario) => {
    despacharFormulario(accion)
    setErroresServidor((actuales) => limpiarErroresServidor(actuales, accion))
  }, [])

  // ---------------------------------------------------------------- D3: confirmar al salir con cambios
  const sucio = estado.revision > 0
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

  // ---------------------------------------------------------------- errores y avisos
  const erroresCliente = useMemo(() => validarCliente(estado), [estado])
  const advertencias = useMemo(() => advertenciasFechas(estado), [estado])
  const etiquetas = useMemo(() => etiquetasPorEmpleado(estado), [estado])
  const rangoListo = tieneRangoProyecto(estado.cabecera)

  // El mensaje del servidor tiene prioridad sobre la ayuda del cliente.
  const erroresCabecera = { ...erroresCliente.cabecera, ...erroresServidor.cabecera }
  const erroresFilas = useMemo(
    () => combinarFilas(erroresCliente.filas, erroresServidor.filas),
    [erroresCliente.filas, erroresServidor.filas],
  )

  const manejarError = (error: unknown, claves: ClavesEnvio, operacion: 'previsualizar' | 'registrar') => {
    if (error instanceof ErrorApi && error.tipo === 'validacion') {
      setErroresServidor(
        error.errores ? distribuirErrores(error.errores, claves) : { ...SIN_ERRORES_SERVIDOR, generales: [error.titulo] },
      )
      return
    }
    const reintentable = error instanceof ErrorApi && (error.tipo === 'noDisponible' || error.tipo === 'red')
    setErrorEnvio({ error, reintentar: reintentable ? operacion : undefined })
  }

  // ---------------------------------------------------------------- vista previa (R11)
  const generarVistaPrevia = () => {
    const revision = estado.revision
    const claves = clavesDelEnvio(estado)
    setErrorEnvio(null)
    previsualizar.mutate(aSolicitud(estado), {
      onSuccess: (datos) => {
        setVista({ revision, datos })
        setErroresServidor(SIN_ERRORES_SERVIDOR)
      },
      onError: (error) => {
        setVista(null)
        manejarError(error, claves, 'previsualizar')
      },
    })
  }

  // ---------------------------------------------------------------- registro (R12, R13)
  const habilitadoRegistrar = puedeRegistrar(vista, estado.revision, crear.isPending)

  const registrar = () => {
    // El ref evita un doble envío aunque el segundo clic llegue antes de que se deshabilite el botón.
    if (enviandoRef.current || !puedeRegistrar(vista, estado.revision, crear.isPending)) {
      return
    }
    enviandoRef.current = true
    const claves = clavesDelEnvio(estado)
    setErrorEnvio(null)
    crear.mutate(aSolicitud(estado), {
      onSuccess: (creado) => {
        registradoRef.current = true // desactiva la confirmación al salir
        const estadoNavegacion: EstadoNavegacionProyectos = { codigoCreado: creado.codigo }
        void navigate(`/proyectos/${creado.id}`, { replace: true, state: estadoNavegacion })
      },
      onError: (error) => {
        const conflicto = error instanceof ErrorApi && error.tipo === 'conflicto' ? crucesDeConflicto(error.extensiones) : null
        if (conflicto) {
          // 409: cruces que aparecieron después de la vista previa. Se muestran y "Registrar" queda deshabilitado.
          setVista((actual) => actual && { ...actual, datos: { ...actual.datos, ...conflicto } })
          setErrorEnvio({ error })
          return
        }
        manejarError(error, claves, 'registrar')
      },
      onSettled: () => {
        enviandoRef.current = false
      },
    })
  }

  const enviando = previsualizar.isPending || crear.isPending
  const vigente = vistaVigente(vista, estado.revision)
  const habilitadoVistaPrevia = puedeGenerarVistaPrevia(estado, enviando)
  // P6: ayuda neutra mientras falten principales; el error rojo solo con un 400 del servidor en `principales`.
  const ayudaMinimo = ayudaPrincipales(estado)

  const puedeAgregar =
    buscador === 'principal'
      ? estado.principales.length < opciones.maxPrincipales
      : estado.backs.length < opciones.maxBacks

  return (
    <>
      {erroresServidor.generales.length > 0 && (
        <MessageBar intent="error">
          <MessageBarBody>
            <MessageBarTitle>Los datos del proyecto no son válidos.</MessageBarTitle>
            {erroresServidor.generales.map((mensaje) => (
              <div key={mensaje}>{mensaje}</div>
            ))}
          </MessageBarBody>
        </MessageBar>
      )}

      <Card className={estilos.seccion}>
        <Text as="h3" size={500} weight="semibold">
          Cabecera
        </Text>
        <SeccionCabecera
          cabecera={estado.cabecera}
          dispatch={dispatch}
          grupos={catalogos.gruposProyecto}
          companias={companias}
          horarios={horarios}
          opciones={opciones}
          errores={erroresCabecera}
        />
      </Card>

      <ListaPrincipales
        principales={estado.principales}
        dispatch={dispatch}
        jornadas={catalogos.jornadas}
        maximo={opciones.maxPrincipales}
        rangoListo={rangoListo}
        advertencias={advertencias}
        errores={erroresFilas}
        errorSeccion={erroresServidor.secciones.principales}
        ayuda={ayudaMinimo}
        onAgregar={() => setBuscador('principal')}
      />

      <ListaBacks
        backs={estado.backs}
        principales={estado.principales}
        dispatch={dispatch}
        maximo={opciones.maxBacks}
        maxDiasDescanso={opciones.backMaxDiasDescanso}
        rangoListo={rangoListo}
        advertencias={advertencias}
        errores={erroresFilas}
        errorSeccion={erroresServidor.secciones.backs}
        onAgregar={() => setBuscador('back')}
      />

      <Card className={estilos.seccion}>
        <Text as="h3" size={500} weight="semibold">
          Vista previa
        </Text>

        {vista === null && (
          <Text size={200}>
            Genere la vista previa para revisar el cronograma y los cruces. Es obligatoria antes de registrar.
          </Text>
        )}

        {vista !== null && !vigente && (
          <MessageBar intent="warning">
            <MessageBarBody>
              <MessageBarTitle>Vista previa desactualizada.</MessageBarTitle>
              El formulario cambió después de generarla. Genere otra vista previa para poder registrar.
            </MessageBarBody>
          </MessageBar>
        )}

        {vista !== null && (
          <div className={vigente ? undefined : estilos.desactualizada}>
            <VistaPrevia datos={vista.datos} />
          </div>
        )}
      </Card>

      {errorEnvio && (
        <MessageBar intent="error">
          <MessageBarBody>
            <MessageBarTitle>
              {errorEnvio.error instanceof ErrorApi ? errorEnvio.error.titulo : 'Ocurrió un error inesperado.'}
            </MessageBarTitle>
            {errorEnvio.error instanceof ErrorApi ? errorEnvio.error.detalle : undefined}
          </MessageBarBody>
          {errorEnvio.reintentar && (
            <MessageBarActions>
              <Button
                disabled={enviando}
                onClick={errorEnvio.reintentar === 'registrar' ? registrar : generarVistaPrevia}
              >
                Reintentar
              </Button>
            </MessageBarActions>
          )}
        </MessageBar>
      )}

      <div className={estilos.barra}>
        <Button icon={<Eye20Regular />} disabled={!habilitadoVistaPrevia} onClick={generarVistaPrevia}>
          {previsualizar.isPending ? 'Generando…' : 'Generar vista previa'}
        </Button>
        <Button
          appearance="primary"
          icon={crear.isPending ? <Spinner size="tiny" /> : <Save20Regular />}
          disabled={!habilitadoRegistrar || previsualizar.isPending}
          onClick={registrar}
        >
          {crear.isPending ? 'Registrando…' : 'Registrar'}
        </Button>
        <Text size={200}>
          {ayudaMinimo !== undefined && !enviando ? ayudaMinimo : motivoSinRegistro(vista, vigente, crear.isPending)}
        </Text>
      </div>

      <BuscadorEmpleados
        abierto={buscador !== null}
        titulo={buscador === 'back' ? 'Agregar back' : 'Agregar principal'}
        etiquetas={etiquetas}
        puedeAgregar={puedeAgregar}
        onSeleccionar={(empleado) =>
          dispatch(
            buscador === 'back'
              ? { tipo: 'agregarBack', empleado, maximo: opciones.maxBacks }
              : { tipo: 'agregarPrincipal', empleado, maximo: opciones.maxPrincipales },
          )
        }
        onCerrar={() => setBuscador(null)}
      />

      <Dialog open={bloqueo.state === 'blocked'} onOpenChange={(_, d) => !d.open && bloqueo.reset?.()}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>¿Salir sin registrar?</DialogTitle>
            <DialogContent>El proyecto no se ha registrado. Si sale ahora, se perderán los datos ingresados.</DialogContent>
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

/** Por qué "Registrar" está deshabilitado (texto junto al botón). */
function motivoSinRegistro(vista: VistaPreviaFormulario | null, vigente: boolean, enviando: boolean): string {
  if (enviando) {
    return ''
  }
  if (vista === null) {
    return 'Para registrar, primero genere la vista previa.'
  }
  if (!vigente) {
    return 'La vista previa está desactualizada.'
  }
  if (vista.datos.cruces.length > 0) {
    return 'No se puede registrar con cruces de asignación.'
  }
  return ''
}

function combinarFilas(
  cliente: Record<string, ErroresFila>,
  servidor: Record<string, ErroresFila>,
): Record<string, ErroresFila> {
  const resultado: Record<string, ErroresFila> = { ...cliente }
  for (const [clave, errores] of Object.entries(servidor)) {
    resultado[clave] = { ...resultado[clave], ...errores }
  }
  return resultado
}
