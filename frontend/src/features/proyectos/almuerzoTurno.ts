// Almuerzo según el turno (TAREA-26c, pendiente 42). Lógica pura que replica ReglasAlmuerzo / ReglasHorarioAlmuerzo
// del servidor, que tiene la última palabra. Turno diurno: RN09 sin cambios (opciones del endpoint). Turno nocturno
// (salida < entrada; la salida 00:00 es el fin del día): almuerzo dentro del turno en tiempo circular, en horas en punto
// y sin cruzar la medianoche.
import type { HorarioErp } from './tipos'

/** Horas del horario ("HH:mm" o "HH:mm:ss") y minutos del ERP. */
export type Turno = Pick<HorarioErp, 'horaEntrada' | 'horaSalida' | 'minutosJornada' | 'minutosTrabajados'>

export interface OpcionesAlmuerzo {
  salida: string[]
  regreso: string[]
}

export interface ErroresAlmuerzo {
  salidaAlmuerzo?: string
  regresoAlmuerzo?: string
}

const MINUTOS_DIA = 24 * 60

/** "HH:mm" o "HH:mm:ss" → minutos desde las 00:00; null si falta o es ilegible. */
export function aMinutos(hora: string | null | undefined): number | null {
  const partes = /^(\d{1,2}):(\d{2})/.exec(hora ?? '')
  return partes ? Number(partes[1]) * 60 + Number(partes[2]) : null
}

function aTexto(minutos: number): string {
  const h = Math.floor(minutos / 60)
  return `${String(h).padStart(2, '0')}:${String(minutos % 60).padStart(2, '0')}`
}

/** Entrada y salida del turno nocturno en minutos; null si el turno es diurno o le faltan horas. */
function nocturno(turno: Turno | undefined): { entrada: number; fin: number } | null {
  const entrada = aMinutos(turno?.horaEntrada)
  const fin = aMinutos(turno?.horaSalida)
  return entrada !== null && fin !== null && fin < entrada ? { entrada, fin } : null
}

export function esNocturno(turno: Turno | undefined): boolean {
  return nocturno(turno) !== null
}

/** Sin almuerzo obligatorio: el ERP no descuenta tiempo (minutos de jornada = minutos trabajados). */
export function almuerzoOpcional(turno: Turno | undefined): boolean {
  return turno?.minutosJornada != null && turno.minutosJornada === turno.minutosTrabajados
}

/** "(22:00–06:00)" para los mensajes. */
export function textoTurno(turno: Turno | undefined): string {
  const t = nocturno(turno)
  return t ? `${aTexto(t.entrada)}–${aTexto(t.fin)}` : ''
}

const posicion = (hora: number, entrada: number) => (hora - entrada + MINUTOS_DIA) % MINUTOS_DIA
const salidaEnTurno = (s: number, t: { entrada: number; fin: number }) => posicion(s, t.entrada) < posicion(t.fin, t.entrada)
const regresoEnTurno = (r: number, t: { entrada: number; fin: number }) => {
  const p = posicion(r, t.entrada)
  return p > 0 && p <= posicion(t.fin, t.entrada)
}
// Regreso posterior en el eje del turno y sin cruzar la medianoche (CK_Proyecto_Almuerzo: regreso > salida).
const parValido = (s: number, r: number, t: { entrada: number; fin: number }) =>
  posicion(r, t.entrada) > posicion(s, t.entrada) && r > s

/**
 * Opciones de almuerzo. Diurno: las del endpoint (RN09). Nocturno: horas en punto dentro del turno, desde la entrada;
 * solo las salidas que tienen algún regreso válido y los regresos que tienen alguna salida válida.
 */
export function opcionesAlmuerzo(turno: Turno | undefined, diurnas: OpcionesAlmuerzo): OpcionesAlmuerzo {
  const t = nocturno(turno)
  if (!t) {
    return diurnas
  }
  const horas = Array.from({ length: 24 }, (_, i) => i * 60).sort((a, b) => posicion(a, t.entrada) - posicion(b, t.entrada))
  const salidas = horas.filter((s) => salidaEnTurno(s, t))
  const regresos = horas.filter((r) => regresoEnTurno(r, t))
  return {
    salida: salidas.filter((s) => regresos.some((r) => parValido(s, r, t))).map(aTexto),
    regreso: regresos.filter((r) => salidas.some((s) => parValido(s, r, t))).map(aTexto),
  }
}

/** Errores de almuerzo con los mismos textos del servidor (ayudas: no bloquean el envío). */
export function validarAlmuerzo(turno: Turno | undefined, salida: string | null, regreso: string | null): ErroresAlmuerzo {
  const s = aMinutos(salida)
  const r = aMinutos(regreso)
  const t = nocturno(turno)
  if (!t) {
    // "HH:mm" se compara como texto (RN09 diurna; los rangos ya los imponen las opciones).
    return salida !== null && regreso !== null && regreso <= salida
      ? { regresoAlmuerzo: 'El regreso de almuerzo debe ser posterior a la salida.' }
      : {}
  }
  const fuera = `El almuerzo debe estar dentro del turno (${textoTurno(turno)}).`
  const errores: ErroresAlmuerzo = {}
  if (s !== null && !salidaEnTurno(s, t)) {
    errores.salidaAlmuerzo = fuera
  }
  if (r !== null && !regresoEnTurno(r, t)) {
    errores.regresoAlmuerzo = fuera
  }
  if (s !== null && r !== null && !errores.salidaAlmuerzo && !errores.regresoAlmuerzo) {
    if (posicion(r, t.entrada) <= posicion(s, t.entrada)) {
      errores.regresoAlmuerzo = 'El regreso de almuerzo debe ser posterior a la salida.'
    } else if (r <= s) {
      errores.regresoAlmuerzo = 'El almuerzo no puede cruzar la medianoche.'
    }
  }
  return errores
}

/**
 * Al cambiar el horario se limpia (null) la hora que deja de ser válida para el nuevo turno: diurno, fuera del rango de
 * las opciones RN09 (se conservan horas guardadas como 13:30); nocturno, fuera del turno.
 */
export function almuerzoParaTurno(
  turno: Turno | undefined,
  diurnas: OpcionesAlmuerzo,
  salida: string | null,
  regreso: string | null,
): { salidaAlmuerzo: string | null; regresoAlmuerzo: string | null } {
  const t = nocturno(turno)
  const enRango = (hora: string, opciones: string[]) => opciones.length > 0 && hora >= opciones[0] && hora <= opciones[opciones.length - 1]
  const s = aMinutos(salida)
  const r = aMinutos(regreso)
  return {
    salidaAlmuerzo: salida !== null && s !== null && (t ? salidaEnTurno(s, t) : enRango(salida, diurnas.salida)) ? salida : null,
    regresoAlmuerzo: regreso !== null && r !== null && (t ? regresoEnTurno(r, t) : enRango(regreso, diurnas.regreso)) ? regreso : null,
  }
}
