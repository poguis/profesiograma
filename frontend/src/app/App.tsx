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
      // TAREA-21: cronograma persona × día (la política Gestor la impone la API).
      {
        path: 'cronograma',
        lazy: async () => ({ Component: (await import('../features/cronograma/pages/PaginaCronograma')).PaginaCronograma }),
      },
      // TAREA-24a: novedades (la API decide la visibilidad, J2). 'nueva' (24b) antes de ':id'.
      {
        path: 'novedades',
        lazy: async () => ({ Component: (await import('../features/novedades/pages/ListadoNovedades')).ListadoNovedades }),
      },
      {
        path: 'novedades/nueva',
        lazy: async () => ({ Component: (await import('../features/novedades/pages/NuevaNovedad')).NuevaNovedad }),
      },
      {
        path: 'novedades/:id/editar',
        lazy: async () => ({ Component: (await import('../features/novedades/pages/EditarNovedad')).EditarNovedad }),
      },
      {
        path: 'novedades/:id',
        lazy: async () => ({ Component: (await import('../features/novedades/pages/DetalleNovedad')).DetalleNovedad }),
      },
      // TAREA-25-2: centro de administración y usuarios (Admin; las páginas no llaman al backend si el usuario no lo es).
      {
        path: 'admin',
        lazy: async () => ({ Component: (await import('../features/admin/PaginaAdmin')).PaginaAdmin }),
      },
      {
        path: 'admin/usuarios',
        lazy: async () => ({ Component: (await import('../features/admin/usuarios/PaginaUsuarios')).PaginaUsuarios }),
      },
      {
        path: 'admin/usuarios/:id',
        lazy: async () => ({ Component: (await import('../features/admin/usuarios/PaginaUsuario')).PaginaUsuario }),
      },
      // TAREA-62-2: criterio de personal elegible (Admin; la pantalla no llama al backend si el usuario no lo es).
      {
        path: 'admin/personal-elegible',
        lazy: async () => ({
          Component: (await import('../features/admin/personal-elegible/PaginaPersonalElegible')).PaginaPersonalElegible,
        }),
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
