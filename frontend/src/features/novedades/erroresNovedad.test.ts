import { describe, expect, it } from 'vitest'
import { ErrorApi } from '../../api/errores'
import {
  CAMPOS_ANULACION,
  CAMPOS_EDICION,
  CAMPOS_REGISTRO,
  interpretarErrorDesconocido,
  interpretarErrorNovedad,
} from './erroresNovedad'

const interpretar = (error: ErrorApi) => interpretarErrorNovedad(error, CAMPOS_REGISTRO)
const MENSAJE_DUPLICADO = 'Ya existe una novedad de ese tipo para ese sujeto en esas fechas.'

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
      new ErrorApi({
        tipo: 'conflicto',
        estado: 409,
        titulo: MENSAJE_DUPLICADO,
        extensiones: { tipoConflicto: 'duplicado', codigo: 'NOV-20700101-abc123' },
      }),
    )
    expect(r).toMatchObject({ generales: [MENSAJE_DUPLICADO], codigoDuplicado: 'NOV-20700101-abc123', ofrecerRecarga: false })
  })

  it('409 duplicado sin código (R6: no la ve): sin enlace ni «Recargar»', () => {
    const r = interpretar(
      new ErrorApi({ tipo: 'conflicto', estado: 409, titulo: MENSAJE_DUPLICADO, extensiones: { tipoConflicto: 'duplicado' } }),
    )
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

describe('interpretarErrorNovedad en la edición y la anulación (TAREA-24c)', () => {
  const editar = (error: ErrorApi) => interpretarErrorNovedad(error, CAMPOS_EDICION)

  it('400: cada clave del PUT a su control; `general`, `origen` y `version` arriba', () => {
    const errores = Object.fromEntries(CAMPOS_EDICION.map((c) => [c, [`mensaje ${c}`]]))
    const r = editar(
      new ErrorApi({
        tipo: 'validacion',
        estado: 400,
        errores: { ...errores, general: ['General.'], origen: ['Las novedades de PERMISOS_MEDICOS son de solo lectura.'] },
      }),
    )
    for (const campo of CAMPOS_EDICION) {
      expect(r.campos[campo]).toBe(`mensaje ${campo}`)
    }
    expect(r.generales).toEqual(['General.', 'Las novedades de PERMISOS_MEDICOS son de solo lectura.'])
    expect(r.ofrecerRecarga).toBe(false)
  })

  it('400 `version` arriba y con «Recargar»', () => {
    const r = editar(new ErrorApi({ tipo: 'validacion', estado: 400, errores: { version: ['Falta la versión de la novedad; vuelve a cargarla.'] } }))
    expect(r.generales).toEqual(['Falta la versión de la novedad; vuelve a cargarla.'])
    expect(r.ofrecerRecarga).toBe(true)
  })

  it.each(['La novedad cambió; vuelve a cargarla.', 'La novedad está anulada.', 'La novedad ya está anulada.'])(
    '409 «%s» → «Recargar»',
    (titulo) => {
      const r = editar(new ErrorApi({ tipo: 'conflicto', estado: 409, titulo }))
      expect(r).toMatchObject({ generales: [titulo], ofrecerRecarga: true })
      expect(r.codigoDuplicado).toBeUndefined()
    },
  )

  it('409 duplicado al editar, con y sin código: sin «Recargar»', () => {
    const con = editar(
      new ErrorApi({ tipo: 'conflicto', estado: 409, titulo: MENSAJE_DUPLICADO, extensiones: { tipoConflicto: 'duplicado', codigo: 'NOV-1' } }),
    )
    const sin = editar(
      new ErrorApi({ tipo: 'conflicto', estado: 409, titulo: MENSAJE_DUPLICADO, extensiones: { tipoConflicto: 'duplicado' } }),
    )
    expect([con.codigoDuplicado, con.ofrecerRecarga]).toEqual(['NOV-1', false])
    expect([sin.codigoDuplicado, sin.ofrecerRecarga]).toEqual([undefined, false])
  })

  it('403, 404 y 503 en la edición', () => {
    expect(editar(new ErrorApi({ tipo: 'prohibido', estado: 403, titulo: 'No tienes permiso para modificar esta novedad.' })).generales).toEqual([
      'No tienes permiso para modificar esta novedad.',
    ])
    expect(editar(new ErrorApi({ tipo: 'noEncontrado', estado: 404 })).generales).toEqual(['La novedad no existe o no tiene acceso.'])
    const r = editar(new ErrorApi({ tipo: 'noDisponible', estado: 503, titulo: 'Base de datos no disponible', detalle: 'No se guardó.' }))
    expect(r).toMatchObject({ generales: ['Base de datos no disponible: No se guardó.'], ofrecerReintento: true })
  })

  it('anulación: 400 `motivo` bajo su campo', () => {
    const r = interpretarErrorNovedad(
      new ErrorApi({ tipo: 'validacion', estado: 400, errores: { motivo: ['El motivo debe tener entre 5 y 500 caracteres.'] } }),
      CAMPOS_ANULACION,
    )
    expect(r.campos.motivo).toBe('El motivo debe tener entre 5 y 500 caracteres.')
    expect(r.generales).toEqual([])
  })

  it('un error que no es de la API se puede reintentar', () => {
    expect(interpretarErrorDesconocido(new Error('x'), CAMPOS_EDICION)).toMatchObject({
      generales: ['Ocurrió un error inesperado.'],
      ofrecerReintento: true,
    })
  })
})

describe('tipoConflicto (TAREA-27)', () => {
  const conflicto = (extensiones: Record<string, unknown>, titulo = 'Mensaje cualquiera.') =>
    interpretar(new ErrorApi({ tipo: 'conflicto', estado: 409, titulo, extensiones }))

  it.each(['cambio', 'anulada'])('«%s» → «Recargar», sin «Reintentar»', (tipoConflicto) => {
    expect(conflicto({ tipoConflicto })).toMatchObject({ ofrecerRecarga: true, ofrecerReintento: false })
  })

  it('«registroConcurrente» → «Reintentar», sin «Recargar» (P3)', () => {
    const r = conflicto({ tipoConflicto: 'registroConcurrente' }, 'Otro registro se guardó al mismo tiempo; vuelva a intentarlo.')
    expect(r).toMatchObject({
      generales: ['Otro registro se guardó al mismo tiempo; vuelva a intentarlo.'],
      ofrecerRecarga: false,
      ofrecerReintento: true,
    })
  })

  it('sin tipoConflicto: 409 genérico con «Recargar», aunque traiga el texto o el código del duplicado (P4)', () => {
    const porTexto = conflicto({}, MENSAJE_DUPLICADO)
    const porCodigo = conflicto({ codigo: 'NOV-1' })
    expect(porTexto).toMatchObject({ ofrecerRecarga: true })
    expect(porTexto.codigoDuplicado).toBeUndefined()
    expect(porCodigo).toMatchObject({ ofrecerRecarga: true })
    expect(porCodigo.codigoDuplicado).toBeUndefined()
  })

  it('valor desconocido: 409 genérico', () => {
    expect(conflicto({ tipoConflicto: 'otro' })).toMatchObject({ ofrecerRecarga: true, ofrecerReintento: false })
  })
})
