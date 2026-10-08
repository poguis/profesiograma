// Pantalla "Reactivar proyecto" (TAREA-19c, contrato TAREA-17b/19x/19y). Lógica pura, sin React: se prueba con Vitest.
// Se monta sobre el estado y el reducer de "Actualizar personal" (TAREA-19b): todo el personal guardado es HISTÓRICO
// (solo lectura) y todas las personas del cuerpo son NUEVAS (sin id). Diferencias: el corte es la fecha de reactivación
// R, el rango del proyecto termina en la fecha fin nueva, el primer principal nuevo empieza en R y es el inicial (R6)
// y el mínimo de personal se cuenta solo sobre las personas nuevas (R5). El servidor decide; aquí solo ayudas.
// Ver docs/fases/FASE_5_Estados_Proyecto.md §8–§10.
import { formatearFecha } from '../../utils/formato'
import {
  type AccionEdicionPersonal,
  type ErrorRespuestaPersonal,
  type ErroresPersonal,
  type EstadoEdicionPersonal,
  type FilaEdicion,
  ROL_PRINCIPAL,
  SIN_ERRORES_PERSONAL,
  aSolicitudPersonal,
  backsEnviados,
  clavesDelEnvioEdicion,
  crearEstadoEdicionPersonal,
  interpretarErrorPersonal,
  principalesEnviados,
  reducerEdicionPersonal,
} from './edicionPersonal'
import type { ClavesEnvio, ErroresFila } from './formularioProyecto'
import { ayudaMinimo } from './minimoPersonal'
import { MENSAJE_CAMBIO_POR_OTRO, cambioPorOtro, conToken } from './tokenConcurrencia'
import type {
  EtapaProyecto,
  PersonaEdicion,
  PersonaReactivacion,
  PrevisualizacionReactivacion,
  Reactivacion,
  SolicitudReactivar,
} from './tipos'

const MOVIMIENTO_SUSPENSION = 'SUSPENSION'

/** Mismo texto que la advertencia R7 del servidor (ReactivacionServicio.AdvertenciaInactivo). */
export const AVISO_PROPUESTO_INACTIVO = 'El empleado del principal propuesto no está activo; elige otro principal.'

/** Texto del inicio bloqueado del primer principal nuevo (P4). */
export const MOTIVO_INICIO_EN_R = 'Empieza en la fecha de reactivación.'

export interface EstadoReactivacion {
  /** Aumenta al cambiar R o la fecha fin nueva (la del personal la lleva `personal.revision`). */
  revisionFechas: number
  /** Cambios de fechas desde que se cargó (los del personal están en `personal.editado`). */
  fechasEditadas: boolean
  /** Token BASE (TAREA-19b2): `versionProyecto` del GET …/reactivacion con que se armó el formulario. */
  versionBase: number
  /** Fecha de reactivación R ("yyyy-MM-dd"). */
  fecha: string | null
  /** Nueva fecha fin del proyecto. */
  fechaFin: string | null
  /** FechaFin actual + 1 (mínimo de R). */
  fechaMinima: string
  fechaFinActual: string
  fechaInicioProyecto: string
  /** Id de los principales históricos con EsPrincipalInicial (marca en la tabla de históricos). */
  inicialesHistoricas: ReadonlySet<number>
  /** P5: clave de la fila del propuesto con empleado inactivo, hasta que se cambie el empleado o se quite la fila. */
  claveInactivo: string | null
  /** Personal: históricas (todo el personal guardado) y nuevas. corte = R; fechaFinProyecto = fecha fin nueva. */
  personal: EstadoEdicionPersonal
}

export type AccionReactivacion =
  | { tipo: 'fecha'; fecha: string | null }
  | { tipo: 'fechaFin'; fechaFin: string | null }
  | { tipo: 'personal'; accion: AccionEdicionPersonal }

// ------------------------------------------------------------------ valores iniciales (P2, P3)

/** P2: R inicial = max(fechaMinima, hoy en Ecuador). */
export function fechaReactivacionInicial(fechaMinima: string, hoy: string): string {
  return hoy > fechaMinima ? hoy : fechaMinima
}

/**
 * P3: fecha fin que tenía el proyecto antes de la última SUSPENSION (la etapa anterior a ella en el historial).
 * null si no hay suspensión o etapa anterior.
 */
export function finAntesDeSuspension(etapas: readonly EtapaProyecto[]): string | null {
  const ordenadas = [...etapas].sort((a, b) => a.version - b.version)
  let indice = -1
  ordenadas.forEach((e, i) => {
    if (e.tipoMovimiento === MOVIMIENTO_SUSPENSION) {
      indice = i
    }
  })
  return indice > 0 ? ordenadas[indice - 1].fechaFin : null
}

