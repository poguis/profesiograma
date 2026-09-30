import { Text, makeStyles, tokens } from '@fluentui/react-components'
import { Person24Regular } from '@fluentui/react-icons'
import { Outlet } from 'react-router'
import { ErrorApi } from '../api/errores'
import { devAuthActivo } from '../auth'
import { SelectorUsuarioDev } from '../auth/SelectorUsuarioDev'
import { useUsuarioActual } from '../auth/useUsuarioActual'
import { MenuLateral } from './MenuLateral'

const useEstilos = makeStyles({
  raiz: {
    display: 'grid',
    gridTemplateRows: 'auto 1fr',
    gridTemplateColumns: '220px 1fr',
    height: '100vh',
  },
  barra: {
    gridColumn: '1 / -1',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: tokens.spacingHorizontalL,
    padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalL}`,
    backgroundColor: tokens.colorBrandBackground,
    color: tokens.colorNeutralForegroundOnBrand,
  },
  titulo: { color: tokens.colorNeutralForegroundOnBrand },
  derecha: { display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalL },
  usuario: { display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalXS },
  lateral: {
    borderRight: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke2}`,
    backgroundColor: tokens.colorNeutralBackground2,
    padding: tokens.spacingVerticalM,
  },
  contenido: { padding: tokens.spacingHorizontalXXL, overflow: 'auto' },
})

/** Barra superior (app, usuario actual, selector dev) + menú lateral + contenido de la ruta. */
export function DisenoPrincipal() {
  const estilos = useEstilos()

  return (
    <div className={estilos.raiz}>
      <header className={estilos.barra}>
        <Text as="h1" size={500} weight="semibold" className={estilos.titulo}>
          PROFESIOGRAMA
        </Text>
        <div className={estilos.derecha}>
          <UsuarioBarra />
          {devAuthActivo && <SelectorUsuarioDev />}
        </div>
      </header>
      <nav className={estilos.lateral} aria-label="Menú principal">
        <MenuLateral />
      </nav>
      <main className={estilos.contenido}>
        <Outlet />
      </main>
    </div>
  )
}

function UsuarioBarra() {
  const estilos = useEstilos()
  const { data, error, isPending } = useUsuarioActual()

  let texto: string
  if (isPending) {
    texto = 'Cargando…'
  } else if (data) {
    texto = data.nombreMostrar ?? data.email
  } else if (error instanceof ErrorApi && error.tipo === 'noAutenticado') {
    texto = 'No autenticado'
  } else {
    texto = 'Usuario no disponible'
  }

  return (
    <span className={estilos.usuario}>
      <Person24Regular aria-hidden />
      <Text>{texto}</Text>
    </span>
  )
}
