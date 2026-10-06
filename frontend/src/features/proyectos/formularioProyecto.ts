// Estado del formulario "Nuevo proyecto" (TAREA-13). Lógica pura, sin React: se prueba con Vitest.
// Concentra la limpieza de campos dependientes (R3), las referencias back → principal (R8) y la revisión que
// invalida la vista previa (R12). El servidor tiene la última palabra en todas las validaciones.
import { formatearFecha } from '../../utils/formato'
import type {
  BackSolicitud,
  CruceAsignacion,
  EmpleadoBusqueda,
  GrupoProyectoCatalogo,
  Previsualizacion,
  PrincipalSolicitud,
  ResumenCruce,
  SolicitudCrearProyecto,
  TipoRegistroBack,
} from './tipos'

/** Grupo elegido con sus flags del catálogo (RN02: nunca se comparan nombres de grupo). */
export type GrupoSeleccionado = Pick<GrupoProyectoCatalogo, 'codigo' | 'requiereProyectoErp' | 'requiereDimension'>

export type EmpleadoFila = Pick<EmpleadoBusqueda, 'id' | 'codigoEkon' | 'nombreCompleto' | 'cargo'>

/** Fechas "yyyy-MM-dd"; horas "HH:mm" (nunca "HH:mm:ss" en el formulario). */
export interface CabeceraFormulario {
  companiaId: number | null
  grupo: GrupoSeleccionado | null
  proyectoErpId: string | null
  actividadId: string | null
  dimensionUegpId: string | null
  fechaInicio: string | null
  fechaFin: string | null
  horarioCodigo: number | null
  salidaAlmuerzo: string | null
  regresoAlmuerzo: string | null
  departamentoId: number | null
}

export interface FilaPrincipal {
  /** Identificador local estable (no cambia al reordenar). */
  clave: string
  empleado: EmpleadoFila
  jornada: string | null
  fechaInicio: string | null
  fechaFin: string | null
  cargo: string
}

export interface FilaBack {
  clave: string
  empleado: EmpleadoFila
  tipoRegistro: TipoRegistroBack
  diasDescanso: number
  fechaInicio: string | null
  fechaFin: string | null
  /** Clave del principal que cubre (no su número): reordenar no rompe la referencia. */
  principalClave: string | null
  observacion: string
  /** El principal relacionado se eliminó: se avisa hasta que el usuario elija otra opción. */
  avisoRelacion: boolean
}

export interface EstadoFormulario {
  /** Aumenta con cada cambio de datos: una vista previa solo vale para su revisión (R12). */
  revision: number
  /** Contador para generar claves locales sin efectos secundarios. */
  siguienteClave: number
  cabecera: CabeceraFormulario
  principales: FilaPrincipal[]
  backs: FilaBack[]
}

/** Campos de la cabecera sin dependientes (se cambian sin limpiar otros). */
export type CambiosCabecera = Partial<
  Pick<
    CabeceraFormulario,
    | 'actividadId'
    | 'dimensionUegpId'
    | 'fechaInicio'
    | 'fechaFin'
    | 'horarioCodigo'
    | 'salidaAlmuerzo'
    | 'regresoAlmuerzo'
    | 'departamentoId'
  >
>

export type CambiosPrincipal = Partial<Pick<FilaPrincipal, 'jornada' | 'fechaInicio' | 'fechaFin' | 'cargo'>>

export type CambiosBack = Partial<
  Pick<FilaBack, 'tipoRegistro' | 'diasDescanso' | 'fechaInicio' | 'fechaFin' | 'principalClave' | 'observacion'>
>

export type AccionFormulario =
  | { tipo: 'compania'; companiaId: number | null }
  | { tipo: 'grupo'; grupo: GrupoSeleccionado | null }
  | { tipo: 'proyectoErp'; proyectoErpId: string | null }
  | { tipo: 'cabecera'; cambios: CambiosCabecera }
  | { tipo: 'agregarPrincipal'; empleado: EmpleadoFila; maximo: number }
  | { tipo: 'actualizarPrincipal'; clave: string; cambios: CambiosPrincipal }
  | { tipo: 'moverPrincipal'; clave: string; direccion: -1 | 1 }
  | { tipo: 'eliminarPrincipal'; clave: string }
  | { tipo: 'agregarBack'; empleado: EmpleadoFila; maximo: number }
  | { tipo: 'actualizarBack'; clave: string; cambios: CambiosBack }
  | { tipo: 'eliminarBack'; clave: string }

