import { Text, makeStyles, tokens } from '@fluentui/react-components'
import { Link } from 'react-router'

const useEstilos = makeStyles({
  pagina: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
})

/** 403: el usuario está autenticado pero no tiene permisos. */
export function NoAutorizado() {
  const estilos = useEstilos()

  return (
    <section className={estilos.pagina}>
      <Text as="h2" size={600} weight="semibold">
        Acceso no autorizado
      </Text>
      <Text>No tiene permisos para ver esta página.</Text>
      <Link to="/">Volver al inicio</Link>
    </section>
  )
}
