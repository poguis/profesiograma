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
      // Antes de ':id' para que "nuevo" no se tome como un id.
      {
        path: 'proyectos/nuevo',
        lazy: async () => ({ Component: (await import('../features/proyectos/pages/NuevoProyecto')).NuevoProyecto }),
      },
      {
        path: 'proyectos/:id/personal',
        lazy: async () => ({ Component: (await import('../features/proyectos/pages/ActualizarPersonal')).ActualizarPersonal }),
      },
      {
        path: 'proyectos/:id/reactivar',
        lazy: async () => ({ Component: (await import('../features/proyectos/pages/ReactivarProyecto')).ReactivarProyecto }),
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