export interface DatosIniciales {
  /** Departamentos del usuario: si tiene uno solo, se usa automáticamente (P5). */
  departamentos: { id: number }[]
  /** Compañías del ERP: si hay una sola, queda elegida. */
  companias: { id: number }[]
}

export function crearEstadoInicial({ departamentos, companias }: DatosIniciales): EstadoFormulario {
  return {
    revision: 0,
    siguienteClave: 1,
    cabecera: {
      companiaId: companias.length === 1 ? companias[0].id : null,
      grupo: null,
      proyectoErpId: null,
      actividadId: null,
      dimensionUegpId: null,
      fechaInicio: null,
      fechaFin: null,
      horarioCodigo: null,
      salidaAlmuerzo: null,
      regresoAlmuerzo: null,
      departamentoId: departamentos.length === 1 ? departamentos[0].id : null,
    },
    principales: [],
    backs: [],
  }
}

const SIN_DATOS_ERP = { proyectoErpId: null, actividadId: null, dimensionUegpId: null } as const

export function reducerFormulario(estado: EstadoFormulario, accion: AccionFormulario): EstadoFormulario {
  switch (accion.tipo) {
    case 'compania':
      // R3: otra compañía → proyecto ERP, actividad y dimensión ya no aplican.
      if (accion.companiaId === estado.cabecera.companiaId) {
        return estado
      }
      return conCabecera(estado, { companiaId: accion.companiaId, ...SIN_DATOS_ERP })

    case 'grupo':
      // R3: al cambiar el grupo se limpian SIEMPRE proyecto ERP, actividad y dimensión.
      if (accion.grupo?.codigo === estado.cabecera.grupo?.codigo) {
        return estado
      }
      return conCabecera(estado, { grupo: accion.grupo, ...SIN_DATOS_ERP })

    case 'proyectoErp':
      // R3: otro proyecto ERP → la actividad ya no aplica.
      if (accion.proyectoErpId === estado.cabecera.proyectoErpId) {
        return estado
      }
      return conCabecera(estado, { proyectoErpId: accion.proyectoErpId, actividadId: null })

    case 'cabecera':
      // R10: cambiar las fechas del proyecto NO modifica las del personal (se avisa por fila).
      return conCabecera(estado, accion.cambios)

    case 'agregarPrincipal': {
      if (estado.principales.length >= accion.maximo) {
        return estado // R9: el botón ya está deshabilitado; el servidor valida igual
      }
      const { clave, siguienteClave } = nuevaClave(estado, 'p')
      const fila: FilaPrincipal = {
        clave,
        empleado: accion.empleado,
        jornada: null,
        // P2: se sugiere el rango del proyecto.
        fechaInicio: estado.cabecera.fechaInicio,
        fechaFin: estado.cabecera.fechaFin,
        cargo: '',
      }
      return revisar({ ...estado, siguienteClave, principales: [...estado.principales, fila] })
    }

    case 'actualizarPrincipal':
      return revisar({
        ...estado,
        principales: estado.principales.map((p) => (p.clave === accion.clave ? { ...p, ...accion.cambios } : p)),
      })

    case 'moverPrincipal': {
      const origen = estado.principales.findIndex((p) => p.clave === accion.clave)
      const destino = origen + accion.direccion
      if (origen < 0 || destino < 0 || destino >= estado.principales.length) {
        return estado
      }
      const principales = [...estado.principales]
      ;[principales[origen], principales[destino]] = [principales[destino], principales[origen]]
      return revisar({ ...estado, principales })
    }

    case 'eliminarPrincipal':
      // R8: los backs que lo cubrían quedan sin relación y con aviso; el resto conserva su referencia.
      return revisar({
        ...estado,
        principales: estado.principales.filter((p) => p.clave !== accion.clave),
        backs: estado.backs.map((b) =>
          b.principalClave === accion.clave ? { ...b, principalClave: null, avisoRelacion: true } : b,
        ),
      })

    case 'agregarBack': {
      if (estado.backs.length >= accion.maximo) {
        return estado
      }
      const { clave, siguienteClave } = nuevaClave(estado, 'b')
      const fila: FilaBack = {
        clave,
        empleado: accion.empleado,
        tipoRegistro: 'JORNADA',
        diasDescanso: 0,
        fechaInicio: estado.cabecera.fechaInicio,
        fechaFin: estado.cabecera.fechaFin,
        principalClave: null,
        observacion: '',
        avisoRelacion: false,
      }
      return revisar({ ...estado, siguienteClave, backs: [...estado.backs, fila] })
    }

    case 'actualizarBack':
      return revisar({
        ...estado,
        backs: estado.backs.map((b) => (b.clave === accion.clave ? actualizarBack(b, accion.cambios) : b)),
      })

    case 'eliminarBack':
      return revisar({ ...estado, backs: estado.backs.filter((b) => b.clave !== accion.clave) })
  }
}

