import {
  Button,
  Card,
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
  Spinner,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components'
import { ArrowLeft20Regular, Eye20Regular, Play20Regular } from '@fluentui/react-icons'
import { useQueryClient } from '@tanstack/react-query'
import { useCallback, useEffect, useMemo, useReducer, useRef, useState } from 'react'
import { Link, useBlocker, useLocation, useNavigate, useParams } from 'react-router'
import { ErrorApi } from '../../../api/errores'
import { EstadoError } from '../../../components/EstadoError'
import { SelectorFecha } from '../../../components/SelectorFecha'
import { formatearFecha, hoyEnNegocio } from '../../../utils/formato'
import { clavesProyectos } from '../api'
import { BuscadorEmpleados } from '../components/BuscadorEmpleados'
import { ListaPersonalEdicion } from '../components/ListaPersonalEdicion'
import { VistaPreviaReactivacion } from '../components/VistaPreviaReactivacion'
import { type AccionEdicionPersonal, type FilaEdicion, ROL_PRINCIPAL, backsEnviados, etiquetasPorEmpleadoEdicion, principalesEnviados } from '../edicionPersonal'
import type { ErroresFila } from '../formularioProyecto'
import { useCatalogos, usePrevisualizarReactivacion, useProyecto, useReactivacion, useRegistrarReactivacion } from '../hooks'
import {
  type ErroresReactivacion,
  MOTIVO_INICIO_EN_R,
  SIN_ERRORES_REACTIVACION,
  type VistaReactivacion,
  aSolicitudReactivar,
  aSolicitudRegistroReactivacion,
  avisosFilaReactivacion,
  ayudaBacksEnR,
  ayudaReactivacion,
  claveInicialReactivacion,
  clavesDelEnvioReactivacion,
  crearEstadoReactivacion,
  finAntesDeSuspension,
  interpretarErrorReactivacion,
  mensajeExitoReactivacion,
  motivoSinRegistroReactivacion,
  puedeGenerarVistaPreviaReactivacion,
  puedeRegistrarReactivacion,
  reactivacionEditada,
  reducerReactivacion,
  revisionReactivacion,
  validarFechasReactivacion,
  validarPersonalReactivacion,
  vistaReactivacionVigente,
} from '../reactivacion'
import type { JornadaCatalogo, Reactivacion } from '../tipos'
import { MENSAJE_CAMBIO_POR_OTRO, cambioPorOtro } from '../tokenConcurrencia'
import type { EstadoNavegacionProyectos } from './ListadoProyectos'

const useEstilos = makeStyles({
  pagina: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL },
  volver: { alignSelf: 'flex-start' },
  seccion: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
  fechas: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(min(100%, 260px), 1fr))',
    gap: tokens.spacingHorizontalL,
  },
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
 * Ruta /proyectos/:id/reactivar (TAREA-19c): "Reactivar proyecto" (SUSPENDIDO → ACTIVO, contrato TAREA-17b/19x/19y).
 * Carga la reactivación, los catálogos (jornadas) y el detalle (solo para proponer la fecha fin anterior a la
 * suspensión, P3: si el detalle falla, la fecha fin queda vacía sin bloquear la pantalla); luego monta el formulario.
 * "Recargar datos del proyecto" lo vuelve a montar con los datos nuevos (y su token base).
 */
export function ReactivarProyecto() {
  const estilos = useEstilos()
  const navigate = useNavigate()
  const location = useLocation()
  const queryClient = useQueryClient()
  const { id } = useParams()
  const numero = Number(id)
  const idValido = Number.isInteger(numero) && numero > 0
  const reactivacion = useReactivacion(numero)
  const proyecto = useProyecto(numero)
  const catalogos = useCatalogos()
  const [recargas, setRecargas] = useState(0)

  const busqueda = (location.state as EstadoNavegacionProyectos | null)?.busqueda ?? ''
  const rutaDetalle = `/proyectos/${numero}`
  const rutaListado = `/proyectos${busqueda}`
  const error = reactivacion.error ?? catalogos.error
  const noEncontrado = !idValido || (error instanceof ErrorApi && error.tipo === 'noEncontrado')
  // P3: el detalle no bloquea; si falla, no hay fecha fin planificada.
  const detalleListo = !proyecto.isPending
  const finPlanificado = proyecto.data ? finAntesDeSuspension(proyecto.data.etapas) : null

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
        Reactivar proyecto{reactivacion.data ? ` · ${reactivacion.data.codigo}` : ''}
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
      ) : reactivacion.data && catalogos.data && detalleListo ? (
        reactivacion.data.puedeReactivar ? (
          <FormularioReactivacion
            key={`${recargas}`}
            dto={reactivacion.data}
            finPlanificado={finPlanificado}
            jornadas={catalogos.data.jornadas}
            rutaDetalle={rutaDetalle}
            rutaListado={rutaListado}
            busqueda={busqueda}
            onRecargar={recargar}
          />
        ) : (
          <MessageBar intent="warning">
            <MessageBarBody>
              <MessageBarTitle>No se puede reactivar el proyecto.</MessageBarTitle>
              {reactivacion.data.motivo}
            </MessageBarBody>
          </MessageBar>
        )
      ) : (
        <Spinner label="Cargando datos de la reactivación…" />
      )}
    </section>
  )
}

