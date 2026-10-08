import { describe, expect, it } from 'vitest'
import { esReactivacion, opcionesDestino } from './cambioEstado'
import { opcionesRelacion } from './edicionPersonal'
import type { AccionEdicionPersonal } from './edicionPersonal'
import {
  AVISO_PROPUESTO_INACTIVO,
  type AccionReactivacion,
  type EstadoReactivacion,
  type VistaReactivacion,
  aSolicitudRegistroReactivacion,
  aSolicitudReactivar,
  avisosFilaReactivacion,
  ayudaBacksEnR,
  ayudaReactivacion,
  claveInicialReactivacion,
  clavesDelEnvioReactivacion,
  crearEstadoReactivacion,
  fechaFinInicial,
  fechaReactivacionInicial,
  finAntesDeSuspension,
  interpretarErrorReactivacion,
  mensajeExitoReactivacion,
  motivoSinRegistroReactivacion,
  puedeGenerarVistaPreviaReactivacion,
  puedeRegistrarReactivacion,
  reactivacionEditada,
  reducerReactivacion,
  revisionReactivacion,
  validarFechasReactivacion,
  validarPersonalReactivacion,
  vistaReactivacionVigente,
} from './reactivacion'
import type { EtapaProyecto, PersonaReactivacion, PrevisualizacionReactivacion, Reactivacion } from './tipos'

// Datos ficticios con la forma del ReactivacionDto (como el Id 10 de la 17b: suspendido al 03/10/2026).
const persona = (id: number, rol: string, numero: number, empleado: number, cambios: Partial<PersonaReactivacion> = {}): PersonaReactivacion => ({
  id,
  rol,
  numero,
  empleado: { id: empleado, codigoEkon: `DEV00${empleado}`, nombreCompleto: `EMPLEADO PRUEBA 0${empleado}` },
  jornada: rol === 'PRINCIPAL' ? 'TIPO_2' : null,
  fechaInicio: '2026-10-02',
  fechaFin: '2026-10-03',
  tipoRegistro: 'JORNADA',
  diasDescanso: rol === 'PRINCIPAL' ? 4 : 3,
  esPrincipalInicial: false,
  ...cambios,
})

const P1 = persona(31, 'PRINCIPAL', 1, 7, { esPrincipalInicial: true })
const K1 = persona(32, 'BACK', 1, 1, { fechaInicio: '2026-10-03' })

const dto = (cambios: Partial<Reactivacion> = {}): Reactivacion => ({
  id: 10,
  codigo: 'PRY-PRUEBA',
  estadoActual: 'SUSPENDIDO',
  fechaInicio: '2026-10-02',
  fechaFinActual: '2026-10-03',
  fechaMinima: '2026-10-04',
  puedeReactivar: true,
  motivo: null,
  principalPropuesto: {
    empleado: { id: 7, codigoEkon: 'DEV007', nombreCompleto: 'EMPLEADO PRUEBA 07', activo: true },
    jornada: 'TIPO_2',
    cargo: 'PUESTO 7',
  },
  personal: [P1, K1],
  limites: { maxPrincipales: 20, maxBacks: 20, backMaxDiasDescanso: 20, exigePrincipal: false },
  advertencias: [],
  versionProyecto: 2,
  ...cambios,
})

const HOY = '2026-10-07'
const FIN = '2026-11-29'
const inicial = (cambios: Partial<Reactivacion> = {}, fin: string | null = FIN) => crearEstadoReactivacion(dto(cambios), HOY, fin)

const aplicar = (estado: EstadoReactivacion, ...acciones: AccionReactivacion[]) => acciones.reduce(reducerReactivacion, estado)
const personal = (accion: AccionEdicionPersonal): AccionReactivacion => ({ tipo: 'personal', accion })
const empleado = (id: number) => ({ codigoEkon: `DEV00${id}`, nombreCompleto: `EMPLEADO PRUEBA 0${id}`, cargo: null })
const agregar = (rol: 'PRINCIPAL' | 'BACK', id: number) => personal({ tipo: 'agregar', rol, empleado: empleado(id), maximo: 20 })

const etapa = (version: number, tipoMovimiento: string, fechaFin: string): EtapaProyecto => ({
  version,
  tipoMovimiento,
  estado: 'ACTIVO',
  fechaInicio: '2026-10-02',
  fechaFin,
  fechaCorte: null,
  actividadCodigo: null,
  fechaRegistroUtc: '2026-10-05T12:00:00Z',
})

