import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'
import App from './app/App.tsx'
import { inicializarMsal } from './auth/msal'

const raiz = createRoot(document.getElementById('root')!)

// Fase 7b: en modo Entra (todo lo que no es DevAuth, y siempre en el build) MSAL se inicializa antes del primer render.
// Condición literal, como en auth/index.ts.
const faltan = import.meta.env.DEV && import.meta.env.VITE_AUTH_MODE === 'dev' ? [] : await inicializarMsal()

raiz.render(
  <StrictMode>
    {faltan.length > 0 ? (
      <p style={{ padding: 16 }}>Configuración de Entra ID incompleta: falta {faltan.join(', ')}.</p>
    ) : (
      <App />
    )}
  </StrictMode>,
)
