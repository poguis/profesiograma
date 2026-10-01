import {
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  makeStyles,
} from '@fluentui/react-components'
import type { ResumenCruce } from '../tipos'

const useEstilos = makeStyles({
  contenedor: { overflowX: 'auto' },
})

/**
 * Cruces agrupados como la app original: persona · rol · proyecto · mes · días ("5, 6, 7").
 * La usan la vista previa (200) y el 409 de registrar (extensión `resumen`).
 */
export function TablaCruces({ resumen }: { resumen: ResumenCruce[] }) {
  const estilos = useEstilos()

  return (
    <div className={estilos.contenedor}>
      <Table size="small" aria-label="Cruces de asignación">
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Persona</TableHeaderCell>
            <TableHeaderCell>Rol</TableHeaderCell>
            <TableHeaderCell>Proyecto</TableHeaderCell>
            <TableHeaderCell>Mes</TableHeaderCell>
            <TableHeaderCell>Días</TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {resumen.map((r) => (
            <TableRow key={`${r.nombreEmpleado}|${r.rol}|${r.proyecto}|${r.mes}`}>
              <TableCell>{r.nombreEmpleado}</TableCell>
              <TableCell>{r.rol}</TableCell>
              <TableCell>{r.proyecto}</TableCell>
              <TableCell>{r.mes}</TableCell>
              <TableCell>{r.dias}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  )
}
