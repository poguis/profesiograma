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
  /** PROYECTO_EXIGE_PRINCIPAL (TAREA-19y): true = al menos 1 principal (C10); false = al menos 1 persona. */
  exigePrincipal: boolean
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
  /** TAREA-19y: avisos que no bloquean (p. ej. "El proyecto no tendrá principal: el responsable quedará vacío."). */
  advertencias: string[]
}

/** 201 de POST /api/proyectos */
export interface ProyectoCreado {
  id: number
  codigo: string
}

// ------------------------------------------------------------------ Cambio de estado (TAREA-15, contrato TAREA-14)

/** Destinos que la interfaz permite elegir (REACTIVACION llega en la TAREA-17). */
export type DestinoCambioEstado = 'SUSPENDIDO' | 'TERMINADO'

/**
 * Cuerpo de POST /api/proyectos/{id}/cambio-estado[/previsualizar]. Fecha "yyyy-MM-dd".
 * versionProyecto: token de concurrencia (TAREA-19x); obligatorio al aplicar (el de la vista previa vigente).
 */
export interface SolicitudCambioEstado {
  estadoDestino: string | null
  fecha: string | null
  versionProyecto?: number | null
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
  /** Token de concurrencia (TAREA-19x): se envía al aplicar. */
  versionProyecto: number
}

/** 200 de POST /api/proyectos/{id}/cambio-estado. `estado` es el código del estado nuevo. */
export interface CambioEstadoRealizado {
  id: number
  estado: string
  version: number
}

// ------------------------------------------------------------------ Edición de cabecera (TAREA-19a, contrato TAREA-18/18b)
// Origen: backend/src/App.Application/Proyectos/Cabecera/EdicionCabeceraDtos.cs y EdicionCabeceraContratos.cs.

/** ActividadCabeceraDto (ProyectoActividad con descripción y tipo del ERP). */
export interface ActividadCabecera {
  version: number
  codigo: string
  descripcion: string | null
  tipo: string | null
  tipoMovimiento: string
  fechaInicio: string
  fechaFin: string
}

/** PermisosCabeceraDto. fechaFinMinima = hoy (C4, P2); actividadEditable solo en grupos con proyecto ERP (C6). */
export interface PermisosCabecera {
  fechaInicioEditable: boolean
  motivoFechaInicio: string | null
  fechaFinMinima: string
  actividadEditable: boolean
}

/**
 * GET /api/proyectos/{id}/cabecera (CabeceraDto). Horas del horario "HH:mm:ss"; almuerzo "HH:mm".
 * actividadVigente = regla común O3 con hoy; corte = hoy en Ecuador. No trae la compañía ni el proyecto ERP.
 */
export interface CabeceraEdicion {
  id: number
  codigo: string
  estado: string
  puedeEditar: boolean
  motivo: string | null
  grupo: string
  fechaInicio: string
  fechaFin: string
  horario: { codigo: number | null; descripcion: string | null; horaEntrada: string | null; horaSalida: string | null }
  salidaAlmuerzo: string | null
  regresoAlmuerzo: string | null
  actividadVigente: ActividadCabecera | null
  /** Ordenadas por versión. */
  actividades: ActividadCabecera[]
  permisos: PermisosCabecera
  opcionesAlmuerzo: { salida: string[]; regreso: string[] }
  corte: string
  /** Token de concurrencia (TAREA-19x): última versión de etapa (0 si no hay). */
  versionProyecto: number
}

/**
 * Cuerpo de POST /api/proyectos/{id}/cabecera[/previsualizar] (EditarCabeceraSolicitud). null = no cambia.
 * versionProyecto: token de concurrencia (TAREA-19x); obligatorio al registrar (el de la vista previa vigente).
 */
export interface SolicitudEditarCabecera {
  fechaInicio: string | null
  fechaFin: string | null
  horarioCodigo: number | null
  salidaAlmuerzo: string | null
  regresoAlmuerzo: string | null
  actividad: { actividadId: string | null; desde: string | null } | null
  versionProyecto?: number | null
}

/** CambioCampoDto. campo = fechaInicio | fechaFin | horario | salidaAlmuerzo | regresoAlmuerzo | actividad. */
export interface CambioCampoCabecera {
  campo: string
  anterior: string | null
  nuevo: string | null
}

/** DiasAgregadosDto (H15): descanso posterior de un back que se vuelve a insertar tras el recorte. */
export interface DiasAgregadosCabecera {
  empleado: EmpleadoCambio
  rol: string
  cantidad: number
  desde: string
  hasta: string
}

