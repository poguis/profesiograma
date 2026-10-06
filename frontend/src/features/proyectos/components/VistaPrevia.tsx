import {
  Accordion,
  AccordionHeader,
  AccordionItem,
  AccordionPanel,
  MessageBar,
  MessageBarBody,
  MessageBarTitle,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components'
import { useMemo } from 'react'
import { formatearFecha } from '../../../utils/formato'
import type { Previsualizacion, TramoCronograma } from '../tipos'
import { TablaCruces } from './TablaCruces'

const useEstilos = makeStyles({
  contenedor: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
  tabla: { overflowX: 'auto' },
})

const ROL_DESCANSO = 'DESCANSO'

interface GrupoPersona {
  clave: string
  /** Principales primero (P1..n), luego backs. */
  orden: number
  etiqueta: string
  tramos: TramoCronograma[]
  diasTrabajo: number
  diasDescanso: number
}

/** R11: resumen, cruces agrupados y tramos por persona. Informativa: el servidor recalcula todo al registrar. */
export function VistaPrevia({ datos }: { datos: Previsualizacion }) {
  const estilos = useEstilos()
  const personas = useMemo(() => agruparPorPersona(datos), [datos])
  const totalDias = personas.reduce((suma, p) => suma + p.diasTrabajo + p.diasDescanso, 0)

  return (
    <div className={estilos.contenedor}>
      <Text>
        {personas.length} {personas.length === 1 ? 'persona' : 'personas'} · {datos.tramos.length} tramos ·{' '}
        {totalDias} días asignados
      </Text>

      {/* TAREA-19y: avisos que no bloquean (p. ej. proyecto sin principal). */}
      {datos.advertencias.length > 0 && (
        <MessageBar intent="warning">
          <MessageBarBody>
            <MessageBarTitle>Advertencias</MessageBarTitle>
            {datos.advertencias.map((a) => (
              <div key={a}>{a}</div>
            ))}
          </MessageBarBody>
        </MessageBar>
      )}

      {datos.cruces.length > 0 ? (
        <>
          <MessageBar intent="error">
            <MessageBarBody>
              <MessageBarTitle>Hay {datos.cruces.length} días con cruces de asignación.</MessageBarTitle>
              No se puede registrar el proyecto. Ajuste el personal o las fechas y genere otra vista previa.
            </MessageBarBody>
          </MessageBar>
          <TablaCruces resumen={datos.resumen} />
        </>
      ) : (
        <MessageBar intent="success">
          <MessageBarBody>Sin cruces de asignación.</MessageBarBody>
        </MessageBar>
      )}

      {personas.length > 0 && (
        <Accordion multiple collapsible>
          {personas.map((p) => (
            <AccordionItem key={p.clave} value={p.clave}>
              <AccordionHeader>
                {p.etiqueta} — {p.diasTrabajo} días de trabajo, {p.diasDescanso} de descanso
              </AccordionHeader>
              <AccordionPanel>
                <div className={estilos.tabla}>
                  <Table size="small" aria-label={`Tramos de ${p.etiqueta}`}>
                    <TableHeader>
                      <TableRow>
                        <TableHeaderCell>Rol</TableHeaderCell>
                        <TableHeaderCell>Tipo</TableHeaderCell>
                        <TableHeaderCell>Bloque</TableHeaderCell>
                        <TableHeaderCell>Inicio</TableHeaderCell>
                        <TableHeaderCell>Fin</TableHeaderCell>
                        <TableHeaderCell>Días</TableHeaderCell>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {p.tramos.map((t) => (
                        <TableRow key={`${t.rol}|${t.bloque}|${t.inicio}`}>
                          <TableCell>{t.rol}</TableCell>
                          <TableCell>{t.tipo}</TableCell>
                          <TableCell>{t.bloque}</TableCell>
                          <TableCell>{formatearFecha(t.inicio)}</TableCell>
                          <TableCell>{formatearFecha(t.fin)}</TableCell>
                          <TableCell>{t.dias}</TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>
              </AccordionPanel>
            </AccordionItem>
          ))}
        </Accordion>
      )}
    </div>
  )
}

/** Principales y luego backs, por número; tramos por fecha. Los días salen de `diasPorPersona` (días finales). */
function agruparPorPersona(datos: Previsualizacion): GrupoPersona[] {
  const claveDe = (persona: { rol: string; numero: number }) => `${persona.rol}-${persona.numero}`

  return datos.diasPorPersona
    .map((d): GrupoPersona => {
      const clave = claveDe(d.persona)
      const prefijo = d.persona.rol === 'PRINCIPAL' ? `P${d.persona.numero}` : `Back ${d.persona.numero}`
      const descanso = d.dias.filter((dia) => dia.rol === ROL_DESCANSO).length
      return {
        clave,
        orden: (d.persona.rol === 'PRINCIPAL' ? 0 : 1000) + d.persona.numero,
        etiqueta: `${prefijo} · ${d.codigoEkon} · ${d.nombreEmpleado}`,
        tramos: datos.tramos
          .filter((t) => claveDe(t.persona) === clave)
          .sort((a, b) => a.inicio.localeCompare(b.inicio)),
        diasTrabajo: d.dias.length - descanso,
        diasDescanso: descanso,
      }
    })
    .sort((a, b) => a.orden - b.orden)
}
