import { describe, expect, it } from 'vitest'
import { ErrorApi } from '../../../api/errores'
import {
  type CargoInfor,
  MENSAJE_CARGO_NO_ENCONTRADO,
  MENSAJE_CODIGO_FORMATO,
  MENSAJE_CODIGO_LARGO,
  MENSAJE_SIN_CAMBIOS,
  type VistaPreviaImportacion,
  aConsultaApiCargos,
  aParametrosUrlCargos,
  aSolicitudCargo,
  afectaAlReporte,
  esBajaCargo,
  estadoEdicionCargo,
  hayCambiosCargo,
  interpretarErrorCargo,
  interpretarErrorImportacion,
  leerFiltrosCargos,
  motivoSinGuardarCargo,
  normalizarCodigo,
  resumenCargo,
  textoListaDe,
  textoMasVariantes,
  validarCodigo,
} from './cargosInfor'

// Datos ficticios.
const cargo = (cambios: Partial<CargoInfor> = {}): CargoInfor => ({
  id: 10,
  cargo: 'CARGO FICTICIO',
  codigoInfor: null,
  activo: true,
  personasGuardadas: 0,
  activosErp: null,
  ...cambios,
})

const vistaPrevia = (cambios: Partial<VistaPreviaImportacion> = {}): VistaPreviaImportacion => ({
  puestosApi: 3,
  nuevos: 1,
  yaExistentes: 2,
  existentesInactivos: 0,
  fusionados: 0,
  gruposVariantes: 0,
  variantes: [],
  obtenidaUtc: '2026-10-29T17:53:12.5+00:00',
  esAnterior: false,
  ...cambios,
})

describe('filtros de la URL', () => {
  it('por defecto: «Sin código», sin inactivos, página 1', () => {
    expect(leerFiltrosCargos(new URLSearchParams())).toEqual({ filtro: 'sinCodigo', incluirInactivos: false, pagina: 1 })
  })

  it('lee texto, filtro, inactivos y página; filtro desconocido o página inválida → por defecto', () => {
    expect(leerFiltrosCargos(new URLSearchParams('texto=%20ABC%20&filtro=todos&incluirInactivos=true&pagina=3'))).toEqual({
      texto: 'ABC',
      filtro: 'todos',
      incluirInactivos: true,
      pagina: 3,
    })
    expect(leerFiltrosCargos(new URLSearchParams('filtro=otro&pagina=0'))).toEqual({ filtro: 'sinCodigo', incluirInactivos: false, pagina: 1 })
    expect(leerFiltrosCargos(new URLSearchParams('pagina=2.5')).pagina).toBe(1)
  })

  it('la URL omite los valores por defecto', () => {
    expect(aParametrosUrlCargos({ filtro: 'sinCodigo', incluirInactivos: false, pagina: 1 }).toString()).toBe('')
    expect(aParametrosUrlCargos({ texto: 'x', filtro: 'conCodigo', incluirInactivos: true, pagina: 2 }).toString()).toBe(
      'texto=x&filtro=conCodigo&incluirInactivos=true&pagina=2',
    )
  })

  it('la API lleva siempre filtro, pagina y tamano', () => {
    expect(aConsultaApiCargos({ filtro: 'sinCodigo', incluirInactivos: false, pagina: 1 })).toBe('filtro=sinCodigo&pagina=1&tamano=25')
    expect(aConsultaApiCargos({ texto: 'abc', filtro: 'todos', incluirInactivos: true, pagina: 4 })).toBe(
      'texto=abc&filtro=todos&incluirInactivos=true&pagina=4&tamano=25',
    )
  })
})

