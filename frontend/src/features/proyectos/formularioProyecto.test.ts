import { describe, expect, it } from 'vitest'
import {
  type AccionFormulario,
  type EmpleadoFila,
  type EstadoFormulario,
  aSolicitud,
  advertenciasFechas,
  crearEstadoInicial,
  etiquetasPorEmpleado,
  numeroPrincipal,
  ayudaPrincipales,
  puedeGenerarVistaPrevia,
  reducerFormulario,
  validarCliente,
} from './formularioProyecto'

const CAMPO = { codigo: 'CAMPO', requiereProyectoErp: true, requiereDimension: false }
const PLANTA = { codigo: 'PLANTA', requiereProyectoErp: false, requiereDimension: true }
const MAX = 20

const empleado = (id: number): EmpleadoFila => ({
  id,
  codigoEkon: `DEV00${id}`,
  nombreCompleto: `EMPLEADO PRUEBA 0${id}`,
  cargo: null,
})

function aplicar(estado: EstadoFormulario, ...acciones: AccionFormulario[]) {
  return acciones.reduce(reducerFormulario, estado)
}

const inicial = () => crearEstadoInicial({ departamentos: [], companias: [] })

/** CAMPO con ERP y actividad, diciembre 2026. */
const conCabeceraCampo = () =>
  aplicar(
    inicial(),
    { tipo: 'compania', companiaId: 9001 },
    { tipo: 'grupo', grupo: CAMPO },
    { tipo: 'proyectoErp', proyectoErpId: 'DEV-ERP-001' },
    { tipo: 'cabecera', cambios: { actividadId: 'DEV.01', fechaInicio: '2026-12-01', fechaFin: '2026-12-31' } },
  )

describe('estado inicial', () => {
  it('con un solo departamento y una sola compañía los elige automáticamente (P5)', () => {
    const estado = crearEstadoInicial({ departamentos: [{ id: 7 }], companias: [{ id: 9001 }] })
    expect(estado.cabecera.departamentoId).toBe(7)
    expect(estado.cabecera.companiaId).toBe(9001)
  })

  it('con varios (o ninguno) no elige', () => {
    const estado = crearEstadoInicial({ departamentos: [{ id: 7 }, { id: 8 }], companias: [] })
    expect(estado.cabecera.departamentoId).toBeNull()
    expect(estado.cabecera.companiaId).toBeNull()
  })
})

describe('R3: limpieza de campos dependientes', () => {
  it('al cambiar el grupo se limpian proyecto ERP, actividad y dimensión', () => {
    const estado = aplicar(
      conCabeceraCampo(),
      { tipo: 'cabecera', cambios: { dimensionUegpId: 'DEV-DIM-01' } },
      { tipo: 'grupo', grupo: PLANTA },
    )
    expect(estado.cabecera.proyectoErpId).toBeNull()
    expect(estado.cabecera.actividadId).toBeNull()
    expect(estado.cabecera.dimensionUegpId).toBeNull()
  })

  it('CAMPO → PLANTA → CAMPO deja todo vacío', () => {
    const estado = aplicar(
      conCabeceraCampo(),
      { tipo: 'grupo', grupo: PLANTA },
      { tipo: 'cabecera', cambios: { dimensionUegpId: 'DEV-DIM-01' } },
      { tipo: 'grupo', grupo: CAMPO },
    )
    expect(estado.cabecera).toMatchObject({ proyectoErpId: null, actividadId: null, dimensionUegpId: null })
  })

  it('al cambiar la compañía se limpian proyecto ERP, actividad y dimensión', () => {
    const estado = aplicar(conCabeceraCampo(), { tipo: 'compania', companiaId: 9002 })
    expect(estado.cabecera).toMatchObject({ proyectoErpId: null, actividadId: null, dimensionUegpId: null })
    expect(estado.cabecera.grupo).toEqual(CAMPO)
  })

  it('al cambiar el proyecto ERP se limpia la actividad', () => {
    const estado = aplicar(conCabeceraCampo(), { tipo: 'proyectoErp', proyectoErpId: 'DEV-ERP-002' })
    expect(estado.cabecera.proyectoErpId).toBe('DEV-ERP-002')
    expect(estado.cabecera.actividadId).toBeNull()
  })

  it('elegir el mismo valor no limpia ni cambia la revisión', () => {
    const antes = conCabeceraCampo()
    const despues = aplicar(
      antes,
      { tipo: 'grupo', grupo: CAMPO },
      { tipo: 'compania', companiaId: 9001 },
      { tipo: 'proyectoErp', proyectoErpId: 'DEV-ERP-001' },
    )
    expect(despues).toBe(antes)
  })

  it('cambiar las fechas del proyecto no toca las del personal (R10)', () => {
    const estado = aplicar(
      conCabeceraCampo(),
      { tipo: 'agregarPrincipal', empleado: empleado(6), maximo: MAX },
      { tipo: 'cabecera', cambios: { fechaFin: '2026-12-20' } },
    )
    expect(estado.principales[0]).toMatchObject({ fechaInicio: '2026-12-01', fechaFin: '2026-12-31' })
    expect(advertenciasFechas(estado).get('p1')).toContain('fuera del rango')
  })
})

