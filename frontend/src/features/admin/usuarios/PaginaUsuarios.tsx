import {
  Badge,
  Button,
  Checkbox,
  DataGrid,
  DataGridBody,
  DataGridCell,
  DataGridHeader,
  DataGridHeaderCell,
  DataGridRow,
  Input,
  MessageBar,
  MessageBarActions,
  MessageBarBody,
  Skeleton,
  SkeletonItem,
  Spinner,
  Text,
  createTableColumn,
  makeStyles,
  mergeClasses,
  tokens,
  type TableColumnDefinition,
} from '@fluentui/react-components'
import { Add20Regular, Search20Regular } from '@fluentui/react-icons'
import { useCallback, useMemo, useState, type KeyboardEvent } from 'react'
import { useNavigate, useSearchParams } from 'react-router'
import { EstadoError } from '../../../components/EstadoError'
import { Paginacion } from '../../../components/Paginacion'
import { AdminRequerido } from '../AdminRequerido'
import { useUsuariosAdmin } from './api'
import { DialogoNuevoUsuario } from './DialogoNuevoUsuario'
import {
  type FiltrosUsuarios,
  PAGINA_POR_DEFECTO,
  TAMANO_PAGINA,
  type UsuarioAdmin,
  aParametrosUrlUsuarios,
  leerFiltrosUsuarios,
} from './usuariosAdmin'

const MOVIL = '@media (max-width: 599px)'

const useEstilos = makeStyles({
  pagina: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL },
  encabezado: { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: tokens.spacingHorizontalM },
  nuevo: { marginLeft: 'auto', [MOVIL]: { marginLeft: 0, width: '100%' } },
  filtros: { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: tokens.spacingHorizontalM },
  texto: { minWidth: '260px', [MOVIL]: { minWidth: 0, width: '100%' } },
  boton: { [MOVIL]: { width: '100%' } },
  esqueleto: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalS },
  desplazable: { overflowX: 'auto' },
  tabla: { minWidth: '820px' },
  fila: { cursor: 'pointer', ':focus-visible': { outline: `${tokens.strokeWidthThick} solid ${tokens.colorStrokeFocus2}` } },
  inactivo: { color: tokens.colorNeutralForeground3 },
  estado: { display: 'flex', flexWrap: 'wrap', gap: tokens.spacingHorizontalXS },
})

/** Filtros sincronizados con la URL; cualquier cambio que no sea de página vuelve a la página 1. */
function useFiltrosUsuarios() {
  const [parametros, setParametros] = useSearchParams()
  const consulta = parametros.toString()
  const filtros = useMemo(() => leerFiltrosUsuarios(new URLSearchParams(consulta)), [consulta])
  const actualizar = useCallback(
    (cambios: Partial<FiltrosUsuarios>) => {
      const nuevo = { ...filtros, ...cambios }
      if (cambios.pagina === undefined) {
        nuevo.pagina = PAGINA_POR_DEFECTO
      }
      setParametros(aParametrosUrlUsuarios(nuevo))
    },
    [filtros, setParametros],
  )
  return { filtros, actualizar }
}

/** Ruta /admin/usuarios (TAREA-25-2): lista de usuarios con sus departamentos. Solo Admin. */
export function PaginaUsuarios() {
  return (
    <AdminRequerido>
      <ListaUsuarios />
    </AdminRequerido>
  )
}

