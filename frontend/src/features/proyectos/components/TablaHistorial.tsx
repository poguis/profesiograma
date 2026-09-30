import { Table, TableBody, TableCell, TableHeader, TableHeaderCell, TableRow, Text } from '@fluentui/react-components'
import { formatearFecha, formatearFechaHora } from '../../../utils/formato'
import { useNombresCatalogo } from '../hooks'
import type { EtapaProyecto } from '../tipos'
import { EtiquetaEstado } from './EtiquetaEstado'

/** Historial de etapas (ya viene ordenado por versión desde la API). */
export function TablaHistorial({ etapas }: { etapas: EtapaProyecto[] }) {
  const nombres = useNombresCatalogo()

  if (etapas.length === 0) {
    return <Text>Sin historial registrado.</Text>
  }

  return (
    <Table aria-label="Historial de etapas" size="small">
      <TableHeader>
        <TableRow>
          <TableHeaderCell>Versión</TableHeaderCell>
          <TableHeaderCell>Movimiento</TableHeaderCell>
          <TableHeaderCell>Estado</TableHeaderCell>
          <TableHeaderCell>Inicio</TableHeaderCell>
          <TableHeaderCell>Fin</TableHeaderCell>
          <TableHeaderCell>Corte</TableHeaderCell>
          <TableHeaderCell>Actividad</TableHeaderCell>
          <TableHeaderCell>Registro (hora Ecuador)</TableHeaderCell>
        </TableRow>
      </TableHeader>
      <TableBody>
        {etapas.map((e) => (
          <TableRow key={e.version}>
            <TableCell>{e.version}</TableCell>
            <TableCell>{nombres.movimiento(e.tipoMovimiento)}</TableCell>
            <TableCell>
              <EtiquetaEstado codigo={e.estado} nombre={nombres.estado(e.estado)} />
            </TableCell>
            <TableCell>{formatearFecha(e.fechaInicio)}</TableCell>
            <TableCell>{formatearFecha(e.fechaFin)}</TableCell>
            <TableCell>{e.fechaCorte ? formatearFecha(e.fechaCorte) : '—'}</TableCell>
            <TableCell>{e.actividadCodigo ?? '—'}</TableCell>
            <TableCell>{formatearFechaHora(e.fechaRegistroUtc)}</TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}
