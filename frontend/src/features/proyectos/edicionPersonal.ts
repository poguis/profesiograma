// Pantalla "Actualizar personal" (TAREA-19b, contrato TAREA-17/19x/19y). Lógica pura, sin React: se prueba con Vitest.
// Reglas del servidor (EdicionPersonalValidador): las históricas NO se envían; las vigentes que ya empezaron deben
// enviarse; omitir una vigente que aún no empieza la elimina; la relación del back va con principalClave (principal del
// cuerpo) o principalId (principal histórico); los errores 400 llevan el índice DEL ENVÍO. Ver
// docs/fases/FASE_5_Edicion_Cronograma.md §8–§10.
import type { TipoErrorApi } from '../../api/errores'
import { formatearFecha } from '../../utils/formato'
import { type ErroresDialogo, interpretarErrorDialogo, sinErrores } from './erroresDialogo'
import {
  type ClavesEnvio,
  type EmpleadoFila,
  type ErroresFila,
  type ErroresServidor,
  SIN_ERRORES_SERVIDOR,
  crucesDeConflicto,
  distribuirErrores,
} from './formularioProyecto'
import { ayudaMinimo, cumpleMinimo } from './minimoPersonal'
import type {
  BackEdicionSolicitud,
  CruceAsignacion,
  EdicionPersonal,
  PermisosEdicionPersona,
  PersonaEdicion,
  PrevisualizacionPersonal,
  PrincipalEdicionSolicitud,
  ResumenCruce,
  SolicitudActualizarPersonal,
  TipoRegistroBack,
} from './tipos'

export const ROL_PRINCIPAL = 'PRINCIPAL'
export const ROL_BACK = 'BACK'
const CLASE_HISTORICO = 'HISTORICO'

/** Relación de un back: principal del cuerpo (por clave) o principal histórico (por Id); null = "Sin relación". */
export type RelacionBack = { clave: string } | { historicoId: number } | null

/** Campos que el usuario puede cambiar (según permisos en las vigentes). */
export interface CamposPersona {
  jornada: string | null
  fechaInicio: string | null
  fechaFin: string | null
  cargo: string
  tipoRegistro: TipoRegistroBack
  diasDescanso: number
  relacion: RelacionBack
  observacion: string
}

export interface FilaEdicion extends CamposPersona {
  /** Clave estable: p{id}/k{id} (guardadas) o pn{n}/kn{n} (nuevas). */
  clave: string
  /** null = nueva. */
  id: number | null
  rol: typeof ROL_PRINCIPAL | typeof ROL_BACK
  /** Número guardado (las nuevas lo recibe el servidor: máx + 1). */
  numero: number | null
  empleado: EmpleadoFila
  /** null en las nuevas (todo editable). */
  permisos: PermisosEdicionPersona | null
  /** Valores guardados (null en las nuevas). */
  original: CamposPersona | null
  esPrincipalInicial: boolean
  /** Vigente que aún no empieza marcada para eliminar (P3 de la Fase A: atenuada, con "Restaurar"). */
  eliminada: boolean
  /** Su principal relacionado se quitó: se avisa hasta que el usuario elija otra opción. */
  avisoRelacion: boolean
}

export interface EstadoEdicionPersonal {
  /** Aumenta con cada cambio de datos: una vista previa solo vale para su revisión. */
  revision: number
  /** Hubo cambios desde que se cargó (criterio de la TAREA-13 para confirmar la salida). */
  editado: boolean
  /** Casilla "Entiendo que esta acción no se puede deshacer." (se desmarca con cada cambio). */
  entiende: boolean
  siguienteClave: number
  corte: string
  fechaInicioProyecto: string
  fechaFinProyecto: string
  historicas: PersonaEdicion[]
  /** Hay un principal inicial entre las históricas (P3: entonces ninguna nueva es inicial). */
  historicaInicial: boolean
  /** Vigentes por número y luego las nuevas (en el orden en que se envían). */
  principales: FilaEdicion[]
  backs: FilaEdicion[]
}

