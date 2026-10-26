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
import { ArrowLeft20Regular, Save20Regular } from '@fluentui/react-icons'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState } from 'react'
import { Link, useBlocker, useLocation, useNavigate, useParams } from 'react-router'
import { ErrorApi } from '../../../api/errores'
import { EstadoError } from '../../../components/EstadoError'
import { formatearFecha } from '../../../utils/formato'
import { clavesNovedades } from '../api'
import { CamposNovedad, type ValoresCamposNovedad } from '../components/CamposNovedad'
import { SelectorDiasExcluidos } from '../components/SelectorDiasExcluidos'
import { diasDelRango, excedeMaximo } from '../diasNovedad'
import {
  MENSAJE_SIN_CAMBIOS,
  type CampoEdicion,
  type EstadoEdicion,
  aSolicitudEditar,
  alternarDia,
  conRango,
  estadoInicial,
  hayCambios,
  incluirTodos,
  resumenCambios,
  validarEdicion,
} from '../edicionNovedad'
import { CAMPOS_EDICION, type ErroresNovedad, MENSAJE_NOVEDAD_NO_ENCONTRADA, interpretarErrorDesconocido } from '../erroresNovedad'
import { useEditarNovedad, useNovedad, useOpcionesNovedad } from '../hooks'
import { textoSujeto, useNombresNovedad } from '../presentacion'
import type { AplicacionNovedad, NovedadDetalle, TipoNovedadResumen } from '../tipos'
import type { EstadoNavegacionNovedades } from './ListadoNovedades'

const MOVIL = '@media (max-width: 599px)'

const useEstilos = makeStyles({
  pagina: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL, maxWidth: '720px' },
  formulario: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
  boton: { alignSelf: 'flex-start', [MOVIL]: { alignSelf: 'stretch' } },
  fijos: { padding: tokens.spacingHorizontalL, display: 'grid', gridTemplateColumns: 'max-content 1fr', gap: tokens.spacingHorizontalL },
  acciones: { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: tokens.spacingHorizontalM },
  secundario: { color: tokens.colorNeutralForeground3 },
  resumen: { margin: 0, paddingLeft: tokens.spacingHorizontalL },
})

/**
 * Ruta /novedades/:id/editar (TAREA-24c, U1). Carga el detalle y las opciones y monta el formulario con esos datos; el
 * formulario no cambia aunque el detalle se refresque en segundo plano. «Recargar» lo vuelve a montar con datos nuevos.
 */
export function EditarNovedad() {
  const estilos = useEstilos()
  const navigate = useNavigate()
  const location = useLocation()
  const queryClient = useQueryClient()
  const { id } = useParams()
  const numero = Number(id)
  const idValido = Number.isInteger(numero) && numero > 0
  const detalle = useNovedad(numero)
  const opciones = useOpcionesNovedad()
  const [recargas, setRecargas] = useState(0)

  const estadoNavegacion = location.state as EstadoNavegacionNovedades | null
  const rutaDetalle = `/novedades/${numero}`
  const error = detalle.error ?? opciones.error
  const noEncontrado = !idValido || (error instanceof ErrorApi && error.tipo === 'noEncontrado')

  const recargar = async () => {
    await queryClient.invalidateQueries({ queryKey: clavesNovedades.detalle(numero) })
    setRecargas((n) => n + 1) // vuelve a montar el formulario con los datos nuevos
  }

  return (
    <section className={estilos.pagina}>
      <Button
        className={estilos.boton}
        icon={<ArrowLeft20Regular />}
        onClick={() => void navigate(rutaDetalle, { state: { busqueda: estadoNavegacion?.busqueda } })}
      >
        Volver a la novedad
      </Button>
      <Text as="h2" size={600} weight="semibold">
        Editar novedad{detalle.data ? ` · ${detalle.data.codigo}` : ''}
      </Text>

      {noEncontrado ? (
        <MessageBar intent="warning">
          <MessageBarBody>
            <MessageBarTitle>{MENSAJE_NOVEDAD_NO_ENCONTRADA}</MessageBarTitle>
            <Link to="/novedades">Ir al listado de novedades</Link>
          </MessageBarBody>
        </MessageBar>
      ) : error ? (
        <>
          <EstadoError error={error} />
          <Button
            className={estilos.boton}
            onClick={() => {
              void detalle.refetch()
              void opciones.refetch()
            }}
          >
            Reintentar
          </Button>
        </>
      ) : detalle.data && opciones.data ? (
        detalle.data.puedeEditar ? (
          <FormularioEdicion
            key={recargas}
            novedad={detalle.data}
            tiposElegibles={opciones.data.tiposPorAplicacion[detalle.data.aplicacion as AplicacionNovedad] ?? []}
            maxDias={opciones.data.maxDias}
            busqueda={estadoNavegacion?.busqueda}
            onRecargar={recargar}
          />
        ) : (
          // Lo decide el backend (N3): el cliente no infiere permisos.
          <MessageBar intent="warning">
            <MessageBarBody>
              <MessageBarTitle>No se puede editar esta novedad.</MessageBarTitle>
              {detalle.data.motivoNoEditable}
            </MessageBarBody>
          </MessageBar>
        )
      ) : (
        <Spinner label="Cargando novedad…" labelPosition="after" />
      )}
    </section>
  )
}

