import { Card, MessageBar, MessageBarBody, Text, makeStyles, tokens } from '@fluentui/react-components'
import type { ReactNode } from 'react'
import type { EmpleadoFila } from '../formularioProyecto'

const useEstilos = makeStyles({
  encabezado: {
    display: 'flex',
    flexWrap: 'wrap',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: tokens.spacingHorizontalS,
  },
  titulo: { display: 'flex', flexDirection: 'column', minWidth: 0 },
  acciones: { display: 'flex', gap: tokens.spacingHorizontalXS },
  campos: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(180px, 1fr))',
    gap: tokens.spacingHorizontalM,
    alignItems: 'start',
  },
})

export interface TarjetaPersonaProps {
  /** "P1", "Back 2"… */
  etiqueta: string
  empleado: EmpleadoFila
  acciones: ReactNode
  /** Advertencias visibles (fuera de rango, relación eliminada…). */
  advertencias: string[]
  children: ReactNode
}

/** Una persona del proyecto: encabezado con acciones y campos en grilla (una columna en móvil). */
export function TarjetaPersona({ etiqueta, empleado, acciones, advertencias, children }: TarjetaPersonaProps) {
  const estilos = useEstilos()

  return (
    <Card>
      <div className={estilos.encabezado}>
        <div className={estilos.titulo}>
          <Text weight="semibold">
            {etiqueta} · {empleado.nombreCompleto}
          </Text>
          <Text size={200}>
            {empleado.codigoEkon}
            {empleado.cargo ? ` · ${empleado.cargo}` : ''}
          </Text>
        </div>
        <div className={estilos.acciones}>{acciones}</div>
      </div>

      {advertencias.map((mensaje) => (
        <MessageBar key={mensaje} intent="warning">
          <MessageBarBody>{mensaje}</MessageBarBody>
        </MessageBar>
      ))}

      <div className={estilos.campos}>{children}</div>
    </Card>
  )
}
