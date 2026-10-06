import { describe, expect, it } from 'vitest'
import {
  type AccionEdicion,
  type EstadoEdicion,
  type ValoresCabecera,
  type VistaCabecera,
  aSolicitudCabecera,
  crearEstadoEdicion,
  etiquetaCampo,
  interpretarErrorCabecera,
  mensajeExitoCabecera,
  motivoSinRegistro,
  puedeRegistrar,
  puedeVerImpacto,
  rangoDesde,
  rangoFechaFin,
  rangoFechaInicio,
  reducerEdicion,
  requiereConfirmacion,
  sinCambios,
  textoValorCambio,
  validarEdicionCliente,
  vistaCabeceraVigente,
} from './edicionCabecera'
import type { CabeceraEdicion, PrevisualizacionCabecera } from './tipos'

// Datos ficticios con la forma del CabeceraDto (no son del proyecto real).
const cabecera = (cambios: Partial<CabeceraEdicion> = {}): CabeceraEdicion => ({
  id: 11,
  codigo: 'PRY-PRUEBA',
  estado: 'ACTIVO',
  puedeEditar: true,
  motivo: null,
  grupo: 'CAMPO',
  fechaInicio: '2026-10-11',
  fechaFin: '2026-10-16',
  horario: { codigo: 1, descripcion: 'HORARIO 1', horaEntrada: '08:00:00', horaSalida: '17:00:00' },
  salidaAlmuerzo: '12:00',
  regresoAlmuerzo: '13:00',
  actividadVigente: null,
  actividades: [],
  permisos: { fechaInicioEditable: true, motivoFechaInicio: null, fechaFinMinima: '2026-10-06', actividadEditable: true },
  opcionesAlmuerzo: { salida: ['12:00', '12:30'], regreso: ['13:00', '13:30'] },
  corte: '2026-10-06',
  ...cambios,
})

function aplicar(estado: EstadoEdicion, ...acciones: AccionEdicion[]) {
  return acciones.reduce(reducerEdicion, estado)
}

const campos = (cambios: Partial<ValoresCabecera>): AccionEdicion => ({ tipo: 'campos', cambios })

const datos = (cambios: Partial<PrevisualizacionCabecera> = {}): PrevisualizacionCabecera => ({
  corte: '2026-10-06',
  tipoEtapa: 'EDICION_CABECERA',
  fechaInicioNueva: '2026-10-11',
  fechaFinNueva: '2026-10-20',
  cambios: [{ campo: 'fechaFin', anterior: '2026-10-16', nuevo: '2026-10-20' }],
  diasEliminados: [],
  personalEliminado: [],
  personalRecortado: [],
  diasAgregados: [],
  actividades: [],
  advertencias: [],
  ...cambios,
})

const empleado = { id: 1, codigoEkon: 'DEV001', nombreCompleto: 'EMPLEADO PRUEBA 01' }

describe('estado del formulario', () => {
  it('empieza con los valores guardados, revisión 0 y sin cambio de actividad', () => {
    const e = crearEstadoEdicion(cabecera())
    expect(e.revision).toBe(0)
    expect(e.editado).toBe(false)
    expect(e.valores).toEqual({
      fechaInicio: '2026-10-11',
      fechaFin: '2026-10-16',
      horarioCodigo: 1,
      salidaAlmuerzo: '12:00',
      regresoAlmuerzo: '13:00',
      actividadId: null,
      actividadDesde: null,
    })
    expect(e.original).toEqual(e.valores)
  })

  it.each([
    ['fechaInicio', { fechaInicio: '2026-10-12' }],
    ['fechaFin', { fechaFin: '2026-10-20' }],
    ['horarioCodigo', { horarioCodigo: 2 }],
    ['salidaAlmuerzo', { salidaAlmuerzo: '12:30' }],
    ['regresoAlmuerzo', { regresoAlmuerzo: '13:30' }],
    ['actividadId', { actividadId: 'DEV.02' }],
    ['actividadDesde', { actividadDesde: '2026-10-14' }],
  ] as const)('cambiar %s sube la revisión y marca editado', (_, cambio) => {
    const e = aplicar(crearEstadoEdicion(cabecera()), campos(cambio))
    expect(e.revision).toBe(1)
    expect(e.editado).toBe(true)
  })

  it('el mismo valor no cambia la revisión', () => {
    const e = crearEstadoEdicion(cabecera())
    expect(aplicar(e, campos({ fechaFin: '2026-10-16' }))).toBe(e)
  })

  it('un cambio desmarca la casilla; marcarla no cambia la revisión', () => {
    const marcado = aplicar(crearEstadoEdicion(cabecera()), { tipo: 'entiende', valor: true })
    expect(marcado.entiende).toBe(true)
    expect(marcado.revision).toBe(0)
    expect(aplicar(marcado, campos({ fechaFin: '2026-10-20' })).entiende).toBe(false)
  })

  it('reiniciar toma la cabecera recargada y sigue subiendo la revisión', () => {
    const e = aplicar(crearEstadoEdicion(cabecera()), campos({ fechaFin: '2026-10-20' }), {
      tipo: 'reiniciar',
      cabecera: cabecera({ fechaFin: '2026-10-18' }),
    })
    expect(e.revision).toBe(2)
    expect(e.editado).toBe(false)
    expect(e.valores.fechaFin).toBe('2026-10-18')
    expect(e.original.fechaFin).toBe('2026-10-18')
  })
})

