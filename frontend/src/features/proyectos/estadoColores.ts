/**
 * Colores de EstadoProyecto. Referencia: formato de la columna ESTADO de la lista SharePoint "SIG PROYECTOS",
 * que usa las clases sp-css-backgroundColor-BgGreen / BgGold / BgRed / BgCoral.
 * No hay valores hex exactos: se usan tokens de la paleta de Fluent UI v9.
 * Fluent no tiene "Coral": se usa **Peach** como reemplazo para TERMINADO.
 */
export type TonoEstado = 'verde' | 'dorado' | 'rojo' | 'coral' | 'neutro'

export const TONO_POR_ESTADO: Readonly<Record<string, TonoEstado>> = {
  ACTIVO: 'verde', // BgGreen → colorPaletteGreen*
  SUSPENDIDO: 'dorado', // BgGold → colorPaletteGold*
  INACTIVO: 'rojo', // BgRed → colorPaletteRed*
  TERMINADO: 'coral', // BgCoral → colorPalettePeach* (Fluent no tiene Coral)
}

export function tonoEstado(codigo: string): TonoEstado {
  return TONO_POR_ESTADO[codigo] ?? 'neutro'
}
