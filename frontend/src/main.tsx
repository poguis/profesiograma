import { StrictMode, type ReactNode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './app/App.tsx'
import { TITULO_FALLO_MSAL, TITULO_REQUIERE_HTTPS, decidirArranque } from './auth/entra'
import { faltantesEntra, inicializarMsal } from './auth/msal'

const raiz = createRoot(document.getElementById('root')!)

// Función (no componente): main.tsx no exporta nada y así no rompe Fast Refresh (oxlint only-export-components).
function aviso(texto: string): ReactNode {
  return <p style={{ padding: 16 }}>{texto}</p>
}

// Fase 7b: en modo Entra (todo lo que no es DevAuth, y siempre en el build) MSAL se inicializa antes del primer render.
// TAREA-7d-1 (J4): antes de MSAL se revisa el contexto seguro; un fallo de MSAL muestra un aviso, nunca una página en
// blanco. Condición literal de DevAuth, como en auth/index.ts.
const arranque = decidirArranque({
  devAuth: import.meta.env.DEV && import.meta.env.VITE_AUTH_MODE === 'dev',
  contextoSeguro: window.isSecureContext,
  faltan: faltantesEntra(),
})

let contenido: ReactNode
if (arranque === 'requiereHttps') {
  contenido = aviso(TITULO_REQUIERE_HTTPS)
} else if (arranque === 'configIncompleta') {
  contenido = aviso(`Configuración de Entra ID incompleta: falta ${faltantesEntra().join(', ')}.`)
} else if (arranque === 'devAuth') {
  contenido = <App />
} else {
  try {
    await inicializarMsal()
    contenido = <App />
  } catch {
    contenido = aviso(TITULO_FALLO_MSAL)
  }
}

raiz.render(<StrictMode>{contenido}</StrictMode>)
