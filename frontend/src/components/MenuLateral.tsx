import { makeStyles, mergeClasses, tokens } from '@fluentui/react-components'
import { Briefcase24Regular, CalendarCancel24Regular, CalendarLtr24Regular, Home24Regular, Settings24Regular } from '@fluentui/react-icons'
import { NavLink } from 'react-router'
import { useUsuarioActual } from '../auth/useUsuarioActual'
import { esAdmin } from '../auth/roles'

const useEstilos = makeStyles({
  lista: {
    listStyle: 'none',
    margin: 0,
    padding: 0,
    display: 'flex',
    flexDirection: 'column',
    gap: tokens.spacingVerticalXS,
    // U4 (TAREA-24a): en el celular el menú es una fila con desplazamiento horizontal.
    '@media (max-width: 767px)': { flexDirection: 'row', gap: tokens.spacingHorizontalXS },
  },
  item: {
    '@media (max-width: 767px)': { whiteSpace: 'nowrap' },
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
  const { data: usuario } = useUsuarioActual() // misma consulta (y caché) que la barra superior

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
      <li>
        <NavLink
          to="/cronograma"
          className={({ isActive }) => mergeClasses(estilos.item, estilos.enlace, isActive && estilos.activo)}
        >
          <CalendarLtr24Regular aria-hidden />
          Cronograma
        </NavLink>
      </li>
      <li>
        <NavLink
          to="/novedades"
          className={({ isActive }) => mergeClasses(estilos.item, estilos.enlace, isActive && estilos.activo)}
        >
          <CalendarCancel24Regular aria-hidden />
          Novedades
        </NavLink>
      </li>
      {/* TAREA-62-2 / 25-2: solo para Admin (solo decide si se ve; la API exige la política Admin igual). Abre el centro
          /admin y queda marcado en todas sus subpáginas. */}
      {esAdmin(usuario) && (
        <li>
          <NavLink
            to="/admin"
            className={({ isActive }) => mergeClasses(estilos.item, estilos.enlace, isActive && estilos.activo)}
          >
            <Settings24Regular aria-hidden />
            Administración
          </NavLink>
        </li>
      )}
    </ul>
  )
}