/** P3: la fecha fin planificada solo se propone si es ≥ R; si no, vacía. */
export function fechaFinInicial(finPlanificado: string | null, fecha: string): string | null {
  return finPlanificado !== null && finPlanificado >= fecha ? finPlanificado : null
}

// ------------------------------------------------------------------ estado inicial

/** Persona guardada → histórica del estado de personal (el GET no trae relación, cargo ni observación: pendiente 37). */
function aHistorica(p: PersonaReactivacion): PersonaEdicion {
  return {
    id: p.id,
    rol: p.rol,
    numero: p.numero,
    empleado: p.empleado,
    clase: 'HISTORICO',
    jornada: p.jornada,
    fechaInicio: p.fechaInicio,
    fechaFin: p.fechaFin,
    tipoRegistro: p.tipoRegistro,
    diasDescanso: p.diasDescanso,
    principalRelacionadoId: null,
    cargo: null,
    observacion: null,
    permisos: { fechaInicio: false, fechaFinMinima: null, jornada: false, eliminable: false },
  }
}

/**
 * Estado a partir del GET …/reactivacion. `hoy` = hoy en Ecuador; `finPlanificado` = finAntesDeSuspension(etapas)
 * del detalle (null si no cargó). El principal propuesto (R7) se precarga como primer principal nuevo, sin marcar el
 * formulario como editado.
 */
export function crearEstadoReactivacion(dto: Reactivacion, hoy: string, finPlanificado: string | null): EstadoReactivacion {
  const fecha = fechaReactivacionInicial(dto.fechaMinima, hoy)
  const fechaFin = fechaFinInicial(finPlanificado, fecha)
  const iniciales = new Set(dto.personal.filter((p) => p.esPrincipalInicial).map((p) => p.id))
  let personal = crearEstadoEdicionPersonal(
    {
      id: dto.id,
      codigo: dto.codigo,
      estado: dto.estadoActual,
      fechaInicio: dto.fechaInicio,
      fechaFin: fechaFin ?? fecha,
      corte: fecha,
      puedeEditar: dto.puedeReactivar,
      motivo: dto.motivo,
      personal: dto.personal.map(aHistorica),
      limites: dto.limites,
      versionProyecto: dto.versionProyecto,
    },
    iniciales,
  )

  let claveInactivo: string | null = null
  const propuesto = dto.principalPropuesto
  if (propuesto) {
    const clave = `pn${personal.siguienteClave}`
    const fila: FilaEdicion = {
      clave,
      id: null,
      rol: ROL_PRINCIPAL,
      numero: null,
      // TAREA-26d-2: persona nueva → viaja por codigoEkon (aSolicitudPersonal).
      empleado: {
        codigoEkon: propuesto.empleado.codigoEkon,
        nombreCompleto: propuesto.empleado.nombreCompleto,
        cargo: null,
      },
      jornada: propuesto.jornada,
      fechaInicio: fecha,
      fechaFin,
      cargo: propuesto.cargo ?? '',
      tipoRegistro: 'JORNADA',
      diasDescanso: 0,
      relacion: null,
      observacion: '',
      permisos: null,
      original: null,
      esPrincipalInicial: false,
      eliminada: false,
      avisoRelacion: false,
    }
    personal = { ...personal, siguienteClave: personal.siguienteClave + 1, principales: [fila] }
    claveInactivo = propuesto.empleado.activo ? null : clave
  }

  return {
    revisionFechas: 0,
    fechasEditadas: false,
    versionBase: dto.versionProyecto,
    fecha,
    fechaFin,
    fechaMinima: dto.fechaMinima,
    fechaFinActual: dto.fechaFinActual,
    fechaInicioProyecto: dto.fechaInicio,
    inicialesHistoricas: iniciales,
    claveInactivo,
    personal,
  }
}

// ------------------------------------------------------------------ reducer

function mapearFilas(personal: EstadoEdicionPersonal, f: (fila: FilaEdicion) => FilaEdicion): EstadoEdicionPersonal {
  return { ...personal, principales: personal.principales.map(f), backs: personal.backs.map(f) }
}

/** R6 / P4: el primer principal nuevo empieza en R (también después de ↑↓). */
function anclar(personal: EstadoEdicionPersonal, fecha: string | null): EstadoEdicionPersonal {
  const primera = principalesEnviados(personal)[0]
  if (!primera || primera.fechaInicio === fecha) {
    return personal
  }
  return {
    ...personal,
    principales: personal.principales.map((p) => (p.clave === primera.clave ? { ...p, fechaInicio: fecha } : p)),
  }
}

