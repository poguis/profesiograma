import { describe, expect, it } from 'vitest'
import { ErrorApi } from '../../api/errores'
import {
  DETALLE_EMPLEADOS_NO_DISPONIBLE,
  TITULO_EMPLEADOS_NO_DISPONIBLE,
  aEmpleadoFila,
  detalleEmpleado,
  errorBuscador,
  marcasEmpleado,
} from './buscadorEmpleados'
import { claveEmpleado } from './formularioProyecto'
import type { EmpleadoBusqueda } from './tipos'

// TAREA-26d-2: buscador por codigoEkon (datos ficticios).
const item = (cambios: Partial<EmpleadoBusqueda> = {}): EmpleadoBusqueda => ({
  codigoEkon: 'SIM001',
  nombreCompleto: 'PERSONA FICTICIA 1',
  cargo: 'PUESTO X',
  departamento: 'UNIDAD SISTEMA INTEGRADO DE GESTION',
  unidad: 'UNIDAD SISTEMA INTEGRADO DE GESTION',
  ...cambios,
})

describe('buscador de empleados (TAREA-26d-2)', () => {
  it('la fila se arma por código, sin id, departamento ni unidad', () => {
    expect(aEmpleadoFila(item())).toEqual({ codigoEkon: 'SIM001', nombreCompleto: 'PERSONA FICTICIA 1', cargo: 'PUESTO X' })
  })

  it('detalle: código · cargo · departamento, y la unidad solo si es distinta', () => {
    expect(detalleEmpleado(item())).toBe('SIM001 · PUESTO X · UNIDAD SISTEMA INTEGRADO DE GESTION')
    expect(detalleEmpleado(item({ cargo: null, departamento: 'OPERACIONES', unidad: 'TALLER' }))).toBe('SIM001 · OPERACIONES · TALLER')
  })

  it('marcas "ya agregado" por código, sin distinguir mayúsculas ni espacios', () => {
    const etiquetas = new Map([[claveEmpleado(' dev006 '), ['P1', 'Back 2']]])
    expect(marcasEmpleado(etiquetas, item({ codigoEkon: 'DEV006' }))).toEqual(['P1', 'Back 2'])
    expect(marcasEmpleado(etiquetas, item({ codigoEkon: 'DEV007' }))).toEqual([])
  })

  it('503: "Servicio de empleados no disponible" con Reintentar (sin romper la pantalla)', () => {
    const conDetalle = new ErrorApi({ tipo: 'noDisponible', estado: 503, titulo: 'Servicio de empleados no disponible', detalle: 'Detalle del servidor.' })
    expect(errorBuscador(conDetalle)).toEqual({ titulo: TITULO_EMPLEADOS_NO_DISPONIBLE, detalle: 'Detalle del servidor.', reintentable: true })
    expect(errorBuscador(new ErrorApi({ tipo: 'noDisponible', estado: 503 }))).toEqual({
      titulo: TITULO_EMPLEADOS_NO_DISPONIBLE,
      detalle: DETALLE_EMPLEADOS_NO_DISPONIBLE,
      reintentable: true,
    })
  })

  it('red y 5xx se reintentan; 400 no', () => {
    expect(errorBuscador(new ErrorApi({ tipo: 'red', estado: null })).reintentable).toBe(true)
    expect(errorBuscador(new ErrorApi({ tipo: 'servidor', estado: 500 })).reintentable).toBe(true)
    const invalido = errorBuscador(new ErrorApi({ tipo: 'validacion', estado: 400, titulo: 'Parámetros de consulta no válidos.' }))
    expect(invalido).toEqual({ titulo: 'Parámetros de consulta no válidos.', detalle: null, reintentable: false })
  })
})
