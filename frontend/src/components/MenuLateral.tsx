import { makeStyles, mergeClasses, tokens } from '@fluentui/react-components'
import { Briefcase24Regular, Home24Regular } from '@fluentui/react-icons'
import { NavLink } from 'react-router'

const useEstilos = makeStyles({
  lista: { listStyle: 'none', margin: 0, padding: 0, display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXS },
  item: {
    display: 'flex',
    alignItems: 'center',
    gap: tokens.spacingHorizontalS,
    padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalM}`,
    borderRadius: tokens.borderRadiusMedium,
    color: tokens.colorNeutralForeground1,
    textDecoration: 'none',
  },
  enlace: { ':hover': { backgroundColor: tokens.colorNeutralBackground2Hover } },
  activo: { backgroundColor: tokens.colorNeutralBackground1Selected, fontWeight: tokens.fontWeightSemibold },
})

export function MenuLateral() {
  const estilos = useEstilos()

  return (
    <ul className={estilos.lista}>
      <li>
        <NavLink
          to="/"
          end
          className={({ isActive }) => mergeClasses(estilos.item, estilos.enlace, isActive && estilos.activo)}
        >
          <Home24Regular aria-hidden />
          Inicio
        </NavLink>
      </li>
      <li>
        <NavLink
          to="/proyectos"
          className={({ isActive }) => mergeClasses(estilos.item, estilos.enlace, isActive && estilos.activo)}
        >
          <Briefcase24Regular aria-hidden />
          Proyectos
        </NavLink>
      </li>
    </ul>
  )
}