/** Corte y rango del estado de personal según R y la fecha fin nueva. */
function conLimites(personal: EstadoEdicionPersonal, fecha: string | null, fechaFin: string | null, fechaMinima: string) {
  const corte = fecha ?? fechaMinima
  return { ...personal, corte, fechaFinProyecto: fechaFin ?? corte }
}

export function reducerReactivacion(estado: EstadoReactivacion, accion: AccionReactivacion): EstadoReactivacion {
  switch (accion.tipo) {
    case 'fecha': {
      if (accion.fecha === estado.fecha) {
        return estado
      }
      // P4: las personas nuevas que empezaban en la R anterior siguen a la R nueva.
      const anterior = estado.fecha
      const seguidas = mapearFilas(estado.personal, (f) => (f.fechaInicio === anterior ? { ...f, fechaInicio: accion.fecha } : f))
      return {
        ...estado,
        revisionFechas: estado.revisionFechas + 1,
        fechasEditadas: true,
        fecha: accion.fecha,
        personal: anclar(conLimites(seguidas, accion.fecha, estado.fechaFin, estado.fechaMinima), accion.fecha),
      }
    }

    case 'fechaFin': {
      if (accion.fechaFin === estado.fechaFin) {
        return estado
      }
      // P4: las que tenían el fin anterior (o vacío) siguen al fin nuevo.
      const anterior = estado.fechaFin
      const seguidas = mapearFilas(estado.personal, (f) =>
        f.fechaFin === anterior || f.fechaFin === null ? { ...f, fechaFin: accion.fechaFin } : f,
      )
      return {
        ...estado,
        revisionFechas: estado.revisionFechas + 1,
        fechasEditadas: true,
        fechaFin: accion.fechaFin,
        personal: conLimites(seguidas, estado.fecha, accion.fechaFin, estado.fechaMinima),
      }
    }

    case 'personal': {
      let interna = accion.accion
      // P4: el inicio del primer principal nuevo no se cambia a mano (queda en R).
      if (interna.tipo === 'actualizar' && interna.clave === claveInicialReactivacion(estado) && 'fechaInicio' in interna.cambios) {
        const resto = { ...interna.cambios }
        delete resto.fechaInicio
        if (Object.keys(resto).length === 0) {
          return estado
        }
        interna = { ...interna, cambios: resto }
      }
      const siguiente = estado.personal.siguienteClave
      let personal = reducerEdicionPersonal(estado.personal, interna)
      if (personal === estado.personal) {
        return estado
      }
      if (interna.tipo === 'agregar' && personal.siguienteClave !== siguiente) {
        // Fechas sugeridas de una nueva: de R a la fecha fin nueva (vacías si aún no se eligieron).
        const clave = `${interna.rol === ROL_PRINCIPAL ? 'pn' : 'kn'}${siguiente}`
        personal = mapearFilas(personal, (f) => (f.clave === clave ? { ...f, fechaInicio: estado.fecha, fechaFin: estado.fechaFin } : f))
      }
      const resuelveInactivo =
        (interna.tipo === 'cambiarEmpleado' || interna.tipo === 'quitar') && interna.clave === estado.claveInactivo
      return {
        ...estado,
        claveInactivo: resuelveInactivo ? null : estado.claveInactivo,
        personal: anclar(personal, estado.fecha),
      }
    }
  }
}

// ------------------------------------------------------------------ consultas sobre el estado

/** Revisión conjunta (fechas + personal): una vista previa solo vale para su revisión. */
export function revisionReactivacion(estado: EstadoReactivacion): number {
  return estado.revisionFechas + estado.personal.revision
}

/** Hubo cambios desde que se cargó (confirmación al salir y al recargar). */
export function reactivacionEditada(estado: EstadoReactivacion): boolean {
  return estado.fechasEditadas || estado.personal.editado
}

/** R6: el primer principal nuevo será el inicial (responsable); solo con backs no hay inicial nuevo. */
export function claveInicialReactivacion(estado: EstadoReactivacion): string | null {
  return principalesEnviados(estado.personal)[0]?.clave ?? null
}

/** R5 (TAREA-19y): el mínimo se cuenta solo sobre las personas nuevas (todo lo guardado queda histórico). */
function conteoNuevas(estado: EstadoReactivacion): { principales: number; backs: number } {
  return { principales: principalesEnviados(estado.personal).length, backs: backsEnviados(estado.personal).length }
}