export type CambiosPersona = Partial<CamposPersona>

export type AccionEdicionPersonal =
  | { tipo: 'actualizar'; clave: string; cambios: CambiosPersona }
  | { tipo: 'agregar'; rol: FilaEdicion['rol']; empleado: EmpleadoFila; maximo: number }
  | { tipo: 'cambiarEmpleado'; clave: string; empleado: EmpleadoFila }
  | { tipo: 'quitar'; clave: string }
  | { tipo: 'restaurar'; clave: string }
  | { tipo: 'moverNuevo'; clave: string; direccion: -1 | 1 }
  | { tipo: 'entiende'; valor: boolean }

// ------------------------------------------------------------------ estado inicial

function textoCampo(valor: string | null): string {
  return valor ?? ''
}

function aFila(p: PersonaEdicion, principalesVigentes: Set<number>, iniciales: ReadonlySet<number>): FilaEdicion {
  const esPrincipal = p.rol === ROL_PRINCIPAL
  const relacion: RelacionBack =
    p.principalRelacionadoId === null
      ? null
      : principalesVigentes.has(p.principalRelacionadoId)
        ? { clave: `p${p.principalRelacionadoId}` }
        : { historicoId: p.principalRelacionadoId }
  const campos: CamposPersona = {
    jornada: p.jornada,
    fechaInicio: p.fechaInicio,
    fechaFin: p.fechaFin,
    cargo: textoCampo(p.cargo),
    tipoRegistro: p.tipoRegistro === 'DESCANSO' ? 'DESCANSO' : 'JORNADA',
    diasDescanso: p.diasDescanso,
    relacion: esPrincipal ? null : relacion,
    observacion: textoCampo(p.observacion),
  }
  return {
    ...campos,
    clave: `${esPrincipal ? 'p' : 'k'}${p.id}`,
    id: p.id,
    rol: esPrincipal ? ROL_PRINCIPAL : ROL_BACK,
    numero: p.numero,
    empleado: { id: p.empleado.id, codigoEkon: p.empleado.codigoEkon, nombreCompleto: p.empleado.nombreCompleto, cargo: null },
    permisos: p.permisos,
    original: campos,
    esPrincipalInicial: iniciales.has(p.id),
    eliminada: false,
    avisoRelacion: false,
  }
}

/**
 * Estado a partir del GET …/edicion. `iniciales` = Id del personal con EsPrincipalInicial (el GET de edición no lo
 * trae; sale del detalle del proyecto).
 */
export function crearEstadoEdicionPersonal(dto: EdicionPersonal, iniciales: ReadonlySet<number> = new Set()): EstadoEdicionPersonal {
  const historicas = dto.personal.filter((p) => p.clase === CLASE_HISTORICO)
  const vigentes = dto.personal.filter((p) => p.clase !== CLASE_HISTORICO).sort((a, b) => a.numero - b.numero)
  const principalesVigentes = new Set(vigentes.filter((p) => p.rol === ROL_PRINCIPAL).map((p) => p.id))
  const filas = vigentes.map((p) => aFila(p, principalesVigentes, iniciales))
  return {
    revision: 0,
    editado: false,
    entiende: false,
    siguienteClave: 1,
    corte: dto.corte,
    fechaInicioProyecto: dto.fechaInicio,
    fechaFinProyecto: dto.fechaFin,
    historicas,
    historicaInicial: historicas.some((h) => h.rol === ROL_PRINCIPAL && iniciales.has(h.id)),
    principales: filas.filter((f) => f.rol === ROL_PRINCIPAL),
    backs: filas.filter((f) => f.rol === ROL_BACK),
  }
}

// ------------------------------------------------------------------ reducer

/** Quita de los cambios lo que los permisos de una vigente no dejan cambiar (la interfaz ya lo deshabilita). */
function permitidos(fila: FilaEdicion, cambios: CambiosPersona): CambiosPersona {
  if (fila.permisos === null) {
    return cambios
  }
  const resultado = { ...cambios }
  if (!fila.permisos.fechaInicio) {
    delete resultado.fechaInicio
  }
  if (fila.rol === ROL_PRINCIPAL && !fila.permisos.jornada) {
    delete resultado.jornada
  }
  return resultado
}

