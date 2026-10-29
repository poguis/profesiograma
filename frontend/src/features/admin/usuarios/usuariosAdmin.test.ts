import { describe, expect, it } from 'vitest'
import { ErrorApi } from '../../../api/errores'
import {
  ADVERTENCIA_SIN_DEPARTAMENTOS,
  CAMPOS_ALTA,
  CAMPOS_DATOS,
  CAMPOS_DEPARTAMENTOS,
  MENSAJE_BAJA_PROPIA,
  MENSAJE_EMAIL_FORMATO,
  MENSAJE_EMAIL_VACIO,
  MENSAJE_NOMBRE_LARGO,
  MENSAJE_NOMBRE_VACIO,
  MENSAJE_SIN_CAMBIOS,
  MENSAJE_USUARIO_NO_ENCONTRADO,
  type UsuarioAdminDetalle,
  aConsultaApiUsuarios,
  aParametrosUrlUsuarios,
  aSolicitudDatos,
  aSolicitudDepartamentos,
  advertenciaDepartamentos,
  alternarDepartamento,
  esBaja,
  estadoDatos,
  estadoDepartamentos,
  etiquetaDepartamento,
  hayCambiosDatos,
  hayCambiosDepartamentos,
  interpretarErrorUsuario,
  leerFiltrosUsuarios,
  motivoSinGuardarDatos,
  motivoSinGuardarDepartamentos,
  normalizarAlta,
  resumenDatos,
  resumenDepartamentos,
  validarAlta,
} from './usuariosAdmin'

// Datos FICTICIOS.
const detalle: UsuarioAdminDetalle = {
  id: 5,
  email: 'persona@prueba.local',
  nombreMostrar: 'PERSONA PRUEBA',
  activo: true,
  conIdentidad: false,
  departamentos: [{ id: 2, nombreCorto: 'B' }],
  departamentosDisponibles: [
    { id: 1, nombre: 'DEPARTAMENTO A', nombreCorto: 'A', activo: true, asignado: false },
    { id: 2, nombre: 'DEPARTAMENTO B', nombreCorto: 'B', activo: true, asignado: true },
    { id: 9, nombre: 'DEPARTAMENTO VIEJO', nombreCorto: 'VIEJO', activo: false, asignado: true },
  ],
  esUsuarioActual: false,
}

describe('filtros de la URL', () => {
  it('lectura: valores por defecto, texto recortado y página ilegible', () => {
    expect(leerFiltrosUsuarios(new URLSearchParams(''))).toEqual({ incluirInactivos: false, pagina: 1 })
    expect(leerFiltrosUsuarios(new URLSearchParams('texto=%20ana%20&incluirInactivos=true&pagina=3'))).toEqual({
      texto: 'ana',
      incluirInactivos: true,
      pagina: 3,
    })
    expect(leerFiltrosUsuarios(new URLSearchParams('pagina=0&incluirInactivos=si')).pagina).toBe(1)
    expect(leerFiltrosUsuarios(new URLSearchParams('pagina=x&texto=%20')).texto).toBeUndefined()
  })

  it('escritura: omite valores por defecto; la API lleva siempre pagina y tamano', () => {
    expect(aParametrosUrlUsuarios({ incluirInactivos: false, pagina: 1 }).toString()).toBe('')
    expect(aParametrosUrlUsuarios({ texto: 'ana', incluirInactivos: true, pagina: 2 }).toString()).toBe(
      'texto=ana&incluirInactivos=true&pagina=2',
    )
    expect(aConsultaApiUsuarios({ incluirInactivos: false, pagina: 1 })).toBe('pagina=1&tamano=25')
  })
})

