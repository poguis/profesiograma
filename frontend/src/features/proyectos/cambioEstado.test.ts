import { describe, expect, it } from 'vitest'
import {
  type AccionDialogo,
  type EstadoDialogo,
  type VistaCambio,
  aSolicitudCambio,
  aSolicitudRegistroCambio,
  crearEstadoDialogo,
  fechaPorDefecto,
  interpretarErrorCambio,
  mensajeExito,
  opcionesDestino,
  puedeCambiarEstado,
  puedeConfirmar,
  reducerDialogo,
  vistaCambioVigente,
} from './cambioEstado'
import type { PrevisualizacionCambioEstado } from './tipos'

const FIN = '2027-04-30'

function aplicar(estado: EstadoDialogo, ...acciones: AccionDialogo[]) {
  return acciones.reduce(reducerDialogo, estado)
}

const elegir = (destino: 'SUSPENDIDO' | 'TERMINADO', estadoProyecto = 'ACTIVO'): AccionDialogo => ({
  tipo: 'destino',
  destino,
  estadoProyecto,
  fechaFin: FIN,
})

const datos = (): PrevisualizacionCambioEstado => ({
  movimiento: 'SUSPENSION',
  estadoActual: 'ACTIVO',
  estadoNuevo: 'SUSPENDIDO',
  fecha: '2027-04-20',
  fechaFinActual: FIN,
  fechaFinNueva: '2027-04-20',
  diasEliminados: [],
  personalEliminado: [],
  personalRecortado: [],
  actividadesAfectadas: [],
  advertencias: [],
  versionProyecto: 1,
})

describe('R1/R2: máquina de estados (solo interfaz)', () => {
  it.each([
    ['ACTIVO', true],
    ['SUSPENDIDO', true],
    ['TERMINADO', false],
    ['INACTIVO', false],
  ])('%s → botón visible: %s', (estado, visible) => {
    expect(puedeCambiarEstado(estado)).toBe(visible)
  })

  it('ACTIVO: Suspender y Terminar, sin Reactivar', () => {
    expect(opcionesDestino('ACTIVO').map((o) => [o.codigo, o.habilitada])).toEqual([
      ['SUSPENDIDO', true],
      ['TERMINADO', true],
    ])
  })

  it('SUSPENDIDO: Terminar y Reactivar deshabilitado con ayuda', () => {
    const opciones = opcionesDestino('SUSPENDIDO')
    expect(opciones.map((o) => [o.codigo, o.habilitada])).toEqual([
      ['TERMINADO', true],
      ['ACTIVO', false],
    ])
    expect(opciones[1]).toMatchObject({ etiqueta: 'Reactivar', ayuda: 'Disponible próximamente' })
  })

  it('TERMINADO: sin opciones', () => {
    expect(opcionesDestino('TERMINADO')).toEqual([])
  })
})

describe('fecha por defecto (H1)', () => {
  it('CIERRE desde SUSPENDIDO propone la fecha fin actual', () => {
    expect(fechaPorDefecto('SUSPENDIDO', 'TERMINADO', FIN)).toBe(FIN)
  })

  it.each([
    ['ACTIVO', 'SUSPENDIDO'],
    ['ACTIVO', 'TERMINADO'],
  ] as const)('%s → %s: vacía', (estado, destino) => {
    expect(fechaPorDefecto(estado, destino, FIN)).toBeNull()
  })
})

