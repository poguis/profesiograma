// DTOs de novedades (TAREA-24; contratos en docs/fases/FASE_5_Novedades.md §1 y §7). Fechas "yyyy-MM-dd".
// Nunca viajan la cédula ni DetalleOrigenJson; `registro` solo llega para el Admin (lo decide el backend).

export type AplicacionNovedad = 'PERSONA' | 'PROYECTO' | 'GENERAL'

export interface TipoNovedadResumen {
  codigo: string
  nombre: string
  sigla: string
  /** "#RRGGBB" */
  color: string
}

/** PERSONA: `empleado`; PROYECTO: `proyecto`. GENERAL: el sujeto es null. */
export interface SujetoNovedad {
  empleado: { codigoEkon: string; nombre: string } | null
  proyecto: { id: number; codigo: string; nombre: string } | null
}

/** Ítem de GET /api/novedades. */
export interface NovedadResumen {
  id: number
  codigo: string
  aplicacion: string
  tipo: TipoNovedadResumen
  origen: string
  sujeto: SujetoNovedad | null
  fechaInicio: string
  fechaFin: string
  totalDias: number
  anulada: boolean
  /** Instante UTC. */
  fechaCreacion: string
}

/** GET /api/novedades/{id}. `version` = RowVer en Base64 (token de la 24c). */
export interface NovedadDetalle extends Omit<NovedadResumen, 'totalDias'> {
  dias: string[]
  observacion: string | null
  fechaAnulacion: string | null
  motivoAnulacion: string | null
  registro: { nombre: string | null; correo: string | null } | null
  version: string
  puedeEditar: boolean
  puedeAnular: boolean
  motivoNoEditable: string | null
}

/** GET /api/novedades/opciones-formulario. GENERAL viene vacío si no `puedeRegistrarGeneral` (J1). */
export interface OpcionesFormularioNovedad {
  puedeRegistrarGeneral: boolean
  tiposPorAplicacion: Partial<Record<AplicacionNovedad, TipoNovedadResumen[]>>
  maxDias: number
}

/** POST /api/novedades (sin días excluidos: eso es la edición, 24c). PERSONA: codigoEkon; PROYECTO: proyectoId. */
export interface SolicitudCrearNovedad {
  tipoAplicacion: AplicacionNovedad
  tipoNovedad: string
  codigoEkon?: string
  proyectoId?: number
  fechaInicio: string
  fechaFin: string
  observacion?: string
}

/** 201 de POST /api/novedades. */
export interface NovedadCreada {
  id: number
  codigo: string
  version: string
}

/** Filtros del listado tal como viven en la URL y se envían a la API. */
export interface FiltrosNovedades {
  aplicacion?: string
  tipo?: string
  origen?: string
  texto?: string
  desde?: string
  hasta?: string
  incluirAnuladas: boolean
  pagina: number
  tamano: number
}
