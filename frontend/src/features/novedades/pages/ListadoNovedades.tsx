import {
  Button,
  MessageBar,
  MessageBarActions,
  MessageBarBody,
  MessageBarTitle,
  Skeleton,
  SkeletonItem,
  Spinner,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components'
import { Add20Regular, ArrowSync20Regular } from '@fluentui/react-icons'
import { useState } from 'react'
import { useLocation, useNavigate } from 'react-router'
import { ErrorApi } from '../../../api/errores'
import { esAdmin } from '../../../auth/roles'
import { useUsuarioActual } from '../../../auth/useUsuarioActual'
import { EstadoError } from '../../../components/EstadoError'
import { Paginacion } from '../../../components/Paginacion'
import { FiltrosNovedades } from '../components/FiltrosNovedades'
import { TablaNovedades } from '../components/TablaNovedades'
import { PAGINA_POR_DEFECTO, TAMANOS_PAGINA, TAMANO_POR_DEFECTO, useFiltrosUrl } from '../filtrosUrl'
import { DialogoSincronizacion } from '../components/DialogoSincronizacion'
import { useNovedades } from '../hooks'

/**
 * Estado de navegación hacia el detalle: `busqueda` para volver al listado con los mismos filtros; `aviso` de éxito
 * (TAREA-24b: «Novedad … registrada.»).
 */
export interface EstadoNavegacionNovedades {
  busqueda?: string
  aviso?: string
}

const CAMPOS_EN_FILTROS = new Set(['aplicacion', 'tipo', 'origen', 'desde', 'hasta'])

const useEstilos = makeStyles({
  pagina: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL },
  encabezado: { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: tokens.spacingHorizontalM },
  esqueleto: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalS },
  boton: { alignSelf: 'flex-start', '@media (max-width: 599px)': { alignSelf: 'stretch' } },
  acciones: {
    marginLeft: 'auto',
    display: 'flex',
    flexWrap: 'wrap',
    gap: tokens.spacingHorizontalS,
    '@media (max-width: 599px)': { marginLeft: 0, width: '100%', flexDirection: 'column' },
  },
})

/** Listado de novedades (TAREA-24a). La API decide qué novedades ve cada usuario (J2). */
export function ListadoNovedades() {
  const estilos = useEstilos()
  const navigate = useNavigate()
  const location = useLocation()
  const { filtros, actualizar, limpiar, hayFiltros } = useFiltrosUrl()
  const { data, error, isPending, isFetching, refetch } = useNovedades(filtros)
  const { data: usuario } = useUsuarioActual() // misma consulta (y caché) que la barra superior
  const [sincronizando, setSincronizando] = useState(false)

  const errorValidacion = error instanceof ErrorApi && error.tipo === 'validacion' ? error : undefined
  const erroresApi = errorValidacion?.errores ?? {}
  // Errores que no corresponden a un filtro visible (pagina, tamano, texto…) se muestran aparte.
  const erroresGenerales = Object.entries(erroresApi).filter(([campo]) => !CAMPOS_EN_FILTROS.has(campo))

  const abrir = (id: number) => {
    const estado: EstadoNavegacionNovedades = { busqueda: location.search }
    void navigate(`/novedades/${id}`, { state: estado })
  }

  return (
    <section className={estilos.pagina}>
      <div className={estilos.encabezado}>
        <Text as="h2" size={600} weight="semibold">
          Novedades
        </Text>
        {isFetching && !isPending && <Spinner size="tiny" label="Actualizando…" />}
        <div className={estilos.acciones}>
          {/* TAREA-24d: solo decide si se ve el botón; la API exige la política Admin igual. */}
          {esAdmin(usuario) && (
            <Button icon={<ArrowSync20Regular />} onClick={() => setSincronizando(true)}>
              Sincronizar permisos médicos
            </Button>
          )}
          <Button appearance="primary" icon={<Add20Regular />} onClick={() => void navigate('/novedades/nueva')}>
            Nueva novedad
          </Button>
        </div>
      </div>

      <DialogoSincronizacion abierto={sincronizando} onCerrar={() => setSincronizando(false)} />

      <FiltrosNovedades
        filtros={filtros}
        hayFiltros={hayFiltros}
        erroresApi={errorValidacion ? erroresApi : undefined}
        onCambiar={actualizar}
        onLimpiar={limpiar}
      />

      {isPending && !error && (
        <Skeleton aria-label="Cargando novedades" className={estilos.esqueleto}>
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
          <MessageBarActions>
            <Button onClick={() => actualizar({ pagina: PAGINA_POR_DEFECTO, tamano: TAMANO_POR_DEFECTO })}>
              Restablecer paginación
            </Button>
          </MessageBarActions>
        </MessageBar>
      )}

      {error && !errorValidacion && (
        <>
          <EstadoError error={error} />
          <Button className={estilos.boton} disabled={isFetching} onClick={() => void refetch()}>
            Reintentar
          </Button>
        </>
      )}

      {data && data.items.length === 0 && data.total === 0 && (
        <MessageBar intent="info">
          <MessageBarBody>No hay novedades con estos filtros.</MessageBarBody>
        </MessageBar>
      )}

      {data && data.items.length === 0 && data.total > 0 && (
        <MessageBar intent="warning">
          <MessageBarBody>La página {data.pagina} no tiene resultados.</MessageBarBody>
          <MessageBarActions>
            <Button onClick={() => actualizar({ pagina: PAGINA_POR_DEFECTO })}>Ir a la página 1</Button>
          </MessageBarActions>
        </MessageBar>
      )}

      {data && data.items.length > 0 && <TablaNovedades novedades={data.items} onAbrir={abrir} />}

      {data && data.total > 0 && (
        <Paginacion
          pagina={data.pagina}
          tamano={data.tamano}
          total={data.total}
          unidad={['novedad', 'novedades']}
          tamanos={TAMANOS_PAGINA}
          deshabilitada={isFetching}
          onPagina={(pagina) => actualizar({ pagina })}
          onTamano={(tamano) => actualizar({ tamano })}
        />
      )}
    </section>
  )
}
