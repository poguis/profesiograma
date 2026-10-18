import { describe, expect, it } from 'vitest'
import {
  type Turno,
  aMinutos,
  almuerzoOpcional,
  almuerzoParaTurno,
  esNocturno,
  opcionesAlmuerzo,
  textoTurno,
  validarAlmuerzo,
} from './almuerzoTurno'

// Horarios con las horas observadas en la B1 (TAREA-26c); minutos trabajados ilustrativos donde la B1 no los detalló.
const turno = (horaEntrada: string | null, horaSalida: string | null, minutosJornada: number | null = 480, minutosTrabajados: number | null = 420): Turno => ({
  horaEntrada,
  horaSalida,
  minutosJornada,
  minutosTrabajados,
})
const diurno = turno('07:00:00', '18:00:00', 660, 600)
const t73 = turno('17:00:00', '04:00:00', 660, 600)
const t118 = turno('22:00:00', '06:00:00', 480, 480)
const t110 = turno('15:30:00', '00:00:00', 510, 450)
const diurnas = { salida: ['11:00', '12:00', '13:00', '14:00'], regreso: ['12:00', '13:00', '14:00', '15:00'] }

describe('turno', () => {
  it('aMinutos lee "HH:mm" y "HH:mm:ss"', () => {
    expect(aMinutos('22:00:00')).toBe(1320)
    expect(aMinutos('06:30')).toBe(390)
    expect(aMinutos(null)).toBeNull()
    expect(aMinutos('x')).toBeNull()
  })

  it('nocturno = salida anterior a la entrada (00:00 es el fin del día)', () => {
    expect(esNocturno(t118)).toBe(true)
    expect(esNocturno(t73)).toBe(true)
    expect(esNocturno(t110)).toBe(true)
    expect(esNocturno(diurno)).toBe(false)
    expect(esNocturno(turno('00:00:00', '08:00:00'))).toBe(false)
    expect(esNocturno(turno('22:00:00', null))).toBe(false)
    expect(esNocturno(undefined)).toBe(false)
  })

  it('almuerzo opcional si jornada = trabajados', () => {
    expect(almuerzoOpcional(t118)).toBe(true)
    expect(almuerzoOpcional(t73)).toBe(false)
    expect(almuerzoOpcional(turno('07:00', '15:00', null, null))).toBe(false)
    expect(almuerzoOpcional(undefined)).toBe(false)
  })

  it('texto del turno para los mensajes', () => {
    expect(textoTurno(t118)).toBe('22:00–06:00')
    expect(textoTurno(t110)).toBe('15:30–00:00')
  })
})

describe('opcionesAlmuerzo', () => {
  it('diurno o sin horario: las del endpoint (RN09)', () => {
    expect(opcionesAlmuerzo(diurno, diurnas)).toBe(diurnas)
    expect(opcionesAlmuerzo(undefined, diurnas)).toBe(diurnas)
  })

  it('22:00–06:00: horas en punto dentro del turno, sin cruzar la medianoche', () => {
    // 23:00 no tiene regreso posible (00:00 o después cruzarían la medianoche); 00:00 no es regreso.
    expect(opcionesAlmuerzo(t118, diurnas)).toEqual({
      salida: ['22:00', '00:00', '01:00', '02:00', '03:00', '04:00', '05:00'],
      regreso: ['23:00', '01:00', '02:00', '03:00', '04:00', '05:00', '06:00'],
    })
  })

  it('17:00–04:00', () => {
    const o = opcionesAlmuerzo(t73, diurnas)
    expect(o.salida[0]).toBe('17:00')
    expect(o.salida).not.toContain('23:00')
    expect(o.salida.at(-1)).toBe('03:00')
    expect(o.regreso[0]).toBe('18:00')
    expect(o.regreso).not.toContain('00:00')
    expect(o.regreso.at(-1)).toBe('04:00')
  })

  it('15:30–00:00: desde las 16:00; el último regreso es 23:00', () => {
    expect(opcionesAlmuerzo(t110, diurnas)).toEqual({
      salida: ['16:00', '17:00', '18:00', '19:00', '20:00', '21:00', '22:00'],
      regreso: ['17:00', '18:00', '19:00', '20:00', '21:00', '22:00', '23:00'],
    })
  })
})

describe('validarAlmuerzo', () => {
  it('diurno: regreso posterior a la salida (como antes)', () => {
    expect(validarAlmuerzo(diurno, '13:00', '13:00')).toEqual({ regresoAlmuerzo: 'El regreso de almuerzo debe ser posterior a la salida.' })
    expect(validarAlmuerzo(undefined, '13:00', '14:00')).toEqual({})
  })

  it('nocturno: dentro del turno', () => {
    expect(validarAlmuerzo(t118, '02:00', '03:00')).toEqual({})
    expect(validarAlmuerzo(t118, '22:00', '23:00')).toEqual({})
    expect(validarAlmuerzo(t118, '05:00', '06:00')).toEqual({})
    expect(validarAlmuerzo(t118, '13:00', '14:00')).toEqual({
      salidaAlmuerzo: 'El almuerzo debe estar dentro del turno (22:00–06:00).',
      regresoAlmuerzo: 'El almuerzo debe estar dentro del turno (22:00–06:00).',
    })
    expect(validarAlmuerzo(t118, '06:00', null)).toEqual({ salidaAlmuerzo: 'El almuerzo debe estar dentro del turno (22:00–06:00).' })
    expect(validarAlmuerzo(t118, null, '22:00')).toEqual({ regresoAlmuerzo: 'El almuerzo debe estar dentro del turno (22:00–06:00).' })
  })

  it('nocturno: no cruza la medianoche y el regreso es posterior en el turno', () => {
    expect(validarAlmuerzo(t118, '23:00', '01:00')).toEqual({ regresoAlmuerzo: 'El almuerzo no puede cruzar la medianoche.' })
    expect(validarAlmuerzo(t110, '23:00', '00:00')).toEqual({ regresoAlmuerzo: 'El almuerzo no puede cruzar la medianoche.' })
    expect(validarAlmuerzo(t73, '02:00', '01:00')).toEqual({ regresoAlmuerzo: 'El regreso de almuerzo debe ser posterior a la salida.' })
  })
})

describe('almuerzoParaTurno', () => {
  it('a nocturno: se limpia lo que queda fuera del turno', () => {
    expect(almuerzoParaTurno(t118, diurnas, '13:00', '14:00')).toEqual({ salidaAlmuerzo: null, regresoAlmuerzo: null })
    expect(almuerzoParaTurno(t118, diurnas, '02:00', '14:00')).toEqual({ salidaAlmuerzo: '02:00', regresoAlmuerzo: null })
  })

  it('a diurno: se conservan las horas dentro del rango RN09 (incluso 13:30)', () => {
    expect(almuerzoParaTurno(diurno, diurnas, '13:30', '14:30')).toEqual({ salidaAlmuerzo: '13:30', regresoAlmuerzo: '14:30' })
    expect(almuerzoParaTurno(diurno, diurnas, '02:00', '03:00')).toEqual({ salidaAlmuerzo: null, regresoAlmuerzo: null })
    expect(almuerzoParaTurno(undefined, diurnas, null, null)).toEqual({ salidaAlmuerzo: null, regresoAlmuerzo: null })
  })
})
