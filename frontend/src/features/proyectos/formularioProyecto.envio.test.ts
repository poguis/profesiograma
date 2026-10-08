// Pruebas de la B2: errores 400 → campos, habilitación de "Registrar" y 409.
import { describe, expect, it } from 'vitest'
import {
  type AccionFormulario,
  type EmpleadoFila,
  type EstadoFormulario,
  SIN_ERRORES_SERVIDOR,
  type VistaPreviaFormulario,
  clavesDelEnvio,
  crearEstadoInicial,
  crucesDeConflicto,
  distribuirErrores,
  limpiarErroresServidor,
  mensajesSinCampo,
  puedeRegistrar,
  reducerFormulario,
  vistaVigente,
} from './formularioProyecto'
import type { Previsualizacion } from './tipos'

const CAMPO = { codigo: 'CAMPO', requiereProyectoErp: true, requiereDimension: false }
const PLANTA = { codigo: 'PLANTA', requiereProyectoErp: false, requiereDimension: true }
const MAX = 20

const empleado = (id: number): EmpleadoFila => ({
  codigoEkon: `DEV00${id}`,
  nombreCompleto: `EMPLEADO PRUEBA 0${id}`,
  cargo: null,
})

function aplicar(estado: EstadoFormulario, ...acciones: AccionFormulario[]) {
  return acciones.reduce(reducerFormulario, estado)
}

const conCabeceraCampo = () =>
  aplicar(
    crearEstadoInicial({ departamentos: [], companias: [] }),
    { tipo: 'compania', companiaId: 9001 },
    { tipo: 'grupo', grupo: CAMPO },
    { tipo: 'cabecera', cambios: { fechaInicio: '2026-12-01', fechaFin: '2026-12-31' } },
  )

/** P1 = emp 6 (p1), P2 = emp 7 (p2); back emp 8 (b3). */
const conPersonal = () =>
  aplicar(
    conCabeceraCampo(),
    { tipo: 'agregarPrincipal', empleado: empleado(6), maximo: MAX },
    { tipo: 'agregarPrincipal', empleado: empleado(7), maximo: MAX },
    { tipo: 'agregarBack', empleado: empleado(8), maximo: MAX },
  )

describe('R13: distribuirErrores (400 → campos)', () => {
  it('claves de cabecera, con índice y de sección', () => {
    const errores = distribuirErrores(
      {
        proyectoErpId: ['El grupo CAMPO requiere un proyecto ERP.'],
        regresoAlmuerzo: ['Uno.', 'Dos.'],
        'principales[1].fechaFin': ['Fuera de rango.'],
        'backs[0].principalRelacionado': ['El principal relacionado 5 no existe.'],
        principales: ['Se permiten como máximo 20 principales.'],
      },
      clavesDelEnvio(conPersonal()),
    )
    expect(errores.cabecera).toEqual({
      proyectoErpId: 'El grupo CAMPO requiere un proyecto ERP.',
      regresoAlmuerzo: 'Uno. Dos.',
    })
    expect(errores.filas).toEqual({
      p2: { fechaFin: 'Fuera de rango.' },
      b3: { principalRelacionado: 'El principal relacionado 5 no existe.' },
    })
    expect(errores.secciones.principales).toBe('Se permiten como máximo 20 principales.')
    expect(errores.generales).toEqual([])
  })

  it('claves desconocidas o con índice fuera del envío van arriba (generales)', () => {
    const errores = distribuirErrores(
      {
        usuario: ['No se pudo identificar al usuario actual.'],
        '$.principales[0].fechaInicio': ['JSON inválido.'],
        'backs[7].fechaFin': ['Índice fuera del envío.'],
      },
      clavesDelEnvio(conPersonal()),
    )
    expect(errores.generales).toEqual([
      'No se pudo identificar al usuario actual.',
      'JSON inválido.',
      'Índice fuera del envío.',
    ])
    expect(errores.filas).toEqual({})
  })

  it('el error sigue a la fila tras reordenar (índice → clave del envío)', () => {
    const enviado = conPersonal()
    const errores = distribuirErrores({ 'principales[0].jornada': ['La jornada es obligatoria.'] }, clavesDelEnvio(enviado))
    const mover: AccionFormulario = { tipo: 'moverPrincipal', clave: 'p2', direccion: -1 }
    const reordenado = aplicar(enviado, mover)

    // El empleado 6 (clave p1) pasa a la posición 2 y su error lo acompaña.
    expect(reordenado.principales[1].clave).toBe('p1')
    expect(errores.filas.p1).toEqual({ jornada: 'La jornada es obligatoria.' })
    expect(limpiarErroresServidor(errores, mover)).toBe(errores)
  })

  it('editar un campo borra solo su error', () => {
    const errores = distribuirErrores(
      {
        salidaAlmuerzo: ['a'],
        regresoAlmuerzo: ['b'],
        'principales[0].jornada': ['c'],
        'principales[0].fechaFin': ['d'],
        'backs[0].principalRelacionado': ['e'],
      },
      clavesDelEnvio(conPersonal()),
    )

    const sinRegreso = limpiarErroresServidor(errores, { tipo: 'cabecera', cambios: { regresoAlmuerzo: '14:00' } })
    expect(sinRegreso.cabecera).toEqual({ salidaAlmuerzo: 'a' })

    const sinJornada = limpiarErroresServidor(errores, {
      tipo: 'actualizarPrincipal',
      clave: 'p1',
      cambios: { jornada: 'TIPO_2' },
    })
    expect(sinJornada.filas.p1).toEqual({ fechaFin: 'd' })

    const sinRelacion = limpiarErroresServidor(errores, {
      tipo: 'actualizarBack',
      clave: 'b3',
      cambios: { principalClave: 'p1' },
    })
    expect(sinRelacion.filas.b3).toEqual({})
  })

  it('cambiar el grupo borra los errores de los campos que limpia', () => {
    const errores = distribuirErrores(
      { grupo: ['x'], proyectoErpId: ['y'], fechaInicio: ['z'] },
      { principales: [], backs: [] },
    )
    expect(limpiarErroresServidor(errores, { tipo: 'grupo', grupo: PLANTA }).cabecera).toEqual({ fechaInicio: 'z' })
  })

  it('quitar una fila borra sus errores y el de su sección', () => {
    const errores = distribuirErrores(
      { 'principales[0].jornada': ['c'], principales: ['máximo'] },
      clavesDelEnvio(conPersonal()),
    )
    const despues = limpiarErroresServidor(errores, { tipo: 'eliminarPrincipal', clave: 'p1' })
    expect(despues.filas.p1).toBeUndefined()
    expect(despues.secciones.principales).toBeUndefined()
  })

  it('mensajesSinCampo devuelve los errores que la tarjeta no muestra', () => {
    expect(mensajesSinCampo({ empleadoId: 'Inactivo.', jornada: 'x' }, ['jornada'])).toEqual(['Inactivo.'])
  })

  it('sin errores: estructura vacía', () => {
    expect(distribuirErrores({}, { principales: [], backs: [] })).toEqual(SIN_ERRORES_SERVIDOR)
  })
})