/**
 * Ayuda neutra que bloquea "Generar vista previa" (undefined = se puede generar): fechas, mínimo de personal según
 * PROYECTO_EXIGE_PRINCIPAL y propuesto inactivo (P5).
 */
export function ayudaReactivacion(estado: EstadoReactivacion, exigePrincipal: boolean): string | undefined {
  if (estado.fecha === null) {
    return 'Indique la fecha de reactivación para generar la vista previa.'
  }
  if (estado.fechaFin === null) {
    return 'Indique la nueva fecha fin del proyecto para generar la vista previa.'
  }
  const c = conteoNuevas(estado)
  const minimo = ayudaMinimo(c.principales, c.backs, exigePrincipal)
  if (minimo) {
    return minimo
  }
  if (estado.claveInactivo !== null) {
    return 'Cambie el empleado del principal propuesto (no está activo) o quite la fila para generar la vista previa.'
  }
  return undefined
}

export function puedeGenerarVistaPreviaReactivacion(estado: EstadoReactivacion, enviando: boolean, exigePrincipal: boolean): boolean {
  return !enviando && ayudaReactivacion(estado, exigePrincipal) === undefined
}

/** R6 solo con backs (P6: ayuda que no bloquea; el servidor responde 400 `backs`). */
export function ayudaBacksEnR(estado: EstadoReactivacion): string | undefined {
  const backs = backsEnviados(estado.personal)
  if (estado.fecha === null || principalesEnviados(estado.personal).length > 0 || backs.length === 0) {
    return undefined
  }
  return backs.some((b) => b.fechaInicio === estado.fecha)
    ? undefined
    : `Al menos un back debe empezar en la fecha de reactivación (${formatearFecha(estado.fecha)}).`
}

/** Avisos por fila (P5: propuesto inactivo). */
export function avisosFilaReactivacion(estado: EstadoReactivacion): Record<string, string[]> {
  return estado.claveInactivo ? { [estado.claveInactivo]: [AVISO_PROPUESTO_INACTIVO] } : {}
}

// ------------------------------------------------------------------ ayudas del cliente (no bloquean)

export interface ErroresCabeceraReactivacion {
  fecha?: string
  fechaFin?: string
}

/** R2: R > fecha fin actual; fecha fin nueva ≥ R (textos del servidor). */
export function validarFechasReactivacion(estado: EstadoReactivacion): ErroresCabeceraReactivacion {
  const errores: ErroresCabeceraReactivacion = {}
  if (estado.fecha !== null && estado.fecha <= estado.fechaFinActual) {
    errores.fecha = `La fecha de reactivación debe ser posterior a la fecha fin actual del proyecto (${formatearFecha(estado.fechaFinActual)}).`
  }
  if (estado.fecha !== null && estado.fechaFin !== null && estado.fechaFin < estado.fecha) {
    errores.fechaFin = `La fecha fin no puede ser anterior a la fecha de reactivación (${formatearFecha(estado.fecha)}).`
  }
  return errores
}

/** Ayudas por fila (clave → campo → mensaje): obligatorios, inicio ≥ R y RN08 sobre (inicio, fecha fin nueva). */
export function validarPersonalReactivacion(estado: EstadoReactivacion): Record<string, ErroresFila> {
  const errores: Record<string, ErroresFila> = {}
  const { fecha, fechaFin } = estado
  for (const f of [...principalesEnviados(estado.personal), ...backsEnviados(estado.personal)]) {
    const fila: ErroresFila = {}
    if (f.fechaInicio === null) {
      fila.fechaInicio = 'La fecha de inicio es obligatoria.'
    } else if (fecha !== null && f.fechaInicio < fecha) {
      fila.fechaInicio = `La fecha de inicio no puede ser anterior a la fecha de reactivación (${formatearFecha(fecha)}).`
    }
    if (f.fechaFin === null) {
      fila.fechaFin = 'La fecha fin es obligatoria.'
    }
    if (f.fechaInicio !== null && f.fechaFin !== null) {
      if (f.fechaFin < f.fechaInicio) {
        fila.fechaFin = 'La fecha fin debe ser mayor o igual a la fecha de inicio.'
      } else if (fechaFin !== null && (f.fechaInicio < estado.fechaInicioProyecto || f.fechaFin > fechaFin)) {
        fila.fechaFin = `Las fechas deben estar dentro del rango del proyecto (${formatearFecha(estado.fechaInicioProyecto)} – ${formatearFecha(fechaFin)}).`
      }
    }
    if (f.rol === ROL_PRINCIPAL && !f.jornada) {
      fila.jornada = 'La jornada es obligatoria.'
    }
    if (Object.keys(fila).length > 0) {
      errores[f.clave] = fila
    }
  }
  return errores
}

