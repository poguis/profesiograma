import { makeStyles, mergeClasses, shorthands, tokens } from '@fluentui/react-components'
import { tonoEstado } from '../estadoColores'

const useEstilos = makeStyles({
  base: {
    display: 'inline-flex',
    alignItems: 'center',
    padding: `0 ${tokens.spacingHorizontalS}`,
    borderRadius: tokens.borderRadiusMedium,
    border: `${tokens.strokeWidthThin} solid transparent`,
    fontSize: tokens.fontSizeBase200,
    lineHeight: tokens.lineHeightBase300,
    fontWeight: tokens.fontWeightSemibold,
    whiteSpace: 'nowrap',
  },
  verde: {
    backgroundColor: tokens.colorPaletteGreenBackground2,
    color: tokens.colorPaletteGreenForeground2,
    ...shorthands.borderColor(tokens.colorPaletteGreenBorderActive),
  },
  dorado: {
    backgroundColor: tokens.colorPaletteGoldBackground2,
    color: tokens.colorPaletteGoldForeground2,
    ...shorthands.borderColor(tokens.colorPaletteGoldBorderActive),
  },
  rojo: {
    backgroundColor: tokens.colorPaletteRedBackground2,
    color: tokens.colorPaletteRedForeground2,
    ...shorthands.borderColor(tokens.colorPaletteRedBorderActive),
  },
  coral: {
    backgroundColor: tokens.colorPalettePeachBackground2,
    color: tokens.colorPalettePeachForeground2,
    ...shorthands.borderColor(tokens.colorPalettePeachBorderActive),
  },
  neutro: {
    backgroundColor: tokens.colorNeutralBackground3,
    color: tokens.colorNeutralForeground2,
    ...shorthands.borderColor(tokens.colorNeutralStroke1),
  },
})

/** Etiqueta de color del estado del proyecto (colores en estadoColores.ts). */
export function EtiquetaEstado({ codigo, nombre }: { codigo: string; nombre?: string }) {
  const estilos = useEstilos()
  return <span className={mergeClasses(estilos.base, estilos[tonoEstado(codigo)])}>{nombre ?? codigo}</span>
}
