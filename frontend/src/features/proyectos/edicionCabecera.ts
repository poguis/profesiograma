// Diálogo "Editar datos generales" (TAREA-19a, contrato TAREA-18/18b). Lógica pura, sin React: se prueba con Vitest.
// El servidor tiene la última palabra (C1–C11); aquí solo se arma el cuerpo con los campos cambiados, se dan ayudas
// y se decide cuándo se puede registrar. Ver docs/fases/FASE_5_Edicion_Cabecera.md.
import { formatearFecha } from '../../utils/formato'
import { type ErrorRespuesta, type ErroresDialogo, interpretarErrorDialogo, sinErrores } from './erroresDialogo'
import type { CabeceraEdicion, PrevisualizacionCabecera, SolicitudEditarCabecera } from './tipos'

/** Valores del formulario. Fechas "yyyy-MM-dd", horas "HH:mm". actividadId null = sin cambio de actividad. */
export interface ValoresCabecera {
  fechaInicio: string | null
  fechaFin: string | null
  horarioCodigo: number | null
  salidaAlmuerzo: string | null
  regresoAlmuerzo: string | null
  actividadId: string | null
  actividadDesde: string | null
}

export interface EstadoEdicion {
  /** Aumenta con cada cambio de datos: una vista previa solo vale para su revisión. */
  revision: number
  /** Valores guardados (los de la cabecera con que se abrió o recargó el diálogo). */
  original: ValoresCabecera
  valores: ValoresCabecera
  /** Hubo cambios desde que se abrió o recargó (criterio de la TAREA-13 para confirmar la salida). */
  editado: boolean
  /** Casilla "Entiendo que esta acción no se puede deshacer." (se desmarca con cada cambio). */
  entiende: boolean
}

export type AccionEdicion =
  | { tipo: 'campos'; cambios: Partial<ValoresCabecera> }
  | { tipo: 'entiende'; valor: boolean }
  | { tipo: 'reiniciar'; cabecera: CabeceraEdicion }

export function valoresDeCabecera(c: CabeceraEdicion): ValoresCabecera {
  return {
    fechaInicio: c.fechaInicio,
    fechaFin: c.fechaFin,
    horarioCodigo: c.horario.codigo,
    salidaAlmuerzo: c.salidaAlmuerzo,
    regresoAlmuerzo: c.regresoAlmuerzo,
    actividadId: null,
    actividadDesde: null,
  }
}

export function crearEstadoEdicion(cabecera: CabeceraEdicion): EstadoEdicion {
  const original = valoresDeCabecera(cabecera)
  return { revision: 0, original, valores: original, editado: false, entiende: false }
}

export function reducerEdicion(estado: EstadoEdicion, accion: AccionEdicion): EstadoEdicion {
  switch (accion.tipo) {
    case 'campos': {
      const claves = Object.keys(accion.cambios) as (keyof ValoresCabecera)[]
      if (claves.every((k) => accion.cambios[k] === estado.valores[k])) {
        return estado
      }
      return {
        ...estado,
        revision: estado.revision + 1,
        valores: { ...estado.valores, ...accion.cambios },
        editado: true,
        entiende: false,
      }
    }
    case 'entiende':
      return { ...estado, entiende: accion.valor }
    case 'reiniciar':
      // Tras "Recargar datos del proyecto": valores guardados nuevos; la revisión sigue subiendo (vista previa vieja).
      return { ...crearEstadoEdicion(accion.cabecera), revision: estado.revision + 1 }
  }
}

// ------------------------------------------------------------------ solicitud

/** C1: solo los campos distintos de los guardados; el resto null ("no cambia"). */
export function aSolicitudCabecera(estado: EstadoEdicion): SolicitudEditarCabecera {
  const { original: o, valores: v } = estado
  const distinto = <T>(nuevo: T | null, anterior: T | null) => (nuevo !== null && nuevo !== anterior ? nuevo : null)
  // Con actividad o "desde" se envía el bloque: si falta uno, el servidor responde 400 en ese campo.
  const conActividad = v.actividadId !== null || v.actividadDesde !== null
  return {
    fechaInicio: distinto(v.fechaInicio, o.fechaInicio),
    fechaFin: distinto(v.fechaFin, o.fechaFin),
    horarioCodigo: distinto(v.horarioCodigo, o.horarioCodigo),
    salidaAlmuerzo: distinto(v.salidaAlmuerzo, o.salidaAlmuerzo),
    regresoAlmuerzo: distinto(v.regresoAlmuerzo, o.regresoAlmuerzo),
    actividad: conActividad ? { actividadId: v.actividadId, desde: v.actividadDesde } : null,
  }
}