describe('aSolicitudCabecera (C1: solo lo que cambia)', () => {
  it('sin cambios: todo null', () => {
    expect(aSolicitudCabecera(crearEstadoEdicion(cabecera()))).toEqual({
      fechaInicio: null,
      fechaFin: null,
      horarioCodigo: null,
      salidaAlmuerzo: null,
      regresoAlmuerzo: null,
      actividad: null,
    })
  })

  it('solo los campos cambiados', () => {
    const e = aplicar(crearEstadoEdicion(cabecera()), campos({ fechaFin: '2026-10-20', regresoAlmuerzo: '13:30' }))
    expect(aSolicitudCabecera(e)).toEqual({
      fechaInicio: null,
      fechaFin: '2026-10-20',
      horarioCodigo: null,
      salidaAlmuerzo: null,
      regresoAlmuerzo: '13:30',
      actividad: null,
    })
  })

  it('volver al valor guardado deja el campo en null', () => {
    const e = aplicar(crearEstadoEdicion(cabecera()), campos({ horarioCodigo: 2 }), campos({ horarioCodigo: 1 }))
    expect(aSolicitudCabecera(e).horarioCodigo).toBeNull()
  })

  it('actividad con desde', () => {
    const e = aplicar(crearEstadoEdicion(cabecera()), campos({ actividadId: 'DEV.02', actividadDesde: '2026-10-14' }))
    expect(aSolicitudCabecera(e).actividad).toEqual({ actividadId: 'DEV.02', desde: '2026-10-14' })
  })

  it('actividad sin desde (o desde sin actividad) se envía: el servidor responde 400 en el campo que falta', () => {
    const sinDesde = aplicar(crearEstadoEdicion(cabecera()), campos({ actividadId: 'DEV.02' }))
    expect(aSolicitudCabecera(sinDesde).actividad).toEqual({ actividadId: 'DEV.02', desde: null })
    const sinActividad = aplicar(crearEstadoEdicion(cabecera()), campos({ actividadDesde: '2026-10-14' }))
    expect(aSolicitudCabecera(sinActividad).actividad).toEqual({ actividadId: null, desde: '2026-10-14' })
  })
})

describe('rangos y ayudas', () => {
  it('inicio: mínimo hoy (corte), sin máximo', () => {
    expect(rangoFechaInicio(cabecera())).toEqual({ minima: '2026-10-06' })
  })

  it('fin: el mayor entre fechaFinMinima e inicio', () => {
    const c = cabecera()
    const e = crearEstadoEdicion(c)
    expect(rangoFechaFin(c, e.valores)).toEqual({ minima: '2026-10-11' })
    const pasado = cabecera({ fechaInicio: '2026-10-01', permisos: { ...c.permisos, fechaInicioEditable: false } })
    expect(rangoFechaFin(pasado, crearEstadoEdicion(pasado).valores)).toEqual({ minima: '2026-10-06' })
  })

  it('desde: inicio y fin resultantes', () => {
    const e = aplicar(crearEstadoEdicion(cabecera()), campos({ fechaFin: '2026-10-20' }))
    expect(rangoDesde(e.valores)).toEqual({ minima: '2026-10-11', maxima: '2026-10-20' })
  })

  it('ayudas: fin < inicio, regreso ≤ salida y desde fuera del rango', () => {
    const e = aplicar(
      crearEstadoEdicion(cabecera()),
      campos({ fechaFin: '2026-10-10', salidaAlmuerzo: '13:00', regresoAlmuerzo: '13:00', actividadDesde: '2026-10-30' }),
    )
    expect(validarEdicionCliente(e.valores)).toEqual({
      fechaFin: 'La fecha fin debe ser mayor o igual a la fecha de inicio.',
      regresoAlmuerzo: 'El regreso de almuerzo debe ser posterior a la salida.',
      'actividad.desde': 'La fecha desde debe estar dentro del rango del proyecto (11/10/2026 – 10/10/2026).',
    })
  })

  it('sin ayudas con los valores guardados', () => {
    expect(validarEdicionCliente(crearEstadoEdicion(cabecera()).valores)).toEqual({})
  })

  it('una fecha vacía bloquea "Ver impacto" (se enviaría como "no cambia")', () => {
    const vacia = aplicar(crearEstadoEdicion(cabecera()), campos({ fechaFin: null }))
    expect(validarEdicionCliente(vacia.valores).fechaFin).toBe('La fecha fin es obligatoria.')
    expect(puedeVerImpacto(vacia.valores, false)).toBe(false)
    expect(puedeVerImpacto(crearEstadoEdicion(cabecera()).valores, false)).toBe(true)
    expect(puedeVerImpacto(crearEstadoEdicion(cabecera()).valores, true)).toBe(false)
  })
})

