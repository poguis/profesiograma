// Vista del cronograma (TAREA-21). Lógica pura, sin React: se prueba con Vitest. La respuesta de la API es dispersa
// (D7): los días LIBRE no vienen y se expanden aquí. Ver docs/fases/FASE_5_Cronograma.md §7.
import { formatearFecha } from '../../utils/formato'
import type { CeldaCronograma, Cronograma, PersonaCronograma, ProyectoCronograma } from './tipos'

// ------------------------------------------------------------------ fechas ("yyyy-MM-dd", sin zona horaria)

const PATRON_ISO = /^(\d{4})-(\d{2})-(\d{2})$/
const MS_DIA = 86_400_000

function aUtc(fecha: string): number {
  const p = PATRON_ISO.exec(fecha)
  if (!p) {
    throw new Error(`Fecha no válida: ${fecha}`)
  }
  return Date.UTC(Number(p[1]), Number(p[2]) - 1, Number(p[3]))
}

function aIso(ms: number): string {
  return new Date(ms).toISOString().slice(0, 10)
}

/** Todos los días de [desde, hasta] (vacío si hasta < desde). */
export function expandirDias(desde: string, hasta: string): string[] {
  const dias: string[] = []
  for (let ms = aUtc(desde), fin = aUtc(hasta); ms <= fin; ms += MS_DIA) {
    dias.push(aIso(ms))
  }
  return dias
}

export function esFinDeSemana(fecha: string): boolean {
  const dia = new Date(aUtc(fecha)).getUTCDay()
  return dia === 0 || dia === 6
}

// Como el original (l. 832): la inicial, salvo martes y miércoles, que se abrevian para distinguirlos.
const INICIALES = ['D', 'L', 'Mar', 'Mié', 'J', 'V', 'S'] as const

export function inicialDia(fecha: string): string {
  return INICIALES[new Date(aUtc(fecha)).getUTCDay()]
}

export function numeroDia(fecha: string): number {
  return Number(fecha.slice(8, 10))
}

const NOMBRES_MES = ['enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio', 'julio', 'agosto', 'septiembre', 'octubre',
  'noviembre', 'diciembre'] as const

/** Tramos de meses del rango para la banda superior del encabezado: { mes: "octubre 2026", dias }. */
export function mesesDelRango(dias: readonly string[]): { mes: string; dias: number }[] {
  const tramos: { mes: string; dias: number }[] = []
  for (const dia of dias) {
    const mes = `${NOMBRES_MES[Number(dia.slice(5, 7)) - 1]} ${dia.slice(0, 4)}`
    const ultimo = tramos.at(-1)
    if (ultimo?.mes === mes) {
      ultimo.dias++
    } else {
      tramos.push({ mes, dias: 1 })
    }
  }
  return tramos
}

/** Mes completo que contiene la fecha. */
export function mesDe(fecha: string): { desde: string; hasta: string } {
  const anio = Number(fecha.slice(0, 4))
  const mes = Number(fecha.slice(5, 7)) - 1
  return { desde: aIso(Date.UTC(anio, mes, 1)), hasta: aIso(Date.UTC(anio, mes + 1, 0)) }
}

export function mesAnterior(fecha: string): { desde: string; hasta: string } {
  const anio = Number(fecha.slice(0, 4))
  const mes = Number(fecha.slice(5, 7)) - 1
  return mesDe(aIso(Date.UTC(anio, mes - 1, 1)))
}

export function mesSiguiente(fecha: string): { desde: string; hasta: string } {
  const anio = Number(fecha.slice(0, 4))
  const mes = Number(fecha.slice(5, 7)) - 1
  return mesDe(aIso(Date.UTC(anio, mes + 1, 1)))
}

/**
 * Fechas que se escriben en la URL al cambiar UNA desde su selector: la cambiada y la otra con el valor que el usuario
 * ve. Si solo quedara una fecha en la URL, el backend aplicaría D6 (el mes de esa fecha) y movería la que no tocó.
 * La otra fecha: la de la URL; si no, la de la última respuesta (para "hasta", la SOLICITADA si hubo recorte, D5
 * revisada); si todavía no hay respuesta, la del mes actual en Ecuador (como D6).
 */
