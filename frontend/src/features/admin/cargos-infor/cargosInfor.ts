// Cargos Infor (TAREA-25-3-2, FASE_5_Usuarios_Departamentos §10): tipos, filtros de la URL, edición del código Infor,
// insignia «Afecta al reporte», textos de la importación e interpretación de errores. Lógica pura, sin React.
// Los logs del navegador no llevan nombres de puestos ni códigos (como el backend).
import { ErrorApi } from '../../../api/errores'
import { formatearHoraUtc } from '../../../utils/formato'
import { type ErroresDialogo, interpretarErrorDialogo } from '../../proyectos/erroresDialogo'

// ------------------------------------------------------------------ contratos (/api/admin/cargos-infor)

export interface CargoInfor {
  id: number
  cargo: string
  codigoInfor: string | null
  activo: boolean
  /** Filas de Empleado con ese puesto en cualquier proyecto (aproximación de lo que verá el reporte). */
  personasGuardadas: number
  /** Activos del ERP con ese puesto, desde la caché; null si no hay lista utilizable. */
  activosErp: number | null
}

export interface VistaPreviaImportacion {
  puestosApi: number
  nuevos: number
  yaExistentes: number
  existentesInactivos: number
  fusionados: number
  gruposVariantes: number
  /** Hasta 20 grupos de puestos que solo difieren en tildes o espacios internos. */
  variantes: string[][]
  /** DateTimeOffset serializado ("…+00:00"). */
  obtenidaUtc: string
  esAnterior: boolean
}

export interface ResultadoImportacion {
  puestosApi: number
  insertados: number
  yaExistentes: number
  existentesInactivos: number
}

export interface SolicitudEditarCargo {
  codigoInfor: string | null
  activo: boolean
  codigoInforAnterior: string | null
  activoAnterior: boolean
}

export interface CargoEditado {
  id: number
  codigoInfor: string | null
  activo: boolean
}

// ------------------------------------------------------------------ textos

export const LARGO_CODIGO = 20 // CargoInfor.CodigoInfor VARCHAR(20)
export const MENSAJE_CODIGO_LARGO = 'El código Infor supera los 20 caracteres.'
export const MENSAJE_CODIGO_FORMATO = 'El código Infor solo admite letras, números, punto, guion y guion bajo.'
export const MENSAJE_SIN_CAMBIOS = 'No hay cambios para registrar.'
export const MENSAJE_CARGO_NO_ENCONTRADO = 'El cargo no existe.'
export const TEXTO_CODIGO_INFOR =
  'El código Infor se usa en la columna CARGO INFOR del reporte Excel. Se compara con el puesto del empleado tal como viene del ERP: las tildes y los espacios cuentan.'
export const TEXTO_CONFIRMAR_BAJA = 'El reporte dejará de mostrar el código Infor de este cargo.'
export const TEXTO_AFECTA_REPORTE =
  'Hay personas guardadas con este puesto y el reporte no mostrará su código Infor (sin código o cargo inactivo).'
export const NOTA_IMPORTACION = 'La importación lee el ERP en este momento; los conteos pueden variar.'
export const AVISO_LISTA_ANTERIOR =
  'El ERP no respondió: la vista previa usa la última lista obtenida, de antes de la falla.'
export const TEXTO_VARIANTES =
  'Estos puestos solo difieren en tildes o espacios internos. Se guardan como cargos distintos y cada uno necesita su propio código.'
export const AVISO_GUARDADO = 'Cargo actualizado.'

// ------------------------------------------------------------------ filtros de la URL (patrón de usuariosAdmin.ts)

export type FiltroCodigo = 'todos' | 'sinCodigo' | 'conCodigo'

export const FILTROS_CODIGO: readonly { valor: FiltroCodigo; etiqueta: string }[] = [
  { valor: 'sinCodigo', etiqueta: 'Sin código' },
  { valor: 'conCodigo', etiqueta: 'Con código' },
  { valor: 'todos', etiqueta: 'Todos' },
]