export type AccionActividadResultante = 'SIN_CAMBIO' | 'MODIFICADA' | 'ELIMINADA' | 'NUEVA'

/** ActividadResultanteDto. Fechas resultantes (en ELIMINADA, las que tenía); las anteriores solo en MODIFICADA. */
export interface ActividadResultante {
  version: number | null
  codigo: string
  descripcion: string | null
  fechaInicio: string
  fechaFin: string
  fechaInicioAnterior: string | null
  fechaFinAnterior: string | null
  accion: AccionActividadResultante
}

/** 200 de POST /api/proyectos/{id}/cabecera/previsualizar (no guarda). tipoEtapa = EDICION_CABECERA | CAMBIO_ACTIVIDAD. */
export interface PrevisualizacionCabecera {
  corte: string
  tipoEtapa: string
  fechaInicioNueva: string
  fechaFinNueva: string
  cambios: CambioCampoCabecera[]
  diasEliminados: DiasEliminadosCambio[]
  personalEliminado: PersonaEliminadaCambio[]
  personalRecortado: PersonaRecortadaCambio[]
  diasAgregados: DiasAgregadosCabecera[]
  actividades: ActividadResultante[]
  advertencias: string[]
  /** Token de concurrencia (TAREA-19x): se envía al registrar. */
  versionProyecto: number
}

/** 200 de POST /api/proyectos/{id}/cabecera (CabeceraActualizadaDto). */
export interface CabeceraActualizada {
  id: number
  version: number
}

// ------------------------------------------------------------------ Actualización de personal (contrato TAREA-17; pantalla en la TAREA-19b)
// Origen: backend/src/App.Application/Proyectos/Personal/EdicionPersonalDtos.cs y EdicionPersonalContratos.cs.

export interface EmpleadoEdicion {
  id: number
  codigoEkon: string
  nombreCompleto: string
}

export interface LimitesEdicion {
  maxPrincipales: number
  maxBacks: number
  backMaxDiasDescanso: number
  /** PROYECTO_EXIGE_PRINCIPAL (TAREA-19y). */
  exigePrincipal: boolean
}

/** PermisosEdicionDto. Históricos: todo false / null. */
export interface PermisosEdicionPersona {
  /** Solo vigentes que aún no empiezan (inicio ≥ corte). */
  fechaInicio: boolean
  /** Corte − 1 para vigentes (se exige si la fecha fin cambia). */
  fechaFinMinima: string | null
  jornada: boolean
  /** Vigentes que aún no empiezan: se eliminan omitiéndolas del cuerpo. */
  eliminable: boolean
}

/** PersonaEdicionDto. rol = PRINCIPAL | BACK; clase = HISTORICO | VIGENTE. */
export interface PersonaEdicion {
  id: number
  rol: string
  numero: number
  empleado: EmpleadoEdicion
  clase: string
  jornada: string | null
  fechaInicio: string
  fechaFin: string
  tipoRegistro: string
  diasDescanso: number
  principalRelacionadoId: number | null
  cargo: string | null
  observacion: string | null
  permisos: PermisosEdicionPersona
}

/** GET /api/proyectos/{id}/edicion (EdicionPersonalDto). */
export interface EdicionPersonal {
  id: number
  codigo: string
  estado: string
  fechaInicio: string
  fechaFin: string
  corte: string
  puedeEditar: boolean
  motivo: string | null
  personal: PersonaEdicion[]
  limites: LimitesEdicion
  /** Token de concurrencia (TAREA-19x). */
  versionProyecto: number
}

/** PrincipalEdicionSolicitud. id presente = vigente existente; ausente = nuevo. cargo null en un vigente = se conserva. */
export interface PrincipalEdicionSolicitud {
  clave: string | null
  id: number | null
  empleadoId: number | null
  jornada: string | null
  fechaInicio: string | null
  fechaFin: string | null
  cargo: string | null
}

/** BackEdicionSolicitud. principalClave (principal del cuerpo) y principalId (principal HISTÓRICO) son excluyentes. */
export interface BackEdicionSolicitud {
  clave: string | null
  id: number | null
  empleadoId: number | null
  tipoRegistro: string | null
  fechaInicio: string | null
  fechaFin: string | null
  diasDescanso: number | null
  principalClave: string | null
  principalId: number | null
  observacion: string | null
}