function actualizarBack(back: FilaBack, cambios: CambiosBack): FilaBack {
  const actualizado = { ...back, ...cambios }
  if (actualizado.tipoRegistro === 'DESCANSO') {
    actualizado.diasDescanso = 0 // R8: los días de descanso solo aplican a JORNADA
  }
  if ('principalClave' in cambios) {
    actualizado.avisoRelacion = false // el usuario ya eligió (otro principal o "sin relación")
  }
  return actualizado
}

function conCabecera(estado: EstadoFormulario, cambios: Partial<CabeceraFormulario>): EstadoFormulario {
  return revisar({ ...estado, cabecera: { ...estado.cabecera, ...cambios } })
}

function revisar(estado: EstadoFormulario): EstadoFormulario {
  return { ...estado, revision: estado.revision + 1 }
}

function nuevaClave(estado: EstadoFormulario, prefijo: string) {
  return { clave: `${prefijo}${estado.siguienteClave}`, siguienteClave: estado.siguienteClave + 1 }
}

// ------------------------------------------------------------------ consultas sobre el estado

/** Número 1..n del principal (posición actual); null si no hay referencia o ya no existe. */
export function numeroPrincipal(principales: FilaPrincipal[], clave: string | null): number | null {
  if (clave === null) {
    return null
  }
  const indice = principales.findIndex((p) => p.clave === clave)
  return indice < 0 ? null : indice + 1
}

/** Etiquetas de cada empleado ya agregado ("P1", "Back 2") para marcarlo en el buscador (R6). */
export function etiquetasPorEmpleado(estado: EstadoFormulario): Map<number, string[]> {
  const etiquetas = new Map<number, string[]>()
  const agregar = (id: number, etiqueta: string) => etiquetas.set(id, [...(etiquetas.get(id) ?? []), etiqueta])
  estado.principales.forEach((p, i) => agregar(p.empleado.id, `P${i + 1}`))
  estado.backs.forEach((b, i) => agregar(b.empleado.id, `Back ${i + 1}`))
  return etiquetas
}

/** Agregar personal requiere las fechas del proyecto (se sugieren a cada fila). */
export function tieneRangoProyecto(cabecera: CabeceraFormulario): boolean {
  return cabecera.fechaInicio !== null && cabecera.fechaFin !== null
}

/**
 * R10: filas cuyas fechas quedan fuera del rango del proyecto (clave → mensaje).
 * No es un error del cliente: el servidor lo rechaza (RN08) y aquí solo se avisa.
 */
export function advertenciasFechas(estado: EstadoFormulario): Map<string, string> {
  const { fechaInicio, fechaFin } = estado.cabecera
  const advertencias = new Map<string, string>()
  if (fechaInicio === null || fechaFin === null || fechaFin < fechaInicio) {
    return advertencias
  }

  const mensaje = `Las fechas están fuera del rango del proyecto (${formatearFecha(fechaInicio)} – ${formatearFecha(fechaFin)}).`
  for (const fila of [...estado.principales, ...estado.backs]) {
    // "yyyy-MM-dd" se compara como texto.
    const fuera =
      (fila.fechaInicio !== null && (fila.fechaInicio < fechaInicio || fila.fechaInicio > fechaFin)) ||
      (fila.fechaFin !== null && (fila.fechaFin < fechaInicio || fila.fechaFin > fechaFin))
    if (fuera) {
      advertencias.set(fila.clave, mensaje)
    }
  }
  return advertencias
}

