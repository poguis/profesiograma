import { describe, expect, it } from 'vitest'
import {
  type AccionEdicionPersonal,
  type EstadoEdicionPersonal,
  type VistaPersonal,
  aSolicitudPersonal,
  aSolicitudRegistroPersonal,
  ayudaPersonalEdicion,
  claveSeraInicial,
  clavesDelEnvioEdicion,
  crearEstadoEdicionPersonal,
  etiquetaFila,
  interpretarErrorPersonal,
  mensajeExitoPersonal,
  motivoSinRegistroPersonal,
  opcionesRelacion,
  puedeGenerarVistaPreviaEdicion,
  puedeRegistrarPersonal,
  reducerEdicionPersonal,
  relacionDeValor,
  requiereConfirmacionPersonal,
  sinCambiosPersonal,
  validarEdicionPersonal,
  valorRelacion,
} from './edicionPersonal'
import type { EdicionPersonal, PersonaEdicion, PrevisualizacionPersonal } from './tipos'

// Datos ficticios con la forma del EdicionPersonalDto (como el Id 10: históricos y vigentes). Corte 06/10/2026.
const historico = { fechaInicio: false, fechaFinMinima: null, jornada: false, eliminable: false }
const iniciada = { fechaInicio: false, fechaFinMinima: '2026-10-05', jornada: true, eliminable: false }
const futura = { fechaInicio: true, fechaFinMinima: '2026-10-05', jornada: true, eliminable: true }

const persona = (id: number, rol: string, numero: number, empleado: number, cambios: Partial<PersonaEdicion>): PersonaEdicion => ({
  id,
  rol,
  numero,
  empleado: { id: empleado, codigoEkon: `DEV00${empleado}`, nombreCompleto: `EMPLEADO PRUEBA 0${empleado}` },
  clase: 'VIGENTE',
  jornada: rol === 'PRINCIPAL' ? 'TIPO_2' : null,
  fechaInicio: '2026-10-04',
  fechaFin: '2026-11-29',
  tipoRegistro: 'JORNADA',
  diasDescanso: rol === 'PRINCIPAL' ? 4 : 2,
  principalRelacionadoId: null,
  cargo: null,
  observacion: null,
  permisos: iniciada,
  ...cambios,
})

const P1H = persona(31, 'PRINCIPAL', 1, 7, { clase: 'HISTORICO', fechaInicio: '2026-10-02', fechaFin: '2026-10-03', permisos: historico })
const K1H = persona(32, 'BACK', 1, 1, { clase: 'HISTORICO', fechaInicio: '2026-10-03', fechaFin: '2026-10-03', principalRelacionadoId: 31, permisos: historico })
const P2 = persona(33, 'PRINCIPAL', 2, 7, { cargo: 'PUESTO 7' })
const K2 = persona(34, 'BACK', 2, 3, { fechaInicio: '2026-10-20', fechaFin: '2026-10-23', principalRelacionadoId: 33, permisos: futura, observacion: 'Cubre a P2' })
const K3 = persona(35, 'BACK', 3, 4, { fechaInicio: '2026-10-25', fechaFin: '2026-10-26', principalRelacionadoId: 31, permisos: futura })

const dto = (personal: PersonaEdicion[] = [P1H, K1H, P2, K2, K3]): EdicionPersonal => ({
  id: 10,
  codigo: 'PRY-PRUEBA',
  estado: 'ACTIVO',
  fechaInicio: '2026-10-02',
  fechaFin: '2026-11-29',
  corte: '2026-10-06',
  puedeEditar: true,
  motivo: null,
  personal,
  limites: { maxPrincipales: 20, maxBacks: 20, backMaxDiasDescanso: 20, exigePrincipal: false },
  versionProyecto: 3,
})

const empleado = (id: number) => ({ id, codigoEkon: `DEV00${id}`, nombreCompleto: `EMPLEADO PRUEBA 0${id}`, cargo: null })

function aplicar(estado: EstadoEdicionPersonal, ...acciones: AccionEdicionPersonal[]) {
  return acciones.reduce(reducerEdicionPersonal, estado)
}

const inicial = (iniciales: number[] = [31]) => crearEstadoEdicionPersonal(dto(), new Set(iniciales))

