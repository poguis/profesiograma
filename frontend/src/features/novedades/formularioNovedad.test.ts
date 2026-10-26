import { describe, expect, it } from 'vitest'
import { REGISTRO_VACIO, aSolicitudCrear, conAplicacion, tieneCambios, validarRegistro, type EstadoRegistro } from './formularioNovedad'

const persona: EstadoRegistro = {
  aplicacion: 'PERSONA',
  empleado: { codigoEkon: 'DEV001', nombreCompleto: 'Empleado de prueba', cargo: null },
  proyecto: null,
  tipo: 'VACACIONES',
  fechaInicio: '2070-03-02',
  fechaFin: '2070-03-06',
  observacion: '  viaje  ',
}

describe('formularioNovedad (TAREA-24b)', () => {
  it('vacío: obligatorios con los mensajes del backend; sin cambios', () => {
    expect(validarRegistro(REGISTRO_VACIO)).toEqual({
      tipoAplicacion: 'El tipo de aplicación es obligatorio.',
      tipoNovedad: 'El tipo de novedad es obligatorio.',
      fechaInicio: 'La fecha de inicio es obligatoria.',
      fechaFin: 'La fecha fin es obligatoria.',
    })
    expect(tieneCambios(REGISTRO_VACIO)).toBe(false)
  })

  it('sujeto obligatorio según la aplicación; GENERAL no lo pide', () => {
    expect(validarRegistro({ ...persona, empleado: null }).codigoEkon).toBe('El empleado es obligatorio.')
    expect(validarRegistro({ ...persona, aplicacion: 'PROYECTO', empleado: null }).proyectoId).toBe('El proyecto es obligatorio.')
    expect(validarRegistro({ ...persona, aplicacion: 'GENERAL', empleado: null })).toEqual({})
  })

  it('fin anterior al inicio, más de 366 días y observación de más de 500', () => {
    expect(validarRegistro({ ...persona, fechaFin: '2070-03-01' }).fechaFin).toBe('La fecha fin debe ser mayor o igual a la fecha inicio.')
    expect(validarRegistro({ ...persona, fechaInicio: '2070-01-01', fechaFin: '2071-01-02' }).fechaFin).toBe(
      'La novedad no puede superar 366 días.',
    )
    expect(validarRegistro({ ...persona, observacion: 'x'.repeat(501) }).observacion).toBe(
      'La observación no puede superar 500 caracteres.',
    )
    expect(validarRegistro({ ...persona, observacion: ` ${'x'.repeat(500)} ` })).toEqual({})
  })

  it('cambiar la aplicación limpia el sujeto y el tipo', () => {
    expect(conAplicacion(persona, 'PROYECTO')).toMatchObject({ aplicacion: 'PROYECTO', empleado: null, proyecto: null, tipo: undefined })
    expect(conAplicacion(persona, 'PERSONA')).toBe(persona)
  })

  it('solicitud: solo el sujeto de la aplicación y la observación recortada (vacía no viaja)', () => {
    expect(aSolicitudCrear(persona)).toEqual({
      tipoAplicacion: 'PERSONA',
      tipoNovedad: 'VACACIONES',
      codigoEkon: 'DEV001',
      fechaInicio: '2070-03-02',
      fechaFin: '2070-03-06',
      observacion: 'viaje',
    })
    const proyecto = { ...persona, aplicacion: 'PROYECTO' as const, proyecto: { id: 7, codigo: 'PRY-1', nombre: 'P' }, observacion: ' ' }
    expect(aSolicitudCrear(proyecto)).toEqual({
      tipoAplicacion: 'PROYECTO',
      tipoNovedad: 'VACACIONES',
      proyectoId: 7,
      fechaInicio: '2070-03-02',
      fechaFin: '2070-03-06',
    })
  })
})
