import {
  Button,
  Card,
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
import { ArrowLeft20Regular, Eye20Regular, Save20Regular } from '@fluentui/react-icons'
import { useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useReducer, useRef, useState } from 'react'
import { Link, useBlocker, useLocation, useNavigate, useParams } from 'react-router'
import { ErrorApi } from '../../../api/errores'
import { EstadoError } from '../../../components/EstadoError'
import { formatearFecha } from '../../../utils/formato'
import { clavesProyectos } from '../api'
import { BuscadorEmpleados } from '../components/BuscadorEmpleados'
import { ListaPersonalEdicion } from '../components/ListaPersonalEdicion'
import { VistaPreviaPersonal } from '../components/VistaPreviaPersonal'
import {
  type AccionEdicionPersonal,
  type ErroresPersonal,
  type FilaEdicion,
  type VistaPersonal,
  ROL_PRINCIPAL,
  SIN_ERRORES_PERSONAL,
  aSolicitudPersonal,
  aSolicitudRegistroPersonal,
  ayudaPersonalEdicion,
  clavesDelEnvioEdicion,
  crearEstadoEdicionPersonal,
  etiquetasPorEmpleadoEdicion,
  interpretarErrorPersonal,
  mensajeExitoPersonal,
  motivoSinRegistroPersonal,
  principalesEnviados,
  backsEnviados,
  puedeGenerarVistaPreviaEdicion,
  puedeRegistrarPersonal,
  reducerEdicionPersonal,
  requiereConfirmacionPersonal,
  validarEdicionPersonal,
  vistaPersonalVigente,
} from '../edicionPersonal'
import type { ErroresFila } from '../formularioProyecto'
import { useCatalogos, useEdicionPersonal, usePrevisualizarPersonal, useProyecto, useRegistrarPersonal } from '../hooks'
import type { EdicionPersonal, JornadaCatalogo, ProyectoDetalle } from '../tipos'
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
  secundario: { color: tokens.colorNeutralForeground3 },
})

/**
 * Ruta /proyectos/:id/personal (TAREA-19b): "Actualizar personal" (ACTUALIZACION_PERSONAL, contrato TAREA-17/19x/19y).
 * Carga la edición, el detalle (principal inicial: P3) y los catálogos (jornadas); luego monta el formulario. "Recargar
 * datos del proyecto" lo vuelve a montar con los datos nuevos.
 */
export function ActualizarPersonal() {
  const estilos = useEstilos()
  const navigate = useNavigate()
  const location = useLocation()
  const queryClient = useQueryClient()
  const { id } = useParams()
  const numero = Number(id)
  const idValido = Number.isInteger(numero) && numero > 0
  const edicion = useEdicionPersonal(numero)
  const proyecto = useProyecto(numero)
  const catalogos = useCatalogos()
  const [recargas, setRecargas] = useState(0)

  const busqueda = (location.state as EstadoNavegacionProyectos | null)?.busqueda ?? ''
  const rutaDetalle = `/proyectos/${numero}`
  const rutaListado = `/proyectos${busqueda}`
  const error = edicion.error ?? proyecto.error ?? catalogos.error
  const noEncontrado = !idValido || (error instanceof ErrorApi && error.tipo === 'noEncontrado')

  const recargar = async () => {
    await queryClient.invalidateQueries({ queryKey: clavesProyectos.todos })
    setRecargas((n) => n + 1) // vuelve a montar el formulario con los datos nuevos
  }

  return (
    <section className={estilos.pagina}>
      <Button
        className={estilos.volver}
        appearance="subtle"
        icon={<ArrowLeft20Regular />}
        onClick={() => void navigate(rutaDetalle, { state: { busqueda } satisfies EstadoNavegacionProyectos })}
      >
        Volver al proyecto
      </Button>

      <Text as="h2" size={600} weight="semibold">
        Actualizar personal{edicion.data ? ` · ${edicion.data.codigo}` : ''}
      </Text>

      {noEncontrado ? (
        <MessageBar intent="warning">
          <MessageBarBody>
            <MessageBarTitle>Proyecto no encontrado o sin acceso</MessageBarTitle>
            <Link to={rutaListado}>Ir al listado de proyectos</Link>
          </MessageBarBody>
        </MessageBar>
      ) : error ? (
        <EstadoError error={error} />
      ) : edicion.data && proyecto.data && catalogos.data ? (
        edicion.data.puedeEditar ? (
          <FormularioPersonal
            key={`${recargas}`}
            dto={edicion.data}
            proyecto={proyecto.data}
            jornadas={catalogos.data.jornadas}
            rutaDetalle={rutaDetalle}
            rutaListado={rutaListado}
            busqueda={busqueda}
            onRecargar={recargar}
          />
        ) : (
          <MessageBar intent="warning">
            <MessageBarBody>
              <MessageBarTitle>No se puede actualizar el personal.</MessageBarTitle>
              {edicion.data.motivo}
            </MessageBarBody>
          </MessageBar>
        )
      ) : (
        <Spinner label="Cargando personal del proyecto…" />
      )}
    </section>
  )
}

