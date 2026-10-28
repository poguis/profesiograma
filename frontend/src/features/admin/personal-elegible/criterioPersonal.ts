// Criterio de personal elegible (TAREA-62-2, FASE_4_Empleados_API §10): estado de la pantalla de administración,
// «sin cambios», resumen previo, cuerpo del PUT e interpretación de errores. Lógica pura, sin React.
import { ErrorApi } from '../../../api/errores'

/** GET /api/admin/empleados/familias. Solo familias y conteos (sin datos personales). */
export interface FamiliaCriterio {
  familia: string
  activos: number
  asignable: boolean
}

export interface CriterioPersonal {
  /** Texto guardado tal cual: es el `valorAnterior` del PUT (token de concurrencia). */
  valorActual: string
  sinCriterio: boolean
  familias: FamiliaCriterio[]
}

export interface SolicitudGuardarCriterio {
  familias: string[]
  valorAnterior: string
}

export const MENSAJE_SIN_CAMBIOS = 'No hay cambios para registrar.'
export const MENSAJE_SIN_FAMILIAS = 'Seleccione al menos una familia.'
export const MENSAJE_SIN_CRITERIO = 'Sin criterio: se puede elegir a cualquier persona activa.'

export interface EstadoCriterio {
  /** Familias en el orden del backend (activos descendente). */
  familias: FamiliaCriterio[]
  /** Marcadas al abrir (asignables según el GET). */
  originales: string[]
  /** Marcadas ahora. */
  marcadas: string[]
  /** `valorActual` del GET que abrió la pantalla. */
  valorAnterior: string
}

const clave = (familia: string) => familia.trim().toLowerCase()

export function estadoInicial(c: CriterioPersonal): EstadoCriterio {
  const marcadas = c.familias.filter((f) => f.asignable).map((f) => f.familia)
  return { familias: c.familias, originales: marcadas, marcadas, valorAnterior: c.valorActual }
}

export function estaMarcada(e: EstadoCriterio, familia: string): boolean {
  return e.marcadas.some((m) => clave(m) === clave(familia))
}

/** Marca o desmarca una familia; las marcadas quedan en el orden del backend. */
export function alternarFamilia(e: EstadoCriterio, familia: string): EstadoCriterio {
  const marcar = !estaMarcada(e, familia)
  const marcadas = e.familias
    .map((f) => f.familia)
    .filter((f) => (clave(f) === clave(familia) ? marcar : estaMarcada(e, f)))
  return { ...e, marcadas }
}

export function hayCambios(e: EstadoCriterio): boolean {
  const antes = new Set(e.originales.map(clave))
  return e.marcadas.length !== antes.size || e.marcadas.some((m) => !antes.has(clave(m)))
}

/** Motivo por el que «Guardar» está deshabilitado (o undefined si se puede guardar). */
export function motivoSinGuardar(e: EstadoCriterio): string | undefined {
  if (e.marcadas.length === 0) {
    return MENSAJE_SIN_FAMILIAS
  }
  return hayCambios(e) ? undefined : MENSAJE_SIN_CAMBIOS
}

/** «Se guardará: A → B». Sin familias antes = «(sin criterio)». */
export function resumenCambio(e: EstadoCriterio): string {
  const texto = (lista: string[]) => (lista.length === 0 ? '(sin criterio)' : lista.join(', '))
  return `Se guardará: ${texto(e.originales)} → ${texto(e.marcadas)}`
}

export function aSolicitud(e: EstadoCriterio): SolicitudGuardarCriterio {
  return { familias: [...e.marcadas], valorAnterior: e.valorAnterior }
}

export interface ErroresCriterio {
  /** 400 en `familias` / `valorAnterior`: uno por línea, bajo la lista. */
  lista: string[]
  /** Arriba de la pantalla. */
  generales: string[]
  /** 409: «Recargar» (con confirmación si hay cambios sin guardar). */
  ofrecerRecarga: boolean
  /** 503 y red: «Reintentar». */
  ofrecerReintento: boolean
}

/** 400 → bajo la lista; 409 → «Recargar»; 403 → title sin «Reintentar»; 503 y red → «título: detalle» con «Reintentar». */
export function interpretarErrorCriterio(error: unknown): ErroresCriterio {
  const base: ErroresCriterio = { lista: [], generales: [], ofrecerRecarga: false, ofrecerReintento: false }
  if (!(error instanceof ErrorApi)) {
    return { ...base, generales: ['Ocurrió un error inesperado.'], ofrecerReintento: true }
  }
  switch (error.tipo) {
    case 'validacion': {
      const errores = error.errores ?? {}
      const lista = [...(errores.familias ?? []), ...(errores.valorAnterior ?? [])]
      const otros = Object.entries(errores)
        .filter(([k]) => k !== 'familias' && k !== 'valorAnterior')
        .flatMap(([, m]) => m)
      return { ...base, lista, generales: lista.length === 0 && otros.length === 0 ? [error.titulo] : otros }
    }
    case 'conflicto':
      return { ...base, generales: [error.titulo], ofrecerRecarga: true }
    case 'noDisponible':
      return { ...base, generales: [error.detalle ? `${error.titulo}: ${error.detalle}` : error.titulo], ofrecerReintento: true }
    case 'red':
      return { ...base, generales: [error.titulo], ofrecerReintento: true }
    default:
      return { ...base, generales: [error.titulo] } // 403, 401, 404 y otros: sin «Reintentar»
  }
}