type Buscador = { modo: 'agregar'; rol: FilaEdicion['rol'] } | { modo: 'cambiar'; clave: string } | null
type Operacion = 'previsualizar' | 'registrar'

interface FormularioReactivacionProps {
  dto: Reactivacion
  finPlanificado: string | null
  jornadas: JornadaCatalogo[]
  rutaDetalle: string
  rutaListado: string
  busqueda: string
  onRecargar: () => Promise<void>
}

function FormularioReactivacion({ dto, finPlanificado, jornadas, rutaDetalle, rutaListado, busqueda, onRecargar }: FormularioReactivacionProps) {
  const estilos = useEstilos()
  const navigate = useNavigate()
  const [estado, despachar] = useReducer(reducerReactivacion, undefined, () =>
    crearEstadoReactivacion(dto, hoyEnNegocio(), finPlanificado),
  )
  const [vista, setVista] = useState<VistaReactivacion | null>(null)
  const [errores, setErrores] = useState<ErroresReactivacion>(SIN_ERRORES_REACTIVACION)
  const [ultimaOperacion, setUltimaOperacion] = useState<Operacion>('previsualizar')
  const [buscador, setBuscador] = useState<Buscador>(null)
  const [recargando, setRecargando] = useState(false)
  const [confirmarRecarga, setConfirmarRecarga] = useState(false)
  const [errorFormatoFecha, setErrorFormatoFecha] = useState<string>()
  const [errorFormatoFin, setErrorFormatoFin] = useState<string>()
  const previsualizar = usePrevisualizarReactivacion(dto.id)
  const registrar = useRegistrarReactivacion(dto.id)
  // Refs: se leen en el mismo instante del clic o de la navegación, antes del siguiente render.
  const enviandoRef = useRef(false)
  const registradoRef = useRef(false)
  const enfocarErrorRef = useRef(false)
  const contenedorRef = useRef<HTMLDivElement>(null)

  const limites = dto.limites
  const enviando = previsualizar.isPending || registrar.isPending || recargando
  const vigente = vistaReactivacionVigente(vista, estado)
  const editado = reactivacionEditada(estado)

  /** Cada cambio del personal actualiza el formulario y borra los errores del servidor de esa fila. */
  const dispatchPersonal = useCallback((accion: AccionEdicionPersonal) => {
    despachar({ tipo: 'personal', accion })
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

  const cambiarFecha = (campo: 'fecha' | 'fechaFin', valor: string | null) => {
    despachar(campo === 'fecha' ? { tipo: 'fecha', fecha: valor } : { tipo: 'fechaFin', fechaFin: valor })
    setErrores((actuales) => ({ ...actuales, campos: { ...actuales.campos, [campo]: undefined } }))
  }

  // ---------------------------------------------------------------- confirmar al salir con cambios (TAREA-13)
  const bloqueo = useBlocker(
    ({ currentLocation, nextLocation }) => editado && !registradoRef.current && currentLocation.pathname !== nextLocation.pathname,
  )

  useEffect(() => {
    if (!editado) {
      return
    }
    const alSalir = (evento: BeforeUnloadEvent) => {
      if (!registradoRef.current) {
        evento.preventDefault()
      }
    }
    window.addEventListener('beforeunload', alSalir)
    return () => window.removeEventListener('beforeunload', alSalir)
  }, [editado])

  // ---------------------------------------------------------------- errores y ayudas
  const erroresFechas = validarFechasReactivacion(estado)
  const erroresCliente = useMemo(() => validarPersonalReactivacion(estado), [estado])
  const erroresFilas = useMemo(() => {
    const resultado: Record<string, ErroresFila> = { ...erroresCliente }
    for (const [clave, campos] of Object.entries(errores.servidor.filas)) {
      resultado[clave] = { ...resultado[clave], ...campos } // el mensaje del servidor tiene prioridad
    }
    return resultado
  }, [erroresCliente, errores.servidor.filas])
  const ayuda = ayudaReactivacion(estado, limites.exigePrincipal)
  const ayudaBacks = ayudaBacksEnR(estado)
  const generales = [...errores.servidor.generales, ...errores.dialogo.generales]
  const claveInicial = claveInicialReactivacion(estado)

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
      setErrores({
        ...SIN_ERRORES_REACTIVACION,
        dialogo: { ...SIN_ERRORES_REACTIVACION.dialogo, generales: ['Ocurrió un error inesperado.'] },
      })
      return
    }
    const interpretado = interpretarErrorReactivacion(error, clavesDelEnvioReactivacion(estado))
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
    if (!puedeGenerarVistaPreviaReactivacion(estado, enviando, limites.exigePrincipal)) {
      return
    }
    const revision = revisionReactivacion(estado)
    setErrores(SIN_ERRORES_REACTIVACION)
    previsualizar.mutate(aSolicitudReactivar(estado), {
      onSuccess: (datos) => setVista({ revision, datos }),
      onError: (error) => {
        setVista(null)
        manejarError(error, 'previsualizar')
      },
    })
  }

  const confirmarRegistro = () => {
    // El ref evita un doble envío aunque el segundo clic llegue antes de que se deshabilite el botón (P8).
    if (enviandoRef.current || !puedeRegistrarReactivacion(vista, estado, enviando)) {
      return
    }
    enviandoRef.current = true
    setErrores(SIN_ERRORES_REACTIVACION)
    registrar.mutate(aSolicitudRegistroReactivacion(estado), {
      onSuccess: (r) => {
        registradoRef.current = true // desactiva la confirmación al salir
        const destino: EstadoNavegacionProyectos = { busqueda, aviso: mensajeExitoReactivacion(r.version), pestana: 'historial' }
        void navigate(rutaDetalle, { replace: true, state: destino })
      },
      onError: (error) => manejarError(error, 'registrar'),
      onSettled: () => {
        enviandoRef.current = false
      },
    })
  }

  /** "Recargar datos del proyecto": con cambios sin registrar pide confirmación (P2 de la 19b2); no navega. */
  const pedirRecarga = () => {
    if (editado) {
      setConfirmarRecarga(true)
    } else {
      void recargar()
    }
  }

  const recargar = async () => {
    setRecargando(true)
    try {
      await onRecargar()
    } finally {
      setRecargando(false)
    }
  }

  // ---------------------------------------------------------------- buscador
  const etiquetas = useMemo(() => etiquetasPorEmpleadoEdicion(estado.personal), [estado.personal])
  const puedeAgregar =
    buscador?.modo === 'agregar'
      ? (buscador.rol === ROL_PRINCIPAL ? principalesEnviados(estado.personal) : backsEnviados(estado.personal)).length <
        (buscador.rol === ROL_PRINCIPAL ? limites.maxPrincipales : limites.maxBacks)
      : true

  const habilitadoVistaPrevia = puedeGenerarVistaPreviaReactivacion(estado, enviando, limites.exigePrincipal)
  const habilitadoRegistrar = puedeRegistrarReactivacion(vista, estado, enviando)
  const minimaFin = estado.fecha ?? estado.fechaMinima

  return (
    <div ref={contenedorRef} className={estilos.seccion}>
      <Text className={estilos.secundario}>
        Proyecto SUSPENDIDO: inicio {formatearFecha(dto.fechaInicio)}, fecha fin actual {formatearFecha(dto.fechaFinActual)}. Todo el
        personal guardado queda histórico (solo lectura). Agregue las personas que trabajarán desde la fecha de reactivación:
        el primer principal empieza en esa fecha y será el principal inicial (responsable).
      </Text>

      {dto.advertencias.length > 0 && (
        <MessageBar intent="warning">
          <MessageBarBody>
            {dto.advertencias.map((a) => (
              <div key={a}>{a}</div>
            ))}
          </MessageBarBody>
        </MessageBar>
      )}

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
            <Button disabled={enviando} onClick={pedirRecarga}>
              Recargar datos del proyecto
            </Button>
          </MessageBarActions>
        </MessageBar>
      )}

      <Card className={estilos.seccion}>
        <Text as="h3" size={500} weight="semibold">
          Fechas
        </Text>
        <div className={estilos.fechas}>
          <Field
            label="Fecha de reactivación"
            required
            hint={`Desde ${formatearFecha(dto.fechaMinima)} (día siguiente a la fecha fin actual).`}
            validationMessage={errorFormatoFecha ?? errores.campos.fecha ?? erroresFechas.fecha}
          >
            <SelectorFecha
              valor={estado.fecha ?? undefined}
              fechaMinima={dto.fechaMinima}
              onCambiar={(valor) => cambiarFecha('fecha', valor ?? null)}
              onErrorFormato={setErrorFormatoFecha}
            />
          </Field>
          <Field
            label="Nueva fecha fin del proyecto"
            required
            hint={`Desde ${formatearFecha(minimaFin)}.`}
            validationMessage={errorFormatoFin ?? errores.campos.fechaFin ?? erroresFechas.fechaFin}
          >
            <SelectorFecha
              valor={estado.fechaFin ?? undefined}
              fechaMinima={minimaFin}
              onCambiar={(valor) => cambiarFecha('fechaFin', valor ?? null)}
              onErrorFormato={setErrorFormatoFin}
            />
          </Field>
        </div>
      </Card>

      {ayuda && <Text>{ayuda}</Text>}

      <ListaPersonalEdicion
        estado={estado.personal}
        dispatch={dispatchPersonal}
        jornadas={jornadas}
        limites={limites}
        errores={erroresFilas}
        secciones={errores.servidor.secciones}
        onAgregar={(rol) => setBuscador({ modo: 'agregar', rol })}
        onCambiarEmpleado={(clave) => setBuscador({ modo: 'cambiar', clave })}
        claveInicial={claveInicial}
        claveInicioFijo={claveInicial}
        motivoInicioFijo={MOTIVO_INICIO_EN_R}
        fechaMinimaInicio={estado.fecha ?? undefined}
        fechaMinimaFinNuevas={estado.fecha ?? undefined}
        fechaMaxima={estado.fechaFin}
        inicialesHistoricas={estado.inicialesHistoricas}
        avisosFila={avisosFilaReactivacion(estado)}
        ayudasSeccion={{ backs: ayudaBacks }}
        textoSinFilas={{ principales: 'Sin principales nuevos.', backs: 'Sin backs nuevos.' }}
      />

      <Card className={estilos.seccion}>
        <Text as="h3" size={500} weight="semibold">
          Vista previa
        </Text>

        {vista === null && (
          <Text size={200}>Genere la vista previa para revisar la actividad, el cronograma y los cruces. Es obligatoria antes de registrar.</Text>
        )}

        {/* TAREA-19b2: la vista previa trae otra versión que la de los datos con que se abrió: no se registra. */}
        {vigente && cambioPorOtro(vista, estado.versionBase) && (
          <MessageBar intent="warning">
            <MessageBarBody>
              <MessageBarTitle>{MENSAJE_CAMBIO_POR_OTRO}</MessageBarTitle>
            </MessageBarBody>
            <MessageBarActions>
              <Button disabled={enviando} onClick={pedirRecarga}>
                Recargar datos del proyecto
              </Button>
            </MessageBarActions>
          </MessageBar>
        )}

        {vista !== null && !vigente && (
          <MessageBar intent="warning">
            <MessageBarBody>
              <MessageBarTitle>Vista previa desactualizada.</MessageBarTitle>
              Las fechas o el personal cambiaron después de generarla. Genere otra vista previa para poder registrar.
            </MessageBarBody>
          </MessageBar>
        )}

        {vista !== null && (
          <div className={vigente ? undefined : estilos.desactualizada}>
            <VistaPreviaReactivacion datos={vista.datos} />
          </div>
        )}
      </Card>

      <div className={estilos.barra}>
        <Button icon={<Eye20Regular />} disabled={!habilitadoVistaPrevia} onClick={generarVistaPrevia}>
          {previsualizar.isPending ? 'Generando…' : 'Generar vista previa'}
        </Button>
        <Button
          appearance="primary"
          icon={registrar.isPending ? <Spinner size="tiny" /> : <Play20Regular />}
          disabled={!habilitadoRegistrar}
          onClick={confirmarRegistro}
        >
          {registrar.isPending ? 'Registrando…' : 'Registrar'}
        </Button>
        <Text size={200} weight="semibold">
          Esta acción no se puede deshacer.
        </Text>
        <Text size={200}>{ayuda && !enviando ? ayuda : motivoSinRegistroReactivacion(vista, estado, enviando)}</Text>
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
            dispatchPersonal({ tipo: 'cambiarEmpleado', clave: buscador.clave, empleado })
            setBuscador(null)
          } else if (buscador?.modo === 'agregar') {
            dispatchPersonal({
              tipo: 'agregar',
              rol: buscador.rol,
              empleado,
              maximo: buscador.rol === ROL_PRINCIPAL ? limites.maxPrincipales : limites.maxBacks,
            })
          }
        }}
        onCerrar={() => setBuscador(null)}
      />

      <Dialog open={confirmarRecarga} onOpenChange={(_, d) => !d.open && setConfirmarRecarga(false)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>¿Recargar los datos del proyecto?</DialogTitle>
            <DialogContent>Se perderán los cambios que no registraste.</DialogContent>
            <DialogActions>
              <Button onClick={() => setConfirmarRecarga(false)}>Seguir editando</Button>
              <Button
                appearance="primary"
                onClick={() => {
                  setConfirmarRecarga(false)
                  void recargar()
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
            <DialogTitle>¿Salir sin registrar?</DialogTitle>
            <DialogContent>Hay cambios en la reactivación sin registrar. Si sale ahora, se perderán.</DialogContent>
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