export type CampoCabecera = keyof CabeceraFormulario

/** Campo de una fila de personal → mensaje (p. ej. "fechaFin", "jornada"). */
export type ErroresFila = Partial<Record<string, string>>

export interface ErroresCliente {
  cabecera: Partial<Record<CampoCabecera, string>>
  /** clave de fila → campo → mensaje */
  filas: Record<string, ErroresFila>
}

/**
 * R4: ayudas del cliente (no bloquean el envío; D4). Los rangos de RN09 ya los imponen las opciones
 * del endpoint, así que aquí solo se revisa regreso > salida y fin ≥ inicio.
 */
export function validarCliente(estado: EstadoFormulario): ErroresCliente {
  const errores: ErroresCliente = { cabecera: {}, filas: {} }
  const c = estado.cabecera

  if (c.fechaInicio !== null && c.fechaFin !== null && c.fechaFin < c.fechaInicio) {
    errores.cabecera.fechaFin = 'La fecha fin debe ser mayor o igual a la fecha de inicio.'
  }
  // "HH:mm" se compara como texto.
  if (c.salidaAlmuerzo !== null && c.regresoAlmuerzo !== null && c.regresoAlmuerzo <= c.salidaAlmuerzo) {
    errores.cabecera.regresoAlmuerzo = 'El regreso de almuerzo debe ser posterior a la salida.'
  }
  for (const fila of [...estado.principales, ...estado.backs]) {
    if (fila.fechaInicio !== null && fila.fechaFin !== null && fila.fechaFin < fila.fechaInicio) {
      errores.filas[fila.clave] = { fechaFin: 'La fecha fin debe ser mayor o igual a la fecha de inicio.' }
    }
  }
  return errores
}

// ------------------------------------------------------------------ solicitud

/**
 * Cuerpo de POST /api/proyectos y /previsualizar. Los campos que el grupo no usa van como null (nunca ""),
 * aunque el estado tuviera valor: la API responde 400 si llegan con dato (R3).
 */
export function aSolicitud(estado: EstadoFormulario): SolicitudCrearProyecto {
  const c = estado.cabecera
  const usaErp = c.grupo?.requiereProyectoErp === true
  const usaDimension = c.grupo?.requiereDimension === true

  const principales: PrincipalSolicitud[] = estado.principales.map((p) => ({
    empleadoId: p.empleado.id,
    jornada: texto(p.jornada),
    fechaInicio: p.fechaInicio,
    fechaFin: p.fechaFin,
    cargo: texto(p.cargo),
  }))

  const backs: BackSolicitud[] = estado.backs.map((b) => ({
    empleadoId: b.empleado.id,
    tipoRegistro: b.tipoRegistro,
    diasDescanso: b.tipoRegistro === 'DESCANSO' ? 0 : b.diasDescanso,
    fechaInicio: b.fechaInicio,
    fechaFin: b.fechaFin,
    principalRelacionado: numeroPrincipal(estado.principales, b.principalClave),
    observacion: texto(b.observacion),
    cargo: null,
  }))

  return {
    companiaId: c.companiaId,
    grupo: c.grupo?.codigo ?? null,
    proyectoErpId: usaErp ? texto(c.proyectoErpId) : null,
    actividadId: usaErp ? texto(c.actividadId) : null,
    dimensionUegpId: usaDimension ? texto(c.dimensionUegpId) : null,
    fechaInicio: c.fechaInicio,
    fechaFin: c.fechaFin,
    horarioCodigo: c.horarioCodigo,
    salidaAlmuerzo: texto(c.salidaAlmuerzo),
    regresoAlmuerzo: texto(c.regresoAlmuerzo),
    departamentoId: c.departamentoId,
    principales,
    backs,
  }
}

/** Texto recortado; "" → null. */
function texto(valor: string | null): string | null {
  const recortado = valor?.trim()
  return recortado ? recortado : null
}

