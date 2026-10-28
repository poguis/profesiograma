import { describe, expect, it } from 'vitest'
import { ErrorApi } from '../../api/errores'
import { CAMPOS_REGISTRO, interpretarErrorNovedad } from '../novedades/erroresNovedad'
import { textoGruposAsignables } from './buscadorEmpleados'
import { interpretarErrorPersonal } from './edicionPersonal'
import { distribuirErrores, mensajesSinCampo } from './formularioProyecto'
import { CAMPOS_VISIBLES_BACK, CAMPOS_VISIBLES_PRINCIPAL } from './camposVisibles'
import { interpretarErrorReactivacion } from './reactivacion'

// TAREA-62-2: criterio de personal elegible en las pantallas de personal (buscador y 400 en «…codigoEkon»).
const MENSAJE = 'El empleado SIM010 no pertenece a un grupo asignable.'

describe('textoGruposAsignables', () => {
  it('una, varias, vacía y ausente', () => {
    expect(textoGruposAsignables(['ADMINISTRATIVO'])).toBe('Solo grupos asignables: ADMINISTRATIVO.')
    expect(textoGruposAsignables(['ADMINISTRATIVO', ' OPERATIVO '])).toBe('Solo grupos asignables: ADMINISTRATIVO, OPERATIVO.')
    expect(textoGruposAsignables([])).toBeNull()
    expect(textoGruposAsignables(undefined)).toBeNull()
    expect(textoGruposAsignables(null)).toBeNull()
  })
})

describe('400 «no pertenece a un grupo asignable» junto a la persona, nunca solo arriba', () => {
  it('Nuevo proyecto: la fila del envío y su tarjeta (mensajesSinCampo)', () => {
    const s = distribuirErrores({ 'principales[0].codigoEkon': [MENSAJE], 'backs[0].codigoEkon': [MENSAJE] }, { principales: ['p1'], backs: ['k1'] })
    expect(s.generales).toEqual([])
    expect(mensajesSinCampo(s.filas.p1, CAMPOS_VISIBLES_PRINCIPAL)).toEqual([MENSAJE])
    expect(mensajesSinCampo(s.filas.k1, CAMPOS_VISIBLES_BACK)).toEqual([MENSAJE])
  })

  it('Actualizar personal: la persona nueva por su clave', () => {
    const r = interpretarErrorPersonal(
      { tipo: 'validacion', titulo: 'Datos no válidos.', errores: { 'backs[1].codigoEkon': [MENSAJE] } },
      { principales: ['p1'], backs: ['k1', 'kn'] },
    )
    expect(r.servidor.filas.kn).toEqual({ codigoEkon: MENSAJE })
    expect(r.servidor.generales).toEqual([])
    expect(r.dialogo.generales).toEqual([])
  })

  it('Nueva novedad (PERSONA): bajo el control «Persona» (clave codigoEkon)', () => {
    const r = interpretarErrorNovedad(new ErrorApi({ tipo: 'validacion', estado: 400, errores: { codigoEkon: [MENSAJE] } }), CAMPOS_REGISTRO)
    expect(r.campos.codigoEkon).toBe(MENSAJE)
    expect(r.generales).toEqual([])
  })

  it('Reactivar: la persona nueva por su clave', () => {
    const r = interpretarErrorReactivacion(
      { tipo: 'validacion', titulo: 'Datos no válidos.', errores: { 'principales[0].codigoEkon': [MENSAJE] } },
      { principales: ['p1'], backs: [] },
    )
    expect(r.servidor.filas.p1).toEqual({ codigoEkon: MENSAJE })
    expect(r.servidor.generales).toEqual([])
  })
})
