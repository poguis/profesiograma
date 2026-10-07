import {
  Accordion,
  AccordionHeader,
  AccordionItem,
  AccordionPanel,
  Badge,
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
import { textoRol } from '../textos'
import { unirTramosContiguos } from '../tramos'
import type { PersonaCambioEdicion, PrevisualizacionPersonal, TramoCronograma } from '../tipos'
import { TablaCruces } from './TablaCruces'

const useEstilos = makeStyles({
  contenedor: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
  tabla: { overflowX: 'auto' },
})

const ACCIONES: Readonly<Record<string, { texto: string; color: 'informative' | 'brand' | 'warning' | 'danger' }>> = {
  SIN_CAMBIO: { texto: 'Sin cambio', color: 'informative' },
  MODIFICADO: { texto: 'Modificado', color: 'warning' },
  NUEVO: { texto: 'Nuevo', color: 'brand' },
  ELIMINADO: { texto: 'Se elimina', color: 'danger' },
}

const CLASES: Readonly<Record<string, string>> = { HISTORICO: 'Histórico', VIGENTE: 'Vigente', NUEVO: 'Nuevo' }

interface GrupoTramos {
  clave: string
  etiqueta: string
  tramos: TramoCronograma[]
  diasTrabajo: number
  diasDescanso: number
}

/** Vista previa de "Actualizar personal": personas con su acción, advertencias, cruces y tramos (unidos, pendiente 26). */
export function VistaPreviaPersonal({ datos, textoCorte }: { datos: PrevisualizacionPersonal; textoCorte?: string }) {
  const estilos = useEstilos()
  const grupos = useMemo(() => agruparTramos(datos), [datos])

  return (
    <div className={estilos.contenedor}>
      {/* TAREA-19c: la reactivación usa su propio texto (corte = fecha de reactivación). */}
      <Text>{textoCorte ?? `Corte: ${formatearFecha(datos.corte)}. Los días anteriores al corte no cambian.`}</Text>

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
              No se puede registrar. Ajuste el personal o las fechas y genere otra vista previa.
            </MessageBarBody>
          </MessageBar>
          <TablaCruces resumen={datos.resumen} />
        </>
      ) : (
        <MessageBar intent="success">
          <MessageBarBody>Sin cruces de asignación.</MessageBarBody>
        </MessageBar>
      )}

      <div className={estilos.tabla}>
        <Table size="small" aria-label="Personal y su acción">
          <TableHeader>
            <TableRow>
              <TableHeaderCell>Persona</TableHeaderCell>
              <TableHeaderCell>Clase</TableHeaderCell>
              <TableHeaderCell>Acción</TableHeaderCell>
            </TableRow>
          </TableHeader>
          <TableBody>
            {datos.personal.map((p) => (
              <TableRow key={p.clave}>
                <TableCell>{etiquetaPersona(p)}</TableCell>
                <TableCell>{CLASES[p.clase] ?? p.clase}</TableCell>
                <TableCell>
                  <Badge appearance="tint" color={ACCIONES[p.accion]?.color ?? 'informative'}>
                    {ACCIONES[p.accion]?.texto ?? p.accion}
                  </Badge>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>

      {grupos.length > 0 && (
        <Accordion multiple collapsible>
          {grupos.map((g) => (
            <AccordionItem key={g.clave} value={g.clave}>
              <AccordionHeader>
                {g.etiqueta} — {g.diasTrabajo} días de trabajo, {g.diasDescanso} de descanso
              </AccordionHeader>
              <AccordionPanel>
                <div className={estilos.tabla}>
                  <Table size="small" aria-label={`Tramos de ${g.etiqueta}`}>
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
                      {g.tramos.map((t) => (
                        <TableRow key={`${t.rol}|${t.tipo}|${t.bloque}|${t.inicio}`}>
                          <TableCell>{textoRol(t.rol)}</TableCell>
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

function etiquetaPersona(p: PersonaCambioEdicion): string {
  const prefijo = p.rol === 'PRINCIPAL' ? `P${p.numero}` : `Back ${p.numero}`
  return `${prefijo} · ${p.empleado.codigoEkon} · ${p.empleado.nombreCompleto}`
}

/** Tramos (ya unidos) por persona: principales y luego backs, por número. Esta vista previa no trae días por persona. */
function agruparTramos(datos: PrevisualizacionPersonal): GrupoTramos[] {
  const grupos = new Map<string, GrupoTramos & { orden: number }>()
  for (const t of unirTramosContiguos(datos.tramos)) {
    const clave = `${t.persona.rol}-${t.persona.numero}`
    let grupo = grupos.get(clave)
    if (!grupo) {
      const prefijo = t.persona.rol === 'PRINCIPAL' ? `P${t.persona.numero}` : `Back ${t.persona.numero}`
      grupo = {
        clave,
        orden: (t.persona.rol === 'PRINCIPAL' ? 0 : 1000) + t.persona.numero,
        etiqueta: `${prefijo} · ${t.codigoEkon} · ${t.nombreEmpleado}`,
        tramos: [],
        diasTrabajo: 0,
        diasDescanso: 0,
      }
      grupos.set(clave, grupo)
    }
    grupo.tramos.push(t)
    if (t.rol === 'DESCANSO') {
      grupo.diasDescanso += t.dias
    } else {
      grupo.diasTrabajo += t.dias
    }
  }
  return [...grupos.values()].sort((a, b) => a.orden - b.orden)
}
