import { Button, Field, makeStyles, tokens } from '@fluentui/react-components'
import { ArrowLeft20Regular, ArrowRight20Regular } from '@fluentui/react-icons'
import { type ReactElement, useState } from 'react'
import { SelectorFecha } from '../../../components/SelectorFecha'
import { hoyEnNegocio } from '../../../utils/formato'
import { fechasAlCambiar, mesAnterior, mesSiguiente } from '../cronograma'
import type { Cronograma } from '../tipos'

const useEstilos = makeStyles({
  campo: { minWidth: '170px' },
  botones: { display: 'flex', gap: tokens.spacingHorizontalXS, marginTop: '26px' }, // alinea con los controles
})

export interface Rango {
  desde?: string
  hasta?: string
}

export interface SelectorRangoProps {
  /** Rango pedido (URL en /cronograma; estado local en la pestaña del proyecto). */
  pedido: Rango
  /** Rango efectivo de la respuesta (D6, D12 y D5 revisada); tiene prioridad sobre el pedido en los selectores. */
  efectivo?: { desde: string; hasta: string }
  /** Última respuesta: da la fecha que el usuario no tocó (fechasAlCambiar). */
  ultimaRespuesta?: Pick<Cronograma, 'desde' | 'hasta' | 'hastaSolicitado'>
  /** Errores 400 de la API en "desde" y "hasta". */
  errores?: { desde?: string; hasta?: string }
  onCambiar: (rango: Rango) => void
  /** Botón que vuelve al rango por defecto del backend: "Mes actual" (D6) o "Todo el proyecto" (D12). */
  reinicio: { texto: string; icono: ReactElement }
}

/**
 * Desde/hasta y accesos de mes del cronograma (TAREA-21b, Q1: extraído de FiltrosCronograma sin cambiar su
 * comportamiento). Devuelve un fragmento para quedar dentro del contenedor de filtros de quien lo usa.
 */
export function SelectorRango({ pedido, efectivo, ultimaRespuesta, errores, onCambiar, reinicio }: SelectorRangoProps) {
  const estilos = useEstilos()
  const [errorFormatoDesde, setErrorFormatoDesde] = useState<string>()
  const [errorFormatoHasta, setErrorFormatoHasta] = useState<string>()

  // Los selectores muestran el rango efectivo; el pedido conserva lo que el usuario eligió (D5 revisada).
  const desde = efectivo?.desde ?? pedido.desde
  const hasta = efectivo?.hasta ?? pedido.hasta
  // Se escriben las dos fechas: con una sola, el backend aplicaría D6 y movería la que el usuario no tocó.
  const cambiarFecha = (campo: 'desde' | 'hasta', valor: string | undefined) =>
    onCambiar(fechasAlCambiar(campo, valor, pedido, ultimaRespuesta, hoyEnNegocio()))

  return (
    <>
      <Field label="Desde" className={estilos.campo} validationMessage={errorFormatoDesde ?? errores?.desde}>
        <SelectorFecha valor={desde} onCambiar={(valor) => cambiarFecha('desde', valor)} onErrorFormato={setErrorFormatoDesde} />
      </Field>

      <Field label="Hasta" className={estilos.campo} validationMessage={errorFormatoHasta ?? errores?.hasta}>
        <SelectorFecha valor={hasta} onCambiar={(valor) => cambiarFecha('hasta', valor)} onErrorFormato={setErrorFormatoHasta} />
      </Field>

      <div className={estilos.botones}>
        <Button icon={<ArrowLeft20Regular />} disabled={!desde} onClick={() => desde && onCambiar(mesAnterior(desde))}>
          Mes anterior
        </Button>
        <Button icon={reinicio.icono} onClick={() => onCambiar({ desde: undefined, hasta: undefined })}>
          {reinicio.texto}
        </Button>
        <Button
          icon={<ArrowRight20Regular />}
          iconPosition="after"
          disabled={!desde}
          onClick={() => desde && onCambiar(mesSiguiente(desde))}
        >
          Mes siguiente
        </Button>
      </div>
    </>
  )
}