function mapearFilas(estado: EstadoEdicionPersonal, f: (fila: FilaEdicion) => FilaEdicion): Pick<EstadoEdicionPersonal, 'principales' | 'backs'> {
  return { principales: estado.principales.map(f), backs: estado.backs.map(f) }
}

/** Backs relacionados con un principal que deja de enviarse: "Sin relación" y aviso (R8 de la TAREA-13). */
function soltarBacks(backs: FilaEdicion[], clavePrincipal: string): FilaEdicion[] {
  return backs.map((b) =>
    b.relacion && 'clave' in b.relacion && b.relacion.clave === clavePrincipal ? { ...b, relacion: null, avisoRelacion: true } : b,
  )
}

function revisar(estado: EstadoEdicionPersonal): EstadoEdicionPersonal {
  return { ...estado, revision: estado.revision + 1, editado: true, entiende: false }
}

function buscar(estado: EstadoEdicionPersonal, clave: string): FilaEdicion | undefined {
  return estado.principales.find((p) => p.clave === clave) ?? estado.backs.find((b) => b.clave === clave)
}

export function reducerEdicionPersonal(estado: EstadoEdicionPersonal, accion: AccionEdicionPersonal): EstadoEdicionPersonal {
  switch (accion.tipo) {
    case 'actualizar': {
      const fila = buscar(estado, accion.clave)
      if (!fila || fila.eliminada) {
        return estado
      }
      const cambios = permitidos(fila, accion.cambios)
      const claves = Object.keys(cambios) as (keyof CamposPersona)[]
      const igual = (k: keyof CamposPersona) =>
        k === 'relacion' ? valorRelacion(cambios.relacion ?? null) === valorRelacion(fila.relacion) : cambios[k] === fila[k]
      if (claves.every(igual)) {
        return estado
      }
      const actualizada: FilaEdicion = { ...fila, ...cambios }
      if (actualizada.tipoRegistro === 'DESCANSO') {
        actualizada.diasDescanso = 0 // R8: los días de descanso solo aplican a JORNADA
      }
      if ('relacion' in cambios) {
        actualizada.avisoRelacion = false
      }
      return revisar({ ...estado, ...mapearFilas(estado, (f) => (f.clave === fila.clave ? actualizada : f)) })
    }

    case 'agregar': {
      const lista = accion.rol === ROL_PRINCIPAL ? estado.principales : estado.backs
      if (lista.filter((f) => !f.eliminada).length >= accion.maximo) {
        return estado
      }
      const clave = `${accion.rol === ROL_PRINCIPAL ? 'pn' : 'kn'}${estado.siguienteClave}`
      // Fechas sugeridas: del corte (o del inicio, si el proyecto aún no empieza) al fin del proyecto.
      const inicio = estado.corte > estado.fechaInicioProyecto ? estado.corte : estado.fechaInicioProyecto
      const fila: FilaEdicion = {
        clave,
        id: null,
        rol: accion.rol,
        numero: null,
        empleado: accion.empleado,
        jornada: null,
        fechaInicio: inicio,
        fechaFin: estado.fechaFinProyecto,
        cargo: '',
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
      const siguiente = { ...estado, siguienteClave: estado.siguienteClave + 1 }
      return revisar(
        accion.rol === ROL_PRINCIPAL
          ? { ...siguiente, principales: [...estado.principales, fila] }
          : { ...siguiente, backs: [...estado.backs, fila] },
      )
    }

    case 'cambiarEmpleado': {
      // Solo las nuevas: el resto del formulario se conserva.
      const fila = buscar(estado, accion.clave)
      if (!fila || fila.id !== null || fila.empleado.id === accion.empleado.id) {
        return estado
      }
      return revisar({ ...estado, ...mapearFilas(estado, (f) => (f.clave === fila.clave ? { ...f, empleado: accion.empleado } : f)) })
    }

    case 'quitar': {
      const fila = buscar(estado, accion.clave)
      if (!fila || fila.eliminada) {
        return estado
      }
      if (fila.id !== null && !fila.permisos?.eliminable) {
        return estado // una vigente que ya empezó no se puede quitar (el servidor exige acortar su fin)
      }
      const principales =
        fila.id === null
          ? estado.principales.filter((p) => p.clave !== fila.clave)
          : estado.principales.map((p) => (p.clave === fila.clave ? { ...p, eliminada: true } : p))
      const backs =
        fila.rol === ROL_BACK
          ? fila.id === null
            ? estado.backs.filter((b) => b.clave !== fila.clave)
            : estado.backs.map((b) => (b.clave === fila.clave ? { ...b, eliminada: true } : b))
          : soltarBacks(estado.backs, fila.clave)
      return revisar({ ...estado, principales, backs })
    }

    case 'restaurar': {
      const fila = buscar(estado, accion.clave)
      if (!fila || !fila.eliminada) {
        return estado
      }
      return revisar({ ...estado, ...mapearFilas(estado, (f) => (f.clave === fila.clave ? { ...f, eliminada: false } : f)) })
    }

    case 'moverNuevo': {
      // P2 de la Fase A: solo los principales NUEVOS se reordenan; las vigentes conservan su número.
      const origen = estado.principales.findIndex((p) => p.clave === accion.clave)
      const destino = origen + accion.direccion
      if (origen < 0 || destino < 0 || destino >= estado.principales.length) {
        return estado
      }
      if (estado.principales[origen].id !== null || estado.principales[destino].id !== null) {
        return estado
      }
      const principales = [...estado.principales]
      ;[principales[origen], principales[destino]] = [principales[destino], principales[origen]]
      return revisar({ ...estado, principales })
    }

    case 'entiende':
      return { ...estado, entiende: accion.valor }
  }
}

// ------------------------------------------------------------------ consultas sobre el estado

/** Filas que se envían (vigentes no eliminadas y nuevas), en orden. */
export function principalesEnviados(estado: EstadoEdicionPersonal): FilaEdicion[] {
  return estado.principales.filter((p) => !p.eliminada)
}

export function backsEnviados(estado: EstadoEdicionPersonal): FilaEdicion[] {
  return estado.backs.filter((b) => !b.eliminada)
}

/** Etiqueta de la fila: "P2", "Back 1" (guardadas) o "Nuevo principal 1" / "Nuevo back 1". */
export function etiquetaFila(estado: EstadoEdicionPersonal, fila: FilaEdicion): string {
  if (fila.numero !== null) {
    return fila.rol === ROL_PRINCIPAL ? `P${fila.numero}` : `Back ${fila.numero}`
  }
  const nuevas = (fila.rol === ROL_PRINCIPAL ? estado.principales : estado.backs).filter((f) => f.id === null)
  const posicion = nuevas.findIndex((f) => f.clave === fila.clave) + 1
  return fila.rol === ROL_PRINCIPAL ? `Nuevo principal ${posicion}` : `Nuevo back ${posicion}`
}

/** Etiquetas por empleado para el buscador ("P1", "Back 2", históricos incluidos). */
export function etiquetasPorEmpleadoEdicion(estado: EstadoEdicionPersonal): Map<number, string[]> {
  const etiquetas = new Map<number, string[]>()
  const agregar = (id: number, etiqueta: string) => etiquetas.set(id, [...(etiquetas.get(id) ?? []), etiqueta])
  for (const h of estado.historicas) {
    agregar(h.empleado.id, `${h.rol === ROL_PRINCIPAL ? 'P' : 'Back '}${h.numero} (histórico)`)
  }
  for (const f of [...principalesEnviados(estado), ...backsEnviados(estado)]) {
    agregar(f.empleado.id, etiquetaFila(estado, f))
  }
  return etiquetas
}

/** Opciones de "Principal relacionado": principales que se envían (por clave) y principales históricos (por Id). */
export interface OpcionRelacion {
  valor: string
  texto: string
}

export const SIN_RELACION = ''

export function valorRelacion(relacion: RelacionBack): string {
  if (relacion === null) {
    return SIN_RELACION
  }
  return 'clave' in relacion ? `c:${relacion.clave}` : `h:${relacion.historicoId}`
}

export function relacionDeValor(valor: string | undefined): RelacionBack {
  if (!valor) {
    return null
  }
  return valor.startsWith('h:') ? { historicoId: Number(valor.slice(2)) } : { clave: valor.slice(2) }
}

export function opcionesRelacion(estado: EstadoEdicionPersonal): OpcionRelacion[] {
  return [
    ...principalesEnviados(estado).map((p) => ({
      valor: `c:${p.clave}`,
      texto: `${etiquetaFila(estado, p)} · ${p.empleado.nombreCompleto}`,
    })),
    ...estado.historicas
      .filter((h) => h.rol === ROL_PRINCIPAL)
      .map((h) => ({ valor: `h:${h.id}`, texto: `P${h.numero} · ${h.empleado.nombreCompleto} (histórico)` })),
  ]
}

/**
 * P3 (TAREA-19y, regla del servidor): si ningún principal guardado que permanece es inicial, el PRIMER principal nuevo
 * del cuerpo se guarda como inicial (responsable). Devuelve su clave o null. Se recalcula al reordenar.
 */
export function claveSeraInicial(estado: EstadoEdicionPersonal): string | null {
  const quedaInicial = estado.historicaInicial || estado.principales.some((p) => p.id !== null && !p.eliminada && p.esPrincipalInicial)
  return quedaInicial ? null : (principalesEnviados(estado).find((p) => p.id === null)?.clave ?? null)
}

// ------------------------------------------------------------------ mínimo (TAREA-19y, P1: personal resultante)

function conteoResultante(estado: EstadoEdicionPersonal): { principales: number; backs: number } {
  return {
    principales: estado.historicas.filter((h) => h.rol === ROL_PRINCIPAL).length + principalesEnviados(estado).length,
    backs: estado.historicas.filter((h) => h.rol === ROL_BACK).length + backsEnviados(estado).length,
  }
}

/** Ayuda neutra según PROYECTO_EXIGE_PRINCIPAL sobre históricas + vigentes no eliminadas + nuevas. */
export function ayudaPersonalEdicion(estado: EstadoEdicionPersonal, exigePrincipal: boolean): string | undefined {
  const c = conteoResultante(estado)
  return ayudaMinimo(c.principales, c.backs, exigePrincipal)
}

export function puedeGenerarVistaPreviaEdicion(estado: EstadoEdicionPersonal, enviando: boolean, exigePrincipal: boolean): boolean {
  const c = conteoResultante(estado)
  return !enviando && cumpleMinimo(c.principales, c.backs, exigePrincipal)
}

// ------------------------------------------------------------------ ayudas del cliente

/** Ayudas que no bloquean (clave de fila → campo → mensaje), con los textos del servidor. */
export function validarEdicionPersonal(estado: EstadoEdicionPersonal): Record<string, ErroresFila> {
  const errores: Record<string, ErroresFila> = {}
  const rango = `(${formatearFecha(estado.fechaInicioProyecto)} – ${formatearFecha(estado.fechaFinProyecto)})`
  for (const f of [...principalesEnviados(estado), ...backsEnviados(estado)]) {
    const fila: ErroresFila = {}
    if (f.fechaInicio === null) {
      fila.fechaInicio = 'La fecha de inicio es obligatoria.'
    }
    if (f.fechaFin === null) {
      fila.fechaFin = 'La fecha fin es obligatoria.'
    }
    if (f.fechaInicio !== null && f.fechaFin !== null) {
      if (f.fechaFin < f.fechaInicio) {
        fila.fechaFin = 'La fecha fin debe ser mayor o igual a la fecha de inicio.'
      } else if (f.fechaInicio < estado.fechaInicioProyecto || f.fechaFin > estado.fechaFinProyecto) {
        fila.fechaFin = `Las fechas deben estar dentro del rango del proyecto ${rango}.`
      }
    }
    const minima = f.permisos?.fechaFinMinima
    if (minima && f.fechaFin !== null && f.fechaFin !== f.original?.fechaFin && f.fechaFin < minima) {
      fila.fechaFin = `La fecha fin no puede ser anterior al ${formatearFecha(minima)} (día anterior al corte).`
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

function texto(valor: string): string | null {
  const recortado = valor.trim()
  return recortado ? recortado : null
}

/** Cuerpo de la vista previa: vigentes no eliminadas y nuevas; nunca las históricas. */
export function aSolicitudPersonal(estado: EstadoEdicionPersonal): SolicitudActualizarPersonal {
  const principales: PrincipalEdicionSolicitud[] = principalesEnviados(estado).map((p) => ({
    clave: p.clave,
    id: p.id,
    empleadoId: p.empleado.id,
    jornada: p.jornada,
    fechaInicio: p.fechaInicio,
    fechaFin: p.fechaFin,
    // Vigente: cargo null = se conserva (no se puede vaciar); nueva: el del formulario o el del empleado (servidor).
    cargo: p.original !== null && p.cargo === p.original.cargo ? null : texto(p.cargo),
  }))
  const backs: BackEdicionSolicitud[] = backsEnviados(estado).map((b) => ({
    clave: b.clave,
    id: b.id,
    empleadoId: b.empleado.id,
    tipoRegistro: b.tipoRegistro,
    fechaInicio: b.fechaInicio,
    fechaFin: b.fechaFin,
    diasDescanso: b.tipoRegistro === 'DESCANSO' ? 0 : b.diasDescanso,
    principalClave: b.relacion && 'clave' in b.relacion ? b.relacion.clave : null,
    principalId: b.relacion && 'historicoId' in b.relacion ? b.relacion.historicoId : null,
    observacion: texto(b.observacion),
  }))
  return { principales, backs }
}

/** Cuerpo del registro (TAREA-19x): el de la vista previa más su `versionProyecto`. */
export function aSolicitudRegistroPersonal(estado: EstadoEdicionPersonal, vista: VistaPersonal): SolicitudActualizarPersonal {
  return { ...aSolicitudPersonal(estado), versionProyecto: vista.datos.versionProyecto }
}

/** Claves de las filas en el orden del envío: el índice de un error 400 apunta a ESA fila. */
export function clavesDelEnvioEdicion(estado: EstadoEdicionPersonal): ClavesEnvio {
  return { principales: principalesEnviados(estado).map((p) => p.clave), backs: backsEnviados(estado).map((b) => b.clave) }
}

// ------------------------------------------------------------------ vista previa y registro

export interface VistaPersonal {
  revision: number
  datos: PrevisualizacionPersonal
}

export function vistaPersonalVigente(vista: VistaPersonal | null, revision: number): boolean {
  return vista !== null && vista.revision === revision
}

/** C9 / pendiente 25 (lado personal): todas las personas en SIN_CAMBIO (dato estructurado, no el texto). */
export function sinCambiosPersonal(datos: PrevisualizacionPersonal): boolean {
  return datos.personal.every((p) => p.accion === 'SIN_CAMBIO')
}

/** Hay personas eliminadas o vigentes con el fin acortado: hay que confirmar. */
export function requiereConfirmacionPersonal(datos: PrevisualizacionPersonal, estado: EstadoEdicionPersonal): boolean {
  const acortadas = [...principalesEnviados(estado), ...backsEnviados(estado)].some(
    (f) => f.original !== null && f.fechaFin !== null && f.original.fechaFin !== null && f.fechaFin < f.original.fechaFin,
  )
  return acortadas || datos.personal.some((p) => p.accion === 'ELIMINADO')
}

export function puedeRegistrarPersonal(vista: VistaPersonal | null, estado: EstadoEdicionPersonal, enviando: boolean): boolean {
  if (enviando || !vistaPersonalVigente(vista, estado.revision)) {
    return false
  }
  const datos = vista!.datos
  if (datos.cruces.length > 0 || sinCambiosPersonal(datos)) {
    return false
  }
  return !requiereConfirmacionPersonal(datos, estado) || estado.entiende
}

/** Por qué "Registrar" está deshabilitado (texto junto al botón); "" si no hay motivo que mostrar. */
export function motivoSinRegistroPersonal(vista: VistaPersonal | null, estado: EstadoEdicionPersonal, enviando: boolean): string {
  if (enviando) {
    return ''
  }
  if (vista === null) {
    return 'Para registrar, primero genere la vista previa.'
  }
  if (!vistaPersonalVigente(vista, estado.revision)) {
    return 'La vista previa está desactualizada.'
  }
  if (vista.datos.cruces.length > 0) {
    return 'No se puede registrar con cruces de asignación.'
  }
  if (sinCambiosPersonal(vista.datos)) {
    return 'No hay cambios para registrar.'
  }
  if (requiereConfirmacionPersonal(vista.datos, estado) && !estado.entiende) {
    return 'Marque la casilla de confirmación.'
  }
  return ''
}

export function mensajeExitoPersonal(version: number): string {
  return `Personal actualizado (versión ${version}).`
}

// ------------------------------------------------------------------ errores

/** Datos de un error de la API (ErrorApi los cumple). */
export interface ErrorRespuestaPersonal {
  tipo: TipoErrorApi
  titulo: string
  errores?: Record<string, string[]>
  extensiones?: Record<string, unknown>
}

export interface ErroresPersonal {
  /** 400: por fila (clave), por sección y generales. */
  servidor: ErroresServidor
  /** 409 con extensions.cruces: cruces que aparecieron después de la vista previa. */
  cruces: { cruces: CruceAsignacion[]; resumen: ResumenCruce[] } | null
  /** Mensajes, "Recargar", "Reintentar" y 404. */
  dialogo: ErroresDialogo<never>
}

export const SIN_ERRORES_PERSONAL: ErroresPersonal = { servidor: SIN_ERRORES_SERVIDOR, cruces: null, dialogo: sinErrores() }

/** Claves de un 400 que ofrecen "Recargar datos del proyecto" (token ausente o proyecto que dejó de ser editable). */
const CLAVES_RECARGA = ['versionProyecto', 'proyecto']

/**
 * 400: `principales[i].campo` / `backs[i].campo` a la fila del envío (`distribuirErrores`, TAREA-13); `principales` y
 * `backs` a su sección; `personal`, `general`, `versionProyecto`, `proyecto` y las demás arriba. 409 con cruces: se
 * muestran y "Registrar" queda deshabilitado; 409 sin cruces: "Recargar"; 503/red: "Reintentar"; 404: listado.
 */
export function interpretarErrorPersonal(error: ErrorRespuestaPersonal, claves: ClavesEnvio): ErroresPersonal {
  if (error.tipo === 'validacion') {
    const servidor = error.errores ? distribuirErrores(error.errores, claves) : { ...SIN_ERRORES_SERVIDOR, generales: [error.titulo] }
    const recarga = Object.keys(error.errores ?? {}).some((k) => CLAVES_RECARGA.includes(k))
    return { servidor, cruces: null, dialogo: { ...sinErrores(), ofrecerRecarga: recarga } }
  }
  if (error.tipo === 'conflicto' && error.extensiones) {
    const cruces = crucesDeConflicto(error.extensiones)
    if (cruces) {
      return { servidor: SIN_ERRORES_SERVIDOR, cruces, dialogo: { ...sinErrores(), generales: [error.titulo] } }
    }
  }
  return { servidor: SIN_ERRORES_SERVIDOR, cruces: null, dialogo: interpretarErrorDialogo<never>(error, { campos: [] }) }
}