describe('aSolicitud', () => {
  it('CAMPO: envía ERP y actividad; dimensión null', () => {
    const s = aSolicitud(conCabeceraCampo())
    expect(s).toMatchObject({
      companiaId: 9001,
      grupo: 'CAMPO',
      proyectoErpId: 'DEV-ERP-001',
      actividadId: 'DEV.01',
      dimensionUegpId: null,
    })
  })

  it('campos que el grupo no usa van null aunque el estado tenga valor', () => {
    const estado = conCabeceraCampo()
    const forzado: EstadoFormulario = { ...estado, cabecera: { ...estado.cabecera, grupo: PLANTA, dimensionUegpId: 'D1' } }
    const s = aSolicitud(forzado)
    expect(s.proyectoErpId).toBeNull()
    expect(s.actividadId).toBeNull()
    expect(s.dimensionUegpId).toBe('D1')
  })

  it('textos vacíos o con espacios se envían como null, nunca ""', () => {
    const estado = aplicar(
      conCabeceraCampo(),
      { tipo: 'cabecera', cambios: { actividadId: '   ' } },
      { tipo: 'agregarPrincipal', empleado: empleado(6), maximo: MAX },
      { tipo: 'actualizarPrincipal', clave: 'p1', cambios: { cargo: '  ' } },
      { tipo: 'agregarBack', empleado: empleado(8), maximo: MAX },
      { tipo: 'actualizarBack', clave: 'b2', cambios: { observacion: '' } },
    )
    const s = aSolicitud(estado)
    expect(s.actividadId).toBeNull()
    expect(s.salidaAlmuerzo).toBeNull()
    expect(s.principales[0].cargo).toBeNull()
    expect(s.principales[0].jornada).toBeNull()
    expect(s.backs[0].observacion).toBeNull()
    expect(s.backs[0].cargo).toBeNull()
  })

  it('sin grupo: proyecto ERP, actividad y dimensión null', () => {
    const s = aSolicitud(inicial())
    expect(s).toMatchObject({ grupo: null, proyectoErpId: null, actividadId: null, dimensionUegpId: null })
  })
})