describe('código Infor (mismos casos que ReglasCargoInfor)', () => {
  it('normaliza: recorta y pasa a mayúsculas; vacío o blanco → null', () => {
    expect(normalizarCodigo('  p-25.3 ')).toBe('P-25.3')
    expect(normalizarCodigo('')).toBeNull()
    expect(normalizarCodigo('   ')).toBeNull()
    expect(normalizarCodigo(null)).toBeNull()
  })

  it('valida largo y formato', () => {
    expect(validarCodigo(null)).toBeUndefined()
    expect(validarCodigo('A.B-C_1')).toBeUndefined()
    expect(validarCodigo('A'.repeat(20))).toBeUndefined()
    expect(validarCodigo('A'.repeat(21))).toBe(MENSAJE_CODIGO_LARGO)
    expect(validarCodigo('P 1')).toBe(MENSAJE_CODIGO_FORMATO)
    expect(validarCodigo('PÑ1')).toBe(MENSAJE_CODIGO_FORMATO)
  })
})

describe('insignia «Afecta al reporte» (P1)', () => {
  it('solo con personas guardadas y sin código o inactivo', () => {
    expect(afectaAlReporte(cargo({ personasGuardadas: 2 }))).toBe(true)
    expect(afectaAlReporte(cargo({ personasGuardadas: 2, codigoInfor: '  ' }))).toBe(true)
    expect(afectaAlReporte(cargo({ personasGuardadas: 2, codigoInfor: 'P1', activo: false }))).toBe(true)
    expect(afectaAlReporte(cargo({ personasGuardadas: 2, codigoInfor: 'P1' }))).toBe(false)
    expect(afectaAlReporte(cargo({ personasGuardadas: 0 }))).toBe(false)
    expect(afectaAlReporte(cargo({ personasGuardadas: 0, activo: false }))).toBe(false)
  })
})

describe('diálogo de edición', () => {
  it('sin cambios: ignora espacios y mayúsculas del código', () => {
    const e = estadoEdicionCargo(cargo({ codigoInfor: 'P0021' }))
    expect(hayCambiosCargo({ ...e, codigo: ' p0021 ' })).toBe(false)
    expect(motivoSinGuardarCargo(e)).toBe(MENSAJE_SIN_CAMBIOS)
    expect(hayCambiosCargo({ ...e, codigo: '' })).toBe(true)
    expect(hayCambiosCargo({ ...e, activo: false })).toBe(true)
  })

  it('un código inválido impide guardar con su mensaje', () => {
    const e = estadoEdicionCargo(cargo())
    expect(motivoSinGuardarCargo({ ...e, codigo: 'P 1' })).toBe(MENSAJE_CODIGO_FORMATO)
    expect(motivoSinGuardarCargo({ ...e, codigo: 'p-1' })).toBeUndefined()
  })

  it('resumen «Se guardará»', () => {
    const e = estadoEdicionCargo(cargo())
    expect(resumenCargo({ ...e, codigo: ' p0021' })).toBe('Se guardará: (sin código) (Activo) → P0021 (Activo)')
    const conCodigo = estadoEdicionCargo(cargo({ codigoInfor: 'P0021' }))
    expect(resumenCargo({ ...conCodigo, codigo: '', activo: false })).toBe('Se guardará: P0021 (Activo) → (sin código) (Inactivo)')
  })

  it('baja solo al pasar de activo a inactivo', () => {
    const activo = estadoEdicionCargo(cargo())
    const inactivo = estadoEdicionCargo(cargo({ activo: false }))
    expect(esBajaCargo({ ...activo, activo: false })).toBe(true)
    expect(esBajaCargo(activo)).toBe(false)
    expect(esBajaCargo({ ...inactivo, activo: true })).toBe(false)
  })

  it('solicitud con los anteriores de la fila (null = sin código)', () => {
    expect(aSolicitudCargo({ ...estadoEdicionCargo(cargo()), codigo: ' x.1 ' })).toEqual({
      codigoInfor: 'X.1',
      activo: true,
      codigoInforAnterior: null,
      activoAnterior: true,
    })
    expect(aSolicitudCargo({ ...estadoEdicionCargo(cargo({ codigoInfor: 'P1' })), codigo: '', activo: false })).toEqual({
      codigoInfor: null,
      activo: false,
      codigoInforAnterior: 'P1',
      activoAnterior: true,
    })
  })
})