describe('valores iniciales (P2, P3)', () => {
  it('R = max(fechaMinima, hoy)', () => {
    expect(fechaReactivacionInicial('2026-10-04', HOY)).toBe(HOY)
    expect(fechaReactivacionInicial('2026-10-31', HOY)).toBe('2026-10-31')
  })

  it('fin antes de la última suspensión (etapa anterior); null sin suspensión o sin etapa anterior', () => {
    const etapas = [etapa(3, 'SUSPENSION', '2026-10-20'), etapa(1, 'CREACION', '2026-11-29'), etapa(2, 'ACTUALIZACION_PERSONAL', '2026-12-15')]
    expect(finAntesDeSuspension(etapas)).toBe('2026-12-15')
    expect(finAntesDeSuspension([etapa(1, 'CREACION', FIN)])).toBeNull()
    expect(finAntesDeSuspension([etapa(1, 'SUSPENSION', FIN)])).toBeNull()
    expect(finAntesDeSuspension([])).toBeNull()
  })

  it('el fin planificado solo se propone si es ≥ R', () => {
    expect(fechaFinInicial(FIN, HOY)).toBe(FIN)
    expect(fechaFinInicial(HOY, HOY)).toBe(HOY)
    expect(fechaFinInicial('2026-10-05', HOY)).toBeNull()
    expect(fechaFinInicial(null, HOY)).toBeNull()
  })
})

describe('estado inicial', () => {
  it('todo el personal guardado es histórico; el propuesto (R7) se precarga sin marcar edición', () => {
    const e = inicial()
    expect(e.fecha).toBe(HOY)
    expect(e.fechaFin).toBe(FIN)
    expect(e.versionBase).toBe(2)
    expect(e.personal.historicas.map((h) => [h.id, h.clase])).toEqual([
      [31, 'HISTORICO'],
      [32, 'HISTORICO'],
    ])
    expect([...e.inicialesHistoricas]).toEqual([31])
    expect(e.personal.principales).toHaveLength(1)
    expect(e.personal.principales[0]).toMatchObject({
      clave: 'pn1',
      id: null,
      empleado: { codigoEkon: 'DEV007', cargo: null },
      jornada: 'TIPO_2',
      cargo: 'PUESTO 7',
      fechaInicio: HOY,
      fechaFin: FIN,
    })
    expect(e.personal.backs).toEqual([])
    expect(e.claveInactivo).toBeNull()
    expect(reactivacionEditada(e)).toBe(false)
    expect(claveInicialReactivacion(e)).toBe('pn1')
  })

  it('sin propuesto: sin filas nuevas; sin fin planificado: fecha fin vacía', () => {
    const e = inicial({ principalPropuesto: null }, null)
    expect(e.personal.principales).toEqual([])
    expect(e.fechaFin).toBeNull()
    expect(claveInicialReactivacion(e)).toBeNull()
  })

  it('P5: propuesto inactivo → aviso en la fila y vista previa bloqueada hasta cambiarlo o quitarlo', () => {
    const base = dto().principalPropuesto!
    const e = inicial({ principalPropuesto: { ...base, empleado: { ...base.empleado, activo: false } } })
    expect(e.claveInactivo).toBe('pn1')
    expect(avisosFilaReactivacion(e)).toEqual({ pn1: [AVISO_PROPUESTO_INACTIVO] })
    expect(puedeGenerarVistaPreviaReactivacion(e, false, false)).toBe(false)
    expect(ayudaReactivacion(e, false)).toMatch(/no está activo/)

    const cambiado = aplicar(e, personal({ tipo: 'cambiarEmpleado', clave: 'pn1', empleado: empleado(8) }))
    expect(cambiado.claveInactivo).toBeNull()
    expect(puedeGenerarVistaPreviaReactivacion(cambiado, false, false)).toBe(true)

    const quitado = aplicar(e, agregar('BACK', 3), personal({ tipo: 'quitar', clave: 'pn1' }))
    expect(quitado.claveInactivo).toBeNull()
    expect(puedeGenerarVistaPreviaReactivacion(quitado, false, false)).toBe(true)
  })
})

