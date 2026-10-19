import {
  Button,
  MessageBar,
  MessageBarActions,
  MessageBarBody,
  MessageBarTitle,
  Spinner,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components'
import { useCallback, useMemo } from 'react'
import { useSearchParams } from 'react-router'
import { ErrorApi } from '../../../api/errores'
import { EstadoError } from '../../../components/EstadoError'
import { hoyEnNegocio } from '../../../utils/formato'
import { useCronograma } from '../api'
import { coloresDeRespuesta } from '../coloresProyecto'
import { aParametrosUrl, type FiltrosCronograma as Filtros, filtrarPersonas, leerFiltrosCronograma, textoAvisoRecorte } from '../cronograma'
import { CuadriculaCronograma } from '../components/CuadriculaCronograma'
import { FiltrosCronograma } from '../components/FiltrosCronograma'
import { LeyendaCronograma } from '../components/LeyendaCronograma'

// Errores 400 que se muestran en su filtro; el resto, en un aviso general.
const CAMPOS_EN_FILTROS = new Set(['desde', 'hasta', 'proyectoId', 'departamentoId'])

const useEstilos = makeStyles({
  pagina: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL },
  encabezado: { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: tokens.spacingHorizontalM },
  resumen: { color: tokens.colorNeutralForeground3 },
})

/** /cronograma (TAREA-21): cuadrícula persona × día de GET /api/cronograma. Ver docs/fases/FASE_5_Cronograma.md §7. */
export function PaginaCronograma() {
  const estilos = useEstilos()
  const [parametros, setParametros] = useSearchParams()
  const consulta = parametros.toString()
  const filtros = useMemo(() => leerFiltrosCronograma(new URLSearchParams(consulta)), [consulta])
  const { data, error, isPending, isFetching, isPlaceholderData, refetch } = useCronograma(filtros)

  const actualizar = useCallback(
    (cambios: Partial<Filtros>, opciones?: { reemplazar?: boolean }) =>
      setParametros(aParametrosUrl({ ...filtros, ...cambios }), { replace: opciones?.reemplazar ?? false }),
    [filtros, setParametros],
  )
  const limpiar = useCallback(() => setParametros(new URLSearchParams()), [setParametros])

  const errorApi = error instanceof ErrorApi ? error : undefined
  const erroresValidacion = errorApi?.tipo === 'validacion' ? (errorApi.errores ?? {}) : undefined
  const erroresGenerales = Object.entries(erroresValidacion ?? {}).filter(([campo]) => !CAMPOS_EN_FILTROS.has(campo))
  const personas = useMemo(() => filtrarPersonas(data?.personas ?? [], filtros.persona), [data?.personas, filtros.persona])
  // P2: color estable por Id, lejos de los colores de las novedades.
  const colores = useMemo(
    () => coloresDeRespuesta((data?.proyectos ?? []).map((p) => p.id), data?.leyendaNovedades ?? []),
    [data?.proyectos, data?.leyendaNovedades],
  )
  const hoy = hoyEnNegocio()

  return (
    <section className={estilos.pagina}>
      <div className={estilos.encabezado}>
        <Text as="h2" size={600} weight="semibold">
          Cronograma de proyectos
        </Text>
        {isFetching && !isPending && <Spinner size="tiny" label="Actualizando…" />}
      </div>

      <FiltrosCronograma
        filtros={filtros}
        // Rango efectivo de la respuesta (D6 y D5 revisada); mientras llega uno nuevo, el de la URL.
        rango={data && !error && !isPlaceholderData ? { desde: data.desde, hasta: data.hasta } : undefined}
        ultimaRespuesta={data && !error ? data : undefined}
        proyectos={data?.proyectos ?? []}
        erroresApi={erroresValidacion}
        onCambiar={actualizar}
        onLimpiar={limpiar}
      />

      {erroresGenerales.length > 0 && (
        <MessageBar intent="error">
          <MessageBarBody>{erroresGenerales.map(([, mensajes]) => mensajes.join(' ')).join(' ')}</MessageBarBody>
        </MessageBar>
      )}

      {/* 400: el mensaje va en su filtro, sin reintento. 403/5xx/red: error con "Reintentar" (P10: sin reintentos automáticos). */}
      {error && errorApi?.tipo !== 'validacion' && (
        <div>
          <EstadoError error={error} />
          {errorApi?.tipo !== 'prohibido' && (
            <MessageBar intent="info">
              <MessageBarBody>
                <MessageBarTitle>No se pudo cargar el cronograma.</MessageBarTitle>
                Revise la conexión (VPN) y vuelva a intentar.
              </MessageBarBody>
              <MessageBarActions>
                <Button onClick={() => void refetch()} disabled={isFetching}>
                  Reintentar
                </Button>
              </MessageBarActions>
            </MessageBar>
          )}
        </div>
      )}

      {isPending && !error && <Spinner label="Cargando el cronograma…" />}

      {data && !error && (
        <>
          <Text size={200} className={estilos.resumen}>
            {personas.length} de {data.personas.length} persona(s) · {data.proyectos.length} proyecto(s) · máximo {data.maxDias} días
          </Text>
          {/* D5 revisada (TAREA-21): la URL conserva el "hasta" pedido; aquí se avisa del rango que se muestra. */}
          {textoAvisoRecorte(data) && (
            <MessageBar intent="warning">
              <MessageBarBody>{textoAvisoRecorte(data)}</MessageBarBody>
            </MessageBar>
          )}
          {data.personas.length === 0 ? (
            <MessageBar intent="info">
              <MessageBarBody>No hay asignaciones en el rango.</MessageBarBody>
            </MessageBar>
          ) : personas.length === 0 ? (
            <MessageBar intent="info">
              <MessageBarBody>Ninguna persona coincide con «{filtros.persona}».</MessageBarBody>
            </MessageBar>
          ) : (
            <CuadriculaCronograma datos={data} personas={personas} colores={colores} hoy={hoy} />
          )}
          <LeyendaCronograma proyectos={data.proyectos} novedades={data.leyendaNovedades} colores={colores} />
        </>
      )}
    </section>
  )
}