/** La pantalla abre en «Sin código» (la API, sin parámetro, usa «todos»: por eso a la API se envía siempre). */
export const FILTRO_POR_DEFECTO: FiltroCodigo = 'sinCodigo'
export const PAGINA_POR_DEFECTO = 1
export const TAMANO_PAGINA = 25 // el de la API

export interface FiltrosCargos {
  texto?: string
  filtro: FiltroCodigo
  incluirInactivos: boolean
  pagina: number
}

function esFiltro(valor: string | null): valor is FiltroCodigo {
  return valor === 'todos' || valor === 'sinCodigo' || valor === 'conCodigo'
}

/** URL → filtros. Filtro desconocido → «Sin código»; página ilegible o < 1 → 1; texto vacío → sin filtro. */
export function leerFiltrosCargos(parametros: URLSearchParams): FiltrosCargos {
  const texto = parametros.get('texto')?.trim()
  const filtro = parametros.get('filtro')
  const pagina = Number(parametros.get('pagina'))
  return {
    ...(texto ? { texto } : {}),
    filtro: esFiltro(filtro) ? filtro : FILTRO_POR_DEFECTO,
    incluirInactivos: parametros.get('incluirInactivos') === 'true',
    pagina: parametros.get('pagina') !== null && Number.isInteger(pagina) && pagina >= 1 ? pagina : PAGINA_POR_DEFECTO,
  }
}

/** Filtros → query string de la URL (omite vacíos y valores por defecto). */
export function aParametrosUrlCargos(f: FiltrosCargos): URLSearchParams {
  const parametros = new URLSearchParams()
  if (f.texto?.trim()) {
    parametros.set('texto', f.texto.trim())
  }
  if (f.filtro !== FILTRO_POR_DEFECTO) {
    parametros.set('filtro', f.filtro)
  }
  if (f.incluirInactivos) {
    parametros.set('incluirInactivos', 'true')
  }
  if (f.pagina !== PAGINA_POR_DEFECTO) {
    parametros.set('pagina', String(f.pagina))
  }
  return parametros
}

/** Filtros → query string de la API (siempre filtro, pagina y tamano). */
export function aConsultaApiCargos(f: FiltrosCargos): string {
  const parametros = aParametrosUrlCargos(f)
  parametros.set('filtro', f.filtro)
  parametros.set('pagina', String(f.pagina))
  parametros.set('tamano', String(TAMANO_PAGINA))
  return parametros.toString()
}

// ------------------------------------------------------------------ código Infor (F2, como ReglasCargoInfor)

const PATRON_CODIGO = /^[A-Z0-9._-]+$/

/** Recortado y en mayúsculas; vacío = null (quitar el código). */
export function normalizarCodigo(codigo: string | null | undefined): string | null {
  const recortado = codigo?.trim() ?? ''
  return recortado ? recortado.toUpperCase() : null
}

/** Mensaje de error del código ya normalizado (undefined = válido o sin código). */
export function validarCodigo(codigo: string | null): string | undefined {
  if (codigo === null) {
    return undefined
  }
  if (codigo.length > LARGO_CODIGO) {
    return MENSAJE_CODIGO_LARGO
  }
  return PATRON_CODIGO.test(codigo) ? undefined : MENSAJE_CODIGO_FORMATO
}

/** Insignia «Afecta al reporte» (P1): hay personas guardadas y el join del reporte no da código (sin código o inactivo). */
export function afectaAlReporte(c: CargoInfor): boolean {
  return c.personasGuardadas > 0 && (normalizarCodigo(c.codigoInfor) === null || !c.activo)
}

// ------------------------------------------------------------------ diálogo de edición

export interface EstadoEdicionCargo {
  /** Valores de la fila que abrió el diálogo: son codigoInforAnterior y activoAnterior del PUT. */
  codigoAnterior: string | null
  activoAnterior: boolean
  /** Texto tal como se escribe; se normaliza al comparar, mostrar y enviar. */
  codigo: string
  activo: boolean
}

export function estadoEdicionCargo(c: CargoInfor): EstadoEdicionCargo {
  return { codigoAnterior: c.codigoInfor, activoAnterior: c.activo, codigo: c.codigoInfor ?? '', activo: c.activo }
}