describe('fechas (P4)', () => {
  it('al cambiar R, las nuevas que empezaban en la R anterior la siguen; las demás no', () => {
    const e = aplicar(
      inicial(),
      agregar('BACK', 3),
      agregar('BACK', 4),
      personal({ tipo: 'actualizar', clave: 'kn3', cambios: { fechaInicio: '2026-10-20' } }),
      { tipo: 'fecha', fecha: '2026-10-10' },
    )
    expect(e.personal.principales[0].fechaInicio).toBe('2026-10-10')
    expect(e.personal.backs.map((b) => b.fechaInicio)).toEqual(['2026-10-10', '2026-10-20'])
    expect(e.personal.corte).toBe('2026-10-10')
    expect(reactivacionEditada(e)).toBe(true)
  })

  it('al cambiar la fecha fin, las que tenían el fin anterior o vacío la siguen', () => {
    const sinFin = inicial({}, null)
    expect(sinFin.personal.principales[0].fechaFin).toBeNull()
    const e = aplicar(
      sinFin,
      agregar('BACK', 3),
      personal({ tipo: 'actualizar', clave: 'kn2', cambios: { fechaFin: '2026-10-15' } }),
      { tipo: 'fechaFin', fechaFin: '2026-11-30' },
    )
    expect(e.personal.principales[0].fechaFin).toBe('2026-11-30')
    expect(e.personal.backs[0].fechaFin).toBe('2026-10-15')
    expect(e.personal.fechaFinProyecto).toBe('2026-11-30')
    const otra = aplicar(e, { tipo: 'fechaFin', fechaFin: '2026-12-10' })
    expect(otra.personal.principales[0].fechaFin).toBe('2026-12-10')
    expect(otra.personal.backs[0].fechaFin).toBe('2026-10-15')
  })

  it('una nueva se agrega de R a la fecha fin nueva (vacía si aún no hay)', () => {
    expect(aplicar(inicial(), agregar('BACK', 3)).personal.backs[0]).toMatchObject({ fechaInicio: HOY, fechaFin: FIN })
    expect(aplicar(inicial({}, null), agregar('BACK', 3)).personal.backs[0]).toMatchObject({ fechaInicio: HOY, fechaFin: null })
  })

  it('el inicio del primer principal queda en R; tras ↑↓ el nuevo primero pasa a R', () => {
    const e = inicial()
    expect(aplicar(e, personal({ tipo: 'actualizar', clave: 'pn1', cambios: { fechaInicio: '2026-10-20' } }))).toBe(e)
    const conJornada = aplicar(e, personal({ tipo: 'actualizar', clave: 'pn1', cambios: { fechaInicio: '2026-10-20', jornada: 'TIPO_1' } }))
    expect(conJornada.personal.principales[0]).toMatchObject({ fechaInicio: HOY, jornada: 'TIPO_1' })

    const dos = aplicar(e, agregar('PRINCIPAL', 8), personal({ tipo: 'actualizar', clave: 'pn2', cambios: { fechaInicio: '2026-10-20' } }))
    expect(dos.personal.principales[1].fechaInicio).toBe('2026-10-20')
    const movido = aplicar(dos, personal({ tipo: 'moverNuevo', clave: 'pn2', direccion: -1 }))
    expect(movido.personal.principales.map((p) => [p.clave, p.fechaInicio])).toEqual([
      ['pn2', HOY],
      ['pn1', HOY],
    ])
    expect(claveInicialReactivacion(movido)).toBe('pn2')
  })

  it('cada cambio de fechas o de personal invalida la vista previa', () => {
    const e = inicial()
    const vista: VistaReactivacion = { revision: revisionReactivacion(e), datos: previa() }
    expect(vistaReactivacionVigente(vista, e)).toBe(true)
    expect(vistaReactivacionVigente(vista, aplicar(e, { tipo: 'fecha', fecha: '2026-10-08' }))).toBe(false)
    expect(vistaReactivacionVigente(vista, aplicar(e, agregar('BACK', 3)))).toBe(false)
    expect(aplicar(e, { tipo: 'fecha', fecha: HOY })).toBe(e) // misma fecha: sin cambios
  })
})

