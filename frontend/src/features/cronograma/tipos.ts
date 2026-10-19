// GET /api/cronograma y GET /api/proyectos/{id}/cronograma (TAREA-20). Copiados de
// backend/src/App.Application/Cronograma/CronogramaContratos.cs. Fechas "yyyy-MM-dd"; horas "HH:mm:ss".

export type EstadoCelda = 'NOVEDAD' | 'CHOQUE' | 'PROYECTO' | 'OTRO_PROYECTO' | 'DESCANSO'
export type RolCelda = 'PRINCIPAL' | 'BACK' | 'DESCANSO'
export type AplicacionNovedad = 'PERSONA' | 'GENERAL' | 'PROYECTO'

export interface HorarioCronograma {
  codigo: number | null
  descripcion: string | null
  entrada: string | null
  salida: string | null
  /** Salida anterior a la entrada: las horas después de las 00:00 pertenecen al día de entrada (pendiente 50). */
  cruzaMedianoche: boolean
}

export interface ProyectoCronograma {
  id: number
  codigo: string
  nombre: string
  estado: string
  grupo: string
  companiaId: number
  proyectoErpId: string | null
  dimensionUegpId: string | null
  horario: HorarioCronograma
  /** null = horario sin almuerzo (TAREA-26c). */
  almuerzo: { salida: string | null; regreso: string | null }
}

export interface AsignacionCelda {
  proyectoId: number
  rol: RolCelda
  actividad: string | null
}

export interface NovedadCelda {
  tipo: string
  sigla: string
  color: string
  aplicacion: AplicacionNovedad
  origen: string
  estadoReporte: string
  observacionReporte: string
}

export interface CeldaCronograma {
  fecha: string
  estado: EstadoCelda
  asignaciones: AsignacionCelda[]
  /** D2: días de trabajo en proyectos que no vienen en la respuesta (sin sus datos). */
  otrosProyectos: number
  novedad: NovedadCelda | null
  /** D9: otras novedades PERSONA del mismo día. */
  novedadesExtra: number
}

export interface PersonaCronograma {
  codigoEkon: string
  nombre: string
  asignadoEn: { proyectoId: number; cargo: string | null }[]
  /** Dispersa (D7): solo los días que no son LIBRE. */
  dias: CeldaCronograma[]
}

export interface LeyendaNovedad {
  tipo: string
  sigla: string
  color: string
}

export interface Cronograma {
  desde: string
  hasta: string
  maxDias: number
  proyectos: ProyectoCronograma[]
  personas: PersonaCronograma[]
  leyendaNovedades: LeyendaNovedad[]
  /** D5 revisada (TAREA-21): el rango superaba maxDias y `hasta` es el fin recortado (desde + maxDias − 1). */
  recortado: boolean
  /** Fin pedido (o el fin del proyecto con D12) cuando hubo recorte; null si no. */
  hastaSolicitado: string | null
}
