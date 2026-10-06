import { Text, makeStyles, tokens } from '@fluentui/react-components'
import type { ReactNode } from 'react'
import { formatearFecha } from '../../../utils/formato'
import { textoPersona as persona, textoRango as rango, textoRol as rol } from '../textos'
import type { DiasEliminadosCambio, PersonaEliminadaCambio, PersonaRecortadaCambio } from '../tipos'

const useEstilos = makeStyles({
  seccion: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXXS },
  lista: { margin: 0, paddingLeft: tokens.spacingHorizontalL },
  secundario: { color: tokens.colorNeutralForeground3 },
})

/** Sección del impacto: título y lista, o "Sin cambios" si está vacía. */
export function SeccionImpacto({ titulo, vacia, children }: { titulo: string; vacia: boolean; children: ReactNode }) {
  const estilos = useEstilos()
  return (
    <div className={estilos.seccion}>
      <Text weight="semibold">{titulo}</Text>
      {vacia ? <Text className={estilos.secundario}>Sin cambios</Text> : <ul className={estilos.lista}>{children}</ul>}
    </div>
  )
}

export interface ImpactoRecorteProps {
  diasEliminados: DiasEliminadosCambio[]
  personalEliminado: PersonaEliminadaCambio[]
  personalRecortado: PersonaRecortadaCambio[]
}

/**
 * Días y personal que borra un recorte (RecorteProyecto, TAREA-14). Lo comparten "Cambiar estado" (TAREA-15) y
 * "Editar datos generales" (TAREA-19a). Listas en lugar de tablas: se leen bien en móvil.
 */
export function ImpactoRecorte({ diasEliminados, personalEliminado, personalRecortado }: ImpactoRecorteProps) {
  return (
    <>
      <SeccionImpacto titulo="Días que se eliminan" vacia={diasEliminados.length === 0}>
        {diasEliminados.map((d) => (
          <li key={`${d.empleado.id}|${d.rol}`}>
            {persona(d.empleado)} · {rol(d.rol)}: {d.cantidad} {d.cantidad === 1 ? 'día' : 'días'} ({rango(d.desde, d.hasta)})
          </li>
        ))}
      </SeccionImpacto>

      <SeccionImpacto titulo="Personal que se elimina" vacia={personalEliminado.length === 0}>
        {personalEliminado.map((p) => (
          <li key={`${p.rol}|${p.numero}`}>
            {rol(p.rol)} {p.numero} · {persona(p.empleado)} ({rango(p.fechaInicio, p.fechaFin)})
          </li>
        ))}
      </SeccionImpacto>

      <SeccionImpacto titulo="Personal recortado" vacia={personalRecortado.length === 0}>
        {personalRecortado.map((p) => (
          <li key={`${p.rol}|${p.numero}`}>
            {rol(p.rol)} {p.numero} · {persona(p.empleado)}: fin {formatearFecha(p.fechaFinAnterior)} →{' '}
            {formatearFecha(p.fechaFinNueva)}
          </li>
        ))}
      </SeccionImpacto>
    </>
  )
}
