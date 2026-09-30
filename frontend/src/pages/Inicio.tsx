import {
  Badge,
  Card,
  CardHeader,
  MessageBar,
  MessageBarBody,
  MessageBarTitle,
  Spinner,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components'
import { useState } from 'react'
import { ErrorApi } from '../api/errores'
import { devAuthActivo } from '../auth'
import { useUsuarioActual } from '../auth/useUsuarioActual'
import { EstadoError } from '../components/EstadoError'
import { formatearFecha } from '../utils/formato'

const useEstilos = makeStyles({
  pagina: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL, maxWidth: '640px' },
  tarjeta: { padding: tokens.spacingHorizontalL },
  datos: {
    display: 'grid',
    gridTemplateColumns: 'max-content 1fr',
    columnGap: tokens.spacingHorizontalL,
    rowGap: tokens.spacingVerticalS,
    alignItems: 'center',
  },
  roles: { display: 'flex', gap: tokens.spacingHorizontalXS, flexWrap: 'wrap' },
})

export function Inicio() {
  const estilos = useEstilos()
  const { data: usuario, error, isPending } = useUsuarioActual()
  const [hoy] = useState(() => formatearFecha(new Date())) // se calcula una vez (render puro)

  return (
    <section className={estilos.pagina}>
      <Text as="h2" size={600} weight="semibold">
        Inicio
      </Text>
      <Text>Hoy: {hoy}</Text>

      {isPending && <Spinner label="Consultando el usuario actual…" labelPosition="after" />}

      {error instanceof ErrorApi && error.tipo === 'noAutenticado' && (
        <MessageBar intent="warning">
          <MessageBarBody>
            <MessageBarTitle>No autenticado</MessageBarTitle>
            La API respondió 401: la petición no tiene una identidad válida.
            {devAuthActivo && ' Elija "admin" o "gestor" en el selector de usuario de la barra superior.'}
          </MessageBarBody>
        </MessageBar>
      )}

      {error && !(error instanceof ErrorApi && error.tipo === 'noAutenticado') && <EstadoError error={error} />}

      {usuario && (
        <Card className={estilos.tarjeta}>
          <CardHeader header={<Text weight="semibold">Usuario actual</Text>} />
          <div className={estilos.datos}>
            <Text weight="semibold">Nombre</Text>
            <Text>{usuario.nombreMostrar ?? '—'}</Text>
            <Text weight="semibold">Correo</Text>
            <Text>{usuario.email}</Text>
            <Text weight="semibold">Roles</Text>
            <span className={estilos.roles}>
              {usuario.roles.length === 0 ? (
                <Text>Sin roles</Text>
              ) : (
                usuario.roles.map((rol) => (
                  <Badge key={rol} appearance="filled" color="brand">
                    {rol}
                  </Badge>
                ))
              )}
            </span>
          </div>
        </Card>
      )}
    </section>
  )
}
