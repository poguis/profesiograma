import { describe, expect, it } from 'vitest'
import { ErrorApi } from '../../api/errores'
import { CAMPOS_REGISTRO, MENSAJE_DUPLICADO, interpretarErrorNovedad } from './erroresNovedad'

const interpretar = (error: ErrorApi) => interpretarErrorNovedad(error, CAMPOS_REGISTRO)

describe('interpretarErrorNovedad (TAREA-24b)', () => {
  it('400: cada clave a su control y `general` arriba, sin «Recargar»', () => {
    const errores = Object.fromEntries(CAMPOS_REGISTRO.map((c) => [c, [`mensaje ${c}`]]))
    const r = interpretar(
      new ErrorApi({ tipo: 'validacion', estado: 400, errores: { ...errores, general: ['No hay cambios para registrar.'] } }),
    )
    for (const campo of CAMPOS_REGISTRO) {
      expect(r.campos[campo]).toBe(`mensaje ${campo}`)
    }
    expect(r.generales).toEqual(['No hay cambios para registrar.'])
    expect(r.ofrecerRecarga).toBe(false)
  })

  it('400 en `version` ofrece «Recargar» (24c)', () => {
    const r = interpretar(new ErrorApi({ tipo: 'validacion', estado: 400, errores: { version: ['Falta la versión…'] } }))
    expect(r.ofrecerRecarga).toBe(true)
  })

  it('409 duplicado con código: title arriba, código para el enlace, sin «Recargar»', () => {
    const r = interpretar(
      new ErrorApi({ tipo: 'conflicto', estado: 409, titulo: MENSAJE_DUPLICADO, extensiones: { codigo: 'NOV-20700101-abc123' } }),
    )
    expect(r).toMatchObject({ generales: [MENSAJE_DUPLICADO], codigoDuplicado: 'NOV-20700101-abc123', ofrecerRecarga: false })
  })

  it('409 duplicado sin código (R6: no la ve): sin enlace ni «Recargar»', () => {
    const r = interpretar(new ErrorApi({ tipo: 'conflicto', estado: 409, titulo: MENSAJE_DUPLICADO }))
    expect(r.codigoDuplicado).toBeUndefined()
    expect(r.ofrecerRecarga).toBe(false)
  })

  it('otro 409 (la novedad cambió) sí ofrece «Recargar»', () => {
    const r = interpretar(new ErrorApi({ tipo: 'conflicto', estado: 409, titulo: 'La novedad cambió; vuelve a cargarla.' }))
    expect(r.ofrecerRecarga).toBe(true)
  })

  it('403 muestra el title arriba', () => {
    const titulo = 'Solo los administradores pueden registrar novedades GENERAL.'
    const r = interpretar(new ErrorApi({ tipo: 'prohibido', estado: 403, titulo }))
    expect(r.generales).toEqual([titulo])
    expect(r.ofrecerReintento).toBe(false)
  })

  it('404 habla de la novedad', () => {
    const r = interpretar(new ErrorApi({ tipo: 'noEncontrado', estado: 404 }))
    expect(r.generales).toEqual(['La novedad no existe o no tiene acceso.'])
    expect(r.noEncontrado).toBe(true)
  })

  it('503: «título: detalle» con «Reintentar»', () => {
    const r = interpretar(
      new ErrorApi({ tipo: 'noDisponible', estado: 503, titulo: 'Servicio de empleados no disponible', detalle: 'Intente luego.' }),
    )
    expect(r.generales).toEqual(['Servicio de empleados no disponible: Intente luego.'])
    expect(r.ofrecerReintento).toBe(true)
  })
})