// ------------------------------------------------------------------ errores 400 del servidor (R13)

/** Claves locales de las filas en el orden en que se enviaron: el índice del error apunta a ESA fila. */
export interface ClavesEnvio {
  principales: string[]
  backs: string[]
}

export function clavesDelEnvio(estado: EstadoFormulario): ClavesEnvio {
  return { principales: estado.principales.map((p) => p.clave), backs: estado.backs.map((b) => b.clave) }
}

export interface ErroresServidor {
  cabecera: Partial<Record<CampoCabecera, string>>
  /** clave de fila → campo → mensaje (sigue a la fila aunque luego se reordene). */
  filas: Record<string, ErroresFila>
  /** Errores de la lista completa (p. ej. máximo de principales). */
  secciones: { principales?: string; backs?: string }
  /** Errores sin campo identificable: se muestran arriba del formulario. */
  generales: string[]
}

export const SIN_ERRORES_SERVIDOR: ErroresServidor = { cabecera: {}, filas: {}, secciones: {}, generales: [] }

const CAMPOS_CABECERA = new Set<string>([
  'companiaId',
  'grupo',
  'proyectoErpId',
  'actividadId',
  'dimensionUegpId',
  'fechaInicio',
  'fechaFin',
  'horarioCodigo',
  'salidaAlmuerzo',
  'regresoAlmuerzo',
  'departamentoId',
] satisfies CampoCabecera[])

const PATRON_CAMPO_FILA = /^(principales|backs)\[(\d+)\]\.(\w+)$/

/**
 * ValidationProblem → errores por campo. Claves "principales[i].campo" / "backs[i].campo" se asignan a la fila
 * que ocupaba el índice i EN EL ENVÍO (claves). Lo que no corresponde a un campo visible va a `generales`.
 */
export function distribuirErrores(errores: Record<string, string[]>, claves: ClavesEnvio): ErroresServidor {
  const resultado: ErroresServidor = { cabecera: {}, filas: {}, secciones: {}, generales: [] }

  for (const [campo, mensajes] of Object.entries(errores)) {
    const mensaje = mensajes.join(' ')
    if (CAMPOS_CABECERA.has(campo)) {
      resultado.cabecera[campo as CampoCabecera] = mensaje
      continue
    }
    if (campo === 'principales' || campo === 'backs') {
      resultado.secciones[campo] = mensaje
      continue
    }
    const partes = PATRON_CAMPO_FILA.exec(campo)
    const clave = partes ? claves[partes[1] as keyof ClavesEnvio][Number(partes[2])] : undefined
    if (partes && clave !== undefined) {
      resultado.filas[clave] = { ...resultado.filas[clave], [partes[3]]: mensaje }
      continue
    }
    resultado.generales.push(mensaje)
  }
  return resultado
}

/** Mensajes de campos que la tarjeta no muestra (p. ej. "empleadoId"): se muestran en la tarjeta. */
export function mensajesSinCampo(errores: ErroresFila, camposVisibles: string[]): string[] {
  return Object.entries(errores)
    .filter(([campo, mensaje]) => mensaje && !camposVisibles.includes(campo))
    .map(([, mensaje]) => mensaje!)
}