describe('alta', () => {
  it('normaliza: recorta y el correo en minúsculas', () => {
    expect(normalizarAlta('  Persona.Nueva@Prueba.LOCAL ', '  PERSONA NUEVA ')).toEqual({
      email: 'persona.nueva@prueba.local',
      nombreMostrar: 'PERSONA NUEVA',
    })
  })

  it('valida lo mismo que el backend (sin el dominio)', () => {
    expect(validarAlta({ email: '', nombreMostrar: '' })).toEqual({ email: MENSAJE_EMAIL_VACIO, nombreMostrar: MENSAJE_NOMBRE_VACIO })
    expect(validarAlta({ email: 'sin-arroba', nombreMostrar: 'X' })).toEqual({ email: MENSAJE_EMAIL_FORMATO })
    expect(validarAlta({ email: 'nombre <a@b.com>', nombreMostrar: 'X' })).toEqual({ email: MENSAJE_EMAIL_FORMATO })
    expect(validarAlta({ email: 'a@@b.com', nombreMostrar: 'X' })).toEqual({ email: MENSAJE_EMAIL_FORMATO })
    expect(validarAlta({ email: 'a@otro.com', nombreMostrar: 'N'.repeat(201) })).toEqual({ nombreMostrar: MENSAJE_NOMBRE_LARGO })
    expect(validarAlta({ email: 'a@otro.com', nombreMostrar: 'N'.repeat(200) })).toEqual({})
  })
})

describe('sección «Datos»', () => {
  it('sin cambios: deshabilitado con el mensaje; espacios extremos no cuentan', () => {
    const e = estadoDatos(detalle)
    expect(hayCambiosDatos({ ...e, nombre: ' PERSONA PRUEBA ' })).toBe(false)
    expect(motivoSinGuardarDatos(e)).toBe(MENSAJE_SIN_CAMBIOS)
    expect(motivoSinGuardarDatos({ ...e, nombre: '  ' })).toBe(MENSAJE_NOMBRE_VACIO)
  })

  it('con cambios: resumen, baja y cuerpo del PUT con los valores del GET', () => {
    const e = { ...estadoDatos(detalle), nombre: ' OTRO NOMBRE ', activo: false }
    expect(hayCambiosDatos(e)).toBe(true)
    expect(motivoSinGuardarDatos(e)).toBeUndefined()
    expect(resumenDatos(e)).toBe('Se guardará: PERSONA PRUEBA (Activo) → OTRO NOMBRE (Inactivo)')
    expect(esBaja(e)).toBe(true)
    expect(aSolicitudDatos(e)).toEqual({
      nombreMostrar: 'OTRO NOMBRE',
      activo: false,
      nombreAnterior: 'PERSONA PRUEBA',
      activoAnterior: true,
    })
    expect(esBaja({ ...estadoDatos({ ...detalle, activo: false }), activo: true })).toBe(false) // reactivación
  })

  it('usuario actual: la regla viaja en el estado', () => {
    expect(estadoDatos({ ...detalle, esUsuarioActual: true }).esUsuarioActual).toBe(true)
    expect(MENSAJE_BAJA_PROPIA).toBe('No puedes darte de baja a ti mismo.')
  })
})

describe('sección «Departamentos»', () => {
  it('estado del GET, etiqueta del inactivo y sin cambios', () => {
    const e = estadoDepartamentos(detalle)
    expect(e.originales).toEqual([2, 9])
    expect(etiquetaDepartamento(detalle.departamentosDisponibles[2])).toBe('DEPARTAMENTO VIEJO (inactivo)')
    expect(etiquetaDepartamento(detalle.departamentosDisponibles[0])).toBe('DEPARTAMENTO A')
    expect(motivoSinGuardarDepartamentos(e)).toBe(MENSAJE_SIN_CAMBIOS)
  })

  it('conjuntos sin importar el orden', () => {
    const e = estadoDepartamentos(detalle)
    expect(hayCambiosDepartamentos({ ...e, marcados: [9, 2] })).toBe(false)
    expect(hayCambiosDepartamentos({ ...e, marcados: [2] })).toBe(true)
    expect(hayCambiosDepartamentos({ ...e, marcados: [2, 9, 1] })).toBe(true)
  })

  it('alternar, resumen y cuerpo del PUT (anteriores = los del GET); conjunto vacío admitido', () => {
    let e = alternarDepartamento(estadoDepartamentos(detalle), 1)
    expect(e.marcados).toEqual([1, 2, 9])
    expect(resumenDepartamentos(e)).toBe('Se guardará: B, VIEJO → A, B, VIEJO')
    expect(aSolicitudDepartamentos(e)).toEqual({ departamentoIds: [1, 2, 9], anteriores: [2, 9] })
    e = [1, 2, 9].reduce(alternarDepartamento, e)
    expect(resumenDepartamentos(e)).toBe('Se guardará: B, VIEJO → (sin departamentos)')
    expect(aSolicitudDepartamentos(e)).toEqual({ departamentoIds: [], anteriores: [2, 9] })
    expect(motivoSinGuardarDepartamentos(e)).toBeUndefined()
  })

  it('advertencia solo si el conjunto guardado está vacío', () => {
    expect(advertenciaDepartamentos([])).toBe(ADVERTENCIA_SIN_DEPARTAMENTOS)
    expect(advertenciaDepartamentos(detalle.departamentos)).toBeUndefined()
  })
})

