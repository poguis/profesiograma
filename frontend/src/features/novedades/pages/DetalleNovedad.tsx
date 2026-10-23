import {
  Badge,
  Button,
  Card,
  MessageBar,
  MessageBarBody,
  MessageBarTitle,
  Spinner,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components'
import { ArrowLeft20Regular } from '@fluentui/react-icons'
import type { ReactNode } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router'
import { ErrorApi } from '../../../api/errores'
import { EstadoError } from '../../../components/EstadoError'
import { formatearFecha, formatearFechaHora } from '../../../utils/formato'
import { EtiquetaTipoNovedad } from '../components/EtiquetaTipoNovedad'
import { agruparPorMes, etiquetaDia } from '../diasNovedad'
import { useNovedad } from '../hooks'
import { textoSujeto, useNombresNovedad } from '../presentacion'
import type { NovedadDetalle } from '../tipos'
import type { EstadoNavegacionNovedades } from './ListadoNovedades'

const MOVIL = '@media (max-width: 599px)'

const useEstilos = makeStyles({
  pagina: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL },
  boton: { alignSelf: 'flex-start', [MOVIL]: { alignSelf: 'stretch' } },
  titulo: { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: tokens.spacingHorizontalM },
  tarjetas: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))',
    gap: tokens.spacingHorizontalL,
    [MOVIL]: { gridTemplateColumns: '1fr' },
  },
  tarjeta: { padding: tokens.spacingHorizontalL },
  datos: {
    display: 'grid',
    gridTemplateColumns: 'max-content 1fr',
    columnGap: tokens.spacingHorizontalL,
    rowGap: tokens.spacingVerticalXS,
    [MOVIL]: { gridTemplateColumns: '1fr' },
  },
  texto: { whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' },
  mes: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXS },
  dias: { display: 'flex', flexWrap: 'wrap', gap: tokens.spacingHorizontalXS },
})

/** Detalle de solo lectura (TAREA-24a). Editar y anular llegan en la 24c; aquí no hay botones de acción. */
export function DetalleNovedad() {
  const estilos = useEstilos()
  const navigate = useNavigate()
  const location = useLocation()
  const { id } = useParams()
  const numero = Number(id)
  const idValido = Number.isInteger(numero) && numero > 0
  const { data: novedad, error, isPending, isFetching, refetch } = useNovedad(numero)

  const busqueda = (location.state as EstadoNavegacionNovedades | null)?.busqueda ?? ''
  const rutaListado = `/novedades${busqueda}`
  const noEncontrado = !idValido || (error instanceof ErrorApi && error.tipo === 'noEncontrado')

  return (
    <section className={estilos.pagina}>
      <Button className={estilos.boton} icon={<ArrowLeft20Regular />} onClick={() => void navigate(rutaListado)}>
        Volver al listado
      </Button>

      {noEncontrado ? (
        // La API responde 404 tanto si no existe como si no es visible: no se distingue.
        <MessageBar intent="warning">
          <MessageBarBody>
            <MessageBarTitle>La novedad no existe o no tiene acceso.</MessageBarTitle>
            <Link to={rutaListado}>Ir al listado de novedades</Link>
          </MessageBarBody>
        </MessageBar>
      ) : error ? (
        <>
          <EstadoError error={error} />
          <Button className={estilos.boton} disabled={isFetching} onClick={() => void refetch()}>
            Reintentar
          </Button>
        </>
      ) : isPending ? (
        <Spinner label="Cargando novedad…" labelPosition="after" />
      ) : (
        novedad && <ContenidoNovedad novedad={novedad} />
      )}
    </section>
  )
}

function Dato({ etiqueta, children }: { etiqueta: string; children: ReactNode }) {
  return (
    <>
      <Text weight="semibold">{etiqueta}</Text>
      <Text>{children ?? '—'}</Text>
    </>
  )
}

function ContenidoNovedad({ novedad }: { novedad: NovedadDetalle }) {
  const estilos = useEstilos()
  const nombres = useNombresNovedad()
  const { sujeto, registro } = novedad
  const meses = agruparPorMes(novedad.dias)

  return (
    <>
      <div className={estilos.titulo}>
        <Text as="h2" size={600} weight="semibold">
          {novedad.codigo}
        </Text>
        <EtiquetaTipoNovedad tipo={novedad.tipo} />
        {novedad.anulada && (
          <Badge appearance="filled" color="danger">
            Anulada
          </Badge>
        )}
      </div>

      {novedad.anulada && (
        <MessageBar intent="warning">
          <MessageBarBody>
            <MessageBarTitle>
              Anulada{novedad.fechaAnulacion ? ` el ${formatearFechaHora(novedad.fechaAnulacion)}` : ''}
            </MessageBarTitle>
            {novedad.motivoAnulacion && <span className={estilos.texto}>Motivo: {novedad.motivoAnulacion}</span>}
          </MessageBarBody>
        </MessageBar>
      )}

      {/* Lo decide el backend (N3); los botones Editar y Anular llegan en la 24c. */}
      {novedad.motivoNoEditable && (
        <MessageBar intent="info">
          <MessageBarBody>{novedad.motivoNoEditable}</MessageBarBody>
        </MessageBar>
      )}

      <div className={estilos.tarjetas}>
        <Card className={estilos.tarjeta}>
          <Text as="h3" weight="semibold">
            Datos de la novedad
          </Text>
          <div className={estilos.datos}>
            <Dato etiqueta="Aplicación">{nombres.aplicacion(novedad.aplicacion)}</Dato>
            <Dato etiqueta={sujeto?.proyecto ? 'Proyecto' : sujeto?.empleado ? 'Persona' : 'Alcance'}>
              {sujeto?.proyecto ? (
                <Link to={`/proyectos/${sujeto.proyecto.id}`}>{textoSujeto(sujeto)}</Link>
              ) : (
                textoSujeto(sujeto)
              )}
            </Dato>
            <Dato etiqueta="Tipo">{novedad.tipo.nombre}</Dato>
            <Dato etiqueta="Origen">{nombres.origen(novedad.origen)}</Dato>
            <Dato etiqueta="Fechas">
              {formatearFecha(novedad.fechaInicio)} – {formatearFecha(novedad.fechaFin)} ({novedad.dias.length}{' '}
              {novedad.dias.length === 1 ? 'día' : 'días'})
            </Dato>
            <Dato etiqueta="Registrada">{formatearFechaHora(novedad.fechaCreacion)}</Dato>
            {/* Solo viene para el Admin (lo decide el backend). */}
            {registro && <Dato etiqueta="Registrado por">{[registro.nombre, registro.correo].filter(Boolean).join(' · ') || null}</Dato>}
            <Dato etiqueta="Observación">
              {novedad.observacion ? <span className={estilos.texto}>{novedad.observacion}</span> : null}
            </Dato>
          </div>
        </Card>

        <Card className={estilos.tarjeta}>
          <Text as="h3" weight="semibold">
            Días ({novedad.dias.length})
          </Text>
          {meses.map((mes) => (
            <div key={mes.clave} className={estilos.mes}>
              <Text weight="semibold">{mes.titulo}</Text>
              <div className={estilos.dias}>
                {mes.dias.map((dia) => (
                  <Badge key={dia} appearance="outline" color="informative" title={formatearFecha(dia)}>
                    {etiquetaDia(dia)}
                  </Badge>
                ))}
              </div>
            </div>
          ))}
        </Card>
      </div>
    </>
  )
}