describe('reducer del diálogo', () => {
  it('elegir destino desde SUSPENDIDO propone la fecha y aumenta la revisión', () => {
    const estado = aplicar(crearEstadoDialogo(), elegir('TERMINADO', 'SUSPENDIDO'))
    expect(estado).toMatchObject({ destino: 'TERMINADO', fecha: FIN, revision: 1 })
  })

  it('D1: cambiar de destino reinicia la fecha y la casilla', () => {
    const estado = aplicar(
      crearEstadoDialogo(),
      elegir('SUSPENDIDO'),
      { tipo: 'fecha', fecha: '2027-04-20' },
      { tipo: 'entiende', valor: true },
      elegir('TERMINADO'),
    )
    expect(estado).toMatchObject({ destino: 'TERMINADO', fecha: null, entiendeTerminado: false })
  })

  it('el mismo destino o la misma fecha no cambian el estado', () => {
    const antes = aplicar(crearEstadoDialogo(), elegir('SUSPENDIDO'), { tipo: 'fecha', fecha: '2027-04-20' })
    expect(aplicar(antes, elegir('SUSPENDIDO'), { tipo: 'fecha', fecha: '2027-04-20' })).toBe(antes)
  })

  it('la casilla no invalida la vista previa (no cambia la revisión)', () => {
    const antes = aplicar(crearEstadoDialogo(), elegir('TERMINADO'))
    const despues = aplicar(antes, { tipo: 'entiende', valor: true })
    expect(despues.revision).toBe(antes.revision)
  })

  it('reiniciar vacía todo e invalida cualquier vista previa', () => {
    const antes = aplicar(crearEstadoDialogo(), elegir('SUSPENDIDO'), { tipo: 'fecha', fecha: '2027-04-20' })
    const despues = aplicar(antes, { tipo: 'reiniciar' })
    expect(despues).toMatchObject({ destino: null, fecha: null, entiendeTerminado: false })
    expect(despues.revision).toBeGreaterThan(antes.revision)
  })

  it('aSolicitudCambio arma { estadoDestino, fecha } (null si faltan)', () => {
    expect(aSolicitudCambio(crearEstadoDialogo())).toEqual({ estadoDestino: null, fecha: null })
    const estado = aplicar(crearEstadoDialogo(), elegir('SUSPENDIDO'), { tipo: 'fecha', fecha: '2027-04-20' })
    expect(aSolicitudCambio(estado)).toEqual({ estadoDestino: 'SUSPENDIDO', fecha: '2027-04-20' })
  })
})

describe('R3/R4: vista previa y "Confirmar"', () => {
  const suspender = aplicar(crearEstadoDialogo(), elegir('SUSPENDIDO'), { tipo: 'fecha', fecha: '2027-04-20' })
  const vista = (revision: number): VistaCambio => ({ revision, datos: datos() })

  it('sin vista previa: deshabilitado', () => {
    expect(puedeConfirmar(null, suspender, false)).toBe(false)
  })

  it('vista previa vigente (SUSPENDER): habilitado', () => {
    expect(puedeConfirmar(vista(suspender.revision), suspender, false)).toBe(true)
  })

  it('cambiar la fecha o el destino invalida la vista previa', () => {
    const v = vista(suspender.revision)
    const otraFecha = aplicar(suspender, { tipo: 'fecha', fecha: '2027-04-21' })
    const otroDestino = aplicar(suspender, elegir('TERMINADO'))
    expect(vistaCambioVigente(v, otraFecha.revision)).toBe(false)
    expect(puedeConfirmar(v, otraFecha, false)).toBe(false)
    expect(puedeConfirmar(v, otroDestino, false)).toBe(false)
  })

  it('durante el envío: deshabilitado (doble clic)', () => {
    expect(puedeConfirmar(vista(suspender.revision), suspender, true)).toBe(false)
  })

  it('TERMINAR exige la casilla', () => {
    const terminar = aplicar(crearEstadoDialogo(), elegir('TERMINADO'), { tipo: 'fecha', fecha: '2027-04-20' })
    const v = vista(terminar.revision)
    expect(puedeConfirmar(v, terminar, false)).toBe(false)
    expect(puedeConfirmar(v, aplicar(terminar, { tipo: 'entiende', valor: true }), false)).toBe(true)
  })
})