describe('interpretarErrorUsuario', () => {
  it('400 por campo; la clave `anterior` ofrece «Recargar»', () => {
    const alta = interpretarErrorUsuario(
      new ErrorApi({ tipo: 'validacion', estado: 400, errores: { email: ['El correo debe ser de un dominio permitido: sedemi.com.'] } }),
      CAMPOS_ALTA,
    )
    expect(alta.campos).toEqual({ email: 'El correo debe ser de un dominio permitido: sedemi.com.' })
    expect(alta.ofrecerRecarga).toBe(false)
    const datos = interpretarErrorUsuario(
      new ErrorApi({ tipo: 'validacion', estado: 400, errores: { activo: [MENSAJE_BAJA_PROPIA], anterior: ['Faltan…'] } }),
      CAMPOS_DATOS,
    )
    expect(datos).toMatchObject({ campos: { activo: MENSAJE_BAJA_PROPIA }, generales: ['Faltan…'], ofrecerRecarga: true })
  })

  it('409 de correo duplicado, con y sin usuarioId: sin «Recargar»', () => {
    const titulo = 'Ya existe un usuario con ese correo.'
    const con = interpretarErrorUsuario(new ErrorApi({ tipo: 'conflicto', estado: 409, titulo, extensiones: { usuarioId: 4 } }), CAMPOS_ALTA)
    const sin = interpretarErrorUsuario(new ErrorApi({ tipo: 'conflicto', estado: 409, titulo }), CAMPOS_ALTA)
    expect(con).toMatchObject({ generales: [titulo], ofrecerRecarga: false, usuarioExistenteId: 4 })
    expect(sin).toMatchObject({ generales: [titulo], ofrecerRecarga: false, usuarioExistenteId: undefined })
  })

  it('409 de datos y de departamentos: el title con «Recargar»', () => {
    for (const titulo of ['Otro usuario cambió el registro; recargue.', 'Otro usuario cambió los departamentos; recargue.']) {
      const r = interpretarErrorUsuario(new ErrorApi({ tipo: 'conflicto', estado: 409, titulo }), CAMPOS_DEPARTAMENTOS)
      expect(r).toMatchObject({ generales: [titulo], ofrecerRecarga: true, ofrecerReintento: false })
    }
  })

  it('403 sin «Reintentar»; 404 «El usuario no existe.»', () => {
    const prohibido = interpretarErrorUsuario(new ErrorApi({ tipo: 'prohibido', estado: 403 }), CAMPOS_DATOS)
    expect(prohibido).toMatchObject({ generales: ['No tiene permisos para esta acción.'], ofrecerReintento: false, ofrecerRecarga: false })
    const noExiste = interpretarErrorUsuario(new ErrorApi({ tipo: 'noEncontrado', estado: 404 }), CAMPOS_DATOS)
    expect(noExiste).toMatchObject({ generales: [MENSAJE_USUARIO_NO_ENCONTRADO], noEncontrado: true })
  })

  it('503 y red con «Reintentar»; un error ajeno a la API también', () => {
    const ocupado = interpretarErrorUsuario(
      new ErrorApi({
        tipo: 'noDisponible',
        estado: 503,
        titulo: 'Registro ocupado',
        detalle: 'Hay otra modificación de este usuario en curso; intente de nuevo.',
      }),
      CAMPOS_DEPARTAMENTOS,
    )
    expect(ocupado).toMatchObject({
      generales: ['Registro ocupado: Hay otra modificación de este usuario en curso; intente de nuevo.'],
      ofrecerReintento: true,
    })
    expect(interpretarErrorUsuario(new ErrorApi({ tipo: 'red', estado: null }), CAMPOS_DATOS).ofrecerReintento).toBe(true)
    expect(interpretarErrorUsuario(new Error('x'), CAMPOS_DATOS)).toMatchObject({
      generales: ['Ocurrió un error inesperado.'],
      ofrecerReintento: true,
    })
  })
})