const vistaDe = (revision: number, cambios: Partial<PrevisualizacionPersonal> = {}): VistaPersonal => ({
  revision,
  datos: {
    corte: '2026-10-06',
    personal: [{ clave: 'p33', id: 33, rol: 'PRINCIPAL', numero: 2, empleado: empleado(7), clase: 'VIGENTE', accion: 'MODIFICADO' }],
    tramos: [],
    cruces: [],
    resumen: [],
    advertencias: [],
    versionProyecto: 3,
    ...cambios,
  },
})

describe('estado inicial', () => {
  it('separa históricas y vigentes, con claves estables y relaciones por clave o por histórico', () => {
    const e = inicial()
    expect(e.historicas.map((h) => h.id)).toEqual([31, 32])
    expect(e.principales.map((p) => p.clave)).toEqual(['p33'])
    expect(e.backs.map((b) => [b.clave, b.relacion])).toEqual([
      ['k34', { clave: 'p33' }],
      ['k35', { historicoId: 31 }],
    ])
    expect(e.historicaInicial).toBe(true)
    expect(e.revision).toBe(0)
    expect(e.editado).toBe(false)
  })

  it('etiquetas: guardadas por número y nuevas por posición', () => {
    const e = aplicar(inicial(), { tipo: 'agregar', rol: 'PRINCIPAL', empleado: empleado(8), maximo: 20 })
    expect(etiquetaFila(e, e.principales[0])).toBe('P2')
    expect(etiquetaFila(e, e.principales[1])).toBe('Nuevo principal 1')
    expect(etiquetaFila(e, e.backs[0])).toBe('Back 2')
  })
})

describe('reducer', () => {
  it('respeta los permisos: el inicio de una vigente que ya empezó no cambia', () => {
    const e = inicial()
    expect(aplicar(e, { tipo: 'actualizar', clave: 'p33', cambios: { fechaInicio: '2026-10-10' } })).toBe(e)
    const fin = aplicar(e, { tipo: 'actualizar', clave: 'p33', cambios: { fechaFin: '2026-11-20' } })
    expect(fin.principales[0].fechaFin).toBe('2026-11-20')
    expect(fin.revision).toBe(1)
    expect(fin.editado).toBe(true)
  })

  it('la misma relación (otro objeto) no cambia la revisión', () => {
    const e = inicial()
    expect(aplicar(e, { tipo: 'actualizar', clave: 'k34', cambios: { relacion: { clave: 'p33' } } })).toBe(e)
  })

  it('DESCANSO pone los días de descanso en 0', () => {
    const e = aplicar(inicial(), { tipo: 'actualizar', clave: 'k34', cambios: { tipoRegistro: 'DESCANSO' } })
    expect(e.backs[0].diasDescanso).toBe(0)
  })

  it('agregar sugiere del corte al fin del proyecto y respeta el máximo', () => {
    const e = aplicar(inicial(), { tipo: 'agregar', rol: 'BACK', empleado: empleado(8), maximo: 20 })
    const nuevo = e.backs.at(-1)!
    expect([nuevo.clave, nuevo.id, nuevo.fechaInicio, nuevo.fechaFin]).toEqual(['kn1', null, '2026-10-06', '2026-11-29'])
    const lleno = inicial()
    expect(aplicar(lleno, { tipo: 'agregar', rol: 'BACK', empleado: empleado(8), maximo: 2 })).toBe(lleno)
  })

  it('cambiar el empleado de una nueva conserva el resto del formulario; en una vigente no aplica', () => {
    const e = aplicar(
      inicial(),
      { tipo: 'agregar', rol: 'BACK', empleado: empleado(8), maximo: 20 },
      { tipo: 'actualizar', clave: 'kn1', cambios: { fechaFin: '2026-10-30', observacion: 'Nota' } },
      { tipo: 'cambiarEmpleado', clave: 'kn1', empleado: empleado(2) },
    )
    const nuevo = e.backs.at(-1)!
    expect([nuevo.empleado.id, nuevo.fechaFin, nuevo.observacion]).toEqual([2, '2026-10-30', 'Nota'])
    expect(aplicar(e, { tipo: 'cambiarEmpleado', clave: 'k34', empleado: empleado(2) })).toBe(e)
  })

  it('quitar: la vigente iniciada no se quita; la futura queda marcada y se restaura; la nueva desaparece', () => {
    const e = inicial()
    expect(aplicar(e, { tipo: 'quitar', clave: 'p33' })).toBe(e)
    const marcada = aplicar(e, { tipo: 'quitar', clave: 'k34' })
    expect(marcada.backs[0].eliminada).toBe(true)
    expect(aplicar(marcada, { tipo: 'restaurar', clave: 'k34' }).backs[0].eliminada).toBe(false)
    const conNuevo = aplicar(e, { tipo: 'agregar', rol: 'BACK', empleado: empleado(8), maximo: 20 })
    expect(aplicar(conNuevo, { tipo: 'quitar', clave: 'kn1' }).backs).toHaveLength(2)
  })

  it('quitar un principal deja sus backs "Sin relación" con aviso', () => {
    const e = aplicar(
      inicial(),
      { tipo: 'agregar', rol: 'PRINCIPAL', empleado: empleado(8), maximo: 20 },
      { tipo: 'actualizar', clave: 'k35', cambios: { relacion: { clave: 'pn1' } } },
      { tipo: 'quitar', clave: 'pn1' },
    )
    const k35 = e.backs.find((b) => b.clave === 'k35')!
    expect([k35.relacion, k35.avisoRelacion]).toEqual([null, true])
  })

  it('moverNuevo solo reordena principales nuevos', () => {
    const e = aplicar(
      inicial(),
      { tipo: 'agregar', rol: 'PRINCIPAL', empleado: empleado(8), maximo: 20 },
      { tipo: 'agregar', rol: 'PRINCIPAL', empleado: empleado(2), maximo: 20 },
    )
    expect(aplicar(e, { tipo: 'moverNuevo', clave: 'pn1', direccion: -1 })).toBe(e) // no pasa delante de la vigente
    expect(aplicar(e, { tipo: 'moverNuevo', clave: 'pn2', direccion: -1 }).principales.map((p) => p.clave)).toEqual(['p33', 'pn2', 'pn1'])
  })

  it('un cambio desmarca la casilla; marcarla no cambia la revisión', () => {
    const marcado = aplicar(inicial(), { tipo: 'entiende', valor: true })
    expect([marcado.entiende, marcado.revision]).toEqual([true, 0])
    expect(aplicar(marcado, { tipo: 'actualizar', clave: 'p33', cambios: { cargo: 'X' } }).entiende).toBe(false)
  })
})