describe('reglas de ayuda (R2, R5, R6, RN08)', () => {
  it('mínimo solo con las nuevas: las históricas no cuentan', () => {
    const vacio = aplicar(inicial(), personal({ tipo: 'quitar', clave: 'pn1' }))
    expect(ayudaReactivacion(vacio, false)).toBe('Agrega al menos 1 persona (principal o back) para generar la vista previa.')
    expect(puedeGenerarVistaPreviaReactivacion(vacio, false, false)).toBe(false)
    const soloBack = aplicar(vacio, agregar('BACK', 3))
    expect(ayudaReactivacion(soloBack, false)).toBeUndefined()
    expect(ayudaReactivacion(soloBack, true)).toBe('Agrega al menos 1 principal para generar la vista previa.')
    expect(puedeGenerarVistaPreviaReactivacion(inicial(), true, false)).toBe(false) // enviando
  })

  it('fechas obligatorias antes de generar la vista previa', () => {
    expect(ayudaReactivacion(aplicar(inicial(), { tipo: 'fecha', fecha: null }), false)).toMatch(/fecha de reactivación/)
    expect(ayudaReactivacion(inicial({}, null), false)).toMatch(/nueva fecha fin/)
  })

  it('R6 solo con backs: ayuda si ninguno empieza en R (no bloquea)', () => {
    const soloBack = aplicar(
      inicial(),
      personal({ tipo: 'quitar', clave: 'pn1' }),
      agregar('BACK', 3),
      personal({ tipo: 'actualizar', clave: 'kn2', cambios: { fechaInicio: '2026-10-08' } }),
    )
    expect(ayudaBacksEnR(soloBack)).toBe('Al menos un back debe empezar en la fecha de reactivación (07/10/2026).')
    expect(puedeGenerarVistaPreviaReactivacion(soloBack, false, false)).toBe(true)
    expect(ayudaBacksEnR(aplicar(soloBack, agregar('BACK', 4)))).toBeUndefined()
    expect(ayudaBacksEnR(inicial())).toBeUndefined() // con principal no aplica
    expect(claveInicialReactivacion(soloBack)).toBeNull()
  })

  it('R2: R posterior a la fecha fin actual; fecha fin nueva ≥ R', () => {
    expect(validarFechasReactivacion(inicial())).toEqual({})
    const e = aplicar(inicial(), { tipo: 'fecha', fecha: '2026-10-03' }, { tipo: 'fechaFin', fechaFin: '2026-10-02' })
    expect(validarFechasReactivacion(e)).toEqual({
      fecha: 'La fecha de reactivación debe ser posterior a la fecha fin actual del proyecto (03/10/2026).',
      fechaFin: 'La fecha fin no puede ser anterior a la fecha de reactivación (03/10/2026).',
    })
  })

  it('por fila: inicio < R, fuera del rango con la fecha fin nueva, fin < inicio, obligatorios y jornada', () => {
    const e = aplicar(
      inicial(),
      agregar('BACK', 3),
      agregar('BACK', 4),
      agregar('PRINCIPAL', 8),
      personal({ tipo: 'actualizar', clave: 'kn2', cambios: { fechaInicio: '2026-10-05' } }),
      personal({ tipo: 'actualizar', clave: 'kn3', cambios: { fechaFin: '2026-12-01' } }),
      personal({ tipo: 'actualizar', clave: 'pn4', cambios: { fechaInicio: '2026-10-20', fechaFin: '2026-10-10' } }),
    )
    expect(validarPersonalReactivacion(e)).toEqual({
      pn4: { fechaFin: 'La fecha fin debe ser mayor o igual a la fecha de inicio.', jornada: 'La jornada es obligatoria.' },
      kn2: { fechaInicio: 'La fecha de inicio no puede ser anterior a la fecha de reactivación (07/10/2026).' },
      kn3: { fechaFin: 'Las fechas deben estar dentro del rango del proyecto (02/10/2026 – 29/11/2026).' },
    })
    expect(validarPersonalReactivacion(inicial({}, null))).toEqual({ pn1: { fechaFin: 'La fecha fin es obligatoria.' } })
  })
})

