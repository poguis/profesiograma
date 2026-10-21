// Exportar el reporte Excel del proyecto (TAREA-22b; backend: TAREA-22-2, docs/fases/FASE_5_Cronograma.md §8).
// Lógica pura, sin React: se prueba con Vitest (reporteExcel.test.ts). Solo guardarArchivo toca el DOM.
import type { TipoErrorApi } from '../../api/errores'
import type { ResumenReporte } from './tipos'

export const AVISO_CEDULAS = 'El archivo contiene cédulas. Manéjelo con cuidado.'
export const MENSAJE_SIN_FILAS = 'No hay asignaciones en el rango.'

export interface RangoReporte {
  desde: string | undefined
  hasta: string | undefined
}

/** Errores del rango por campo (los mismos textos que el backend). */
export interface ValidacionRango {
  desde?: string
  hasta?: string
}

/** Rango inicial: inicio y fin del proyecto (Z18). Salvaguarda: sin fin, el de inicio. */
export function rangoInicial(proyecto: { fechaInicio: string; fechaFin?: string | null }): RangoReporte {
  return { desde: proyecto.fechaInicio, hasta: proyecto.fechaFin || proyecto.fechaInicio }
}

/** Obligatorias, formato del selector y hasta ≥ desde (fechas "yyyy-MM-dd": se comparan como texto). */
export function validarRango(desde: string | undefined, hasta: string | undefined, erroresFormato: ValidacionRango = {}): ValidacionRango {
  const errores: ValidacionRango = {
    desde: erroresFormato.desde ?? (desde ? undefined : "La fecha 'desde' es obligatoria."),
    hasta: erroresFormato.hasta ?? (hasta ? undefined : "La fecha 'hasta' es obligatoria."),
  }
  if (!errores.desde && !errores.hasta && hasta! < desde!) {
    errores.hasta = "La fecha 'hasta' no puede ser anterior a la fecha 'desde'."
  }
  return errores
}

export const rangoValido = (v: ValidacionRango) => !v.desde && !v.hasta

export function consultaReporte(desde: string, hasta: string): string {
  return new URLSearchParams({ desde, hasta }).toString()
}

/** Como el backend: Reporte_{código}_{yyyyMMdd}_{yyyyMMdd}.xlsx (caracteres no válidos del código → "_"). */
export function nombrePorDefecto(codigo: string, desde: string, hasta: string): string {
  const sinGuiones = (f: string) => f.replaceAll('-', '')
  return `Reporte_${codigo.replace(/[^A-Za-z0-9._-]/g, '_')}_${sinGuiones(desde)}_${sinGuiones(hasta)}.xlsx`
}

/**
 * Nombre del archivo (Z22): filename* (UTF-8, RFC 5987) → filename (con o sin comillas) → por defecto. Un valor mal
 * codificado cae al siguiente criterio; una ruta incluida se reduce al nombre.
 */
export function nombreDesdeContentDisposition(valor: string | null | undefined, porDefecto: string): string {
  if (!valor) {
    return porDefecto
  }

  const extendido = /filename\*\s*=\s*([^;]+)/i.exec(valor)?.[1]?.trim()
  if (extendido) {
    const codificado = extendido.replace(/^"|"$/g, '').replace(/^[^']*'[^']*'/, '')
    try {
      const nombre = soloNombre(decodeURIComponent(codificado))
      if (nombre) {
        return nombre
      }
    } catch {
      // mal codificado: se prueba con filename
    }
  }

  const simple = /filename\s*=\s*("([^"]*)"|[^;]+)/i.exec(valor)
  const nombre = soloNombre((simple?.[2] ?? simple?.[1] ?? '').trim())
  return nombre || porDefecto
}

function soloNombre(ruta: string): string {
  return ruta.split(/[\\/]/).pop()!.trim()
}

/** X-Reporte-Filas → número (entero ≥ 0) o undefined si no se puede leer. */
export function filasDesdeEncabezado(valor: string | null | undefined): number | undefined {
  return valor && /^\d+$/.test(valor.trim()) ? Number(valor.trim()) : undefined
}

export function textoExito(filas: number | undefined): string {
  return filas === undefined ? 'Reporte generado.' : `Reporte generado (${filas} filas).`
}

/** Resumen (Z26): la línea de totales siempre; los avisos solo con n > 0. Informativos: no bloquean. */
export function avisosResumen(r: ResumenReporte): { totales: string; avisos: string[] } {
  const avisos = [
    r.personasSinCedula > 0 && `${r.personasSinCedula} persona(s) sin cédula: la columna CEDULA irá vacía.`,
    r.personasSinCodigoInfor > 0 && `${r.personasSinCodigoInfor} persona(s) sin código Infor: la columna CARGO INFOR irá vacía.`,
    r.filasSinActividad > 0 && `${r.filasSinActividad} fila(s) sin actividad: la columna ACTIVIDAD irá vacía.`,
    r.diasConOtroProyecto > 0 && `En ${r.diasConOtroProyecto} caso(s) la persona también tiene asignación en otro proyecto el mismo día.`,
  ].filter((a): a is string => typeof a === 'string')
  return { totales: `${r.filas} fila(s) · ${r.personas} persona(s)`, avisos }
}

export type EstadoResumen = 'cargando' | 'error' | 'listo'

/** «Descargar»: rango válido, resumen listo con filas y sin otra descarga en curso. */
export function puedeDescargar(
  validacion: ValidacionRango, resumen: ResumenReporte | undefined, estadoResumen: EstadoResumen, enviando: boolean,
): boolean {
  return rangoValido(validacion) && estadoResumen === 'listo' && (resumen?.filas ?? 0) > 0 && !enviando
}

/** «Reintentar» en la descarga: 503 (también la base caída, TAREA-22c), red y 5xx. */
export function ofreceReintento(tipo: TipoErrorApi): boolean {
  return tipo === 'noDisponible' || tipo === 'red' || tipo === 'servidor'
}

export function esCancelacion(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'AbortError'
}

/**
 * Descarga el blob con un <a download> temporal y revoca la URL ~1 s después (Z25). No conserva el blob: al salir,
 * solo la URL (ya revocada luego) lo referencia.
 */
export function guardarArchivo(blob: Blob, nombre: string): void {
  const url = URL.createObjectURL(blob)
  const enlace = document.createElement('a')
  enlace.href = url
  enlace.download = nombre
  enlace.style.display = 'none'
  document.body.appendChild(enlace)
  enlace.click()
  enlace.remove()
  setTimeout(() => URL.revokeObjectURL(url), 1000)
}
