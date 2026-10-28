import { describe, expect, it } from 'vitest'
import { ErrorApi } from '../../../api/errores'
import {
  MENSAJE_SIN_CAMBIOS,
  MENSAJE_SIN_FAMILIAS,
  aSolicitud,
  alternarFamilia,
  estadoInicial,
  hayCambios,
  interpretarErrorCriterio,
  motivoSinGuardar,
  resumenCambio,
  type CriterioPersonal,
} from './criterioPersonal'

const get: CriterioPersonal = {
  valorActual: 'ADMINISTRATIVO',
  sinCriterio: false,
  familias: [
    { familia: 'ADMINISTRATIVO', activos: 188, asignable: true },
    { familia: 'OPERATIVO', activos: 20, asignable: false },
    { familia: 'MANDO MEDIO', activos: 0, asignable: true },
  ],
}

describe('criterioPersonal (TAREA-62-2)', () => {
  it('estado inicial: marcadas = asignables del GET; sin cambios', () => {
    const e = estadoInicial(get)
    expect(e.marcadas).toEqual(['ADMINISTRATIVO', 'MANDO MEDIO'])
    expect(e.valorAnterior).toBe('ADMINISTRATIVO')
    expect(hayCambios(e)).toBe(false)
    expect(motivoSinGuardar(e)).toBe(MENSAJE_SIN_CAMBIOS)
  })

  it('alternar una familia (también una con 0 activos) cambia el estado y respeta el orden del backend', () => {
    const conOperativo = alternarFamilia(estadoInicial(get), 'OPERATIVO')
    expect(conOperativo.marcadas).toEqual(['ADMINISTRATIVO', 'OPERATIVO', 'MANDO MEDIO'])
    expect(hayCambios(conOperativo)).toBe(true)
    expect(motivoSinGuardar(conOperativo)).toBeUndefined()

    const sinMando = alternarFamilia(estadoInicial(get), 'MANDO MEDIO')
    expect(sinMando.marcadas).toEqual(['ADMINISTRATIVO'])
    expect(hayCambios(alternarFamilia(sinMando, 'MANDO MEDIO'))).toBe(false) // volver al inicio
  })

  it('selección vacía: Guardar deshabilitado con su mensaje', () => {
    const vacia = alternarFamilia(alternarFamilia(estadoInicial(get), 'ADMINISTRATIVO'), 'MANDO MEDIO')
    expect(vacia.marcadas).toEqual([])
    expect(motivoSinGuardar(vacia)).toBe(MENSAJE_SIN_FAMILIAS)
  })

  it('resumen «antes → después» y cuerpo del PUT con valorAnterior del GET', () => {
    const e = alternarFamilia(alternarFamilia(estadoInicial(get), 'OPERATIVO'), 'MANDO MEDIO')
    expect(resumenCambio(e)).toBe('Se guardará: ADMINISTRATIVO, MANDO MEDIO → ADMINISTRATIVO, OPERATIVO')
    expect(aSolicitud(e)).toEqual({ familias: ['ADMINISTRATIVO', 'OPERATIVO'], valorAnterior: 'ADMINISTRATIVO' })

    const sinCriterio = estadoInicial({ valorActual: '', sinCriterio: true, familias: get.familias.map((f) => ({ ...f, asignable: false })) })
    expect(resumenCambio(alternarFamilia(sinCriterio, 'OPERATIVO'))).toBe('Se guardará: (sin criterio) → OPERATIVO')
  })
})

describe('interpretarErrorCriterio', () => {
  it('400 con varios mensajes en familias y valorAnterior: bajo la lista', () => {
    const r = interpretarErrorCriterio(
      new ErrorApi({
        tipo: 'validacion',
        estado: 400,
        errores: {
          familias: ['La familia ZZ no existe.', 'La familia A,B no puede contener comas.'],
          valorAnterior: ['Falta el valor anterior del criterio; recargue.'],
        },
      }),
    )
    expect(r.lista).toEqual([
      'La familia ZZ no existe.',
      'La familia A,B no puede contener comas.',
      'Falta el valor anterior del criterio; recargue.',
    ])
    expect(r.generales).toEqual([])
    expect([r.ofrecerRecarga, r.ofrecerReintento]).toEqual([false, false])
  })

  it('409 → title con «Recargar»', () => {
    const r = interpretarErrorCriterio(new ErrorApi({ tipo: 'conflicto', estado: 409, titulo: 'Otro usuario cambió el criterio; recargue.' }))
    expect(r).toMatchObject({ generales: ['Otro usuario cambió el criterio; recargue.'], ofrecerRecarga: true, ofrecerReintento: false })
  })

  it('403 → title sin «Reintentar»', () => {
    const r = interpretarErrorCriterio(new ErrorApi({ tipo: 'prohibido', estado: 403 }))
    expect(r).toMatchObject({ generales: ['No tiene permisos para esta acción.'], ofrecerReintento: false, ofrecerRecarga: false })
  })

  it('503 y red → «título: detalle» con «Reintentar»', () => {
    const r = interpretarErrorCriterio(
      new ErrorApi({ tipo: 'noDisponible', estado: 503, titulo: 'Servicio de empleados no disponible', detalle: 'Intente luego.' }),
    )
    expect(r).toMatchObject({ generales: ['Servicio de empleados no disponible: Intente luego.'], ofrecerReintento: true })
    expect(interpretarErrorCriterio(new ErrorApi({ tipo: 'red', estado: null })).ofrecerReintento).toBe(true)
  })
})
