import { Button, Field, Text, ToggleButton, makeStyles, tokens } from '@fluentui/react-components'
import { formatearFecha } from '../../../utils/formato'
import { agruparPorMes, etiquetaDia } from '../diasNovedad'

const useEstilos = makeStyles({
  contenedor: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalS },
  encabezado: { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: tokens.spacingHorizontalM },
  mes: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXS },
  dias: { display: 'flex', flexWrap: 'wrap', gap: tokens.spacingHorizontalXS },
  chip: { minWidth: '64px' },
  secundario: { color: tokens.colorNeutralForeground3 },
})

export interface SelectorDiasExcluidosProps {
  /** Todos los días del rango, ordenados. */
  dias: string[]
  excluidos: string[]
  onAlternar: (dia: string) => void
  onIncluirTodos: () => void
  /** 400 `diasExcluidos` del servidor o «La novedad debe tener al menos 1 día.». */
  error?: string
  deshabilitado?: boolean
}

/** Días excluidos con chips por mes (U2): chip encendido = día incluido. Fines de semana incluidos por defecto (N6). */
export function SelectorDiasExcluidos({ dias, excluidos, onAlternar, onIncluirTodos, error, deshabilitado }: SelectorDiasExcluidosProps) {
  const estilos = useEstilos()
  const apagados = new Set(excluidos)
  const incluidos = dias.length - excluidos.length

  return (
    <Field label="Días" validationMessage={error} hint="Apague un día para excluirlo de la novedad.">
      <div className={estilos.contenedor}>
        <div className={estilos.encabezado}>
          <Text>
            {incluidos} {incluidos === 1 ? 'día' : 'días'}
            {excluidos.length > 0 && ` (${excluidos.length} ${excluidos.length === 1 ? 'excluido' : 'excluidos'})`}
          </Text>
          <Button size="small" disabled={deshabilitado || excluidos.length === 0} onClick={onIncluirTodos}>
            Incluir todos
          </Button>
        </div>
        {agruparPorMes(dias).map((mes) => (
          <div key={mes.clave} className={estilos.mes} role="group" aria-label={mes.titulo}>
            <Text weight="semibold" className={estilos.secundario}>
              {mes.titulo}
            </Text>
            <div className={estilos.dias}>
              {mes.dias.map((dia) => (
                <ToggleButton
                  key={dia}
                  size="small"
                  className={estilos.chip}
                  checked={!apagados.has(dia)}
                  disabled={deshabilitado}
                  title={formatearFecha(dia)}
                  onClick={() => onAlternar(dia)}
                >
                  {etiquetaDia(dia)}
                </ToggleButton>
              ))}
            </div>
          </div>
        ))}
      </div>
    </Field>
  )
}