interface FormularioEdicionProps {
  novedad: NovedadDetalle
  tiposElegibles: TipoNovedadResumen[]
  maxDias: number
  busqueda?: string
  onRecargar: () => Promise<void>
}

function FormularioEdicion({ novedad, tiposElegibles, maxDias, busqueda, onRecargar }: FormularioEdicionProps) {
  const estilos = useEstilos()
  const navigate = useNavigate()
  const nombres = useNombresNovedad()
  const editar = useEditarNovedad(novedad.id)

  // Datos con que se abrió el formulario (patrón de la 19b2): su `version` es la que se envía.
  const [original] = useState(novedad)
  const [estado, setEstado] = useState<EstadoEdicion>(() => estadoInicial(novedad))
  const [recortados, setRecortados] = useState<string[]>([])
  const [errorEnvio, setErrorEnvio] = useState<ErroresNovedad<CampoEdicion> | null>(null)
  const [confirmarRecarga, setConfirmarRecarga] = useState(false)
  const [recargando, setRecargando] = useState(false)
  const enviandoRef = useRef(false)
  const guardadoRef = useRef(false)

  // El tipo guardado que ya no es elegible (RN16) se muestra igual, marcado; el backend respondería 400 si se envía.
  const tipos = tiposElegibles.some((t) => t.codigo === original.tipo.codigo)
    ? tiposElegibles
    : [...tiposElegibles, { ...original.tipo, nombre: `${original.tipo.nombre} (no disponible)` }]
  const nombreTipo = (codigo: string) => tipos.find((t) => t.codigo === codigo)?.nombre ?? codigo

  const cambios = hayCambios(original, estado, maxDias)
  const erroresCliente = validarEdicion(estado, maxDias)
  const errores = { ...erroresCliente, ...errorEnvio?.campos } // el mensaje del servidor tiene prioridad
  const valido = Object.keys(erroresCliente).length === 0
  const resumen = cambios ? resumenCambios(original, estado, nombreTipo, maxDias) : []
  const diasRango =
    estado.fechaInicio && estado.fechaFin && !excedeMaximo(estado.fechaInicio, estado.fechaFin, maxDias)
      ? diasDelRango(estado.fechaInicio, estado.fechaFin)
      : []

  // ---------------------------------------------------------------- confirmar al salir con cambios (patrón de la 19b)
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

  /** Aplica un cambio y borra el error del servidor de los campos tocados. */
  const cambiar = (siguiente: EstadoEdicion, campos: CampoEdicion[]) => {
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

  const cambiarCampos = (c: Partial<ValoresCamposNovedad>) => {
    if ('fechaInicio' in c || 'fechaFin' in c) {
      // A4: al acortar el rango se quitan los excluidos que quedan fuera.
      const r = conRango(estado, 'fechaInicio' in c ? c.fechaInicio : estado.fechaInicio, 'fechaFin' in c ? c.fechaFin : estado.fechaFin)
      setRecortados(r.recortados)
      cambiar(r.estado, ['fechaInicio', 'fechaFin', 'diasExcluidos'])
      return
    }
    if ('tipo' in c) {
      cambiar({ ...estado, tipo: c.tipo }, ['tipoNovedad'])
    }
    if ('observacion' in c) {
      cambiar({ ...estado, observacion: c.observacion ?? '' }, ['observacion'])
    }
  }

  const guardar = () => {
    // El ref evita un doble envío aunque el segundo clic llegue antes de que se deshabilite el botón.
    if (enviandoRef.current || editar.isPending || !valido || !cambios) {
      return
    }
    enviandoRef.current = true
    setErrorEnvio(null)
    editar.mutate(aSolicitudEditar(estado, original.version), {
      onSuccess: () => {
        guardadoRef.current = true // desactiva la confirmación al salir
        const estadoNavegacion: EstadoNavegacionNovedades = { busqueda, aviso: `Novedad ${original.codigo} actualizada.` }
        void navigate(`/novedades/${original.id}`, { replace: true, state: estadoNavegacion })
      },
      onError: (error) => setErrorEnvio(interpretarErrorDesconocido(error, CAMPOS_EDICION)),
      onSettled: () => {
        enviandoRef.current = false
      },
    })
  }

  /** «Recargar» (409, 400 `version`): con cambios sin guardar pide confirmación (TAREA-19b2, P2). */
  const pedirRecarga = () => (cambios ? setConfirmarRecarga(true) : void recargar())
  const recargar = async () => {
    setConfirmarRecarga(false)
    setRecargando(true)
    try {
      await onRecargar()
    } finally {
      setRecargando(false)
    }
  }

  return (
    <div className={estilos.formulario}>
      {errorEnvio && errorEnvio.generales.length > 0 && (
        <MessageBar intent="error">
          <MessageBarBody>
            {errorEnvio.generales.map((mensaje) => (
              <div key={mensaje}>{mensaje}</div>
            ))}
            {errorEnvio.codigoDuplicado && (
              <Link to={`/novedades?texto=${encodeURIComponent(errorEnvio.codigoDuplicado)}`}>Ver {errorEnvio.codigoDuplicado}</Link>
            )}
          </MessageBarBody>
          {(errorEnvio.ofrecerRecarga || errorEnvio.ofrecerReintento) && (
            <MessageBarActions>
              {errorEnvio.ofrecerRecarga && (
                <Button disabled={recargando} onClick={pedirRecarga}>
                  Recargar
                </Button>
              )}
              {errorEnvio.ofrecerReintento && (
                <Button disabled={editar.isPending} onClick={guardar}>
                  Reintentar
                </Button>
              )}
            </MessageBarActions>
          )}
        </MessageBar>
      )}

      {/* N8: aplicación y sujeto no se editan. Sin cédula ni correo. */}
      <Card className={estilos.fijos}>
        <Text weight="semibold">Aplicación</Text>
        <Text>{nombres.aplicacion(original.aplicacion)}</Text>
        <Text weight="semibold">{original.sujeto?.proyecto ? 'Proyecto' : original.sujeto?.empleado ? 'Persona' : 'Alcance'}</Text>
        <Text>{textoSujeto(original.sujeto)}</Text>
      </Card>

      <CamposNovedad valores={estado} tipos={tipos} errores={errores} onCambiar={cambiarCampos} />

      {recortados.length > 0 && (
        <MessageBar intent="info">
          <MessageBarBody>
            Se quitaron de los excluidos los días que quedaron fuera del rango: {recortados.map((d) => formatearFecha(d)).join(', ')}.
          </MessageBarBody>
        </MessageBar>
      )}

      {diasRango.length > 0 && (
        <SelectorDiasExcluidos
          dias={diasRango}
          excluidos={estado.excluidos}
          error={errores.diasExcluidos}
          onAlternar={(dia) => cambiar(alternarDia(estado, dia), ['diasExcluidos'])}
          onIncluirTodos={() => cambiar(incluirTodos(estado), ['diasExcluidos'])}
        />
      )}

      {resumen.length > 0 && valido && (
        <MessageBar intent="info">
          <MessageBarBody>
            <MessageBarTitle>Se guardará:</MessageBarTitle>
            <ul className={estilos.resumen}>
              {resumen.map((linea) => (
                <li key={linea}>{linea}</li>
              ))}
            </ul>
          </MessageBarBody>
        </MessageBar>
      )}

      <div className={estilos.acciones}>
        <Button
          className={estilos.boton}
          appearance="primary"
          icon={editar.isPending ? <Spinner size="tiny" /> : <Save20Regular />}
          disabled={editar.isPending || !valido || !cambios}
          onClick={guardar}
        >
          {editar.isPending ? 'Guardando…' : 'Guardar cambios'}
        </Button>
        {!cambios && !editar.isPending && <Text className={estilos.secundario}>{MENSAJE_SIN_CAMBIOS}</Text>}
      </div>

      <Dialog open={confirmarRecarga} onOpenChange={(_, d) => !d.open && setConfirmarRecarga(false)}>
        <DialogSurface>
          <DialogBody>
            <DialogTitle>¿Recargar la novedad?</DialogTitle>
            <DialogContent>Se perderán los cambios que no guardó.</DialogContent>
            <DialogActions>
              <Button onClick={() => setConfirmarRecarga(false)}>Seguir editando</Button>
              <Button appearance="primary" onClick={() => void recargar()}>
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
            <DialogContent>Los cambios de la novedad no se han guardado. Si sale ahora, se perderán.</DialogContent>
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
