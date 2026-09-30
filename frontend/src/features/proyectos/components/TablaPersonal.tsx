import {
  Badge,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
} from '@fluentui/react-components'
import { Checkmark16Filled } from '@fluentui/react-icons'
import { formatearFecha } from '../../../utils/formato'
import type { PersonalProyecto } from '../tipos'

const NOMBRE_ROL: Record<string, string> = { PRINCIPAL: 'Principal', BACK: 'Back' }

export function TablaPersonal({ personal }: { personal: PersonalProyecto[] }) {
  if (personal.length === 0) {
    return <Text>Sin personal asignado.</Text>
  }

  return (
    <Table aria-label="Personal del proyecto" size="small">
      <TableHeader>
        <TableRow>
          <TableHeaderCell>Rol</TableHeaderCell>
          <TableHeaderCell>N°</TableHeaderCell>
          <TableHeaderCell>EKON</TableHeaderCell>
          <TableHeaderCell>Nombre</TableHeaderCell>
          <TableHeaderCell>Cargo</TableHeaderCell>
          <TableHeaderCell>Jornada</TableHeaderCell>
          <TableHeaderCell>Días trabajo / descanso</TableHeaderCell>
          <TableHeaderCell>Inicio</TableHeaderCell>
          <TableHeaderCell>Fin</TableHeaderCell>
          <TableHeaderCell>Principal inicial</TableHeaderCell>
        </TableRow>
      </TableHeader>
      <TableBody>
        {personal.map((p) => (
          <TableRow key={p.id}>
            <TableCell>
              <Badge appearance="tint" color={p.rol === 'PRINCIPAL' ? 'brand' : 'informative'}>
                {NOMBRE_ROL[p.rol] ?? p.rol}
              </Badge>
            </TableCell>
            <TableCell>{p.numero}</TableCell>
            <TableCell>{p.empleado.codigoEkon}</TableCell>
            <TableCell>{p.empleado.nombreCompleto}</TableCell>
            <TableCell>{p.cargo ?? '—'}</TableCell>
            <TableCell>{p.jornada?.nombre ?? '—'}</TableCell>
            <TableCell>
              {p.diasTrabajo ?? '—'} / {p.diasDescanso}
            </TableCell>
            <TableCell>{formatearFecha(p.fechaInicio)}</TableCell>
            <TableCell>{formatearFecha(p.fechaFin)}</TableCell>
            <TableCell>
              {p.esPrincipalInicial ? <Checkmark16Filled aria-label="Sí" /> : <span aria-label="No">—</span>}
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  )
}
