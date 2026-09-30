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
      // Carga diferida: el código de proyectos (DataGrid, DatePicker, TabList…) va en chunks aparte.
      {
        path: 'proyectos',
        lazy: async () => ({ Component: (await import('../features/proyectos/pages/ListadoProyectos')).ListadoProyectos }),
      },
      {
        path: 'proyectos/:id',
        lazy: async () => ({ Component: (await import('../features/proyectos/pages/DetalleProyecto')).DetalleProyecto }),
      },
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