export function fechasAlCambiar(
  campo: 'desde' | 'hasta',
  valor: string | undefined,
  url: { desde?: string; hasta?: string },
  respuesta: Pick<Cronograma, 'desde' | 'hasta' | 'hastaSolicitado'> | undefined,
  hoy: string,
): { desde?: string; hasta?: string } {
  const mes = mesDe(hoy)
  const otra =
    campo === 'desde'
      ? (url.hasta ?? respuesta?.hastaSolicitado ?? respuesta?.hasta ?? mes.hasta)
      : (url.desde ?? respuesta?.desde ?? mes.desde)
  return campo === 'desde' ? { desde: valor, hasta: otra } : { desde: otra, hasta: valor }
}

/** D5 revisada (TAREA-21): aviso cuando el backend recortó el rango a los primeros maxDias días; undefined si no. */
export function textoAvisoRecorte(c: Pick<Cronograma, 'recortado' | 'maxDias' | 'desde' | 'hasta' | 'hastaSolicitado'>): string | undefined {
  if (!c.recortado) {
    return undefined
  }
  const solicitado = c.hastaSolicitado ? ` (solicitado hasta el ${formatearFecha(c.hastaSolicitado)})` : ''
  return `El rango supera ${c.maxDias} días. Se muestra del ${formatearFecha(c.desde)} al ${formatearFecha(c.hasta)}${solicitado}.`
}

// ------------------------------------------------------------------ celdas y personas

/** Fecha → celda de la persona (las fechas ausentes son LIBRE). */
export function indexarCeldas(dias: readonly CeldaCronograma[]): Map<string, CeldaCronograma> {
  return new Map(dias.map((d) => [d.fecha, d]))
}

/** Minúsculas, sin tildes y con un solo espacio entre palabras. */
export function normalizarTexto(texto: string): string {
  return texto.normalize('NFD').replace(/\p{M}/gu, '').toLowerCase().replace(/\s+/g, ' ').trim()
}

/** P9: por nombre o código EKON, sin tildes ni mayúsculas. Texto vacío = todas. */
export function filtrarPersonas(personas: readonly PersonaCronograma[], texto: string | undefined): PersonaCronograma[] {
  const buscado = normalizarTexto(texto ?? '')
  if (buscado === '') {
    return [...personas]
  }
  return personas.filter(
    (p) => normalizarTexto(p.nombre).includes(buscado) || normalizarTexto(p.codigoEkon).includes(buscado),
  )
}

// P7: prefijo de NombreVisualProyecto para PLANTA y OFICINAS ADMINISTRATIVAS (backend: PrefijoDimension).
const PREFIJOS_NOMBRE = ['PLANTA - ']

/** P7: 2 caracteres del nombre del proyecto, sin el prefijo "PLANTA - ". */
export function abreviatura(nombre: string): string {
  let base = nombre.trim()
  for (const prefijo of PREFIJOS_NOMBRE) {
    if (base.toUpperCase().startsWith(prefijo)) {
      base = base.slice(prefijo.length).trim()
    }
  }
  return base.slice(0, 2).toUpperCase()
}

/** Días de trabajo de la celda: asignaciones visibles con rol ≠ DESCANSO + días en otros proyectos (D2). */
export function totalTrabajo(celda: CeldaCronograma): number {
  return celda.asignaciones.filter((a) => a.rol !== 'DESCANSO').length + celda.otrosProyectos
}

export interface TextoCelda {
  texto: string
  /** "+n" de novedadesExtra (D9). */
  extra?: string
}

export function textoCelda(celda: CeldaCronograma, proyectos: ReadonlyMap<number, ProyectoCronograma>): TextoCelda {
  const nombre = (proyectoId: number | undefined) =>
    proyectoId === undefined ? '' : abreviatura(proyectos.get(proyectoId)?.nombre ?? '')
  switch (celda.estado) {
    case 'NOVEDAD':
      return { texto: celda.novedad?.sigla ?? '', extra: celda.novedadesExtra > 0 ? `+${celda.novedadesExtra}` : undefined }
    case 'CHOQUE':
      return { texto: `C${totalTrabajo(celda)}` }
    case 'PROYECTO':
      return { texto: nombre(celda.asignaciones.find((a) => a.rol !== 'DESCANSO')?.proyectoId) }
    case 'DESCANSO':
      return { texto: `D-${nombre(celda.asignaciones[0]?.proyectoId)}` }
    case 'OTRO_PROYECTO':
      return { texto: '' }
  }
}

// ------------------------------------------------------------------ colores y contraste

export const COLOR_CHOQUE = '#201F1E'
/** Proporción del color del proyecto en el DESCANSO (P6); el resto es blanco. */
export const ATENUACION_DESCANSO = 0.35