// ------------------------------------------------------------------ solicitud

/** Cuerpo de la vista previa: R, fecha fin nueva y las personas nuevas (sin id; nunca las históricas). */
export function aSolicitudReactivar(estado: EstadoReactivacion): SolicitudReactivar {
  return { fecha: estado.fecha, fechaFin: estado.fechaFin, ...aSolicitudPersonal(estado.personal) }
}

/**
 * Cuerpo del registro con el token BASE (TAREA-19b2): la versión del GET …/reactivacion con que se armó el formulario,
 * nunca la de la vista previa. Si el proyecto cambió desde entonces, el servidor responde 409 ("Recargar").
 */
export function aSolicitudRegistroReactivacion(estado: EstadoReactivacion): SolicitudReactivar {
  return conToken(aSolicitudReactivar(estado), estado.versionBase)
}

/** Claves de las filas en el orden del envío: el índice de un error 400 apunta a ESA fila. */
export function clavesDelEnvioReactivacion(estado: EstadoReactivacion): ClavesEnvio {
  return clavesDelEnvioEdicion(estado.personal)
}

// ------------------------------------------------------------------ vista previa y registro

export interface VistaReactivacion {
  revision: number
  datos: PrevisualizacionReactivacion
}

export function vistaReactivacionVigente(vista: VistaReactivacion | null, estado: EstadoReactivacion): boolean {
  return vista !== null && vista.revision === revisionReactivacion(estado)
}

/** P8 (sin casilla): vista previa vigente, sin cruces, sin cambio por otro y sin envío en curso. */
export function puedeRegistrarReactivacion(vista: VistaReactivacion | null, estado: EstadoReactivacion, enviando: boolean): boolean {
  if (enviando || !vistaReactivacionVigente(vista, estado)) {
    return false
  }
  return vista!.datos.cruces.length === 0 && !cambioPorOtro(vista, estado.versionBase)
}

/** Por qué "Registrar" está deshabilitado (texto junto al botón); "" si no hay motivo que mostrar. */
export function motivoSinRegistroReactivacion(vista: VistaReactivacion | null, estado: EstadoReactivacion, enviando: boolean): string {
  if (enviando) {
    return ''
  }
  if (vista === null) {
    return 'Para registrar, primero genere la vista previa.'
  }
  if (!vistaReactivacionVigente(vista, estado)) {
    return 'La vista previa está desactualizada.'
  }
  if (cambioPorOtro(vista, estado.versionBase)) {
    return MENSAJE_CAMBIO_POR_OTRO
  }
  if (vista.datos.cruces.length > 0) {
    return 'No se puede registrar con cruces de asignación.'
  }
  return ''
}

/** P7. */
export function mensajeExitoReactivacion(version: number): string {
  return `Proyecto reactivado (versión ${version}).`
}

// ------------------------------------------------------------------ errores

export interface ErroresReactivacion extends ErroresPersonal {
  /** 400 `fecha` / `fechaFin`: se muestran en su campo. */
  campos: ErroresCabeceraReactivacion
}

export const SIN_ERRORES_REACTIVACION: ErroresReactivacion = { ...SIN_ERRORES_PERSONAL, campos: {} }

/**
 * 400: `fecha` y `fechaFin` a sus campos (se separan antes, porque `distribuirErrores` los trataría como cabecera de
 * la creación o generales); el resto como en "Actualizar personal": `principales[i].x` / `backs[i].x` a la fila del
 * envío, `principales` / `backs` a su sección, `personal` y demás arriba; `versionProyecto` y `proyecto` ofrecen
 * "Recargar". 409 con cruces, 409 sin cruces ("Recargar"), 503/red ("Reintentar") y 404 igual que en personal.
 */
export function interpretarErrorReactivacion(error: ErrorRespuestaPersonal, claves: ClavesEnvio): ErroresReactivacion {
  if (error.tipo === 'validacion' && error.errores) {
    const { fecha, fechaFin, ...resto } = error.errores
    const campos: ErroresCabeceraReactivacion = {}
    if (fecha) {
      campos.fecha = fecha.join(' ')
    }
    if (fechaFin) {
      campos.fechaFin = fechaFin.join(' ')
    }
    return { ...interpretarErrorPersonal({ ...error, errores: resto }, claves), campos }
  }
  return { ...interpretarErrorPersonal(error, claves), campos: {} }
}