describe('R5: respuestas', () => {
  it('mensaje de éxito', () => {
    expect(mensajeExito('SUSPENDIDO', 2)).toBe('Proyecto suspendido (versión 2).')
    expect(mensajeExito('TERMINADO', 3)).toBe('Proyecto terminado (versión 3).')
  })

  it('400 con estadoDestino y fecha: ambos bajo su campo y se ofrece recargar', () => {
    const r = interpretarErrorCambio({
      tipo: 'validacion',
      titulo: 'Los datos del cambio de estado no son válidos.',
      errores: {
        estadoDestino: ['El proyecto ya está en estado SUSPENDIDO.'],
        fecha: ['La fecha del movimiento no puede ser mayor a la fecha fin del proyecto (10/02/2027).'],
      },
    })
    expect(r.campos).toEqual({
      estadoDestino: 'El proyecto ya está en estado SUSPENDIDO.',
      fecha: 'La fecha del movimiento no puede ser mayor a la fecha fin del proyecto (10/02/2027).',
    })
    expect(r.generales).toEqual([])
    expect(r.ofrecerRecarga).toBe(true)
    expect(r.ofrecerReintento).toBe(false)
  })

  it('400 con claves desconocidas: van arriba', () => {
    const r = interpretarErrorCambio({ tipo: 'validacion', titulo: 'x', errores: { $: ['JSON inválido.'] } })
    expect(r.generales).toEqual(['JSON inválido.'])
    expect(r.campos).toEqual({})
  })

  it('409: mensaje del servidor y recarga', () => {
    const r = interpretarErrorCambio({ tipo: 'conflicto', titulo: 'El proyecto cambió de estado; vuelve a cargarlo.' })
    expect(r).toMatchObject({ generales: ['El proyecto cambió de estado; vuelve a cargarlo.'], ofrecerRecarga: true })
  })

  it.each(['noDisponible', 'red'] as const)('%s: reintentar', (tipo) => {
    expect(interpretarErrorCambio({ tipo, titulo: 'x' })).toMatchObject({ ofrecerReintento: true, ofrecerRecarga: false })
  })

  it('404: mensaje y enlace al listado', () => {
    expect(interpretarErrorCambio({ tipo: 'noEncontrado', titulo: 'x' })).toMatchObject({
      noEncontrado: true,
      generales: ['El proyecto no existe o no tiene acceso.'],
    })
  })
})

describe('TAREA-19x: token de concurrencia', () => {
  // TAREA-19b2 (ajuste aprobado): el registro lleva el token BASE, no el de la vista previa.
  it('el registro lleva el token base (última etapa al abrir), aunque la vista previa traiga otra versión', () => {
    const estado = aplicar(crearEstadoDialogo(), elegir('SUSPENDIDO'), { tipo: 'fecha', fecha: '2027-04-20' })
    expect(aSolicitudRegistroCambio(estado, 5)).toEqual({ estadoDestino: 'SUSPENDIDO', fecha: '2027-04-20', versionProyecto: 5 })
    expect(aSolicitudCambio(estado)).toEqual({ estadoDestino: 'SUSPENDIDO', fecha: '2027-04-20' })
  })

  it('TAREA-19b2: una vista previa con otra versión que la base bloquea la confirmación', () => {
    const estado = aplicar(crearEstadoDialogo(), elegir('SUSPENDIDO'), { tipo: 'fecha', fecha: '2027-04-20' })
    const vista: VistaCambio = { revision: estado.revision, datos: { ...datos(), versionProyecto: 6 } }
    expect(puedeConfirmar(vista, estado, false, 5)).toBe(false)
    expect(puedeConfirmar(vista, estado, false, 6)).toBe(true)
    expect(puedeConfirmar(vista, estado, false)).toBe(true) // sin base: como antes
  })

  it('409 por token viejo: mensaje y recarga', () => {
    expect(interpretarErrorCambio({ tipo: 'conflicto', titulo: 'El proyecto cambió; vuelve a cargarlo.' })).toMatchObject({
      generales: ['El proyecto cambió; vuelve a cargarlo.'],
      ofrecerRecarga: true,
    })
  })

  it('400 sin token: mensaje arriba y recarga', () => {
    const r = interpretarErrorCambio({
      tipo: 'validacion',
      titulo: 'x',
      errores: { versionProyecto: ['Falta la versión del proyecto; vuelve a cargarlo.'] },
    })
    expect(r).toMatchObject({ generales: ['Falta la versión del proyecto; vuelve a cargarlo.'], ofrecerRecarga: true })
  })
})
