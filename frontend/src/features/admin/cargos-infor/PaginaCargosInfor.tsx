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
  MessageBarTitle,
  Select,
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
import { CloudArrowDown20Regular, Search20Regular } from '@fluentui/react-icons'
import { useCallback, useMemo, useState, type KeyboardEvent } from 'react'
import { useSearchParams } from 'react-router'
import { EstadoError } from '../../../components/EstadoError'
import { Paginacion } from '../../../components/Paginacion'
import { AdminRequerido } from '../AdminRequerido'
import { useCargosInfor } from './api'
import {
  AVISO_GUARDADO,
  type CargoInfor,
  FILTROS_CODIGO,
  type FiltroCodigo,
  type FiltrosCargos,
  PAGINA_POR_DEFECTO,
  TAMANO_PAGINA,
  TEXTO_AFECTA_REPORTE,
  TEXTO_CODIGO_INFOR,
  aParametrosUrlCargos,
  afectaAlReporte,
  leerFiltrosCargos,
} from './cargosInfor'
import { DialogoEditarCargo } from './DialogoEditarCargo'
import { DialogoImportarCargos } from './DialogoImportarCargos'

const MOVIL = '@media (max-width: 599px)'

const useEstilos = makeStyles({
  pagina: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL },
  encabezado: { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: tokens.spacingHorizontalM },
  importar: { marginLeft: 'auto', [MOVIL]: { marginLeft: 0, width: '100%' } },
  secundario: { color: tokens.colorNeutralForeground3 },
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
function useFiltrosCargos() {
  const [parametros, setParametros] = useSearchParams()
  const consulta = parametros.toString()
  const filtros = useMemo(() => leerFiltrosCargos(new URLSearchParams(consulta)), [consulta])
  const actualizar = useCallback(
    (cambios: Partial<FiltrosCargos>) => {
      const nuevo = { ...filtros, ...cambios }
      if (cambios.pagina === undefined) {
        nuevo.pagina = PAGINA_POR_DEFECTO
      }
      setParametros(aParametrosUrlCargos(nuevo))
    },
    [filtros, setParametros],
  )
  return { filtros, actualizar }
}

/** Ruta /admin/cargos-infor (TAREA-25-3-2): código Infor de cada cargo e importación desde el ERP. Solo Admin. */
export function PaginaCargosInfor() {
  return (
    <AdminRequerido>
      <ListaCargos />
    </AdminRequerido>
  )
}

function ListaCargos() {
  const estilos = useEstilos()
  const { filtros, actualizar } = useFiltrosCargos()
  const { data, error, isPending, isFetching, refetch } = useCargosInfor(filtros)
  const [texto, setTexto] = useState(filtros.texto ?? '')
  const [editando, setEditando] = useState<CargoInfor | null>(null)
  const [importando, setImportando] = useState(false)
  const [aviso, setAviso] = useState<string>()

  const columnas = useMemo<TableColumnDefinition<CargoInfor>[]>(
    () => [
      createTableColumn({ columnId: 'cargo', renderHeaderCell: () => 'Cargo', renderCell: (c) => c.cargo }),
      createTableColumn({ columnId: 'codigo', renderHeaderCell: () => 'Código Infor', renderCell: (c) => c.codigoInfor ?? '—' }),
      createTableColumn({
        columnId: 'estado',
        renderHeaderCell: () => 'Estado',
        renderCell: (c) => (
          <span className={estilos.estado}>
            <Badge appearance="tint" color={c.activo ? 'success' : 'danger'}>
              {c.activo ? 'Activo' : 'Inactivo'}
            </Badge>
            {afectaAlReporte(c) && (
              <Badge appearance="tint" color="warning" title={TEXTO_AFECTA_REPORTE}>
                Afecta al reporte
              </Badge>
            )}
          </span>
        ),
      }),
      createTableColumn({ columnId: 'personas', renderHeaderCell: () => 'Personas guardadas', renderCell: (c) => c.personasGuardadas }),
      createTableColumn({ columnId: 'erp', renderHeaderCell: () => 'Activos ERP', renderCell: (c) => c.activosErp ?? '—' }),
    ],
    [estilos.estado],
  )

  const abrir = (c: CargoInfor) => {
    setAviso(undefined)
    setEditando(c)
  }

  return (
    <section className={estilos.pagina}>
      <div className={estilos.encabezado}>
        <Text as="h2" size={600} weight="semibold">
          Cargos Infor
        </Text>
        {isFetching && !isPending && <Spinner size="tiny" label="Actualizando…" />}
        <Button
          className={estilos.importar}
          appearance="primary"
          icon={<CloudArrowDown20Regular />}
          onClick={() => {
            setAviso(undefined)
            setImportando(true)
          }}
        >
          Importar puestos desde el ERP
        </Button>
      </div>
      <Text className={estilos.secundario}>{TEXTO_CODIGO_INFOR}</Text>

      {aviso && (
        <MessageBar intent="success">
          <MessageBarBody>
            <MessageBarTitle>{aviso}</MessageBarTitle>
          </MessageBarBody>
        </MessageBar>
      )}

      {importando && <DialogoImportarCargos onCerrar={() => setImportando(false)} />}
      {editando && (
        <DialogoEditarCargo
          // La clave reinicia el formulario si se abre otro cargo.
          key={editando.id}
          cargo={editando}
          onCerrar={() => setEditando(null)}
          onGuardado={() => {
            setEditando(null)
            setAviso(AVISO_GUARDADO)
          }}
          onRecargar={async () => {
            setEditando(null)
            await refetch()
          }}
        />
      )}

      <form
        className={estilos.filtros}
        onSubmit={(evento) => {
          evento.preventDefault()
          actualizar({ texto: texto.trim() || undefined })
        }}
      >
        <Input
          className={estilos.texto}
          aria-label="Buscar por cargo o código"
          placeholder="Cargo o código"
          contentBefore={<Search20Regular />}
          value={texto}
          onChange={(_, d) => setTexto(d.value)}
        />
        <Button className={estilos.boton} type="submit">
          Buscar
        </Button>
        <Select
          className={estilos.boton}
          aria-label="Código Infor"
          value={filtros.filtro}
          onChange={(_, d) => actualizar({ filtro: d.value as FiltroCodigo })}
        >
          {FILTROS_CODIGO.map((f) => (
            <option key={f.valor} value={f.valor}>
              {f.etiqueta}
            </option>
          ))}
        </Select>
        <Checkbox
          label="Incluir inactivos"
          checked={filtros.incluirInactivos}
          onChange={(_, d) => actualizar({ incluirInactivos: d.checked === true })}
        />
      </form>

      {isPending && !error && (
        <Skeleton aria-label="Cargando cargos" className={estilos.esqueleto}>
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
          <MessageBarBody>No hay cargos con estos filtros.</MessageBarBody>
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
          <DataGrid className={estilos.tabla} items={data.items} columns={columnas} getRowId={(c: CargoInfor) => c.id} focusMode="composite">
            <DataGridHeader>
              <DataGridRow>{({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}</DataGridRow>
            </DataGridHeader>
            <DataGridBody<CargoInfor>>
              {({ item, rowId }) => (
                <DataGridRow<CargoInfor>
                  key={rowId}
                  className={mergeClasses(estilos.fila, !item.activo && estilos.inactivo)}
                  onClick={() => abrir(item)}
                  onKeyDown={(evento: KeyboardEvent<HTMLDivElement>) => {
                    if (evento.key === 'Enter') {
                      abrir(item)
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
          unidad={['cargo', 'cargos']}
          tamanos={[TAMANO_PAGINA]}
          deshabilitada={isFetching}
          onPagina={(pagina) => actualizar({ pagina })}
          onTamano={() => undefined}
        />
      )}
    </section>
  )
}
