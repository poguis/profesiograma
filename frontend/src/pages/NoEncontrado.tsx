import { Text, makeStyles, tokens } from '@fluentui/react-components'
import { Link } from 'react-router'

const useEstilos = makeStyles({
  pagina: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
})

export function NoEncontrado() {
  const estilos = useEstilos()

  return (
    <section className={estilos.pagina}>
      <Text as="h2" size={600} weight="semibold">
        Página no encontrada
      </Text>
      <Text>La dirección solicitada no existe.</Text>
      <Link to="/">Volver al inicio</Link>
    </section>
  )
}
