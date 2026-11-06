// Fase 7b: puerta de la aplicación en modo Entra. Sin cuenta: un ssoSilent y luego el botón (ajuste 2, nunca un redirect
// automático). Con cuenta: la aplicación, o «No tienes acceso…» si la API o Entra niegan el rol (P1, P5).
import { InteractionStatus } from '@azure/msal-browser'
import { MsalProvider, useMsal } from '@azure/msal-react'
import { Button, MessageBar, MessageBarBody, MessageBarTitle, Spinner, Text, makeStyles, tokens } from '@fluentui/react-components'
import { useQueryClient } from '@tanstack/react-query'
import { useEffect, useRef, useState, type ReactNode } from 'react'
import { DETALLE_SIN_ACCESO, TITULO_SIN_ACCESO, decidirInicio, pantallaPorErrorUsuario, uriRedireccionSilenciosa } from './entra'
import { borrarPista, clienteMsal, guardarPista, leerPista, scopeApi } from './msal'
import { useUsuarioActual } from './useUsuarioActual'

const useEstilos = makeStyles({
  pantalla: {
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'flex-start',
    gap: tokens.spacingVerticalM,
    maxWidth: '520px',
    margin: '0 auto',
    padding: `${tokens.spacingVerticalXXXL} ${tokens.spacingHorizontalL}`,
  },
})

export function SesionEntra({ children }: { children: ReactNode }) {
  return (
    <MsalProvider instance={clienteMsal()}>
      <Puerta>{children}</Puerta>
    </MsalProvider>
  )
}

function Puerta({ children }: { children: ReactNode }) {
  const { instance, accounts, inProgress } = useMsal()
  const ssoIntentado = useRef(false)
  const [mostrarBoton, setMostrarBoton] = useState(false)
  const hayCuenta = accounts.length > 0

  useEffect(() => {
    if (inProgress !== InteractionStatus.None) {
      return
    }
    const accion = decidirInicio({ hayCuenta, ssoIntentado: ssoIntentado.current })
    if (accion === 'listo') {
      if (!instance.getActiveAccount()) {
        instance.setActiveAccount(accounts[0])
      }
      return
    }
    if (accion === 'boton') {
      setMostrarBoton(true)
      return
    }
    ssoIntentado.current = true
    instance
      // TAREA-7d-4: el iframe vuelve a redirect.html (puente de MSAL 5), no a la SPA completa.
      .ssoSilent({ scopes: [scopeApi()], loginHint: leerPista(), redirectUri: uriRedireccionSilenciosa(window.location.origin) })
      .then((r) => {
        instance.setActiveAccount(r.account)
        guardarPista(r.account.username)
      })
      .catch(() => setMostrarBoton(true))
  }, [hayCuenta, inProgress, instance, accounts])

  if (hayCuenta) {
    return <ControlAcceso>{children}</ControlAcceso>
  }
  if (mostrarBoton) {
    return <PantallaIniciarSesion />
  }
  return <Spinner label="Verificando la sesión…" labelPosition="after" />
}

function ControlAcceso({ children }: { children: ReactNode }) {
  const { error } = useUsuarioActual()
  const pantalla = pantallaPorErrorUsuario(error)
  if (pantalla === 'sinAcceso') {
    return <PantallaSinAcceso />
  }
  if (pantalla === 'iniciarSesion') {
    return <PantallaIniciarSesion aviso="La sesión venció; vuelva a iniciar sesión." />
  }
  return <>{children}</>
}

function PantallaIniciarSesion({ aviso }: { aviso?: string }) {
  const estilos = useEstilos()
  const { instance } = useMsal()
  return (
    <section className={estilos.pantalla}>
      <Text as="h1" size={600} weight="semibold">
        PROFESIOGRAMA
      </Text>
      {aviso && <Text>{aviso}</Text>}
      <Button appearance="primary" onClick={() => void instance.loginRedirect({ scopes: [scopeApi()], loginHint: leerPista() })}>
        Iniciar sesión con la cuenta de SEDEMI
      </Button>
    </section>
  )
}

function PantallaSinAcceso() {
  const estilos = useEstilos()
  return (
    <section className={estilos.pantalla}>
      <MessageBar intent="warning">
        <MessageBarBody>
          <MessageBarTitle>{TITULO_SIN_ACCESO}</MessageBarTitle>
          {DETALLE_SIN_ACCESO}
        </MessageBarBody>
      </MessageBar>
      <BotonCerrarSesion />
    </section>
  )
}

/** Cierra la sesión de Entra y descarta la caché de datos (solo en modo Entra; requiere MsalProvider). */
export function BotonCerrarSesion() {
  const { instance } = useMsal()
  const queryClient = useQueryClient()
  const cerrar = () => {
    queryClient.clear()
    borrarPista()
    void instance.logoutRedirect({ account: instance.getActiveAccount() })
  }
  return (
    <Button appearance="secondary" onClick={cerrar}>
      Cerrar sesión
    </Button>
  )
}
