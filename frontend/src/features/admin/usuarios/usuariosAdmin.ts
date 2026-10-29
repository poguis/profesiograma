// Usuarios y departamentos (TAREA-25-2, FASE_5_Usuarios_Departamentos §2–§3): tipos, filtros de la URL, alta, estado de
// las secciones «Datos» y «Departamentos», cuerpos de los PUT e interpretación de errores. Lógica pura, sin React.
// Los correos se muestran en las pantallas (son el identificador), nunca en logs, avisos de error ni títulos.
import { ErrorApi } from '../../../api/errores'
import { type ErroresDialogo, interpretarErrorDialogo } from '../../proyectos/erroresDialogo'

// ------------------------------------------------------------------ contratos (GET/POST/PUT /api/admin/usuarios)

export interface DepartamentoUsuario {
  id: number
  nombreCorto: string
}

export interface UsuarioAdmin {
  id: number
  email: string
  nombreMostrar: string
  activo: boolean
  /** false = «Aún no ha ingresado» (sin EntraObjectId). */
  conIdentidad: boolean
  departamentos: DepartamentoUsuario[]
}

export interface DepartamentoDisponible {
  id: number
  nombre: string
  nombreCorto: string
  activo: boolean
  asignado: boolean
}

export interface UsuarioAdminDetalle extends UsuarioAdmin {
  departamentosDisponibles: DepartamentoDisponible[]
  esUsuarioActual: boolean
}

export interface SolicitudCrearUsuario {
  email: string
  nombreMostrar: string
}

export interface SolicitudEditarUsuario {
  nombreMostrar: string
  activo: boolean
  nombreAnterior: string
  activoAnterior: boolean
}

export interface SolicitudDepartamentos {
  departamentoIds: number[]
  anteriores: number[]
}

// ------------------------------------------------------------------ textos

export const LARGO_NOMBRE = 200
export const MENSAJE_EMAIL_VACIO = 'El correo es obligatorio.'
export const MENSAJE_EMAIL_FORMATO = 'El correo no tiene un formato válido.'
export const MENSAJE_NOMBRE_VACIO = 'El nombre es obligatorio.'
export const MENSAJE_NOMBRE_LARGO = 'El nombre supera los 200 caracteres.'
export const MENSAJE_SIN_CAMBIOS = 'No hay cambios para registrar.'
export const MENSAJE_BAJA_PROPIA = 'No puedes darte de baja a ti mismo.'
export const MENSAJE_USUARIO_NO_ENCONTRADO = 'El usuario no existe.'
export const MENSAJE_DUPLICADO = 'Ya existe un usuario con ese correo.'
export const TEXTO_ROLES =
  'El rol (Gestor o Admin) no se administra aquí: lo asigna TI en Entra ID. Un usuario sin rol no puede entrar aunque esté dado de alta.'
export const TEXTO_CONFIRMAR_BAJA = 'El usuario dejará de poder entrar de inmediato. Sus datos se conservan.'
export const ADVERTENCIA_SIN_DEPARTAMENTOS =
  'Sin departamentos: este usuario ve a todo el personal en el buscador y sus proyectos nuevos no tendrán departamento.'
export const AVISO_CREADO = 'Usuario creado.'
export const AVISO_DATOS = 'Datos actualizados.'
export const AVISO_DEPARTAMENTOS = 'Departamentos actualizados.'

// ------------------------------------------------------------------ filtros de la URL (patrón de novedades/filtrosUrl.ts)

export const PAGINA_POR_DEFECTO = 1
export const TAMANO_PAGINA = 25 // el de la API

export interface FiltrosUsuarios {
  texto?: string
  incluirInactivos: boolean
  pagina: number
}

/** URL → filtros. Página ilegible o < 1 → 1; texto vacío → sin filtro. */
export function leerFiltrosUsuarios(parametros: URLSearchParams): FiltrosUsuarios {
  const texto = parametros.get('texto')?.trim()
  const pagina = Number(parametros.get('pagina'))
  return {
    ...(texto ? { texto } : {}),
    incluirInactivos: parametros.get('incluirInactivos') === 'true',
    pagina: parametros.get('pagina') !== null && Number.isInteger(pagina) && pagina >= 1 ? pagina : PAGINA_POR_DEFECTO,
  }
}