describe('R8: principales y backs', () => {
  /** P1 = emp 6, P2 = emp 7; back emp 8 → P2. */
  const conPersonal = () =>
    aplicar(
      conCabeceraCampo(),
      { tipo: 'agregarPrincipal', empleado: empleado(6), maximo: MAX },
      { tipo: 'agregarPrincipal', empleado: empleado(7), maximo: MAX },
      { tipo: 'agregarBack', empleado: empleado(8), maximo: MAX },
      { tipo: 'actualizarBack', clave: 'b3', cambios: { principalClave: 'p2' } },
    )

  it('los principales nuevos sugieren el rango del proyecto (P2)', () => {
    const estado = conPersonal()
    expect(estado.principales[0]).toMatchObject({ fechaInicio: '2026-12-01', fechaFin: '2026-12-31' })
  })

  it('el back envía el número actual del principal relacionado', () => {
    expect(aSolicitud(conPersonal()).backs[0].principalRelacionado).toBe(2)
  })

  it('al reordenar, la referencia sigue a la misma persona', () => {
    const estado = aplicar(conPersonal(), { tipo: 'moverPrincipal', clave: 'p2', direccion: -1 })
    const s = aSolicitud(estado)
    expect(s.principales.map((p) => p.empleadoId)).toEqual([7, 6])
    expect(s.backs[0].principalRelacionado).toBe(1)
  })

  it('mover fuera de los límites no hace nada', () => {
    const antes = conPersonal()
    expect(aplicar(antes, { tipo: 'moverPrincipal', clave: 'p1', direccion: -1 })).toBe(antes)
    expect(aplicar(antes, { tipo: 'moverPrincipal', clave: 'p2', direccion: 1 })).toBe(antes)
  })

  it('al eliminar otro principal se renumera y la referencia se conserva', () => {
    const estado = aplicar(conPersonal(), { tipo: 'eliminarPrincipal', clave: 'p1' })
    expect(estado.backs[0]).toMatchObject({ principalClave: 'p2', avisoRelacion: false })
    expect(aSolicitud(estado).backs[0].principalRelacionado).toBe(1)
  })

  it('al eliminar el principal relacionado, el back queda sin relación y con aviso', () => {
    const estado = aplicar(conPersonal(), { tipo: 'eliminarPrincipal', clave: 'p2' })
    expect(estado.backs[0]).toMatchObject({ principalClave: null, avisoRelacion: true })
    expect(aSolicitud(estado).backs[0].principalRelacionado).toBeNull()
  })

  it('elegir una relación (o "sin relación") quita el aviso', () => {
    const sinRelacion = aplicar(conPersonal(), { tipo: 'eliminarPrincipal', clave: 'p2' })
    const conOtro = aplicar(sinRelacion, { tipo: 'actualizarBack', clave: 'b3', cambios: { principalClave: 'p1' } })
    const sinNinguno = aplicar(sinRelacion, { tipo: 'actualizarBack', clave: 'b3', cambios: { principalClave: null } })
    expect(conOtro.backs[0].avisoRelacion).toBe(false)
    expect(sinNinguno.backs[0].avisoRelacion).toBe(false)
  })

  it('DESCANSO deja los días de descanso en 0 y no se pueden cambiar', () => {
    const estado = aplicar(
      conPersonal(),
      { tipo: 'actualizarBack', clave: 'b3', cambios: { diasDescanso: 2 } },
      { tipo: 'actualizarBack', clave: 'b3', cambios: { tipoRegistro: 'DESCANSO' } },
      { tipo: 'actualizarBack', clave: 'b3', cambios: { diasDescanso: 3 } },
    )
    expect(estado.backs[0].diasDescanso).toBe(0)
    expect(aSolicitud(estado).backs[0]).toMatchObject({ tipoRegistro: 'DESCANSO', diasDescanso: 0 })
  })

  it('JORNADA conserva los días de descanso', () => {
    const estado = aplicar(conPersonal(), { tipo: 'actualizarBack', clave: 'b3', cambios: { diasDescanso: 2 } })
    expect(aSolicitud(estado).backs[0].diasDescanso).toBe(2)
  })

  it('la misma persona puede ser principal y back (E6); el buscador la marca', () => {
    const estado = aplicar(conPersonal(), { tipo: 'agregarBack', empleado: empleado(6), maximo: MAX })
    expect(estado.backs).toHaveLength(2)
    expect(etiquetasPorEmpleado(estado).get(6)).toEqual(['P1', 'Back 2'])
  })

  it('numeroPrincipal de una clave inexistente es null', () => {
    expect(numeroPrincipal(conPersonal().principales, 'p99')).toBeNull()
  })
})

