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

export type GrupoProyectoCatalogo = ItemCatalogo & { requiereProyectoErp: boolean; requiereDimension: boolean }

export type JornadaCatalogo = ItemCatalogo & { diasTrabajo: number; diasDescanso: number }

export interface Catalogos {
  estadosProyecto: (ItemCatalogo & { esVigente: boolean })[]
  gruposProyecto: GrupoProyectoCatalogo[]
  tiposMovimiento: ItemCatalogo[]
  jornadas: JornadaCatalogo[]
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

// ------------------------------------------------------------------ Nuevo proyecto (TAREA-13)

/** GET /api/proyectos/opciones-formulario. Horas "HH:mm". */
export interface OpcionesFormularioProyecto {
  /** Departamentos del usuario: 0 = no se muestra, 1 = automático, varios = elige. */
  departamentos: { id: number; nombre: string }[]
  almuerzoSalidaOpciones: string[]
  almuerzoRegresoOpciones: string[]
  maxPrincipales: number
  maxBacks: number
  backMaxDiasDescanso: number
}

/** GET /api/erp/companias */
export interface CompaniaErp {
  id: number
  nombre: string
  nombreCorto: string | null
  ruc: string | null
}

/** GET /api/erp/companias/{id}/proyectos (solo "Activo"). */
export interface ProyectoErp {
  id: string
  nombre: string
  estado: string
}

/** GET /api/erp/companias/{id}/dimensiones. El identificador es `uegpId`. */
export interface DimensionErp {
  uegpId: string
  descripcion: string
}

/** GET /api/erp/companias/{id}/proyectos/{pid}/actividades */
export interface ActividadErp {
  id: string
  descripcion: string
  tipo: string | null
}

/** GET /api/erp/horarios (solo activos). Horas "HH:mm:ss": mostrar con formatearHora, no mezclar con el formulario. */
export interface HorarioErp {
  codigo: number
  descripcion: string
  horaEntrada: string | null
  horaSalida: string | null
  minutosJornada: number | null
  minutosTrabajados: number | null
  tipo: string | null
}

/** Item de GET /api/empleados (sin cédula ni correo). */
export interface EmpleadoBusqueda {
  id: number
  codigoEkon: string
  nombreCompleto: string
  cargo: string | null
  departamento: string | null
}

export interface FiltroEmpleados {
  texto?: string
  soloMisDepartamentos: boolean
  pagina: number
  tamano: number
}

export type TipoRegistroBack = 'JORNADA' | 'DESCANSO'

/** Cuerpo de POST /api/proyectos y /previsualizar (FASE_5 §7.1). Fechas "yyyy-MM-dd", horas "HH:mm". */
export interface SolicitudCrearProyecto {
  companiaId: number | null
  grupo: string | null
  proyectoErpId: string | null
  actividadId: string | null
  dimensionUegpId: string | null
  fechaInicio: string | null
  fechaFin: string | null
  horarioCodigo: number | null
  salidaAlmuerzo: string | null
  regresoAlmuerzo: string | null
  departamentoId: number | null
  principales: PrincipalSolicitud[]
  backs: BackSolicitud[]
}

export interface PrincipalSolicitud {
  empleadoId: number | null
  jornada: string | null
  fechaInicio: string | null
  fechaFin: string | null
  cargo: string | null
}

export interface BackSolicitud {
  empleadoId: number | null
  tipoRegistro: TipoRegistroBack
  diasDescanso: number
  fechaInicio: string | null
  fechaFin: string | null
  /** Número (posición 1..n) del principal que cubre. */
  principalRelacionado: number | null
  observacion: string | null
  /** Siempre null: el servidor usa el puesto del empleado (Δ3). */
  cargo: null
}

export interface PersonaCronograma {
  /** PRINCIPAL | BACK */
  rol: string
  numero: number
}

/** Tramo. rol = PRINCIPAL | BACK | DESCANSO; tipo = AUTO | MANUAL. */
export interface TramoCronograma {
  rol: string
  tipo: string
  bloque: number
  persona: PersonaCronograma
  empleadoId: number
  codigoEkon: string
  nombreEmpleado: string
  inicio: string
  fin: string
  dias: number
}

export interface DiaCronograma {
  fecha: string
  rol: string
  tipo: string
  bloque: number
}

export interface DiasPersona {
  persona: PersonaCronograma
  empleadoId: number
  codigoEkon: string
  nombreEmpleado: string
  dias: DiaCronograma[]
}

/** origen = INTERNO | EXTERNO. Del proyecto existente solo código, nombre y estado. */
export interface CruceAsignacion {
  origen: string
  empleadoId: number
  codigoEkon: string
  nombreEmpleado: string
  fecha: string
  rol: string
  proyecto: string
  proyectoCodigo: string | null
  estadoProyecto: string | null
}

/** Resumen de cruces: persona · rol · proyecto · mes ("diciembre 2026") · días ("5, 6, 7"). */
export interface ResumenCruce {
  nombreEmpleado: string
  rol: string
  proyecto: string
  mes: string
  dias: string
}

/** 200 de POST /api/proyectos/previsualizar. `resumen` es el resumen de CRUCES. */
export interface Previsualizacion {
  tramos: TramoCronograma[]
  diasPorPersona: DiasPersona[]
  cruces: CruceAsignacion[]
  resumen: ResumenCruce[]
}

/** 201 de POST /api/proyectos */
export interface ProyectoCreado {
  id: number
  codigo: string
}

// ------------------------------------------------------------------ Cambio de estado (TAREA-15, contrato TAREA-14)

/** Destinos que la interfaz permite elegir (REACTIVACION llega en la TAREA-17). */
export type DestinoCambioEstado = 'SUSPENDIDO' | 'TERMINADO'

/** Cuerpo de POST /api/proyectos/{id}/cambio-estado[/previsualizar]. Fecha "yyyy-MM-dd". */
export interface SolicitudCambioEstado {
  estadoDestino: string | null
  fecha: string | null
}

/** `id` es el Id del EMPLEADO (no el de la fila de personal). Sin cédula ni correo. */
export interface EmpleadoCambio {
  id: number
  codigoEkon: string
  nombreCompleto: string
}

/** rol = PRINCIPAL | BACK | DESCANSO */
export interface DiasEliminadosCambio {
  empleado: EmpleadoCambio
  rol: string
  cantidad: number
  desde: string
  hasta: string
}

export interface PersonaEliminadaCambio {
  empleado: EmpleadoCambio
  rol: string
  numero: number
  fechaInicio: string
  fechaFin: string
}

export interface PersonaRecortadaCambio {
  empleado: EmpleadoCambio
  rol: string
  numero: number
  fechaInicio: string
  fechaFinAnterior: string
  fechaFinNueva: string
}

/** fechaFinNueva es null cuando la actividad se elimina. */
export interface ActividadAfectadaCambio {
  actividadCodigo: string
  version: number
  fechaInicio: string
  fechaFinAnterior: string
  fechaFinNueva: string | null
  accion: 'RECORTADA' | 'ELIMINADA'
}

/** 200 de POST /api/proyectos/{id}/cambio-estado/previsualizar (no guarda nada). */
export interface PrevisualizacionCambioEstado {
  movimiento: 'SUSPENSION' | 'CIERRE'
  estadoActual: string
  estadoNuevo: string
  fecha: string
  fechaFinActual: string
  fechaFinNueva: string
  diasEliminados: DiasEliminadosCambio[]
  personalEliminado: PersonaEliminadaCambio[]
  personalRecortado: PersonaRecortadaCambio[]
  actividadesAfectadas: ActividadAfectadaCambio[]
  advertencias: string[]
}

/** 200 de POST /api/proyectos/{id}/cambio-estado. `estado` es el código del estado nuevo. */
export interface CambioEstadoRealizado {
  id: number
  estado: string
  version: number
}
