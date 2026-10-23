import {
  Badge,
  DataGrid,
  DataGridBody,
  DataGridCell,
  DataGridHeader,
  DataGridHeaderCell,
  DataGridRow,
  createTableColumn,
  makeStyles,
  mergeClasses,
  tokens,
  type TableColumnDefinition,
} from '@fluentui/react-components'
import { useMemo, type KeyboardEvent } from 'react'
import { formatearFecha } from '../../../utils/formato'
import { textoSujeto, useNombresNovedad } from '../presentacion'
import type { NovedadResumen } from '../tipos'
import { EtiquetaTipoNovedad } from './EtiquetaTipoNovedad'

const useEstilos = makeStyles({
  // En el celular la tabla se desplaza a lo ancho sin rediseñarse.
  desplazable: { overflowX: 'auto' },
  tabla: { minWidth: '960px' },
  fila: {
    cursor: 'pointer',
    ':focus-visible': { outline: `${tokens.strokeWidthThick} solid ${tokens.colorStrokeFocus2}` },
  },
  anulada: { color: tokens.colorNeutralForeground3 },
})

export interface TablaNovedadesProps {
  novedades: NovedadResumen[]
  onAbrir: (id: number) => void
}

export function TablaNovedades({ novedades, onAbrir }: TablaNovedadesProps) {
  const estilos = useEstilos()
  const nombres = useNombresNovedad()

  const columnas = useMemo<TableColumnDefinition<NovedadResumen>[]>(
    () => [
      createTableColumn({ columnId: 'codigo', renderHeaderCell: () => 'Código', renderCell: (n) => n.codigo }),
      createTableColumn({
        columnId: 'tipo',
        renderHeaderCell: () => 'Tipo',
        renderCell: (n) => <EtiquetaTipoNovedad tipo={n.tipo} />,
      }),
      createTableColumn({
        columnId: 'aplicacion',
        renderHeaderCell: () => 'Aplicación',
        renderCell: (n) => nombres.aplicacion(n.aplicacion),
      }),
      createTableColumn({ columnId: 'sujeto', renderHeaderCell: () => 'Persona o proyecto', renderCell: (n) => textoSujeto(n.sujeto) }),
      createTableColumn({ columnId: 'inicio', renderHeaderCell: () => 'Inicio', renderCell: (n) => formatearFecha(n.fechaInicio) }),
      createTableColumn({ columnId: 'fin', renderHeaderCell: () => 'Fin', renderCell: (n) => formatearFecha(n.fechaFin) }),
      createTableColumn({ columnId: 'dias', renderHeaderCell: () => 'Días', renderCell: (n) => n.totalDias }),
      createTableColumn({ columnId: 'origen', renderHeaderCell: () => 'Origen', renderCell: (n) => nombres.origen(n.origen) }),
      createTableColumn({
        columnId: 'estado',
        renderHeaderCell: () => 'Estado',
        renderCell: (n) =>
          n.anulada ? (
            <Badge appearance="tint" color="danger">
              Anulada
            </Badge>
          ) : (
            <Badge appearance="tint" color="success">
              Vigente
            </Badge>
          ),
      }),
    ],
    [nombres],
  )

  return (
    <div className={estilos.desplazable}>
      <DataGrid
        className={estilos.tabla}
        items={novedades}
        columns={columnas}
        getRowId={(n: NovedadResumen) => n.id}
        focusMode="composite"
      >
        <DataGridHeader>
          <DataGridRow>{({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}</DataGridRow>
        </DataGridHeader>
        <DataGridBody<NovedadResumen>>
          {({ item, rowId }) => (
            <DataGridRow<NovedadResumen>
              key={rowId}
              className={mergeClasses(estilos.fila, item.anulada && estilos.anulada)}
              onClick={() => onAbrir(item.id)}
              onKeyDown={(evento: KeyboardEvent<HTMLDivElement>) => {
                if (evento.key === 'Enter') {
                  onAbrir(item.id)
                }
              }}
            >
              {({ renderCell }) => <DataGridCell>{renderCell(item)}</DataGridCell>}
            </DataGridRow>
          )}
        </DataGridBody>
      </DataGrid>
    </div>
  )
}