describe('vista previa, confirmación y registro', () => {
  const recorte = datos({
    fechaFinNueva: '2026-10-15',
    personalRecortado: [
      { empleado, rol: 'BACK', numero: 1, fechaInicio: '2026-10-14', fechaFinAnterior: '2026-10-16', fechaFinNueva: '2026-10-15' },
    ],
  })

  it('la vista solo vale para su revisión', () => {
    const vista: VistaCabecera = { revision: 1, datos: datos() }
    expect(vistaCabeceraVigente(vista, 1)).toBe(true)
    expect(vistaCabeceraVigente(vista, 2)).toBe(false)
    expect(vistaCabeceraVigente(null, 0)).toBe(false)
  })

  it('sinCambios usa la lista de cambios', () => {
    expect(sinCambios(datos({ cambios: [], advertencias: ['No hay cambios.'] }))).toBe(true)
    expect(sinCambios(datos())).toBe(false)
  })

  it('requiereConfirmacion: recorte, días o personal eliminados, actividad eliminada o acortada', () => {
    expect(requiereConfirmacion(datos())).toBe(false)
    expect(requiereConfirmacion(recorte)).toBe(true)
    expect(
      requiereConfirmacion(datos({ diasEliminados: [{ empleado, rol: 'DESCANSO', cantidad: 1, desde: '2026-10-16', hasta: '2026-10-16' }] })),
    ).toBe(true)
    expect(
      requiereConfirmacion(datos({ personalEliminado: [{ empleado, rol: 'BACK', numero: 1, fechaInicio: '2026-10-16', fechaFin: '2026-10-16' }] })),
    ).toBe(true)
    const actividad = {
      version: 1,
      codigo: 'DEV.01',
      descripcion: null,
      fechaInicio: '2026-10-11',
      fechaFin: '2026-10-16',
      fechaInicioAnterior: null,
      fechaFinAnterior: null,
    }
    expect(requiereConfirmacion(datos({ actividades: [{ ...actividad, accion: 'ELIMINADA' }] }))).toBe(true)
    expect(
      requiereConfirmacion(
        datos({ actividades: [{ ...actividad, fechaFin: '2026-10-15', fechaInicioAnterior: '2026-10-11', fechaFinAnterior: '2026-10-16', accion: 'MODIFICADA' }] }),
      ),
    ).toBe(true)
    // Ampliar: MODIFICADA con fin mayor, NUEVA y SIN_CAMBIO no borran nada.
    expect(
      requiereConfirmacion(
        datos({
          actividades: [
            { ...actividad, fechaFin: '2026-10-20', fechaInicioAnterior: '2026-10-11', fechaFinAnterior: '2026-10-16', accion: 'MODIFICADA' },
            { ...actividad, version: 2, codigo: 'DEV.02', accion: 'NUEVA' },
            { ...actividad, version: 3, accion: 'SIN_CAMBIO' },
          ],
        }),
      ),
    ).toBe(false)
  })

  it('puedeRegistrar: vista vigente con cambios y sin envío en curso', () => {
    const e = aplicar(crearEstadoEdicion(cabecera()), campos({ fechaFin: '2026-10-20' }))
    expect(puedeRegistrar(null, e, false)).toBe(false)
    expect(puedeRegistrar({ revision: 0, datos: datos() }, e, false)).toBe(false) // desactualizada
    expect(puedeRegistrar({ revision: 1, datos: datos() }, e, false)).toBe(true)
    expect(puedeRegistrar({ revision: 1, datos: datos() }, e, true)).toBe(false) // enviando
    expect(puedeRegistrar({ revision: 1, datos: datos({ cambios: [] }) }, e, false)).toBe(false) // pendiente 25
  })

  it('puedeRegistrar con recorte exige la casilla', () => {
    const e = aplicar(crearEstadoEdicion(cabecera()), campos({ fechaFin: '2026-10-15' }))
    const vista = { revision: 1, datos: recorte }
    expect(puedeRegistrar(vista, e, false)).toBe(false)
    expect(motivoSinRegistro(vista, e, false)).toBe('Marque la casilla de confirmación.')
    const marcado = aplicar(e, { tipo: 'entiende', valor: true })
    expect(puedeRegistrar(vista, marcado, false)).toBe(true)
    expect(motivoSinRegistro(vista, marcado, false)).toBe('')
  })

  it('motivoSinRegistro', () => {
    const e = aplicar(crearEstadoEdicion(cabecera()), campos({ fechaFin: '2026-10-20' }))
    expect(motivoSinRegistro(null, e, false)).toBe('Para registrar, primero pulse "Ver impacto".')
    expect(motivoSinRegistro({ revision: 0, datos: datos() }, e, false)).toBe('La vista previa está desactualizada.')
    expect(motivoSinRegistro({ revision: 1, datos: datos({ cambios: [] }) }, e, false)).toBe('No hay cambios para registrar.')
    expect(motivoSinRegistro(null, e, true)).toBe('')
  })

  it('mensajes de éxito (P4)', () => {
    expect(mensajeExitoCabecera('EDICION_CABECERA', 7)).toBe('Datos generales actualizados (versión 7).')
    expect(mensajeExitoCabecera('CAMBIO_ACTIVIDAD', 8)).toBe('Actividad cambiada (versión 8).')
  })
})

