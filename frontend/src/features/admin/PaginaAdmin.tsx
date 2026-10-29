import { Card, CardHeader, Text, makeStyles, tokens } from '@fluentui/react-components'
import { People24Regular, PersonAccounts24Regular } from '@fluentui/react-icons'
import { useNavigate } from 'react-router'
import { AdminRequerido } from './AdminRequerido'

const useEstilos = makeStyles({
  pagina: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL, maxWidth: '960px' },
  tarjetas: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))',
    gap: tokens.spacingHorizontalL,
    '@media (max-width: 599px)': { gridTemplateColumns: '1fr' },
  },
})

const TARJETAS = [
  {
    ruta: '/admin/personal-elegible',
    titulo: 'Personal elegible',
    descripcion: 'Grupos del ERP que se pueden elegir como personal nuevo.',
    icono: <People24Regular />,
  },
  {
    ruta: '/admin/usuarios',
    titulo: 'Usuarios y departamentos',
    descripcion: 'Alta, baja y departamentos de los usuarios de la aplicación.',
    icono: <PersonAccounts24Regular />,
  },
]

/** Ruta /admin (TAREA-25-2): centro de administración con una tarjeta por pantalla. Solo Admin. */
export function PaginaAdmin() {
  return (
    <AdminRequerido>
      <CentroAdmin />
    </AdminRequerido>
  )
}

function CentroAdmin() {
  const estilos = useEstilos()
  const navigate = useNavigate()
  return (
    <section className={estilos.pagina}>
      <Text as="h2" size={600} weight="semibold">
        Administración
      </Text>
      <div className={estilos.tarjetas}>
        {TARJETAS.map((t) => (
          <Card key={t.ruta} onClick={() => void navigate(t.ruta)} aria-label={t.titulo}>
            <CardHeader
              image={t.icono}
              header={<Text weight="semibold">{t.titulo}</Text>}
              description={<Text size={200}>{t.descripcion}</Text>}
            />
          </Card>
        ))}
      </div>
    </section>
  )
}