function canales(hex: string): [number, number, number] {
  const n = parseInt(hex.replace('#', ''), 16)
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255]
}

function luminancia(hex: string): number {
  const [r, g, b] = canales(hex).map((v) => {
    const c = v / 255
    return c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4
  })
  return 0.2126 * r + 0.7152 * g + 0.0722 * b
}

/** Texto negro o blanco, el de mayor contraste WCAG con el fondo (≥ 4,58 con cualquier fondo opaco). */
export function contrasteTexto(fondo: string): { color: '#000000' | '#FFFFFF'; contraste: number } {
  const l = luminancia(fondo)
  const conNegro = (l + 0.05) / 0.05
  const conBlanco = 1.05 / (l + 0.05)
  return conNegro >= conBlanco ? { color: '#000000', contraste: conNegro } : { color: '#FFFFFF', contraste: conBlanco }
}

/** Mezcla el color con blanco: `proporcion` del color (P6: 35 %). */
export function atenuar(hex: string, proporcion: number): string {
  const aHex = (v: number) => Math.round(v).toString(16).padStart(2, '0')
  return `#${canales(hex).map((v) => aHex(v * proporcion + 255 * (1 - proporcion))).join('')}`.toUpperCase()
}

export type FormaCelda = 'solido' | 'ficha' | 'rayado'

export interface AparienciaCelda {
  forma: FormaCelda
  fondo: string
  texto: string
}

/**
 * Apariencia por estado (P2): PROYECTO relleno sólido del color del proyecto; NOVEDAD ficha con el color de la
 * novedad; DESCANSO color del proyecto atenuado al 35 %; CHOQUE #201F1E; OTRO_PROYECTO rayado gris (sin texto).
 */
export function aparienciaCelda(celda: CeldaCronograma, colores: ReadonlyMap<number, string>): AparienciaCelda {
  const conTexto = (forma: FormaCelda, fondo: string): AparienciaCelda => ({ forma, fondo, texto: contrasteTexto(fondo).color })
  const colorDe = (proyectoId: number | undefined) => (proyectoId === undefined ? undefined : colores.get(proyectoId))
  switch (celda.estado) {
    case 'NOVEDAD':
      return conTexto('ficha', celda.novedad?.color ?? COLOR_CHOQUE)
    case 'CHOQUE':
      return conTexto('solido', COLOR_CHOQUE)
    case 'PROYECTO':
      return conTexto('solido', colorDe(celda.asignaciones.find((a) => a.rol !== 'DESCANSO')?.proyectoId) ?? '#737373')
    case 'DESCANSO':
      return conTexto('solido', atenuar(colorDe(celda.asignaciones[0]?.proyectoId) ?? '#737373', ATENUACION_DESCANSO))
    case 'OTRO_PROYECTO':
      return { forma: 'rayado', fondo: '#E0E0E0', texto: '#000000' }
  }
}

// ------------------------------------------------------------------ detalle (Popover) y accesibilidad

const ESTADOS: Record<CeldaCronograma['estado'], string> = {
  NOVEDAD: 'Novedad',
  CHOQUE: 'Choque',
  PROYECTO: 'Proyecto',
  DESCANSO: 'Descanso del proyecto',
  OTRO_PROYECTO: 'Otro proyecto',
}

const APLICACIONES: Record<string, string> = { PERSONA: 'de la persona', GENERAL: 'general', PROYECTO: 'del proyecto' }

export function nombreEstado(estado: CeldaCronograma['estado']): string {
  return ESTADOS[estado]
}

export interface ResumenCelda {
  /** "dd/MM/yyyy" */
  fecha: string
  estado: string
  asignaciones: string[]
  novedad?: string
  otros?: string
}

/** Contenido del detalle. Nunca datos de los proyectos ajenos (D2): solo su cantidad. */
/**
 * Vista en la que se muestra la celda (TAREA-21b, Q7). 'general': /cronograma (otrosProyectos = proyectos de otros
 * propietarios); 'proyecto': pestaña del detalle (otrosProyectos = todos los demás proyectos).
 */
export type ContextoCronograma = 'general' | 'proyecto'

/**
 * Q3 (TAREA-21b): en la pestaña de un proyecto, el día sin asignación en ESE proyecto es "otro proyecto", también cuando
 * el backend lo marca CHOQUE (choque entre proyectos ajenos, `asignaciones` vacío). Sin proyecto: el estado tal cual.
 */
