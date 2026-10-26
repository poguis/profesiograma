import { describe, expect, it } from 'vitest'
import {
  MENSAJE_MOTIVO,
  MENSAJE_SIN_DIAS,
  aSolicitudEditar,
  alternarDia,
  conRango,
  diasResultantes,
  estadoInicial,
  hayCambios,
  incluirTodos,
  resumenCambios,
  validarEdicion,
  validarMotivo,
  type NovedadOriginal,
} from './edicionNovedad'

// Viernes 03/01/2070 a martes 07/01/2070, con el domingo 05 excluido.
const original: NovedadOriginal = {
  tipo: { codigo: 'VACACIONES', nombre: 'Vacaciones', sigla: 'V', color: '#0277BD' },
  fechaInicio: '2070-01-03',
  fechaFin: '2070-01-07',
  dias: ['2070-01-03', '2070-01-04', '2070-01-06', '2070-01-07'],
  observacion: 'viaje',
}
const nombre = (codigo: string) => (codigo === 'PERMISO' ? 'Permiso' : codigo)

describe('edicionNovedad (TAREA-24c)', () => {
  it('estado inicial: excluidos = días del rango que no están guardados; sin cambios', () => {
    const e = estadoInicial(original)
    expect(e).toEqual({ tipo: 'VACACIONES', fechaInicio: '2070-01-03', fechaFin: '2070-01-07', excluidos: ['2070-01-05'], observacion: 'viaje' })
    expect(diasResultantes(e)).toEqual(original.dias)
    expect(hayCambios(original, e)).toBe(false)
    expect(resumenCambios(original, e, nombre)).toEqual([])
  })

  it('cambio de tipo', () => {
    const e = { ...estadoInicial(original), tipo: 'PERMISO' }
    expect(hayCambios(original, e)).toBe(true)
    expect(resumenCambios(original, e, nombre)).toEqual(['Tipo: Vacaciones → Permiso'])
  })

  it('ampliar el rango: los días nuevos (fin de semana incluido) entran incluidos', () => {
    const { estado, recortados } = conRango(estadoInicial(original), '2070-01-03', '2070-01-12')
    expect(recortados).toEqual([])
    expect(diasResultantes(estado)).toHaveLength(9) // 10 días menos el 05 excluido; sábado 11 y domingo 12 incluidos
    expect(resumenCambios(original, estado, nombre)).toEqual(['Rango: 03/01/2070–07/01/2070 → 03/01/2070–12/01/2070', 'Días: 4 → 9 (1 excluido)'])
  })

  it('acortar el rango quita los excluidos que quedan fuera (A4)', () => {
    const { estado, recortados } = conRango(estadoInicial(original), '2070-01-06', '2070-01-07')
    expect(recortados).toEqual(['2070-01-05'])
    expect(estado.excluidos).toEqual([])
    expect(aSolicitudEditar(estado, 'AAAAAAAAB9E=')).toEqual({
      version: 'AAAAAAAAB9E=',
      tipoNovedad: 'VACACIONES',
      fechaInicio: '2070-01-06',
      fechaFin: '2070-01-07',
      diasExcluidos: [],
      observacion: 'viaje',
    })
  })

  it('volver a encender el día excluido es un cambio; excluir todos da «al menos 1 día»', () => {
    const e = estadoInicial(original)
    expect(hayCambios(original, alternarDia(e, '2070-01-05'))).toBe(true)
    let todos = e
    for (const dia of original.dias) todos = alternarDia(todos, dia)
    expect(diasResultantes(todos)).toEqual([])
    expect(validarEdicion(todos)).toEqual({ diasExcluidos: MENSAJE_SIN_DIAS })
    expect(diasResultantes(incluirTodos(todos))).toHaveLength(5)
  })

  it('366 días se permiten y 367 no', () => {
    const e = { ...estadoInicial(original), excluidos: [] }
    expect(validarEdicion({ ...e, fechaInicio: '2070-01-01', fechaFin: '2071-01-01' })).toEqual({})
    expect(validarEdicion({ ...e, fechaInicio: '2070-01-01', fechaFin: '2071-01-02' }).fechaFin).toBe('La novedad no puede superar 366 días.')
  })

  it('observación: null y vacía equivalen; borrarla es un cambio y viaja como null', () => {
    const sinObs = { ...original, observacion: null }
    expect(hayCambios(sinObs, { ...estadoInicial(sinObs), observacion: '   ' })).toBe(false)
    const borrada = { ...estadoInicial(original), observacion: '  ' }
    expect(hayCambios(original, borrada)).toBe(true)
    expect(resumenCambios(original, borrada, nombre)).toEqual(['Observación: borrada'])
    expect(aSolicitudEditar(borrada, 'v').observacion).toBeNull()
    expect(resumenCambios(original, { ...borrada, observacion: 'otra' }, nombre)).toEqual(['Observación: cambiada'])
  })
})

describe('validarMotivo (N10)', () => {
  it('5 a 500 caracteres recortados', () => {
    expect(validarMotivo('abcd')).toBe(MENSAJE_MOTIVO)
    expect(validarMotivo('abcde')).toBeUndefined()
    expect(validarMotivo('x'.repeat(500))).toBeUndefined()
    expect(validarMotivo('x'.repeat(501))).toBe(MENSAJE_MOTIVO)
    expect(validarMotivo('          ')).toBe(MENSAJE_MOTIVO)
    expect(validarMotivo('  abcd  ')).toBe(MENSAJE_MOTIVO)
  })
})
