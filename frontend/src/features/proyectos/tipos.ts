// DTOs de proyectos según el contrato real de la API (TAREA-07). Fechas "yyyy-MM-dd", horas "HH:mm:ss".

export interface CodigoNombre {
  codigo: string
  nombre: string
}

/** Item de GET /api/proyectos. `grupo` y `estado` son códigos. */
export interface ProyectoResumen {
  id: number
  codigo: string
  nombre: string
  grupo: string
  estado: string
  fechaInicio: string
  fechaFin: string
  responsable: string | null
  backs: string[]
  horaEntrada: string | null
  horaSalida: string | null
}

/** GET /api/proyectos/{id} */
export interface ProyectoDetalle {
  id: number
  uid: string
  codigo: string
  nombre: string
  compania: { id: number; nombre: string; ruc: string | null }
  grupo: CodigoNombre
  estado: CodigoNombre
  fechaInicio: string
  fechaFin: string
  erp: {
    proyectoErpId: string | null
    proyectoErpNombre: string | null
    proyectoErpEstado: string | null
    dimensionUegpId: string | null
    dimensionDescripcion: string | null
  }
  departamento: string | null
  horario: {
    codigo: number | null
    descripcion: string | null
    horaEntrada: string | null
    horaSalida: string | null
    tipo: string | null
    minutosJornada: number | null
    minutosTrabajados: number | null
  }
  almuerzo: { salida: string | null; regreso: string | null }
  propietario: { id: number; nombreMostrar: string; email: string }
  personal: PersonalProyecto[]
  etapas: EtapaProyecto[]
  actividadVigente: ActividadProyecto | null
}

export interface PersonalProyecto {
  id: number
  /** PRINCIPAL | BACK */
  rol: string
  numero: number
  empleado: { id: number; codigoEkon: string; nombreCompleto: string }
  cargo: string | null
  jornada: CodigoNombre | null
  diasTrabajo: number | null
  diasDescanso: number
  fechaInicio: string
  fechaFin: string
  esPrincipalInicial: boolean
  tipoRegistro: string
  observacion: string | null
}

export interface EtapaProyecto {
  version: number
  /** Código de TipoMovimiento. */
  tipoMovimiento: string
  /** Código de EstadoProyecto. */
  estado: string
  fechaInicio: string
  fechaFin: string
  fechaCorte: string | null
  actividadCodigo: string | null
  /** Instante UTC ISO (p. ej. "2026-09-30T16:44:11Z"). */
  fechaRegistroUtc: string
}

export interface ActividadProyecto {
  version: number
  tipoMovimiento: string
  actividadCodigo: string
  actividadDescripcion: string | null
  actividadTipo: string | null
  fechaInicio: string
  fechaFin: string
}

// Subconjunto de GET /api/catalogos que usa esta pantalla.
export interface ItemCatalogo {
  id: number
  codigo: string
  nombre: string
  orden: number
}

export interface Catalogos {
  estadosProyecto: (ItemCatalogo & { esVigente: boolean })[]
  gruposProyecto: (ItemCatalogo & { requiereProyectoErp: boolean; requiereDimension: boolean })[]
  tiposMovimiento: ItemCatalogo[]
}

/** Filtros del listado tal como viven en la URL y se envían a la API. */
export interface FiltrosProyectos {
  estado?: string
  grupo?: string
  texto?: string
  /** "yyyy-MM-dd" */
  desde?: string
  /** "yyyy-MM-dd" */
  hasta?: string
  pagina: number
  tamano: number
}
