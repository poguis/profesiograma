import { MessageBar, MessageBarBody, Spinner, Text, makeStyles, tokens } from '@fluentui/react-components'
import { useCallback, useMemo } from 'react'
import { useSearchParams } from 'react-router'
import { ErrorApi } from '../../../api/errores'
import { hoyEnNegocio } from '../../../utils/formato'
import { useCronograma } from '../api'
import { useCatalogos } from '../../proyectos/hooks'
import { coloresEstables } from '../coloresProyecto'
import { aParametrosUrl, type FiltrosCronograma as Filtros, filtrarPersonas, leerFiltrosCronograma } from '../cronograma'
import { FiltrosCronograma } from '../components/FiltrosCronograma'
import { ErrorCronograma, VistaCronograma } from '../components/VistaCronograma'

// Errores 400 que se muestran en su filtro; el resto, en un aviso general.
const CAMPOS_EN_FILTROS = new Set(['desde', 'hasta', 'proyectoId', 'departamentoId'])

const useEstilos = makeStyles({
  pagina: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL },
  encabezado: { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: tokens.spacingHorizontalM },
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
  // P2 y Q4 (TAREA-21b): color estable por Id, con los colores de novedad del catálogo (no los de cada respuesta).
  const { data: catalogos } = useCatalogos()
  const colores = useMemo(
    () => coloresEstables((data?.proyectos ?? []).map((p) => p.id), catalogos?.tiposNovedad),
    [data?.proyectos, catalogos?.tiposNovedad],
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
      {error && <ErrorCronograma error={error} reintentando={isFetching} onReintentar={() => void refetch()} />}

      {isPending && !error && <Spinner label="Cargando el cronograma…" />}

      {data && !error && (
        <VistaCronograma
          datos={data}
          personas={personas}
          textoPersona={filtros.persona}
          colores={colores}
          hoy={hoy}
          mensajeVacio="No hay asignaciones en el rango."
        />
      )}
    </section>
  )
}