describe('solicitud', () => {
  it('R, fecha fin y solo las nuevas (sin id); relación por clave o por principal histórico', () => {
    const e = aplicar(
      inicial(),
      agregar('BACK', 3),
      agregar('BACK', 4),
      personal({ tipo: 'actualizar', clave: 'kn2', cambios: { relacion: { clave: 'pn1' }, observacion: '  Cubre  ' } }),
      personal({ tipo: 'actualizar', clave: 'kn3', cambios: { relacion: { historicoId: 31 }, tipoRegistro: 'DESCANSO' } }),
    )
    const s = aSolicitudReactivar(e)
    expect(s.fecha).toBe(HOY)
    expect(s.fechaFin).toBe(FIN)
    expect(s.principales).toEqual([
      { clave: 'pn1', id: null, codigoEkon: 'DEV007', jornada: 'TIPO_2', fechaInicio: HOY, fechaFin: FIN, cargo: 'PUESTO 7' },
    ])
    expect(s.backs).toEqual([
      {
        clave: 'kn2', id: null, codigoEkon: 'DEV003', tipoRegistro: 'JORNADA', fechaInicio: HOY, fechaFin: FIN, diasDescanso: 0,
        principalClave: 'pn1', principalId: null, observacion: 'Cubre',
      },
      {
        clave: 'kn3', id: null, codigoEkon: 'DEV004', tipoRegistro: 'DESCANSO', fechaInicio: HOY, fechaFin: FIN, diasDescanso: 0,
        principalClave: null, principalId: 31, observacion: null,
      },
    ])
    expect('versionProyecto' in s).toBe(false)
    expect(clavesDelEnvioReactivacion(e)).toEqual({ principales: ['pn1'], backs: ['kn2', 'kn3'] })
  })

  it('opciones de relación: principales nuevos y principales históricos', () => {
    expect(opcionesRelacion(inicial().personal).map((o) => o.valor)).toEqual(['c:pn1', 'h:31'])
  })

  it('el registro lleva el token base, aunque la vista previa traiga otra versión', () => {
    expect(aSolicitudRegistroReactivacion(inicial()).versionProyecto).toBe(2)
  })
})

function previa(cambios: Partial<PrevisualizacionReactivacion> = {}): PrevisualizacionReactivacion {
  return {
    corte: HOY,
    fechaFinActual: '2026-10-03',
    fechaFinNueva: FIN,
    actividad: null,
    personal: [],
    tramos: [],
    cruces: [],
    resumen: [],
    advertencias: [],
    versionProyecto: 2,
    ...cambios,
  }
}

describe('vista previa y registro (P8: sin casilla)', () => {
  const cruce = {
    origen: 'EXTERNO', empleadoId: 5, codigoEkon: 'DEV005', nombreEmpleado: 'E5', fecha: HOY, rol: 'PRINCIPAL',
    proyecto: 'OTRO', proyectoCodigo: 'PRY-OTRO', estadoProyecto: 'ACTIVO',
  }

  it('puedeRegistrar y su motivo', () => {
    const e = inicial()
    const vista = (datos = previa()): VistaReactivacion => ({ revision: revisionReactivacion(e), datos })
    expect(puedeRegistrarReactivacion(null, e, false)).toBe(false)
    expect(motivoSinRegistroReactivacion(null, e, false)).toBe('Para registrar, primero genere la vista previa.')
    expect(puedeRegistrarReactivacion(vista(), e, false)).toBe(true)
    expect(motivoSinRegistroReactivacion(vista(), e, false)).toBe('')
    expect(puedeRegistrarReactivacion(vista(), e, true)).toBe(false)
    const cambiado = aplicar(e, agregar('BACK', 3))
    expect(motivoSinRegistroReactivacion(vista(), cambiado, false)).toBe('La vista previa está desactualizada.')
    expect(puedeRegistrarReactivacion(vista(previa({ cruces: [cruce] })), e, false)).toBe(false)
    expect(motivoSinRegistroReactivacion(vista(previa({ cruces: [cruce] })), e, false)).toBe(
      'No se puede registrar con cruces de asignación.',
    )
  })

  it('TAREA-19b2: vista previa con otra versión que la base → no se registra, con el motivo', () => {
    const e = inicial()
    const vista: VistaReactivacion = { revision: revisionReactivacion(e), datos: previa({ versionProyecto: 3 }) }
    expect(puedeRegistrarReactivacion(vista, e, false)).toBe(false)
    expect(motivoSinRegistroReactivacion(vista, e, false)).toBe(
      'El proyecto cambió desde que abriste esta pantalla. Recarga los datos para continuar.',
    )
    expect(aSolicitudRegistroReactivacion(e).versionProyecto).toBe(2)
  })

  it('mensaje de éxito (P7)', () => {
    expect(mensajeExitoReactivacion(5)).toBe('Proyecto reactivado (versión 5).')
  })
})