/**
 * Cuerpo del registro (TAREA-19x): el de la vista previa más su `versionProyecto`. Si el proyecto cambió desde la vista
 * previa, el servidor responde 409 y el diálogo ofrece "Recargar datos del proyecto".
 */
export function aSolicitudRegistroCabecera(estado: EstadoEdicion, vista: VistaCabecera): SolicitudEditarCabecera {
  return { ...aSolicitudCabecera(estado), versionProyecto: vista.datos.versionProyecto }
}

// ------------------------------------------------------------------ rangos y ayudas

export interface Rango {
  minima?: string
  maxima?: string
}

/** C2: el inicio nuevo no puede ser anterior a hoy (corte). Sin máximo: fin ≥ inicio se avisa en el campo fin. */
export function rangoFechaInicio(cabecera: CabeceraEdicion): Rango {
  return { minima: cabecera.corte }
}

/** C4: fin ≥ fechaFinMinima (hoy) y ≥ inicio resultante. */
export function rangoFechaFin(cabecera: CabeceraEdicion, valores: ValoresCabecera): Rango {
  const inicio = valores.fechaInicio
  const minimo = cabecera.permisos.fechaFinMinima
  return { minima: inicio !== null && inicio > minimo ? inicio : minimo }
}

/** C6: "desde" dentro de [inicio, fin] resultantes. */
export function rangoDesde(valores: ValoresCabecera): Rango {
  return { minima: valores.fechaInicio ?? undefined, maxima: valores.fechaFin ?? undefined }
}

export type CampoEdicion =
  | 'fechaInicio'
  | 'fechaFin'
  | 'horarioCodigo'
  | 'salidaAlmuerzo'
  | 'regresoAlmuerzo'
  | 'actividad.actividadId'
  | 'actividad.desde'

/** Campo del formulario → clave del ValidationProblem del servidor. */
export const CLAVE_ERROR: Readonly<Record<keyof ValoresCabecera, CampoEdicion>> = {
  fechaInicio: 'fechaInicio',
  fechaFin: 'fechaFin',
  horarioCodigo: 'horarioCodigo',
  salidaAlmuerzo: 'salidaAlmuerzo',
  regresoAlmuerzo: 'regresoAlmuerzo',
  actividadId: 'actividad.actividadId',
  actividadDesde: 'actividad.desde',
}

export type ErroresCampos = Partial<Record<CampoEdicion, string>>

/** Fechas vacías: el cuerpo las mandaría como "no cambia", así que "Ver impacto" se bloquea hasta completarlas. */
export function camposIncompletos(valores: ValoresCabecera): ErroresCampos {
  const errores: ErroresCampos = {}
  if (valores.fechaInicio === null) {
    errores.fechaInicio = 'La fecha de inicio es obligatoria.'
  }
  if (valores.fechaFin === null) {
    errores.fechaFin = 'La fecha fin es obligatoria.'
  }
  return errores
}

/** Ayudas del cliente con los mismos textos del servidor (no bloquean: D4 de la TAREA-13). */
export function validarEdicionCliente(valores: ValoresCabecera): ErroresCampos {
  const errores = camposIncompletos(valores)
  const { fechaInicio: inicio, fechaFin: fin } = valores
  // "yyyy-MM-dd" y "HH:mm" se comparan como texto.
  if (inicio !== null && fin !== null && fin < inicio) {
    errores.fechaFin = 'La fecha fin debe ser mayor o igual a la fecha de inicio.'
  }
  if (valores.salidaAlmuerzo !== null && valores.regresoAlmuerzo !== null && valores.regresoAlmuerzo <= valores.salidaAlmuerzo) {
    errores.regresoAlmuerzo = 'El regreso de almuerzo debe ser posterior a la salida.'
  }
  const desde = valores.actividadDesde
  if (desde !== null && inicio !== null && fin !== null && (desde < inicio || desde > fin)) {
    errores['actividad.desde'] =
      `La fecha desde debe estar dentro del rango del proyecto (${formatearFecha(inicio)} – ${formatearFecha(fin)}).`
  }
  return errores
}

export function puedeVerImpacto(valores: ValoresCabecera, enviando: boolean): boolean {
  return !enviando && Object.keys(camposIncompletos(valores)).length === 0
}

// ------------------------------------------------------------------ vista previa y registro

export interface VistaCabecera {
  revision: number
  datos: PrevisualizacionCabecera
}