type Buscador = { modo: 'agregar'; rol: FilaEdicion['rol'] } | { modo: 'cambiar'; clave: string } | null
type Operacion = 'previsualizar' | 'registrar'

interface FormularioPersonalProps {
  dto: EdicionPersonal
  proyecto: ProyectoDetalle
  jornadas: JornadaCatalogo[]
  rutaDetalle: string
  rutaListado: string
  busqueda: string
  onRecargar: () => Promise<void>
}

function FormularioPersonal({ dto, proyecto, jornadas, rutaDetalle, rutaListado, busqueda, onRecargar }: FormularioPersonalProps) {
  const estilos = useEstilos()
  const navigate = useNavigate()
  const iniciales = useMemo(() => new Set(proyecto.personal.filter((p) => p.esPrincipalInicial).map((p) => p.id)), [proyecto])
  const [estado, despachar] = useReducer(reducerEdicionPersonal, undefined, () => crearEstadoEdicionPersonal(dto, iniciales))
  const [vista, setVista] = useState<VistaPersonal | null>(null)
  const [errores, setErrores] = useState<ErroresPersonal>(SIN_ERRORES_PERSONAL)
  const [ultimaOperacion, setUltimaOperacion] = useState<Operacion>('previsualizar')
  const [buscador, setBuscador] = useState<Buscador>(null)
  const [recargando, setRecargando] = useState(false)
  const previsualizar = usePrevisualizarPersonal(dto.id)
  const registrar = useRegistrarPersonal(dto.id)
  // Refs: se leen en el mismo instante del clic o de la navegación, antes del siguiente render.
  const enviandoRef = useRef(false)
  const registradoRef = useRef(false)
  const enfocarErrorRef = useRef(false)
  const contenedorRef = useRef<HTMLDivElement>(null)

  const limites = dto.limites
  const enviando = previsualizar.isPending || registrar.isPending || recargando
  const vigente = vistaPersonalVigente(vista, estado.revision)

  /** Cada cambio actualiza el formulario y borra los errores del servidor de esa fila. */
  const dispatch = useCallback((accion: AccionEdicionPersonal) => {
    despachar(accion)
    if ('clave' in accion) {
      setErrores((actuales) => {
        if (!actuales.servidor.filas[accion.clave]) {
          return actuales
        }
        const filas = { ...actuales.servidor.filas }
        delete filas[accion.clave]
        return { ...actuales, servidor: { ...actuales.servidor, filas } }
      })
    }
  }, [])

  // ---------------------------------------------------------------- confirmar al salir con cambios (TAREA-13)
  const sucio = estado.editado
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

  // ---------------------------------------------------------------- errores y ayudas
  const erroresCliente = useMemo(() => validarEdicionPersonal(estado), [estado])
  const erroresFilas = useMemo(() => {
    const resultado: Record<string, ErroresFila> = { ...erroresCliente }
    for (const [clave, campos] of Object.entries(errores.servidor.filas)) {
      resultado[clave] = { ...resultado[clave], ...campos } // el mensaje del servidor tiene prioridad
    }
    return resultado
  }, [erroresCliente, errores.servidor.filas])
  const ayuda = ayudaPersonalEdicion(estado, limites.exigePrincipal)
  const generales = [...errores.servidor.generales, ...errores.dialogo.generales]

  // Tras un 400: foco en el primer campo con error (o en el resumen de errores).
  useEffect(() => {
    if (!enfocarErrorRef.current) {
      return
    }
    enfocarErrorRef.current = false
    const contenedor = contenedorRef.current
    const campo = contenedor?.querySelector<HTMLElement>('[aria-invalid="true"]')
    const resumen = contenedor?.querySelector<HTMLElement>('[data-errores-generales]')
    ;(campo ?? resumen)?.focus()
  }, [errores])

  const manejarError = (error: unknown, operacion: Operacion) => {
    if (!(error instanceof ErrorApi)) {
      setErrores({ ...SIN_ERRORES_PERSONAL, dialogo: { ...SIN_ERRORES_PERSONAL.dialogo, generales: ['Ocurrió un error inesperado.'] } })
      return
    }
    const interpretado = interpretarErrorPersonal(error, clavesDelEnvioEdicion(estado))
    if (interpretado.cruces) {
      // 409 con cruces que aparecieron después de la vista previa: se muestran y "Registrar" queda deshabilitado.
      setVista((actual) => actual && { ...actual, datos: { ...actual.datos, ...interpretado.cruces } })
    }
    if (error.tipo === 'validacion') {
      enfocarErrorRef.current = true
    }
    setUltimaOperacion(operacion)
    setErrores(interpretado)
  }

  // ---------------------------------------------------------------- vista previa y registro
  const generarVistaPrevia = () => {
    if (!puedeGenerarVistaPreviaEdicion(estado, enviando, limites.exigePrincipal)) {
      return
    }
    const revision = estado.revision
    setErrores(SIN_ERRORES_PERSONAL)
    previsualizar.mutate(aSolicitudPersonal(estado), {
      onSuccess: (datos) => setVista({ revision, datos }),
      onError: (error) => {
        setVista(null)
        manejarError(error, 'previsualizar')
      },
    })
  }

  const confirmarRegistro = () => {
    // El ref evita un doble envío aunque el segundo clic llegue antes de que se deshabilite el botón.
    if (enviandoRef.current || !puedeRegistrarPersonal(vista, estado, enviando)) {
      return
    }
    enviandoRef.current = true
    setErrores(SIN_ERRORES_PERSONAL)
    registrar.mutate(aSolicitudRegistroPersonal(estado, vista!), {
      onSuccess: (r) => {
        registradoRef.current = true // desactiva la confirmación al salir
        const destino: EstadoNavegacionProyectos = { busqueda, aviso: mensajeExitoPersonal(r.version), pestana: 'historial' }
        void navigate(rutaDetalle, { replace: true, state: destino })
      },
      onError: (error) => manejarError(error, 'registrar'),
      onSettled: () => {
        enviandoRef.current = false
      },
    })
  }

  /** 400 versionProyecto/proyecto y 409: recargar y empezar de nuevo (no navega: no pide confirmación). */
  const recargar = async () => {
    setRecargando(true)
    try {
      await onRecargar()
    } finally {
      setRecargando(false)
    }
  }

  // ---------------------------------------------------------------- buscador
  const etiquetas = useMemo(() => etiquetasPorEmpleadoEdicion(estado), [estado])
  const puedeAgregar =
    buscador?.modo === 'agregar'
      ? (buscador.rol === ROL_PRINCIPAL ? principalesEnviados(estado) : backsEnviados(estado)).length <
        (buscador.rol === ROL_PRINCIPAL ? limites.maxPrincipales : limites.maxBacks)
      : true

  const habilitadoVistaPrevia = puedeGenerarVistaPreviaEdicion(estado, enviando, limites.exigePrincipal)
  const habilitadoRegistrar = puedeRegistrarPersonal(vista, estado, enviando)
  const confirmacion = vigente && requiereConfirmacionPersonal(vista!.datos, estado)

  return (
    <div ref={contenedorRef} className={estilos.seccion}>
      <Text className={estilos.secundario}>
        Proyecto {formatearFecha(dto.fechaInicio)} – {formatearFecha(dto.fechaFin)} · Corte {formatearFecha(dto.corte)}: las
        personas que ya terminaron son históricas (solo lectura); las que ya empezaron solo pueden acortar su fin.
      </Text>

      {generales.length > 0 && (
        <MessageBar intent="error">
          <MessageBarBody tabIndex={-1} data-errores-generales="">
            {generales.map((m) => (
              <div key={m}>{m}</div>
            ))}
            {errores.dialogo.noEncontrado && <Link to={rutaListado}>Ir al listado de proyectos</Link>}
          </MessageBarBody>
          {errores.dialogo.ofrecerReintento && (
            <MessageBarActions>
              <Button disabled={enviando} onClick={ultimaOperacion === 'registrar' ? confirmarRegistro : generarVistaPrevia}>
                Reintentar
              </Button>
            </MessageBarActions>
          )}
        </MessageBar>
      )}

      {errores.dialogo.ofrecerRecarga && (
        <MessageBar intent="warning">
          <MessageBarBody>
            <MessageBarTitle>Los datos del proyecto pueden haber cambiado.</MessageBarTitle>
            Recargue para continuar con la información actual (se descartan los cambios del formulario).
          </MessageBarBody>
          <MessageBarActions>
            <Button disabled={enviando} onClick={() => void recargar()}>
              Recargar datos del proyecto
            </Button>
          </MessageBarActions>
        </MessageBar>
      )}

      {ayuda && <Text>{ayuda}</Text>}

      <ListaPersonalEdicion
        estado={estado}
        dispatch={dispatch}
        jornadas={jornadas}
        limites={limites}
        errores={erroresFilas}
        secciones={errores.servidor.secciones}
        onAgregar={(rol) => setBuscador({ modo: 'agregar', rol })}
        onCambiarEmpleado={(clave) => setBuscador({ modo: 'cambiar', clave })}
      />

      <Card className={estilos.seccion}>
        <Text as="h3" size={500} weight="semibold">
          Vista previa
        </Text>

        {vista === null && (
          <Text size={200}>Genere la vista previa para revisar los cambios, el cronograma y los cruces. Es obligatoria antes de registrar.</Text>
        )}

        {vista !== null && !vigente && (
          <MessageBar intent="warning">
            <MessageBarBody>
              <MessageBarTitle>Vista previa desactualizada.</MessageBarTitle>
              El personal cambió después de generarla. Genere otra vista previa para poder registrar.
            </MessageBarBody>
          </MessageBar>
        )}

        {vista !== null && (
          <div className={vigente ? undefined : estilos.desactualizada}>
            <VistaPreviaPersonal datos={vista.datos} />
          </div>
        )}

        {confirmacion && (
          <Checkbox
            checked={estado.entiende}
            disabled={enviando}
            label="Entiendo que esta acción no se puede deshacer."
            onChange={(_, d) => despachar({ tipo: 'entiende', valor: d.checked === true })}
          />
        )}
      </Card>

      <div className={estilos.barra}>
        <Button icon={<Eye20Regular />} disabled={!habilitadoVistaPrevia} onClick={generarVistaPrevia}>
          {previsualizar.isPending ? 'Generando…' : 'Generar vista previa'}
        </Button>
        <Button
          appearance="primary"
          icon={registrar.isPending ? <Spinner size="tiny" /> : <Save20Regular />}
          disabled={!habilitadoRegistrar}
          onClick={confirmarRegistro}
        >
          {registrar.isPending ? 'Registrando…' : 'Registrar'}
        </Button>
        <Text size={200}>{ayuda && !enviando ? ayuda : motivoSinRegistroPersonal(vista, estado, enviando)}</Text>
      </div>

      <BuscadorEmpleados
        abierto={buscador !== null}
        titulo={
          buscador?.modo === 'cambiar' ? 'Cambiar empleado' : buscador?.rol === ROL_PRINCIPAL ? 'Agregar principal' : 'Agregar back'
        }
        etiquetas={etiquetas}
        puedeAgregar={puedeAgregar}
        onSeleccionar={(empleado) => {
          if (buscador?.modo === 'cambiar') {
            dispatch({ tipo: 'cambiarEmpleado', clave: buscador.clave, empleado })
            setBuscador(null)
          } else if (buscador?.modo === 'agregar') {
            dispatch({
              tipo: 'agregar',
              rol: buscador.rol,
              empleado,
              maximo: buscador.rol === ROL_PRINCIPAL ? limites.maxPrincipales : limites.maxBacks,
            })
          }
        }}
        onCerrar={() => setBuscador(null)}
      />

      <Dialog open={bloqueo.state === 'blocked'} onOpenChange={(_, d) => !d.open && bloqueo.reset?.()}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>¿Salir sin registrar?</DialogTitle>
            <DialogContent>Hay cambios en el personal sin registrar. Si sale ahora, se perderán.</DialogContent>
            <DialogActions>
              <Button onClick={() => bloqueo.reset?.()}>Seguir editando</Button>
              <Button appearance="primary" onClick={() => bloqueo.proceed?.()}>
                Salir sin guardar
              </Button>
            </DialogActions>
          </DialogBody>
        </DialogSurface>
      </Dialog>
    </div>
  )
}