function ListaUsuarios() {
  const estilos = useEstilos()
  const navigate = useNavigate()
  const { filtros, actualizar } = useFiltrosUsuarios()
  const { data, error, isPending, isFetching, refetch } = useUsuariosAdmin(filtros, true)
  const [texto, setTexto] = useState(filtros.texto ?? '')
  const [creando, setCreando] = useState(false)

  const columnas = useMemo<TableColumnDefinition<UsuarioAdmin>[]>(
    () => [
      createTableColumn({ columnId: 'email', renderHeaderCell: () => 'Correo', renderCell: (u) => u.email }),
      createTableColumn({ columnId: 'nombre', renderHeaderCell: () => 'Nombre', renderCell: (u) => u.nombreMostrar }),
      createTableColumn({
        columnId: 'estado',
        renderHeaderCell: () => 'Estado',
        renderCell: (u) => (
          <span className={estilos.estado}>
            <Badge appearance="tint" color={u.activo ? 'success' : 'danger'}>
              {u.activo ? 'Activo' : 'Inactivo'}
            </Badge>
            {!u.conIdentidad && (
              <Badge appearance="tint" color="informative">
                Aún no ha ingresado
              </Badge>
            )}
          </span>
        ),
      }),
      createTableColumn({
        columnId: 'departamentos',
        renderHeaderCell: () => 'Departamentos',
        renderCell: (u) => (u.departamentos.length === 0 ? '—' : u.departamentos.map((d) => d.nombreCorto).join(', ')),
      }),
    ],
    [estilos.estado],
  )

  const abrir = (id: number) => void navigate(`/admin/usuarios/${id}`)

  return (
    <section className={estilos.pagina}>
      <div className={estilos.encabezado}>
        <Text as="h2" size={600} weight="semibold">
          Usuarios y departamentos
        </Text>
        {isFetching && !isPending && <Spinner size="tiny" label="Actualizando…" />}
        <Button className={estilos.nuevo} appearance="primary" icon={<Add20Regular />} onClick={() => setCreando(true)}>
          Nuevo usuario
        </Button>
      </div>

      <DialogoNuevoUsuario abierto={creando} onCerrar={() => setCreando(false)} />

      <form
        className={estilos.filtros}
        onSubmit={(evento) => {
          evento.preventDefault()
          actualizar({ texto: texto.trim() || undefined })
        }}
      >
        <Input
          className={estilos.texto}
          aria-label="Buscar por correo o nombre"
          placeholder="Correo o nombre"
          contentBefore={<Search20Regular />}
          value={texto}
          onChange={(_, d) => setTexto(d.value)}
        />
        <Button className={estilos.boton} type="submit">
          Buscar
        </Button>
        <Checkbox
          label="Incluir inactivos"
          checked={filtros.incluirInactivos}
          onChange={(_, d) => actualizar({ incluirInactivos: d.checked === true })}
        />
      </form>

      {isPending && !error && (
        <Skeleton aria-label="Cargando usuarios" className={estilos.esqueleto}>
          {Array.from({ length: 5 }, (_, i) => (
            <SkeletonItem key={i} size={24} />
          ))}
        </Skeleton>
      )}

      {error && (
        <>
          <EstadoError error={error} />
          <Button className={estilos.boton} disabled={isFetching} onClick={() => void refetch()}>
            Reintentar
          </Button>
        </>
      )}

      {data && data.total === 0 && (
        <MessageBar intent="info">
          <MessageBarBody>No hay usuarios con estos filtros.</MessageBarBody>
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

      {data && data.items.length > 0 && (
        <div className={estilos.desplazable}>
          <DataGrid className={estilos.tabla} items={data.items} columns={columnas} getRowId={(u: UsuarioAdmin) => u.id} focusMode="composite">
            <DataGridHeader>
              <DataGridRow>{({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}</DataGridRow>
            </DataGridHeader>
            <DataGridBody<UsuarioAdmin>>
              {({ item, rowId }) => (
                <DataGridRow<UsuarioAdmin>
                  key={rowId}
                  className={mergeClasses(estilos.fila, !item.activo && estilos.inactivo)}
                  onClick={() => abrir(item.id)}
                  onKeyDown={(evento: KeyboardEvent<HTMLDivElement>) => {
                    if (evento.key === 'Enter') {
                      abrir(item.id)
                    }
                  }}
                >
                  {({ renderCell }) => <DataGridCell>{renderCell(item)}</DataGridCell>}
                </DataGridRow>
              )}
            </DataGridBody>
          </DataGrid>
        </div>
      )}

      {data && data.total > 0 && (
        <Paginacion
          pagina={data.pagina}
          tamano={data.tamano}
          total={data.total}
          unidad={['usuario', 'usuarios']}
          tamanos={[TAMANO_PAGINA]}
          deshabilitada={isFetching}
          onPagina={(pagina) => actualizar({ pagina })}
          onTamano={() => undefined}
        />
      )}
    </section>
  )
}