/** Editar un campo borra su error del servidor (los demás se conservan hasta el próximo envío). */
export function limpiarErroresServidor(errores: ErroresServidor, accion: AccionFormulario): ErroresServidor {
  const sinCabecera = (...campos: CampoCabecera[]) => {
    const cabecera = { ...errores.cabecera }
    campos.forEach((c) => delete cabecera[c])
    return { ...errores, cabecera }
  }
  const sinCamposFila = (clave: string, campos: string[]) => {
    const fila = { ...errores.filas[clave] }
    campos.forEach((c) => delete fila[c])
    return { ...errores, filas: { ...errores.filas, [clave]: fila } }
  }
  const sinFila = (clave: string, seccion: keyof ErroresServidor['secciones']) => {
    const filas = { ...errores.filas }
    delete filas[clave]
    return { ...errores, filas, secciones: { ...errores.secciones, [seccion]: undefined } }
  }

  switch (accion.tipo) {
    case 'compania':
      return sinCabecera('companiaId', 'proyectoErpId', 'actividadId', 'dimensionUegpId')
    case 'grupo':
      return sinCabecera('grupo', 'proyectoErpId', 'actividadId', 'dimensionUegpId')
    case 'proyectoErp':
      return sinCabecera('proyectoErpId', 'actividadId')
    case 'cabecera':
      return sinCabecera(...(Object.keys(accion.cambios) as CampoCabecera[]))
    case 'agregarPrincipal':
      return { ...errores, secciones: { ...errores.secciones, principales: undefined } }
    case 'agregarBack':
      return { ...errores, secciones: { ...errores.secciones, backs: undefined } }
    case 'eliminarPrincipal':
      return sinFila(accion.clave, 'principales')
    case 'eliminarBack':
      return sinFila(accion.clave, 'backs')
    case 'actualizarPrincipal':
      return sinCamposFila(accion.clave, Object.keys(accion.cambios))
    case 'actualizarBack': {
      const campos = Object.keys(accion.cambios).map((c) => (c === 'principalClave' ? 'principalRelacionado' : c))
      if ('tipoRegistro' in accion.cambios) {
        campos.push('diasDescanso')
      }
      return sinCamposFila(accion.clave, campos)
    }
    case 'moverPrincipal':
      return errores // los errores siguen a la fila por su clave
  }
}

// ------------------------------------------------------------------ vista previa y registro (R11–R13)

/** Vista previa ligada a la revisión del formulario con que se generó. */
export interface VistaPreviaFormulario {
  revision: number
  datos: Previsualizacion
}

/** La vista previa corresponde al estado actual del formulario (R12). */
export function vistaVigente(vista: VistaPreviaFormulario | null, revision: number): boolean {
  return vista !== null && vista.revision === revision
}

/**
 * Mínimo de personal (TAREA-19y, pendiente 33): con PROYECTO_EXIGE_PRINCIPAL (`exigePrincipal`) al menos 1 principal
 * (C10); si no, al menos 1 persona (principal o back). El error del servidor solo llega en un 400 (`principales` o
 * `personal`); aquí solo se da la ayuda y se bloquea la vista previa.
 */
export const MINIMO_PRINCIPALES = 1

function cumpleMinimo(estado: EstadoFormulario, exigePrincipal: boolean): boolean {
  return exigePrincipal
    ? estado.principales.length >= MINIMO_PRINCIPALES
    : estado.principales.length + estado.backs.length >= 1
}

/** P6 / TAREA-19y: ayuda neutra (no error) mientras no se cumpla el mínimo; undefined si ya se cumple. */
export function ayudaPersonal(estado: EstadoFormulario, exigePrincipal: boolean): string | undefined {
  if (cumpleMinimo(estado, exigePrincipal)) {
    return undefined
  }
  return exigePrincipal
    ? `Agrega al menos ${MINIMO_PRINCIPALES} principal para generar la vista previa.`
    : 'Agrega al menos 1 persona (principal o back) para generar la vista previa.'
}

/** P6 / TAREA-19y: sin el mínimo no se genera la vista previa (y por tanto no se registra). */
export function puedeGenerarVistaPrevia(estado: EstadoFormulario, enviando: boolean, exigePrincipal: boolean): boolean {
  return !enviando && cumpleMinimo(estado, exigePrincipal)
}

/** R12: "Registrar" solo con una vista previa vigente, sin cruces y sin un envío en curso. */
export function puedeRegistrar(vista: VistaPreviaFormulario | null, revision: number, enviando: boolean): boolean {
  return !enviando && vistaVigente(vista, revision) && vista!.datos.cruces.length === 0
}

/** Extensiones `cruces` y `resumen` del 409 de POST /api/proyectos; null si no tienen la forma esperada. */
export function crucesDeConflicto(
  extensiones: Record<string, unknown>,
): { cruces: CruceAsignacion[]; resumen: ResumenCruce[] } | null {
  const { cruces, resumen } = extensiones
  return Array.isArray(cruces) && Array.isArray(resumen)
    ? { cruces: cruces as CruceAsignacion[], resumen: resumen as ResumenCruce[] }
    : null
}