export function hayCambiosCargo(e: EstadoEdicionCargo): boolean {
  return normalizarCodigo(e.codigo) !== normalizarCodigo(e.codigoAnterior) || e.activo !== e.activoAnterior
}

/** Motivo por el que «Guardar» está deshabilitado (undefined = se puede guardar). */
export function motivoSinGuardarCargo(e: EstadoEdicionCargo): string | undefined {
  return validarCodigo(normalizarCodigo(e.codigo)) ?? (hayCambiosCargo(e) ? undefined : MENSAJE_SIN_CAMBIOS)
}

const textoCargo = (codigo: string | null, activo: boolean) => `${codigo ?? '(sin código)'} (${activo ? 'Activo' : 'Inactivo'})`

/** «Se guardará: (sin código) (Activo) → P0021 (Activo)». */
export function resumenCargo(e: EstadoEdicionCargo): string {
  return `Se guardará: ${textoCargo(normalizarCodigo(e.codigoAnterior), e.activoAnterior)} → ${textoCargo(normalizarCodigo(e.codigo), e.activo)}`
}

/** true si el guardado da de baja el cargo (pide confirmación). */
export function esBajaCargo(e: EstadoEdicionCargo): boolean {
  return e.activoAnterior && !e.activo
}

export function aSolicitudCargo(e: EstadoEdicionCargo): SolicitudEditarCargo {
  return {
    codigoInfor: normalizarCodigo(e.codigo),
    activo: e.activo,
    codigoInforAnterior: e.codigoAnterior,
    activoAnterior: e.activoAnterior,
  }
}

// ------------------------------------------------------------------ diálogo de importación

/** «Lista de las HH:mm» en hora de Ecuador. */
export function textoListaDe(v: VistaPreviaImportacion): string {
  return `Lista de las ${formatearHoraUtc(v.obtenidaUtc)}`
}

/** «y N más» cuando la respuesta trae menos grupos que el total. */
export function textoMasVariantes(v: VistaPreviaImportacion): string | undefined {
  const resto = v.gruposVariantes - v.variantes.length
  return resto > 0 ? `y ${resto} más` : undefined
}

// ------------------------------------------------------------------ errores

export const CAMPOS_CARGO = ['codigoInfor', 'activo'] as const
export type CampoCargo = (typeof CAMPOS_CARGO)[number]

const errorInesperado = <C extends string>(): ErroresDialogo<C> => ({
  campos: {},
  generales: ['Ocurrió un error inesperado.'],
  ofrecerRecarga: false,
  ofrecerReintento: true,
  noEncontrado: false,
})

/**
 * PUT: 400 → bajo su campo (la clave `anterior` ofrece «Recargar»); 409 → el title con «Recargar»; 404 → «El cargo no
 * existe.» con «Recargar» (cierra el diálogo y vuelve a leer la lista); 403 → el title sin «Reintentar»; 503 y red →
 * «Reintentar».
 */
export function interpretarErrorCargo(error: unknown): ErroresDialogo<CampoCargo> {
  if (!(error instanceof ErrorApi)) {
    return errorInesperado()
  }
  const base = interpretarErrorDialogo(error, { campos: CAMPOS_CARGO, clavesRecarga: ['anterior'] })
  return error.tipo === 'noEncontrado' ? { ...base, generales: [MENSAJE_CARGO_NO_ENCONTRADO], ofrecerRecarga: true } : base
}

/**
 * Vista previa e importación: el 409 (otra alta al mismo tiempo o puestos que la base considera iguales) no es un cambio
 * de datos que haya que recargar, así que ofrece «Reintentar», como dice su mensaje. 503 y red → «Reintentar».
 */
export function interpretarErrorImportacion(error: unknown): ErroresDialogo<never> {
  if (!(error instanceof ErrorApi)) {
    return errorInesperado()
  }
  const base = interpretarErrorDialogo<never>(error, { campos: [] })
  return error.tipo === 'conflicto' ? { ...base, ofrecerRecarga: false, ofrecerReintento: true } : base
}