describe('R9 / V20: máximos', () => {
  it('no se agregan principales ni backs por encima del máximo', () => {
    const conDos = aplicar(
      conCabeceraCampo(),
      { tipo: 'agregarPrincipal', empleado: empleado(1), maximo: 2 },
      { tipo: 'agregarPrincipal', empleado: empleado(2), maximo: 2 },
      { tipo: 'agregarBack', empleado: empleado(3), maximo: 1 },
    )
    const despues = aplicar(
      conDos,
      { tipo: 'agregarPrincipal', empleado: empleado(4), maximo: 2 },
      { tipo: 'agregarBack', empleado: empleado(5), maximo: 1 },
    )
    expect(despues).toBe(conDos)
    expect(despues.principales).toHaveLength(2)
    expect(despues.backs).toHaveLength(1)
  })
})

describe('R12: revisión', () => {
  it('cada cambio de datos aumenta la revisión', () => {
    const estado = conCabeceraCampo()
    const cambiado = aplicar(estado, { tipo: 'cabecera', cambios: { salidaAlmuerzo: '13:00' } })
    expect(cambiado.revision).toBe(estado.revision + 1)
  })
})

describe('R4: validación del cliente (ayuda)', () => {
  it('fin anterior al inicio y regreso ≤ salida', () => {
    const estado = aplicar(inicial(), {
      tipo: 'cabecera',
      cambios: { fechaInicio: '2026-12-10', fechaFin: '2026-12-01', salidaAlmuerzo: '14:00', regresoAlmuerzo: '13:00' },
    })
    const errores = validarCliente(estado)
    expect(errores.cabecera.fechaFin).toBeDefined()
    expect(errores.cabecera.regresoAlmuerzo).toBe('El regreso de almuerzo debe ser posterior a la salida.')
  })

  it('datos válidos: sin errores', () => {
    const estado = aplicar(conCabeceraCampo(), {
      tipo: 'cabecera',
      cambios: { salidaAlmuerzo: '13:00', regresoAlmuerzo: '14:00' },
    })
    expect(validarCliente(estado)).toEqual({ cabecera: {}, filas: {} })
  })

  it('fila con fin anterior al inicio', () => {
    const estado = aplicar(
      conCabeceraCampo(),
      { tipo: 'agregarPrincipal', empleado: empleado(6), maximo: MAX },
      { tipo: 'actualizarPrincipal', clave: 'p1', cambios: { fechaInicio: '2026-12-20', fechaFin: '2026-12-10' } },
    )
    expect(validarCliente(estado).filas.p1?.fechaFin).toBeDefined()
  })
})

describe('P6: mínimo de principales (C10)', () => {
  it('sin principales no se genera la vista previa', () => {
    expect(puedeGenerarVistaPrevia(conCabeceraCampo(), false)).toBe(false)
  })

  it('con 1 principal sí, salvo durante un envío', () => {
    const estado = aplicar(conCabeceraCampo(), { tipo: 'agregarPrincipal', empleado: empleado(1), maximo: MAX })
    expect(puedeGenerarVistaPrevia(estado, false)).toBe(true)
    expect(puedeGenerarVistaPrevia(estado, true)).toBe(false)
  })

  it('ayuda neutra solo mientras falten principales', () => {
    expect(ayudaPrincipales(conCabeceraCampo())).toBe('Agrega al menos 1 principal para generar la vista previa.')
    const conUno = aplicar(conCabeceraCampo(), { tipo: 'agregarPrincipal', empleado: empleado(1), maximo: MAX })
    expect(ayudaPrincipales(conUno)).toBeUndefined()
  })

  it('al eliminar el único principal vuelve a bloquearse', () => {
    const conUno = aplicar(conCabeceraCampo(), { tipo: 'agregarPrincipal', empleado: empleado(1), maximo: MAX })
    const sinNinguno = aplicar(conUno, { tipo: 'eliminarPrincipal', clave: conUno.principales[0].clave })
    expect(puedeGenerarVistaPrevia(sinNinguno, false)).toBe(false)
  })
})
