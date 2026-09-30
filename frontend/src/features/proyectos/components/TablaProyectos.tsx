import {
  DataGrid,
  DataGridBody,
  DataGridCell,
  DataGridHeader,
  DataGridHeaderCell,
  DataGridRow,
  createTableColumn,
  makeStyles,
  tokens,
  type TableColumnDefinition,
} from '@fluentui/react-components'
import { useMemo, type KeyboardEvent } from 'react'
import { formatearFecha, formatearHora } from '../../../utils/formato'
import { useNombresCatalogo } from '../hooks'
import type { ProyectoResumen } from '../tipos'
import { CeldaBacks } from './CeldaBacks'
import { EtiquetaEstado } from './EtiquetaEstado'

const useEstilos = makeStyles({
  fila: {
    cursor: 'pointer',
    ':focus-visible': { outline: `${tokens.strokeWidthThick} solid ${tokens.colorStrokeFocus2}` },
  },
})

function horario(p: ProyectoResumen): string {
  return p.horaEntrada && p.horaSalida ? `${formatearHora(p.horaEntrada)} – ${formatearHora(p.horaSalida)}` : '—'
}

export interface TablaProyectosProps {
  proyectos: ProyectoResumen[]
  onAbrir: (id: number) => void
}

export function TablaProyectos({ proyectos, onAbrir }: TablaProyectosProps) {
  const estilos = useEstilos()
  const nombres = useNombresCatalogo()

  const columnas = useMemo<TableColumnDefinition<ProyectoResumen>[]>(
    () => [
      createTableColumn({ columnId: 'codigo', renderHeaderCell: () => 'Código', renderCell: (p) => p.codigo }),
      createTableColumn({ columnId: 'nombre', renderHeaderCell: () => 'Nombre', renderCell: (p) => p.nombre }),
      createTableColumn({ columnId: 'grupo', renderHeaderCell: () => 'Grupo', renderCell: (p) => nombres.grupo(p.grupo) }),
      createTableColumn({
        columnId: 'estado',
        renderHeaderCell: () => 'Estado',
        renderCell: (p) => <EtiquetaEstado codigo={p.estado} nombre={nombres.estado(p.estado)} />,
      }),
      createTableColumn({ columnId: 'inicio', renderHeaderCell: () => 'Inicio', renderCell: (p) => formatearFecha(p.fechaInicio) }),
      createTableColumn({ columnId: 'fin', renderHeaderCell: () => 'Fin', renderCell: (p) => formatearFecha(p.fechaFin) }),
      createTableColumn({ columnId: 'responsable', renderHeaderCell: () => 'Responsable', renderCell: (p) => p.responsable ?? '—' }),
      createTableColumn({ columnId: 'backs', renderHeaderCell: () => 'Backs', renderCell: (p) => <CeldaBacks backs={p.backs} /> }),
      createTableColumn({ columnId: 'horario', renderHeaderCell: () => 'Horario', renderCell: (p) => horario(p) }),
    ],
    [nombres],
  )

  return (
    <DataGrid items={proyectos} columns={columnas} getRowId={(p: ProyectoResumen) => p.id} focusMode="composite">
      <DataGridHeader>
        <DataGridRow>{({ renderHeaderCell }) => <DataGridHeaderCell>{renderHeaderCell()}</DataGridHeaderCell>}</DataGridRow>
      </DataGridHeader>
      <DataGridBody<ProyectoResumen>>
        {({ item, rowId }) => (
          <DataGridRow<ProyectoResumen>
            key={rowId}
            className={estilos.fila}
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
  )
}