describe('diálogo de importación', () => {
  it('«Lista de las HH:mm» en hora de Ecuador', () => {
    expect(textoListaDe(vistaPrevia())).toBe('Lista de las 12:53')
  })

  it('«y N más» solo si faltan grupos', () => {
    expect(textoMasVariantes(vistaPrevia({ gruposVariantes: 1, variantes: [['A', 'Á']] }))).toBeUndefined()
    expect(textoMasVariantes(vistaPrevia({ gruposVariantes: 23, variantes: Array.from({ length: 20 }, () => ['A', 'Á']) }))).toBe('y 3 más')
  })
})

describe('interpretarErrorCargo', () => {
  it('400: el código bajo su campo; `anterior` ofrece «Recargar»', () => {
    const campo = interpretarErrorCargo(new ErrorApi({ tipo: 'validacion', estado: 400, errores: { codigoInfor: [MENSAJE_CODIGO_FORMATO] } }))
    expect(campo.campos.codigoInfor).toBe(MENSAJE_CODIGO_FORMATO)
    expect(campo.ofrecerRecarga).toBe(false)
    const anterior = interpretarErrorCargo(new ErrorApi({ tipo: 'validacion', estado: 400, errores: { anterior: ['Faltan…'] } }))
    expect(anterior.generales).toEqual(['Faltan…'])
    expect(anterior.ofrecerRecarga).toBe(true)
  })

  it('409 del PUT: «Recargar», sin «Reintentar»', () => {
    const r = interpretarErrorCargo(new ErrorApi({ tipo: 'conflicto', estado: 409, titulo: 'Otro usuario cambió el cargo; recargue.' }))
    expect(r.generales).toEqual(['Otro usuario cambió el cargo; recargue.'])
    expect(r.ofrecerRecarga).toBe(true)
    expect(r.ofrecerReintento).toBe(false)
  })

  it('404, 403, 503, red y errores ajenos', () => {
    const noExiste = interpretarErrorCargo(new ErrorApi({ tipo: 'noEncontrado', estado: 404, titulo: 'El cargo no existe.' }))
    expect(noExiste.generales).toEqual([MENSAJE_CARGO_NO_ENCONTRADO])
    expect(noExiste.noEncontrado).toBe(true)
    expect(noExiste.ofrecerRecarga).toBe(true)
    const prohibido = interpretarErrorCargo(new ErrorApi({ tipo: 'prohibido', estado: 403 }))
    expect(prohibido.ofrecerReintento).toBe(false)
    expect(prohibido.ofrecerRecarga).toBe(false)
    const ocupado = interpretarErrorCargo(new ErrorApi({ tipo: 'noDisponible', estado: 503, titulo: 'Ocupado', detalle: 'Intente.' }))
    expect(ocupado.generales).toEqual(['Ocupado: Intente.'])
    expect(ocupado.ofrecerReintento).toBe(true)
    expect(interpretarErrorCargo(new ErrorApi({ tipo: 'red', estado: null })).ofrecerReintento).toBe(true)
    expect(interpretarErrorCargo(new Error('x')).generales).toEqual(['Ocurrió un error inesperado.'])
  })
})

describe('interpretarErrorImportacion', () => {
  it('409: el mensaje del backend con «Reintentar», no «Recargar»', () => {
    const titulo = 'No se pudo importar: otro registro se guardó al mismo tiempo. Vuelva a intentarlo; si se repite, avise a TI.'
    const r = interpretarErrorImportacion(new ErrorApi({ tipo: 'conflicto', estado: 409, titulo }))
    expect(r.generales).toEqual([titulo])
    expect(r.ofrecerReintento).toBe(true)
    expect(r.ofrecerRecarga).toBe(false)
  })

  it('503 del ERP y red con «Reintentar»; 403 sin él', () => {
    expect(interpretarErrorImportacion(new ErrorApi({ tipo: 'noDisponible', estado: 503 })).ofrecerReintento).toBe(true)
    expect(interpretarErrorImportacion(new ErrorApi({ tipo: 'red', estado: null })).ofrecerReintento).toBe(true)
    expect(interpretarErrorImportacion(new ErrorApi({ tipo: 'prohibido', estado: 403 })).ofrecerReintento).toBe(false)
  })
})