/** Filtros → query string de la URL (omite vacíos y valores por defecto). */
export function aParametrosUrlUsuarios(f: FiltrosUsuarios): URLSearchParams {
  const parametros = new URLSearchParams()
  if (f.texto?.trim()) {
    parametros.set('texto', f.texto.trim())
  }
  if (f.incluirInactivos) {
    parametros.set('incluirInactivos', 'true')
  }
  if (f.pagina !== PAGINA_POR_DEFECTO) {
    parametros.set('pagina', String(f.pagina))
  }
  return parametros
}

/** Filtros → query string de la API (siempre pagina y tamano). */
export function aConsultaApiUsuarios(f: FiltrosUsuarios): string {
  const parametros = aParametrosUrlUsuarios(f)
  parametros.set('pagina', String(f.pagina))
  parametros.set('tamano', String(TAMANO_PAGINA))
  return parametros.toString()
}

// ------------------------------------------------------------------ alta

/** Recorta; el correo en minúsculas (como el backend). */
export function normalizarAlta(email: string, nombreMostrar: string): SolicitudCrearUsuario {
  return { email: email.trim().toLowerCase(), nombreMostrar: nombreMostrar.trim() }
}

// Forma mínima: una sola @ con texto a ambos lados y sin espacios ni «<>». El dominio permitido lo decide el backend.
const PATRON_EMAIL = /^[^\s@<>]+@[^\s@<>]+$/

export type CampoAlta = 'email' | 'nombreMostrar'

/** Lo mismo que valida el backend antes del dominio (sobre la solicitud ya normalizada). */
export function validarAlta(s: SolicitudCrearUsuario): Partial<Record<CampoAlta, string>> {
  const errores: Partial<Record<CampoAlta, string>> = {}
  if (!s.email) {
    errores.email = MENSAJE_EMAIL_VACIO
  } else if (!PATRON_EMAIL.test(s.email)) {
    errores.email = MENSAJE_EMAIL_FORMATO
  }
  const nombre = validarNombre(s.nombreMostrar)
  if (nombre) {
    errores.nombreMostrar = nombre
  }
  return errores
}

function validarNombre(nombre: string): string | undefined {
  if (!nombre) {
    return MENSAJE_NOMBRE_VACIO
  }
  return nombre.length > LARGO_NOMBRE ? MENSAJE_NOMBRE_LARGO : undefined
}

// ------------------------------------------------------------------ sección «Datos»

export interface EstadoDatos {
  /** Valores del GET que abrió la sección: son nombreAnterior y activoAnterior del PUT. */
  nombreAnterior: string
  activoAnterior: boolean
  nombre: string
  activo: boolean
  esUsuarioActual: boolean
}

export function estadoDatos(u: UsuarioAdminDetalle): EstadoDatos {
  return {
    nombreAnterior: u.nombreMostrar,
    activoAnterior: u.activo,
    nombre: u.nombreMostrar,
    activo: u.activo,
    esUsuarioActual: u.esUsuarioActual,
  }
}

export function hayCambiosDatos(e: EstadoDatos): boolean {
  return e.nombre.trim() !== e.nombreAnterior || e.activo !== e.activoAnterior
}

/** Motivo por el que «Guardar datos» está deshabilitado (undefined = se puede guardar). */
export function motivoSinGuardarDatos(e: EstadoDatos): string | undefined {
  return validarNombre(e.nombre.trim()) ?? (hayCambiosDatos(e) ? undefined : MENSAJE_SIN_CAMBIOS)
}

const textoDatos = (nombre: string, activo: boolean) => `${nombre} (${activo ? 'Activo' : 'Inactivo'})`

/** «Se guardará: NOMBRE (Activo) → OTRO (Inactivo)». */
export function resumenDatos(e: EstadoDatos): string {
  return `Se guardará: ${textoDatos(e.nombreAnterior, e.activoAnterior)} → ${textoDatos(e.nombre.trim(), e.activo)}`
}

/** true si el guardado da de baja (pide confirmación). */
export function esBaja(e: EstadoDatos): boolean {
  return e.activoAnterior && !e.activo
}

export function aSolicitudDatos(e: EstadoDatos): SolicitudEditarUsuario {
  return { nombreMostrar: e.nombre.trim(), activo: e.activo, nombreAnterior: e.nombreAnterior, activoAnterior: e.activoAnterior }
}

// ------------------------------------------------------------------ sección «Departamentos»