describe('P3: "Será el principal inicial" (se recalcula al reordenar)', () => {
  it('con un inicial histórico, ningún nuevo es inicial', () => {
    const e = aplicar(inicial([31]), { tipo: 'agregar', rol: 'PRINCIPAL', empleado: empleado(8), maximo: 20 })
    expect(claveSeraInicial(e)).toBeNull()
  })

  it('sin inicial que permanezca, el primer principal nuevo; al reordenar cambia', () => {
    const e = aplicar(
      inicial([]),
      { tipo: 'agregar', rol: 'PRINCIPAL', empleado: empleado(8), maximo: 20 },
      { tipo: 'agregar', rol: 'PRINCIPAL', empleado: empleado(2), maximo: 20 },
    )
    expect(claveSeraInicial(e)).toBe('pn1')
    expect(claveSeraInicial(aplicar(e, { tipo: 'moverNuevo', clave: 'pn2', direccion: -1 }))).toBe('pn2')
  })

  it('con el inicial vigente eliminado, el nuevo pasa a ser inicial', () => {
    const futuroInicial = persona(36, 'PRINCIPAL', 3, 5, { fechaInicio: '2026-10-20', permisos: futura })
    const e = aplicar(
      crearEstadoEdicionPersonal(dto([K2, futuroInicial]), new Set([36])),
      { tipo: 'agregar', rol: 'PRINCIPAL', empleado: empleado(8), maximo: 20 },
    )
    expect(claveSeraInicial(e)).toBeNull()
    expect(claveSeraInicial(aplicar(e, { tipo: 'quitar', clave: 'p36' }))).toBe('pn1')
  })
})

