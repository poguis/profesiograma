// TAREA-7d-4 (G1, J1): puente de redirección de MSAL 5. En el iframe oculto de ssoSilent/acquireTokenSilent lee la
// respuesta de Entra de la URL, limpia el fragmento y la envía a la ventana principal por BroadcastChannel
// (node_modules/@azure/msal-browser/src/redirect_bridge/index.ts). Sin el puente, la ventana principal espera 10 s y
// muestra el botón. Importa SOLO el puente: nada de la aplicación (ni React, ni auth/, ni estilos).
import { broadcastResponseToMainFrame } from '@azure/msal-browser/redirect-bridge'

// Abierto a mano (sin respuesta en la URL) el puente lanza: la página queda en blanco, sin errores sin capturar.
broadcastResponseToMainFrame().catch(() => {})
