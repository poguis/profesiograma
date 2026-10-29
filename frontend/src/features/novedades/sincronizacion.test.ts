import { describe, expect, it } from 'vitest'
import { ErrorApi } from '../../api/errores'
import {
  NOTA_AUSENTES,
  SUGERENCIA_PROHIBIDO,
  etiquetaOmision,
  interpretarErrorSincronizacion,
  resumenSincronizacion,
} from './sincronizacion'
import type { ResultadoSincronizacion } from './tipos'

const base: ResultadoSincronizacion = {
  sincronizadaUtc: '2026-10-26T15:30:00Z',
  fuente: 'Simulado',
  leidas: 10,
  nuevas: 6,
  actualizadas: 0,
  anuladas: 0,
  reactivadas: 0,
  sinCambios: 0,
  ausentesEnOrigen: 1,
  omitidas: { tipoNoSoportado: 1, jsonInvalido: 1, sinEmpleado: 1, otroOrigen: 1, conflictoCodigo: 0 },
  codigosOmitidos: ['NOV-SIMPM-0004', 'NOV-SIMPM-0005', 'NOV-SIMPM-0006', 'NOV-SIMPM-0010'],
}

describe('etiquetaOmision (TAREA-24d, §8.5)', () => {
  it('las 11 categorías y una desconocida', () => {
    expect(
      [
        'filaIlegible',
        'uidInvalido',
        'jsonInvalido',
        'otroOrigen',
        'tipoNoSoportado',
        'aplicacionNoSoportada',
        'fechasInvalidas',
        'sinEmpleado',
        'conflictoCodigo',
        'conflictoLegacyId',
        'noActivaSinRegistro',
      ].map(etiquetaOmision),
    ).toEqual([
      'Fila ilegible',
      'Identificador inválido o repetido',
      'Detalle ilegible',
      'Otro origen (no es PERMISOS_MEDICOS)',
      'Tipo de novedad no soportado',
      'Aplicación no soportada',
      'Fechas inválidas',
      'Empleado no encontrado',
      'Conflicto de código',
      'Conflicto de identificador legado',
      'Anulada en el origen y sin registro aquí',
    ])
    expect(etiquetaOmision('categoriaNueva')).toBe('categoriaNueva')
    expect(etiquetaOmision('toString')).toBe('toString') // no hereda de Object.prototype
  })
})

describe('resumenSincronizacion', () => {
  it('conteos en orden fijo con los ceros; omitidas solo con n > 0', () => {
    const r = resumenSincronizacion(base)
    expect(r.conteos.map((f) => [f.etiqueta, f.valor])).toEqual([
      ['Leídas', 10],
      ['Nuevas', 6],
      ['Actualizadas', 0],
      ['Anuladas', 0],
      ['Reactivadas', 0],
      ['Sin cambios', 0],
      ['Ausentes en el origen', 1],
    ])
    expect(r.conteos.at(-1)?.nota).toBe(NOTA_AUSENTES)
    expect(r.totalOmitidas).toBe(4)
    expect(r.omitidas.map((f) => f.etiqueta)).toEqual([
      'Tipo de novedad no soportado',
      'Detalle ilegible',
      'Empleado no encontrado',
      'Otro origen (no es PERMISOS_MEDICOS)',
    ])
    expect(r.codigos).toHaveLength(4)
    expect(r.codigosTruncados).toBe(false)
  })

  it('sin omitidas ni códigos', () => {
    const r = resumenSincronizacion({ ...base, omitidas: {}, codigosOmitidos: [] })
    expect([r.totalOmitidas, r.omitidas, r.codigos]).toEqual([0, [], []])
  })

  it('exactamente 100 códigos activa la nota', () => {
    const codigos = Array.from({ length: 100 }, (_, i) => `fila ID ${i}`)
    expect(resumenSincronizacion({ ...base, codigosOmitidos: codigos }).codigosTruncados).toBe(true)
    expect(resumenSincronizacion({ ...base, codigosOmitidos: codigos.slice(1) }).codigosTruncados).toBe(false)
  })
})

describe('interpretarErrorSincronizacion', () => {
  it('503 «no configurada»: «título: detalle» con «Reintentar»', () => {
    const r = interpretarErrorSincronizacion(
      new ErrorApi({ tipo: 'noDisponible', estado: 503, titulo: 'Fuente de novedades externas no configurada', detalle: 'Configure el modo.' }),
    )
    expect(r).toMatchObject({ generales: ['Fuente de novedades externas no configurada: Configure el modo.'], ofrecerReintento: true })
  })

  it('503 sin detalle también ofrece «Reintentar»', () => {
    const r = interpretarErrorSincronizacion(new ErrorApi({ tipo: 'noDisponible', estado: 503, titulo: 'Base de datos no disponible' }))
    expect(r).toMatchObject({ generales: ['Base de datos no disponible'], ofrecerReintento: true })
  })

  it('403: el title y la sugerencia, sin «Reintentar»', () => {
    const r = interpretarErrorSincronizacion(new ErrorApi({ tipo: 'prohibido', estado: 403 }))
    expect(r.generales).toEqual(['No tiene permisos para esta acción.', SUGERENCIA_PROHIBIDO])
    expect(r.ofrecerReintento).toBe(false)
  })

  it('red: «Reintentar»', () => {
    const r = interpretarErrorSincronizacion(new ErrorApi({ tipo: 'red', estado: null }))
    expect(r).toMatchObject({ generales: ['No se pudo conectar con la API.'], ofrecerReintento: true })
  })
})