describe('mínimo (P1 de la 19y: personal resultante, con históricas)', () => {
  const soloFuturos = () => crearEstadoEdicionPersonal(dto([K2, K3]), new Set())

  it('parámetro en 0: sin nadie bloquea y pide una persona; con históricas no', () => {
    const vacio = aplicar(soloFuturos(), { tipo: 'quitar', clave: 'k34' }, { tipo: 'quitar', clave: 'k35' })
    expect(puedeGenerarVistaPreviaEdicion(vacio, false, false)).toBe(false)
    expect(ayudaPersonalEdicion(vacio, false)).toBe('Agrega al menos 1 persona (principal o back) para generar la vista previa.')
    const conHistoricas = aplicar(inicial(), { tipo: 'quitar', clave: 'k34' }, { tipo: 'quitar', clave: 'k35' })
    expect(puedeGenerarVistaPreviaEdicion(conHistoricas, false, false)).toBe(true)
  })

  it('parámetro en 1: solo backs bloquea con la ayuda de principal', () => {
    expect(puedeGenerarVistaPreviaEdicion(soloFuturos(), false, true)).toBe(false)
    expect(ayudaPersonalEdicion(soloFuturos(), true)).toBe('Agrega al menos 1 principal para generar la vista previa.')
    expect(puedeGenerarVistaPreviaEdicion(inicial(), true, true)).toBe(false) // enviando
  })
})

describe('ayudas del cliente', () => {
  it('fin < inicio, fin antes del mínimo de una vigente, fuera del rango y jornada vacía', () => {
    const e = aplicar(
      inicial(),
      { tipo: 'actualizar', clave: 'p33', cambios: { fechaFin: '2026-10-01', jornada: null } },
      { tipo: 'actualizar', clave: 'k34', cambios: { fechaFin: '2026-12-31' } },
    )
    const errores = validarEdicionPersonal(e)
    expect(errores.p33).toEqual({
      fechaFin: 'La fecha fin no puede ser anterior al 05/10/2026 (día anterior al corte).',
      jornada: 'La jornada es obligatoria.',
    })
    expect(errores.k34?.fechaFin).toBe('Las fechas deben estar dentro del rango del proyecto (02/10/2026 – 29/11/2026).')
    expect(validarEdicionPersonal(inicial())).toEqual({})
  })
})

describe('solicitud', () => {
  it('sin históricas; eliminadas omitidas; principalClave o principalId; cargo de la vigente null si no cambia', () => {
    const e = aplicar(
      inicial(),
      { tipo: 'quitar', clave: 'k34' },
      { tipo: 'agregar', rol: 'BACK', empleado: empleado(8), maximo: 20 },
      { tipo: 'actualizar', clave: 'kn1', cambios: { relacion: { clave: 'p33' }, observacion: '  ' } },
    )
    expect(aSolicitudPersonal(e)).toEqual({
      principales: [
        { clave: 'p33', id: 33, empleadoId: 7, jornada: 'TIPO_2', fechaInicio: '2026-10-04', fechaFin: '2026-11-29', cargo: null },
      ],
      backs: [
        {
          clave: 'k35', id: 35, empleadoId: 4, tipoRegistro: 'JORNADA', fechaInicio: '2026-10-25', fechaFin: '2026-10-26',
          diasDescanso: 2, principalClave: null, principalId: 31, observacion: null,
        },
        {
          clave: 'kn1', id: null, empleadoId: 8, tipoRegistro: 'JORNADA', fechaInicio: '2026-10-06', fechaFin: '2026-11-29',
          diasDescanso: 0, principalClave: 'p33', principalId: null, observacion: null,
        },
      ],
    })
    expect(clavesDelEnvioEdicion(e)).toEqual({ principales: ['p33'], backs: ['k35', 'kn1'] })
  })

  it('cargo cambiado en la vigente se envía', () => {
    const e = aplicar(inicial(), { tipo: 'actualizar', clave: 'p33', cambios: { cargo: 'SUPERVISOR' } })
    expect(aSolicitudPersonal(e).principales![0].cargo).toBe('SUPERVISOR')
  })

  // TAREA-19b2 (ajuste aprobado): el registro lleva el token BASE (GET …/edicion = 3), no el de la vista previa (7).
  it('el registro lleva el token base, aunque la vista previa traiga otra versión', () => {
    const e = aplicar(inicial(), { tipo: 'actualizar', clave: 'p33', cambios: { fechaFin: '2026-11-20' } })
    expect(e.versionBase).toBe(3)
    expect(aSolicitudRegistroPersonal(e).versionProyecto).toBe(3)
    expect(aSolicitudPersonal(e)).not.toHaveProperty('versionProyecto')
  })

  it('TAREA-19b2: vista previa con otra versión (P10 de la 19b) → no se registra, con el motivo', () => {
    const e = aplicar(inicial(), { tipo: 'actualizar', clave: 'p33', cambios: { cargo: 'X' } })
    const deOtro = vistaDe(e.revision, { versionProyecto: 4 })
    expect(puedeRegistrarPersonal(deOtro, e, false)).toBe(false)
    expect(motivoSinRegistroPersonal(deOtro, e, false)).toBe(
      'El proyecto cambió desde que abriste esta pantalla. Recarga los datos para continuar.',
    )
    expect(puedeRegistrarPersonal(vistaDe(e.revision), e, false)).toBe(true)
  })

  it('opciones de relación: principales enviados y principales históricos', () => {
    expect(opcionesRelacion(inicial())).toEqual([
      { valor: 'c:p33', texto: 'P2 · EMPLEADO PRUEBA 07' },
      { valor: 'h:31', texto: 'P1 · EMPLEADO PRUEBA 07 (histórico)' },
    ])
    expect(relacionDeValor(valorRelacion({ historicoId: 31 }))).toEqual({ historicoId: 31 })
    expect(relacionDeValor(valorRelacion({ clave: 'pn1' }))).toEqual({ clave: 'pn1' })
    expect(relacionDeValor('')).toBeNull()
  })
})