export function estadoEnContexto(celda: CeldaCronograma, proyectoId?: number): CeldaCronograma['estado'] {
  if (proyectoId === undefined || celda.estado !== 'CHOQUE') {
    return celda.estado
  }
  return celda.asignaciones.some((a) => a.proyectoId === proyectoId) ? 'CHOQUE' : 'OTRO_PROYECTO'
}

export function resumenCelda(
  celda: CeldaCronograma,
  proyectos: ReadonlyMap<number, ProyectoCronograma>,
  contexto: ContextoCronograma = 'general',
): ResumenCelda {
  const [anio, mes, dia] = celda.fecha.split('-')
  const n = celda.novedad
  return {
    fecha: `${dia}/${mes}/${anio}`,
    estado: ESTADOS[celda.estado],
    asignaciones: celda.asignaciones.map((a) => {
      const p = proyectos.get(a.proyectoId)
      return [p ? `${p.codigo} – ${p.nombre}` : `Proyecto ${a.proyectoId}`, a.rol, a.actividad ? `actividad ${a.actividad}` : '']
        .filter(Boolean)
        .join(' · ')
    }),
    novedad: n
      ? `${n.observacionReporte} (${APLICACIONES[n.aplicacion] ?? n.aplicacion}, ${n.origen})` +
        (celda.novedadesExtra > 0 ? ` y ${celda.novedadesExtra} novedad(es) más` : '')
      : undefined,
    otros:
      celda.otrosProyectos === 0
        ? undefined
        : contexto === 'proyecto'
          ? `Asignado además en ${celda.otrosProyectos} otro(s) proyecto(s).`
          : `Asignado además en ${celda.otrosProyectos} proyecto(s) de otros usuarios.`,
  }
}

/** aria-label de la celda: fecha, estado y lo principal. */
export function etiquetaCelda(
  celda: CeldaCronograma,
  proyectos: ReadonlyMap<number, ProyectoCronograma>,
  contexto: ContextoCronograma = 'general',
): string {
  const r = resumenCelda(celda, proyectos, contexto)
  return [r.fecha, r.estado, r.novedad, ...r.asignaciones, r.otros].filter(Boolean).join('. ')
}

// ------------------------------------------------------------------ filtros ↔ URL

/** Filtros tal como viven en la URL. `persona` filtra en el cliente (P9): no va a la API. */
export interface FiltrosCronograma {
  desde?: string
  hasta?: string
  proyectoId?: number
  departamentoId?: number
  persona?: string
}

export function leerFiltrosCronograma(parametros: URLSearchParams): FiltrosCronograma {
  const texto = (clave: string) => parametros.get(clave)?.trim() || undefined
  const id = (clave: string) => {
    const valor = Number(texto(clave))
    return Number.isInteger(valor) && valor > 0 ? valor : undefined
  }
  return { desde: texto('desde'), hasta: texto('hasta'), proyectoId: id('proyectoId'), departamentoId: id('departamentoId'), persona: texto('persona') }
}

export function aParametrosUrl(filtros: FiltrosCronograma): URLSearchParams {
  const parametros = new URLSearchParams()
  for (const [clave, valor] of Object.entries(filtros)) {
    if (valor !== undefined && valor !== '') {
      parametros.set(clave, String(valor))
    }
  }
  return parametros
}

/** Query string para GET /api/cronograma (sin `persona`). */
export function aConsultaApi(filtros: FiltrosCronograma): string {
  const parametros = aParametrosUrl({ ...filtros, persona: undefined })
  return parametros.toString()
}

/** Q9 (TAREA-21b): /cronograma con el proyecto y el rango efectivo de la pestaña. */
export function enlaceCronogramaGeneral(proyectoId: number, desde: string, hasta: string): string {
  return `/cronograma?${aParametrosUrl({ desde, hasta, proyectoId })}`
}

/** P8: opciones del filtro de proyecto = proyectos de la respuesta + el seleccionado (aunque no venga). */
export function opcionesProyecto(proyectos: readonly ProyectoCronograma[], seleccionado: number | undefined): { id: number; texto: string }[] {
  const opciones = proyectos.map((p) => ({ id: p.id, texto: `${p.codigo} – ${p.nombre}` }))
  if (seleccionado !== undefined && !opciones.some((o) => o.id === seleccionado)) {
    opciones.unshift({ id: seleccionado, texto: `Proyecto ${seleccionado}` })
  }
  return opciones
}