export interface EstadoDepartamentos {
  /** En el orden del backend (Orden del departamento). */
  disponibles: DepartamentoDisponible[]
  /** Asignados según el GET que abrió la sección: son los `anteriores` del PUT. */
  originales: number[]
  marcados: number[]
}

export function estadoDepartamentos(u: UsuarioAdminDetalle): EstadoDepartamentos {
  const originales = u.departamentosDisponibles.filter((d) => d.asignado).map((d) => d.id)
  return { disponibles: u.departamentosDisponibles, originales, marcados: originales }
}

/** Casilla: el nombre; un inactivo (solo aparece si ya estaba asignado) lleva «(inactivo)». */
export function etiquetaDepartamento(d: DepartamentoDisponible): string {
  return d.activo ? d.nombre : `${d.nombre} (inactivo)`
}

/** Marca o desmarca; los marcados quedan en el orden de `disponibles`. */
export function alternarDepartamento(e: EstadoDepartamentos, id: number): EstadoDepartamentos {
  const marcar = !e.marcados.includes(id)
  const marcados = e.disponibles.map((d) => d.id).filter((d) => (d === id ? marcar : e.marcados.includes(d)))
  return { ...e, marcados }
}

/** Compara conjuntos (sin importar el orden). */
export function hayCambiosDepartamentos(e: EstadoDepartamentos): boolean {
  const antes = new Set(e.originales)
  return e.marcados.length !== antes.size || e.marcados.some((id) => !antes.has(id))
}

export function motivoSinGuardarDepartamentos(e: EstadoDepartamentos): string | undefined {
  return hayCambiosDepartamentos(e) ? undefined : MENSAJE_SIN_CAMBIOS
}

/** «Se guardará: A, B → (sin departamentos)», con los nombres cortos. */
export function resumenDepartamentos(e: EstadoDepartamentos): string {
  const texto = (ids: number[]) =>
    ids.length === 0
      ? '(sin departamentos)'
      : e.disponibles
          .filter((d) => ids.includes(d.id))
          .map((d) => d.nombreCorto)
          .join(', ')
  return `Se guardará: ${texto(e.originales)} → ${texto(e.marcados)}`
}

/** Advertencia (pendiente 72) si el conjunto GUARDADO está vacío. */
export function advertenciaDepartamentos(asignados: readonly unknown[]): string | undefined {
  return asignados.length === 0 ? ADVERTENCIA_SIN_DEPARTAMENTOS : undefined
}

export function aSolicitudDepartamentos(e: EstadoDepartamentos): SolicitudDepartamentos {
  return { departamentoIds: [...e.marcados], anteriores: [...e.originales] }
}

// ------------------------------------------------------------------ errores

export const CAMPOS_ALTA = ['email', 'nombreMostrar'] as const
export const CAMPOS_DATOS = ['nombreMostrar', 'activo'] as const
export const CAMPOS_DEPARTAMENTOS = ['departamentoIds'] as const

export interface ErroresUsuario<C extends string> extends ErroresDialogo<C> {
  /** 409 del alta: Id del usuario con ese correo («Abrir usuario»), si el backend lo envía. */
  usuarioExistenteId?: number
}

/**
 * 400 → bajo su campo (las claves `anterior`/`anteriores` ofrecen «Recargar»); 409 → el title con «Recargar», salvo el
 * correo duplicado del alta (con `usuarioId` si viene); 404 → «El usuario no existe.»; 403 → el title sin «Reintentar»;
 * 503 → «título: detalle» con «Reintentar»; red y errores ajenos a la API → «Reintentar».
 */
export function interpretarErrorUsuario<C extends string>(error: unknown, campos: readonly C[]): ErroresUsuario<C> {
  if (!(error instanceof ErrorApi)) {
    return { campos: {}, generales: ['Ocurrió un error inesperado.'], ofrecerRecarga: false, ofrecerReintento: true, noEncontrado: false }
  }
  const base = interpretarErrorDialogo(error, { campos, clavesRecarga: ['anterior', 'anteriores'] })
  if (error.tipo === 'noEncontrado') {
    return { ...base, generales: [MENSAJE_USUARIO_NO_ENCONTRADO] }
  }
  if (error.tipo === 'conflicto') {
    const id = error.extensiones.usuarioId
    const usuarioExistenteId = typeof id === 'number' ? id : undefined
    return usuarioExistenteId !== undefined || error.titulo === MENSAJE_DUPLICADO
      ? { ...base, ofrecerRecarga: false, usuarioExistenteId }
      : base
  }
  return base
}
