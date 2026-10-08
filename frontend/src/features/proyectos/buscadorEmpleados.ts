// Buscador de empleados (TAREA-26d-2): lógica pura, sin React. Los empleados vienen de la API de empleados (caché en
// memoria del servidor); la clave es `codigoEkon`. Ver docs/fases/FASE_4_Empleados_API.md §4–§5.
import { ErrorApi } from '../../api/errores'
import { type EmpleadoFila, claveEmpleado } from './formularioProyecto'
import type { EmpleadoBusqueda } from './tipos'

/** Título del 503 de la API de empleados (ErpNoDisponibleExceptionHandler, operación "empleados"). */
export const TITULO_EMPLEADOS_NO_DISPONIBLE = 'Servicio de empleados no disponible'
export const DETALLE_EMPLEADOS_NO_DISPONIBLE =
  'No se pudo consultar los empleados en el ERP. Intente nuevamente en unos minutos.'

/** Lo que se guarda en la fila al agregar (sin departamento ni unidad). */
export function aEmpleadoFila(e: EmpleadoBusqueda): EmpleadoFila {
  return { codigoEkon: e.codigoEkon, nombreCompleto: e.nombreCompleto, cargo: e.cargo }
}

/** Segunda línea del ítem: "código · cargo · departamento · unidad" (la unidad solo si es distinta del departamento). */
export function detalleEmpleado(e: EmpleadoBusqueda): string {
  const unidad = e.unidad && e.unidad !== e.departamento ? e.unidad : null
  return [e.codigoEkon, e.cargo, e.departamento, unidad].filter(Boolean).join(' · ')
}

/** Marcas "Ya agregado: …" del ítem, por código (sin distinguir mayúsculas ni espacios extremos). */
export function marcasEmpleado(etiquetas: Map<string, string[]>, e: EmpleadoBusqueda): string[] {
  return etiquetas.get(claveEmpleado(e.codigoEkon)) ?? []
}

export interface ErrorBuscador {
  titulo: string
  detalle: string | null
  /** 503 y red: se ofrece "Reintentar" (la pantalla sigue usable). */
  reintentable: boolean
}

/** Error de la búsqueda sin romper la pantalla: 503 → "Servicio de empleados no disponible" con "Reintentar". */
export function errorBuscador(error: unknown): ErrorBuscador {
  if (!(error instanceof ErrorApi)) {
    return { titulo: 'Ocurrió un error inesperado al buscar empleados.', detalle: null, reintentable: true }
  }
  if (error.tipo === 'noDisponible') {
    return {
      titulo: TITULO_EMPLEADOS_NO_DISPONIBLE,
      detalle: error.detalle ?? DETALLE_EMPLEADOS_NO_DISPONIBLE,
      reintentable: true,
    }
  }
  return {
    titulo: error.titulo,
    detalle: error.detalle ?? null,
    reintentable: error.tipo === 'red' || error.tipo === 'servidor',
  }
}
