import { Popover, PopoverSurface, Text, makeStyles, tokens } from '@fluentui/react-components'
import { resumenCelda } from '../cronograma'
import type { CeldaCronograma, PersonaCronograma, ProyectoCronograma } from '../tipos'

const useEstilos = makeStyles({
  superficie: { maxWidth: '360px', display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXS },
  lista: { margin: 0, paddingLeft: tokens.spacingHorizontalL },
  secundario: { color: tokens.colorNeutralForeground3 },
})

export interface DetalleCeldaProps {
  persona: PersonaCronograma
  celda: CeldaCronograma
  /** Celda que se pulsó: el Popover se ancla a ella (P4: uno solo para toda la cuadrícula). */
  destino: HTMLElement
  proyectos: ReadonlyMap<number, ProyectoCronograma>
  onCerrar: () => void
}

/** Detalle de una celda (clic o Enter). Nunca muestra datos de los proyectos ajenos (D2): solo su cantidad. */
export function DetalleCelda({ persona, celda, destino, proyectos, onCerrar }: DetalleCeldaProps) {
  const estilos = useEstilos()
  const r = resumenCelda(celda, proyectos)

  return (
    <Popover
      open
      withArrow
      trapFocus
      positioning={{ target: destino, position: 'below' }}
      onOpenChange={(_, datos) => {
        if (!datos.open) {
          onCerrar()
        }
      }}
    >
      <PopoverSurface aria-label={`Detalle del ${r.fecha}`} className={estilos.superficie}>
        <Text weight="semibold">
          {r.fecha} · {r.estado}
        </Text>
        <Text size={200} className={estilos.secundario}>
          {persona.nombre} (EKON {persona.codigoEkon})
        </Text>
        {r.novedad && <Text size={200}>Novedad: {r.novedad}</Text>}
        {r.asignaciones.length > 0 && (
          <ul className={estilos.lista}>
            {r.asignaciones.map((a) => (
              <li key={a}>
                <Text size={200}>{a}</Text>
              </li>
            ))}
          </ul>
        )}
        {r.otros && <Text size={200}>{r.otros}</Text>}
      </PopoverSurface>
    </Popover>
  )
}