/** Cuerpo de POST /api/proyectos/{id}/personal[/previsualizar]. versionProyecto: obligatorio al registrar (TAREA-19x). */
export interface SolicitudActualizarPersonal {
  principales: PrincipalEdicionSolicitud[] | null
  backs: BackEdicionSolicitud[] | null
  versionProyecto?: number | null
}

/** PersonaCambioDto. clase = HISTORICO | VIGENTE | NUEVO; accion = SIN_CAMBIO | MODIFICADO | ELIMINADO | NUEVO. */
export interface PersonaCambioEdicion {
  clave: string
  id: number | null
  rol: string
  numero: number
  empleado: EmpleadoEdicion
  clase: string
  accion: string
}

/** 200 de POST /api/proyectos/{id}/personal/previsualizar. Cruces con origen INTERNO | HISTORICO | EXTERNO. */
export interface PrevisualizacionPersonal {
  corte: string
  personal: PersonaCambioEdicion[]
  tramos: TramoCronograma[]
  cruces: CruceAsignacion[]
  resumen: ResumenCruce[]
  advertencias: string[]
  /** Token de concurrencia (TAREA-19x): se envía al registrar. */
  versionProyecto: number
}

/** 200 de POST /api/proyectos/{id}/personal. 409 con `extensions.cruces` = cruces; 409 sin ellas = proyecto cambiado. */
export interface PersonalActualizado {
  id: number
  version: number
}

// ------------------------------------------------------------------ Reactivación (contrato TAREA-17b; pantalla en la TAREA-19c)
// Origen: backend/src/App.Application/Proyectos/Reactivacion/ReactivacionDtos.cs y ReactivacionContratos.cs.

/** activo = false: se propone con advertencia y el registro lo rechazará. */
export interface EmpleadoPropuesto {
  id: number
  codigoEkon: string
  nombreCompleto: string
  activo: boolean
}

/** Principal propuesto (R7): mismo empleado, jornada y cargo; las fechas las pone el formulario. */
export interface PrincipalPropuesto {
  empleado: EmpleadoPropuesto
  jornada: string | null
  cargo: string | null
}

/** PersonaReactivacionDto: todas quedan históricas al reactivar. */
export interface PersonaReactivacion {
  id: number
  rol: string
  numero: number
  empleado: EmpleadoEdicion
  jornada: string | null
  fechaInicio: string
  fechaFin: string
  tipoRegistro: string
  diasDescanso: number
  esPrincipalInicial: boolean
}

/** GET /api/proyectos/{id}/reactivacion (ReactivacionDto). fechaMinima = fechaFinActual + 1. */
export interface Reactivacion {
  id: number
  codigo: string
  estadoActual: string
  fechaInicio: string
  fechaFinActual: string
  fechaMinima: string
  puedeReactivar: boolean
  motivo: string | null
  principalPropuesto: PrincipalPropuesto | null
  personal: PersonaReactivacion[]
  limites: LimitesEdicion
  advertencias: string[]
  /** Token de concurrencia (TAREA-19x). */
  versionProyecto: number
}

/** Cuerpo de POST /api/proyectos/{id}/reactivacion[/previsualizar]. fecha = R; todas las personas son nuevas (sin id). */
export interface SolicitudReactivar {
  fecha: string | null
  fechaFin: string | null
  principales: PrincipalEdicionSolicitud[] | null
  backs: BackEdicionSolicitud[] | null
  versionProyecto?: number | null
}

/** ActividadReactivacionDto: fila REACTIVACION que se creará, de R a la nueva fecha fin. */
export interface ActividadReactivacion {
  codigo: string
  descripcion: string | null
  tipo: string | null
  fechaInicio: string
  fechaFin: string
}

/** 200 de POST /api/proyectos/{id}/reactivacion/previsualizar. corte = R. */
export interface PrevisualizacionReactivacion {
  corte: string
  fechaFinActual: string
  fechaFinNueva: string
  actividad: ActividadReactivacion | null
  personal: PersonaCambioEdicion[]
  tramos: TramoCronograma[]
  cruces: CruceAsignacion[]
  resumen: ResumenCruce[]
  advertencias: string[]
  /** Token de concurrencia (TAREA-19x): se envía al registrar. */
  versionProyecto: number
}

/** 200 de POST /api/proyectos/{id}/reactivacion. */
export interface ProyectoReactivado {
  id: number
  estado: string
  version: number
}