describe('textos del impacto', () => {
  it('etiquetas de campo', () => {
    expect(etiquetaCampo('fechaFin')).toBe('Fecha fin')
    expect(etiquetaCampo('actividad')).toBe('Actividad')
    expect(etiquetaCampo('otro')).toBe('otro')
  })

  it('fechas ISO dentro del valor se muestran dd/MM/yyyy', () => {
    expect(textoValorCambio('2026-10-16')).toBe('16/10/2026')
    expect(textoValorCambio('DEV.02 desde 2026-10-14')).toBe('DEV.02 desde 14/10/2026')
    expect(textoValorCambio('12:30')).toBe('12:30')
    expect(textoValorCambio(null)).toBe('—')
  })
})

describe('interpretarErrorCabecera', () => {
  it('400: claves de campo en su campo, general arriba y sin recarga', () => {
    const r = interpretarErrorCabecera({
      tipo: 'validacion',
      titulo: 'Los datos de la cabecera no son válidos.',
      errores: {
        fechaInicio: ['Hay personal que empieza antes de la nueva fecha de inicio; ajusta primero el personal.'],
        'actividad.actividadId': ['La actividad DEV.01 ya está vigente el 14/10/2026.'],
        'actividad.desde': ['La fecha desde es obligatoria.'],
        general: ['No hay cambios para registrar.'],
      },
    })
    expect(r.campos).toEqual({
      fechaInicio: 'Hay personal que empieza antes de la nueva fecha de inicio; ajusta primero el personal.',
      'actividad.actividadId': 'La actividad DEV.01 ya está vigente el 14/10/2026.',
      'actividad.desde': 'La fecha desde es obligatoria.',
    })
    expect(r.generales).toEqual(['No hay cambios para registrar.'])
    expect(r.ofrecerRecarga).toBe(false)
  })

  it.each(['fechaFin', 'horarioCodigo', 'salidaAlmuerzo', 'regresoAlmuerzo'])('400 en %s va a su campo', (clave) => {
    expect(interpretarErrorCabecera({ tipo: 'validacion', titulo: 'x', errores: { [clave]: ['m'] } }).campos).toEqual({
      [clave]: 'm',
    })
  })

  it('400 en proyecto (ya no es ACTIVO): mensaje arriba y recarga', () => {
    const r = interpretarErrorCabecera({
      tipo: 'validacion',
      titulo: 'x',
      errores: { proyecto: ['Solo se puede editar la cabecera de un proyecto ACTIVO (estado actual: SUSPENDIDO).'] },
    })
    expect(r).toMatchObject({
      generales: ['Solo se puede editar la cabecera de un proyecto ACTIVO (estado actual: SUSPENDIDO).'],
      ofrecerRecarga: true,
    })
  })

  it('409: mensaje y recarga', () => {
    expect(interpretarErrorCabecera({ tipo: 'conflicto', titulo: 'El proyecto cambió; vuelve a cargarlo.' })).toMatchObject({
      generales: ['El proyecto cambió; vuelve a cargarlo.'],
      ofrecerRecarga: true,
    })
  })

  it.each(['noDisponible', 'red'] as const)('%s: reintentar', (tipo) => {
    expect(interpretarErrorCabecera({ tipo, titulo: 'x' })).toMatchObject({ ofrecerReintento: true, ofrecerRecarga: false })
  })

  it('404: mensaje y enlace al listado', () => {
    expect(interpretarErrorCabecera({ tipo: 'noEncontrado', titulo: 'x' })).toMatchObject({
      noEncontrado: true,
      generales: ['El proyecto no existe o no tiene acceso.'],
    })
  })
})
