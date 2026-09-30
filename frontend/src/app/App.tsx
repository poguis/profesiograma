import { createBrowserRouter } from 'react-router'
import { RouterProvider } from 'react-router/dom'
import { DisenoPrincipal } from '../components/DisenoPrincipal'
import { Inicio } from '../pages/Inicio'
import { NoAutorizado } from '../pages/NoAutorizado'
import { NoEncontrado } from '../pages/NoEncontrado'
import { Proveedores } from './Proveedores'

const router = createBrowserRouter([
  {
    element: <DisenoPrincipal />,
    children: [
      { index: true, element: <Inicio /> },
      { path: 'no-autorizado', element: <NoAutorizado /> },
      // Aquí se agregará "proyectos" (src/features/proyectos).
      { path: '*', element: <NoEncontrado /> },
    ],
  },
])

export default function App() {
  return (
    <Proveedores>
      <RouterProvider router={router} />
    </Proveedores>
  )
}