describe('R12: habilitación de "Registrar"', () => {
  const datos = (cruces: number): Previsualizacion => ({
    tramos: [],
    diasPorPersona: [],
    cruces: Array.from({ length: cruces }, (_, i) => ({
      origen: 'EXTERNO',
      empleadoId: 6,
      codigoEkon: 'DEV006',
      nombreEmpleado: 'EMPLEADO PRUEBA 06',
      fecha: `2026-12-0${i + 1}`,
      rol: 'PRINCIPAL',
      proyecto: 'PROYECTO ERP DE PRUEBA',
      proyectoCodigo: 'PRY-20261001-4ede0f',
      estadoProyecto: 'ACTIVO',
    })),
    resumen: [],
    advertencias: [],
  })
  const vista = (revision: number, cruces = 0): VistaPreviaFormulario => ({ revision, datos: datos(cruces) })

  it('sin vista previa: deshabilitado', () => {
    expect(puedeRegistrar(null, 3, false)).toBe(false)
  })

  it('vista previa de la revisión actual, sin cruces y sin envío: habilitado', () => {
    expect(puedeRegistrar(vista(3), 3, false)).toBe(true)
  })

  it('vista previa de otra revisión (formulario editado): deshabilitado', () => {
    expect(vistaVigente(vista(3), 4)).toBe(false)
    expect(puedeRegistrar(vista(3), 4, false)).toBe(false)
  })

  it('con cruces: deshabilitado', () => {
    expect(puedeRegistrar(vista(3, 2), 3, false)).toBe(false)
  })

  it('durante el envío: deshabilitado (evita doble registro)', () => {
    expect(puedeRegistrar(vista(3), 3, true)).toBe(false)
  })

  it('cualquier cambio del formulario invalida la vista previa', () => {
    const estado = conCabeceraCampo()
    const v = vista(estado.revision)
    const editado = aplicar(estado, { tipo: 'cabecera', cambios: { regresoAlmuerzo: '15:00' } })
    expect(puedeRegistrar(v, estado.revision, false)).toBe(true)
    expect(puedeRegistrar(v, editado.revision, false)).toBe(false)
  })
})

describe('R13: 409', () => {
  it('lee cruces y resumen de las extensiones', () => {
    const resultado = crucesDeConflicto({
      cruces: [],
      resumen: [{ nombreEmpleado: 'X', rol: 'PRINCIPAL', proyecto: 'P', mes: 'diciembre 2026', dias: '5, 6' }],
      traceId: 'x',
    })
    expect(resultado?.resumen[0].dias).toBe('5, 6')
  })

  it('sin la forma esperada: null', () => {
    expect(crucesDeConflicto({ traceId: 'x' })).toBeNull()
    expect(crucesDeConflicto({ cruces: 'no', resumen: [] })).toBeNull()
  })
})
