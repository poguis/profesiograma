import { Badge, Text, makeStyles, tokens } from '@fluentui/react-components'
import { formatearHora } from '../../../utils/formato'
import { ATENUACION_DESCANSO, COLOR_CHOQUE, abreviatura, atenuar, contrasteTexto } from '../cronograma'
import type { LeyendaNovedad, ProyectoCronograma } from '../tipos'
import { FONDO_RAYADO } from './CuadriculaCronograma'

const useEstilos = makeStyles({
  leyenda: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalS },
  grupo: { display: 'flex', flexWrap: 'wrap', gap: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalL}`, margin: 0, padding: 0, listStyle: 'none' },
  item: { display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalXS },
  muestra: {
    display: 'inline-flex',
    alignItems: 'center',
    justifyContent: 'center',
    minWidth: '30px',
    height: '20px',
    padding: '0 2px',
    fontSize: tokens.fontSizeBase100,
    fontWeight: tokens.fontWeightBold,
    borderRadius: tokens.borderRadiusSmall,
  },
  ficha: { borderRadius: tokens.borderRadiusCircular, border: '2px solid #FFFFFF', boxShadow: `0 0 0 1px ${tokens.colorNeutralStroke2}` },
  secundario: { color: tokens.colorNeutralForeground3 },
})

export interface LeyendaCronogramaProps {
  proyectos: ProyectoCronograma[]
  novedades: LeyendaNovedad[]
  colores: ReadonlyMap<number, string>
}

/** Proyectos (con su horario y la marca "Nocturno"), novedades presentes y estados fijos. */
export function LeyendaCronograma({ proyectos, novedades, colores }: LeyendaCronogramaProps) {
  const estilos = useEstilos()
  const muestra = (fondo: string, texto: string, ficha = false) => (
    <span
      className={ficha ? `${estilos.muestra} ${estilos.ficha}` : estilos.muestra}
      style={{ background: fondo, color: contrasteTexto(fondo.startsWith('#') ? fondo : '#E6E6E6').color }}
      aria-hidden
    >
      {texto}
    </span>
  )

  return (
    <section className={estilos.leyenda} aria-label="Leyenda del cronograma">
      <Text weight="semibold">Leyenda</Text>

      {proyectos.length > 0 && (
        <ul className={estilos.grupo} aria-label="Proyectos">
          {proyectos.map((p) => {
            const horario = p.horario.entrada && p.horario.salida
              ? `${formatearHora(p.horario.entrada)}–${formatearHora(p.horario.salida)}`
              : 'Sin horario'
            return (
              <li key={p.id} className={estilos.item}>
                {muestra(colores.get(p.id) ?? '#737373', abreviatura(p.nombre))}
                <Text size={200}>
                  {p.codigo} – {p.nombre} <span className={estilos.secundario}>({horario})</span>
                </Text>
                {p.horario.cruzaMedianoche && (
                  <Badge appearance="tint" color="informative" size="small" title="Las horas después de las 00:00 pertenecen al día de entrada">
                    Nocturno
                  </Badge>
                )}
              </li>
            )
          })}
        </ul>
      )}

      <ul className={estilos.grupo} aria-label="Novedades y estados">
        {novedades.map((n) => (
          <li key={n.tipo} className={estilos.item}>
            {muestra(n.color, n.sigla, true)}
            <Text size={200}>{nombreTipo(n.tipo)}</Text>
          </li>
        ))}
        <li className={estilos.item}>
          {muestra(COLOR_CHOQUE, 'C')}
          <Text size={200}>Choque (más de un proyecto el mismo día)</Text>
        </li>
        <li className={estilos.item}>
          {muestra(atenuar('#737373', ATENUACION_DESCANSO), 'D-')}
          <Text size={200}>Descanso del proyecto (color del proyecto atenuado)</Text>
        </li>
        <li className={estilos.item}>
          {muestra(FONDO_RAYADO, '')}
          <Text size={200}>Otro proyecto (de otro usuario)</Text>
        </li>
      </ul>
    </section>
  )
}

/** "PERMISO_MEDICO" → "Permiso medico". */
function nombreTipo(tipo: string): string {
  const texto = tipo.replaceAll('_', ' ').toLowerCase()
  return texto.charAt(0).toUpperCase() + texto.slice(1)
}
