import { Field, MessageBar, MessageBarBody, MessageBarTitle, SearchBox, Spinner, makeStyles, tokens } from '@fluentui/react-components'
import { CalendarLtr20Regular, Open16Regular } from '@fluentui/react-icons'
import { useMemo, useState } from 'react'
import { Link } from 'react-router'
import { ErrorApi } from '../../../api/errores'
import { useValorConRetardo } from '../../../hooks/useValorConRetardo'
import { hoyEnNegocio } from '../../../utils/formato'
import { type RangoCronograma, useCronogramaProyecto } from '../../cronograma/api'
import { coloresEstables } from '../../cronograma/coloresProyecto'
import { enlaceCronogramaGeneral, filtrarPersonas } from '../../cronograma/cronograma'
import { SelectorRango } from '../../cronograma/components/SelectorRango'
import { ErrorCronograma, VistaCronograma } from '../../cronograma/components/VistaCronograma'
import { useCatalogos } from '../hooks'

const ESPERA_TEXTO_MS = 300

const useEstilos = makeStyles({
  pestana: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM, paddingTop: tokens.spacingVerticalM },
  filtros: { display: 'flex', flexWrap: 'wrap', alignItems: 'flex-start', gap: tokens.spacingHorizontalM },
  texto: { minWidth: '220px' },
  enlace: { display: 'inline-flex', alignItems: 'center', gap: tokens.spacingHorizontalXXS, marginTop: '30px' },
})

export interface PestanaCronogramaProps {
  proyectoId: number
  /** Solo consulta con la pestaña activa (carga perezosa). */
  activa: boolean
}

/**
 * Pestaña "Cronograma" del detalle (TAREA-21b): GET /api/proyectos/{id}/cronograma con el rango en estado local
 * (Q2, sin URL). Sin fechas: D12 (desde el inicio del proyecto, máximo CRONOGRAMA_MAX_DIAS días).
 */
export function PestanaCronograma({ proyectoId, activa }: PestanaCronogramaProps) {
  const estilos = useEstilos()
  const [rango, setRango] = useState<RangoCronograma>({})
  const [persona, setPersona] = useState('')
  const personaBuscada = useValorConRetardo(persona, ESPERA_TEXTO_MS)
  const { data, error, isPending, isFetching, isPlaceholderData, refetch } = useCronogramaProyecto(proyectoId, rango, activa)
  const { data: catalogos } = useCatalogos()

  const errorApi = error instanceof ErrorApi ? error : undefined
  const errores = errorApi?.tipo === 'validacion' ? errorApi.errores : undefined
  const personas = useMemo(() => filtrarPersonas(data?.personas ?? [], personaBuscada), [data?.personas, personaBuscada])
  // Q4: mismos colores de novedad que /cronograma (catálogo), así el proyecto tiene el mismo color en las dos vistas.
  const colores = useMemo(
    () => coloresEstables((data?.proyectos ?? []).map((p) => p.id), catalogos?.tiposNovedad),
    [data?.proyectos, catalogos?.tiposNovedad],
  )
  const enlace = data && !error ? enlaceCronogramaGeneral(proyectoId, data.desde, data.hasta) : undefined

  return (
    <div className={estilos.pestana}>
      <div className={estilos.filtros} role="search" aria-label="Rango del cronograma del proyecto">
        <SelectorRango
          pedido={rango}
          efectivo={data && !error && !isPlaceholderData ? { desde: data.desde, hasta: data.hasta } : undefined}
          ultimaRespuesta={data && !error ? data : undefined}
          errores={{ desde: errores?.desde?.join(' '), hasta: errores?.hasta?.join(' ') }}
          onCambiar={setRango}
          reinicio={{ texto: 'Todo el proyecto', icono: <CalendarLtr20Regular /> }}
        />
        <Field label="Persona" hint="Nombre o código EKON" className={estilos.texto}>
          <SearchBox value={persona} placeholder="Ej.: 7255 o nombre" onChange={(_, d) => setPersona(d.value)} />
        </Field>
        {enlace && (
          <Link to={enlace} className={estilos.enlace}>
            Abrir en el cronograma general <Open16Regular aria-hidden />
          </Link>
        )}
        {isFetching && !isPending && <Spinner size="tiny" label="Actualizando…" />}
      </div>

      {errorApi?.tipo === 'noEncontrado' ? (
        <MessageBar intent="warning">
          <MessageBarBody>
            <MessageBarTitle>Proyecto no encontrado o sin acceso</MessageBarTitle>
          </MessageBarBody>
        </MessageBar>
      ) : (
        error && <ErrorCronograma error={error} reintentando={isFetching} onReintentar={() => void refetch()} />
      )}

      {isPending && !error && <Spinner label="Cargando el cronograma del proyecto…" />}

      {data && !error && (
        <VistaCronograma
          datos={data}
          personas={personas}
          textoPersona={personaBuscada}
          colores={colores}
          hoy={hoyEnNegocio()}
          mensajeVacio="El proyecto no tiene asignaciones en el rango."
          complementoAviso={
            <>
              Use los accesos de mes o <Link to={enlace ?? '/cronograma'}>abra el cronograma general</Link>.
            </>
          }
          contexto="proyecto"
          proyectoContexto={proyectoId}
        />
      )}
    </div>
  )
}
