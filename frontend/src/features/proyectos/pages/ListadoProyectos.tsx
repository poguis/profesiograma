import {
  Button,
  MessageBar,
  MessageBarBody,
  MessageBarTitle,
  Skeleton,
  SkeletonItem,
  Spinner,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components'
import { Add20Regular } from '@fluentui/react-icons'
import { useLocation, useNavigate } from 'react-router'
import { ErrorApi } from '../../../api/errores'
import { EstadoError } from '../../../components/EstadoError'
import { Paginacion } from '../../../components/Paginacion'
import { FiltrosProyectos } from '../components/FiltrosProyectos'
import { TablaProyectos } from '../components/TablaProyectos'
import { PAGINA_POR_DEFECTO, TAMANOS_PAGINA, TAMANO_POR_DEFECTO, useFiltrosUrl } from '../filtrosUrl'
import { useProyectos } from '../hooks'

/** Estado de navegación: el detalle usa `busqueda` para volver al listado con los mismos filtros. */
export interface EstadoNavegacionProyectos {
  busqueda: string
}

const CAMPOS_EN_FILTROS = new Set(['desde', 'hasta'])

const useEstilos = makeStyles({
  pagina: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL },
  encabezado: { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: tokens.spacingHorizontalM },
  nuevo: { marginLeft: 'auto' },
  esqueleto: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalS },
})

export function ListadoProyectos() {
  const estilos = useEstilos()
  const navigate = useNavigate()
  const location = useLocation()
  const { filtros, actualizar, limpiar, hayFiltros } = useFiltrosUrl()
  const { data, error, isPending, isFetching } = useProyectos(filtros)

  const errorValidacion = error instanceof ErrorApi && error.tipo === 'validacion' ? error : undefined
  const erroresApi = errorValidacion?.errores ?? {}
  // Errores que no corresponden a un filtro visible (pagina, tamano u otros) se muestran junto a la paginación.
  const erroresGenerales = Object.entries(erroresApi).filter(([campo]) => !CAMPOS_EN_FILTROS.has(campo))

  const abrir = (id: number) => {
    const estado: EstadoNavegacionProyectos = { busqueda: location.search }
    void navigate(`/proyectos/${id}`, { state: estado })
  }

  return (
    <section className={estilos.pagina}>
      <div className={estilos.encabezado}>
        <Text as="h2" size={600} weight="semibold">
          Control de proyectos
        </Text>
        {isFetching && !isPending && <Spinner size="tiny" label="Actualizando…" />}
        <Button
          className={estilos.nuevo}
          appearance="primary"
          icon={<Add20Regular />}
          onClick={() => void navigate('/proyectos/nuevo')}
        >
          Nuevo proyecto
        </Button>
      </div>

      <FiltrosProyectos
        filtros={filtros}
        hayFiltros={hayFiltros}
        erroresApi={errorValidacion ? erroresApi : undefined}
        onCambiar={actualizar}
        onLimpiar={limpiar}
      />

      {isPending && (
        <Skeleton aria-label="Cargando proyectos" className={estilos.esqueleto}>
          {Array.from({ length: 5 }, (_, i) => (
            <SkeletonItem key={i} size={24} />
          ))}
        </Skeleton>
      )}

      {errorValidacion && erroresGenerales.length > 0 && (
        <MessageBar intent="error">
          <MessageBarBody>
            <MessageBarTitle>{errorValidacion.titulo}</MessageBarTitle>
            {erroresGenerales.map(([campo, mensajes]) => (
              <div key={campo}>{mensajes.join(' ')}</div>
            ))}
          </MessageBarBody>
          <Button
            appearance="transparent"
            onClick={() => actualizar({ pagina: PAGINA_POR_DEFECTO, tamano: TAMANO_POR_DEFECTO })}
          >
            Restablecer paginación
          </Button>
        </MessageBar>
      )}

      {error && !errorValidacion && <EstadoError error={error} />}

      {data && data.items.length === 0 && data.total === 0 && (
        <MessageBar intent="info">
          <MessageBarBody>No hay proyectos con estos filtros.</MessageBarBody>
        </MessageBar>
      )}

      {data && data.items.length === 0 && data.total > 0 && (
        <MessageBar intent="warning">
          <MessageBarBody>La página {data.pagina} no tiene resultados.</MessageBarBody>
          <Button appearance="transparent" onClick={() => actualizar({ pagina: PAGINA_POR_DEFECTO })}>
            Ir a la página 1
          </Button>
        </MessageBar>
      )}

      {data && data.items.length > 0 && <TablaProyectos proyectos={data.items} onAbrir={abrir} />}

      {data && data.total > 0 && (
        <Paginacion
          pagina={data.pagina}
          tamano={data.tamano}
          total={data.total}
          unidad={['proyecto', 'proyectos']}
          tamanos={TAMANOS_PAGINA}
          deshabilitada={isFetching}
          onPagina={(pagina) => actualizar({ pagina })}
          onTamano={(tamano) => actualizar({ tamano })}
        />
      )}
    </section>
  )
}