export function vistaCabeceraVigente(vista: VistaCabecera | null, revision: number): boolean {
  return vista !== null && vista.revision === revision
}

/** C9 / pendiente 25: sin cambios no se registra. Se usa la lista estructurada, no el texto "No hay cambios.". */
export function sinCambios(datos: PrevisualizacionCabecera): boolean {
  return datos.cambios.length === 0
}

/** La vista previa borra algo (días, personal o actividades) o acorta personal o actividades: hay que confirmar. */
export function requiereConfirmacion(datos: PrevisualizacionCabecera): boolean {
  return (
    datos.diasEliminados.length > 0 ||
    datos.personalEliminado.length > 0 ||
    datos.personalRecortado.length > 0 ||
    datos.actividades.some(
      (a) =>
        a.accion === 'ELIMINADA' ||
        (a.accion === 'MODIFICADA' && a.fechaFinAnterior !== null && a.fechaFin < a.fechaFinAnterior),
    )
  )
}

/** Vista previa vigente y con cambios, sin envío en curso y, si borra algo, con la casilla marcada. */
export function puedeRegistrar(vista: VistaCabecera | null, estado: EstadoEdicion, enviando: boolean): boolean {
  if (enviando || !vistaCabeceraVigente(vista, estado.revision) || sinCambios(vista!.datos)) {
    return false
  }
  return !requiereConfirmacion(vista!.datos) || estado.entiende
}

/** Por qué "Registrar" está deshabilitado (texto junto al botón); "" si no hay motivo que mostrar. */
export function motivoSinRegistro(vista: VistaCabecera | null, estado: EstadoEdicion, enviando: boolean): string {
  if (enviando) {
    return ''
  }
  if (vista === null) {
    return 'Para registrar, primero pulse "Ver impacto".'
  }
  if (!vistaCabeceraVigente(vista, estado.revision)) {
    return 'La vista previa está desactualizada.'
  }
  if (sinCambios(vista.datos)) {
    return 'No hay cambios para registrar.'
  }
  if (requiereConfirmacion(vista.datos) && !estado.entiende) {
    return 'Marque la casilla de confirmación.'
  }
  return ''
}

export const TIPO_CAMBIO_ACTIVIDAD = 'CAMBIO_ACTIVIDAD'

/** P4: "Actividad cambiada (versión n)." para CAMBIO_ACTIVIDAD; si no, "Datos generales actualizados (versión n).". */
export function mensajeExitoCabecera(tipoEtapa: string, version: number): string {
  return tipoEtapa === TIPO_CAMBIO_ACTIVIDAD
    ? `Actividad cambiada (versión ${version}).`
    : `Datos generales actualizados (versión ${version}).`
}

// ------------------------------------------------------------------ textos del impacto

const ETIQUETA_CAMPO: Readonly<Record<string, string>> = {
  fechaInicio: 'Fecha de inicio',
  fechaFin: 'Fecha fin',
  horario: 'Horario',
  salidaAlmuerzo: 'Salida a almuerzo',
  regresoAlmuerzo: 'Regreso de almuerzo',
  actividad: 'Actividad',
}

export function etiquetaCampo(campo: string): string {
  return ETIQUETA_CAMPO[campo] ?? campo
}

const PATRON_FECHA_ISO = /\d{4}-\d{2}-\d{2}/g

/** Valor de un cambio para mostrar: las fechas "yyyy-MM-dd" (también "DEV.02 desde 2026-10-14") como dd/MM/yyyy. */
export function textoValorCambio(valor: string | null): string {
  return valor === null ? '—' : valor.replace(PATRON_FECHA_ISO, (f) => formatearFecha(f))
}

// ------------------------------------------------------------------ errores

export type ErroresCabecera = ErroresDialogo<CampoEdicion>

export const SIN_ERRORES_CABECERA: ErroresCabecera = sinErrores()

const CAMPOS_ERROR: readonly CampoEdicion[] = Object.values(CLAVE_ERROR)

/**
 * 400: claves de campo en su Field; `general` (p. ej. C9) y las desconocidas arriba. Solo `proyecto` (dejó de ser
 * ACTIVO) y `versionProyecto` (falta el token, TAREA-19x) ofrecen "Recargar datos del proyecto"; 409 siempre;
 * 503/red "Reintentar"; 404 enlace al listado.
 */
export function interpretarErrorCabecera(error: ErrorRespuesta): ErroresCabecera {
  return interpretarErrorDialogo(error, { campos: CAMPOS_ERROR, clavesRecarga: ['proyecto', 'versionProyecto'] })
}