describe('vista previa y registro', () => {
  const modificado = () => aplicar(inicial(), { tipo: 'actualizar', clave: 'p33', cambios: { cargo: 'X' } })

  it('sinCambios usa las acciones (pendiente 25, lado personal)', () => {
    const sin = vistaDe(0, { personal: [{ ...vistaDe(0).datos.personal[0], accion: 'SIN_CAMBIO' }] }).datos
    expect(sinCambiosPersonal(sin)).toBe(true)
    expect(sinCambiosPersonal(vistaDe(0).datos)).toBe(false)
  })

  it('requiere confirmación con eliminados o con el fin acortado', () => {
    const e = modificado()
    expect(requiereConfirmacionPersonal(vistaDe(e.revision).datos, e)).toBe(false)
    const eliminado = vistaDe(e.revision, { personal: [{ ...vistaDe(0).datos.personal[0], accion: 'ELIMINADO' }] }).datos
    expect(requiereConfirmacionPersonal(eliminado, e)).toBe(true)
    const acortado = aplicar(inicial(), { tipo: 'actualizar', clave: 'p33', cambios: { fechaFin: '2026-11-20' } })
    expect(requiereConfirmacionPersonal(vistaDe(acortado.revision).datos, acortado)).toBe(true)
  })

  it('puedeRegistrar y su motivo', () => {
    const e = modificado()
    expect(puedeRegistrarPersonal(null, e, false)).toBe(false)
    expect(motivoSinRegistroPersonal(null, e, false)).toBe('Para registrar, primero genere la vista previa.')
    expect(motivoSinRegistroPersonal(vistaDe(0), e, false)).toBe('La vista previa está desactualizada.')
    expect(puedeRegistrarPersonal(vistaDe(e.revision), e, false)).toBe(true)
    expect(puedeRegistrarPersonal(vistaDe(e.revision), e, true)).toBe(false)
    const conCruces = vistaDe(e.revision, {
      cruces: [{ origen: 'EXTERNO', empleadoId: 7, codigoEkon: 'DEV007', nombreEmpleado: 'X', fecha: '2026-10-10', rol: 'PRINCIPAL', proyecto: 'P', proyectoCodigo: 'C', estadoProyecto: 'ACTIVO' }],
    })
    expect(puedeRegistrarPersonal(conCruces, e, false)).toBe(false)
    expect(motivoSinRegistroPersonal(conCruces, e, false)).toBe('No se puede registrar con cruces de asignación.')
    const sin = vistaDe(e.revision, { personal: [{ ...vistaDe(0).datos.personal[0], accion: 'SIN_CAMBIO' }] })
    expect(motivoSinRegistroPersonal(sin, e, false)).toBe('No hay cambios para registrar.')
  })

  it('con eliminados exige la casilla', () => {
    const e = aplicar(inicial(), { tipo: 'quitar', clave: 'k34' })
    const vista = vistaDe(e.revision, { personal: [{ ...vistaDe(0).datos.personal[0], accion: 'ELIMINADO' }] })
    expect(puedeRegistrarPersonal(vista, e, false)).toBe(false)
    expect(motivoSinRegistroPersonal(vista, e, false)).toBe('Marque la casilla de confirmación.')
    expect(puedeRegistrarPersonal(vista, aplicar(e, { tipo: 'entiende', valor: true }), false)).toBe(true)
  })

  it('mensaje de éxito', () => {
    expect(mensajeExitoPersonal(4)).toBe('Personal actualizado (versión 4).')
  })
})