describe('interpretarErrorReactivacion', () => {
  const claves = { principales: ['pn1', 'pn2'], backs: ['kn3'] }

  it('400: fecha y fechaFin a sus campos; índices del envío → fila; secciones; personal arriba', () => {
    const r = interpretarErrorReactivacion(
      {
        tipo: 'validacion',
        titulo: 'Los datos de la reactivación no son válidos.',
        errores: {
          fecha: ['La fecha de reactivación debe ser posterior a la fecha fin actual del proyecto (03/10/2026).'],
          fechaFin: ['La fecha fin es obligatoria.'],
          'principales[1].fechaFin': ['Fuera de rango.'],
          'backs[0].principalId': ['El principal 99 no es un principal histórico de este proyecto.'],
          backs: ['Al menos un back debe empezar en la fecha de reactivación (07/10/2026).'],
          personal: ['Se requiere al menos 1 persona (principal o back).'],
        },
      },
      claves,
    )
    expect(r.campos).toEqual({
      fecha: 'La fecha de reactivación debe ser posterior a la fecha fin actual del proyecto (03/10/2026).',
      fechaFin: 'La fecha fin es obligatoria.',
    })
    expect(r.servidor.filas).toEqual({
      pn2: { fechaFin: 'Fuera de rango.' },
      kn3: { principalId: 'El principal 99 no es un principal histórico de este proyecto.' },
    })
    expect(r.servidor.secciones).toEqual({ backs: 'Al menos un back debe empezar en la fecha de reactivación (07/10/2026).' })
    expect(r.servidor.generales).toEqual(['Se requiere al menos 1 persona (principal o back).'])
    expect(r.servidor.cabecera).toEqual({})
    expect(r.dialogo.ofrecerRecarga).toBe(false)
  })

  it('400 proyecto o versionProyecto: arriba y con "Recargar"', () => {
    const r = interpretarErrorReactivacion(
      {
        tipo: 'validacion',
        titulo: 'Los datos de la reactivación no son válidos.',
        errores: { proyecto: ['Solo se puede reactivar un proyecto SUSPENDIDO (estado actual: ACTIVO).'] },
      },
      claves,
    )
    expect(r.servidor.generales).toEqual(['Solo se puede reactivar un proyecto SUSPENDIDO (estado actual: ACTIVO).'])
    expect(r.dialogo.ofrecerRecarga).toBe(true)
    expect(r.campos).toEqual({})
  })

  it('409 con cruces, 409 sin cruces, 503 y 404', () => {
    const conCruces = interpretarErrorReactivacion(
      { tipo: 'conflicto', titulo: 'El proyecto tiene cruces de asignación.', extensiones: { cruces: [], resumen: [] } },
      claves,
    )
    expect(conCruces.cruces).toEqual({ cruces: [], resumen: [] })
    const cambiado = interpretarErrorReactivacion({ tipo: 'conflicto', titulo: 'El proyecto cambió; vuelve a cargarlo.' }, claves)
    expect(cambiado.cruces).toBeNull()
    expect(cambiado.dialogo).toMatchObject({ generales: ['El proyecto cambió; vuelve a cargarlo.'], ofrecerRecarga: true })
    expect(interpretarErrorReactivacion({ tipo: 'noDisponible', titulo: 'Ocupado' }, claves).dialogo.ofrecerReintento).toBe(true)
    expect(interpretarErrorReactivacion({ tipo: 'noEncontrado', titulo: 'No' }, claves).dialogo.noEncontrado).toBe(true)
  })
})

describe('diálogo de cambio de estado (P1)', () => {
  it('"Reactivar" (ACTIVO) lleva a la pantalla de reactivación; los demás destinos no', () => {
    expect(opcionesDestino('SUSPENDIDO').filter((o) => esReactivacion(o.codigo)).map((o) => o.etiqueta)).toEqual(['Reactivar'])
    expect(esReactivacion('TERMINADO')).toBe(false)
    expect(esReactivacion('SUSPENDIDO')).toBe(false)
  })
})

describe('TAREA-26d-2: el propuesto (R7) es una persona nueva', () => {
  it('viaja por codigoEkon, sin empleadoId', () => {
    const [propuesto] = aSolicitudReactivar(inicial()).principales!
    expect(propuesto).toMatchObject({ id: null, codigoEkon: 'DEV007' })
    expect('empleadoId' in propuesto).toBe(false)
  })
})
