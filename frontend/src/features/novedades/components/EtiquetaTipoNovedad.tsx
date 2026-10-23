import { Text, makeStyles, tokens } from '@fluentui/react-components'
import { contrasteTexto } from '../../cronograma/cronograma'
import type { TipoNovedadResumen } from '../tipos'

const useEstilos = makeStyles({
  contenedor: { display: 'inline-flex', alignItems: 'center', gap: tokens.spacingHorizontalXS },
  sigla: {
    display: 'inline-block',
    minWidth: '28px',
    padding: `0 ${tokens.spacingHorizontalXS}`,
    borderRadius: tokens.borderRadiusMedium,
    textAlign: 'center',
    fontWeight: tokens.fontWeightSemibold,
    fontSize: tokens.fontSizeBase200,
    lineHeight: tokens.lineHeightBase300,
  },
})

/** Sigla sobre el color del tipo (como en el cronograma) y el nombre. Un color ilegible cae a gris. */
export function EtiquetaTipoNovedad({ tipo }: { tipo: TipoNovedadResumen }) {
  const estilos = useEstilos()
  const fondo = /^#[0-9A-Fa-f]{6}$/.test(tipo.color) ? tipo.color : '#E6E6E6'

  return (
    <span className={estilos.contenedor}>
      <span className={estilos.sigla} style={{ background: fondo, color: contrasteTexto(fondo).color }} aria-hidden>
        {tipo.sigla}
      </span>
      <Text>{tipo.nombre}</Text>
    </span>
  )
}