describe('interpretarErrorPersonal', () => {
  const claves = { principales: ['p33', 'pn1'], backs: ['k35'] }

  it('400: índices del envío → fila; secciones; personal/general arriba; versionProyecto ofrece recarga', () => {
    const r = interpretarErrorPersonal(
      {
        tipo: 'validacion',
        titulo: 'Los datos del personal no son válidos.',
        errores: {
          'principales[1].jornada': ['La jornada es obligatoria.'],
          'backs[0].principalId': ['El principal 99 no es un principal histórico de este proyecto.'],
          backs: ['Falta Back 2 (X): una persona que ya empezó no se puede quitar; acorte su fecha fin al 05/10/2026.'],
          personal: ['Se requiere al menos 1 persona (principal o back).'],
          versionProyecto: ['Falta la versión del proyecto; vuelve a cargarlo.'],
        },
      },
      claves,
    )
    expect(r.servidor.filas).toEqual({
      pn1: { jornada: 'La jornada es obligatoria.' },
      k35: { principalId: 'El principal 99 no es un principal histórico de este proyecto.' },
    })
    expect(r.servidor.secciones.backs).toContain('Falta Back 2')
    expect(r.servidor.generales).toEqual([
      'Se requiere al menos 1 persona (principal o back).',
      'Falta la versión del proyecto; vuelve a cargarlo.',
    ])
    expect(r.dialogo.ofrecerRecarga).toBe(true)
  })

  it('400 sin versionProyecto ni proyecto: sin recarga', () => {
    const r = interpretarErrorPersonal({ tipo: 'validacion', titulo: 'x', errores: { general: ['No hay cambios para registrar.'] } }, claves)
    expect(r.servidor.generales).toEqual(['No hay cambios para registrar.'])
    expect(r.dialogo.ofrecerRecarga).toBe(false)
  })

  it('409 con extensions.cruces: muestra los cruces', () => {
    const r = interpretarErrorPersonal(
      { tipo: 'conflicto', titulo: 'El proyecto tiene cruces de asignación.', extensiones: { cruces: [], resumen: [] } },
      claves,
    )
    expect(r.cruces).toEqual({ cruces: [], resumen: [] })
    expect(r.dialogo.ofrecerRecarga).toBe(false)
  })

  it('409 sin cruces: proyecto cambiado y recarga', () => {
    const r = interpretarErrorPersonal({ tipo: 'conflicto', titulo: 'El proyecto cambió; vuelve a cargarlo.', extensiones: {} }, claves)
    expect(r.cruces).toBeNull()
    expect(r.dialogo).toMatchObject({ generales: ['El proyecto cambió; vuelve a cargarlo.'], ofrecerRecarga: true })
  })

  it.each(['noDisponible', 'red'] as const)('%s: reintentar', (tipo) => {
    expect(interpretarErrorPersonal({ tipo, titulo: 'x' }, claves).dialogo.ofrecerReintento).toBe(true)
  })

  it('404: listado', () => {
    expect(interpretarErrorPersonal({ tipo: 'noEncontrado', titulo: 'x' }, claves).dialogo.noEncontrado).toBe(true)
  })
})
