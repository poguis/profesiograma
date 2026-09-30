import { useCallback, useMemo } from 'react'
import { useSearchParams } from 'react-router'
import type { FiltrosProyectos } from './tipos'

export const PAGINA_POR_DEFECTO = 1
export const TAMANO_POR_DEFECTO = 20 // igual al de la API
export const TAMANOS_PAGINA = [10, 20, 50]

const CLAVES_TEXTO = ['estado', 'grupo', 'texto', 'desde', 'hasta'] as const

/** URL → filtros. No valida reglas de negocio: valores fuera de rango se envían tal cual y la API responde 400. */
export function leerFiltros(parametros: URLSearchParams): FiltrosProyectos {
  const filtros: FiltrosProyectos = {
    pagina: leerEntero(parametros.get('pagina'), PAGINA_POR_DEFECTO),
    tamano: leerEntero(parametros.get('tamano'), TAMANO_POR_DEFECTO),
  }
  for (const clave of CLAVES_TEXTO) {
    const valor = parametros.get(clave)?.trim()
    if (valor) {
      filtros[clave] = valor
    }
  }
  return filtros
}

/** Filtros → query string de la URL (omite vacíos y valores por defecto para una URL limpia). */
export function aParametrosUrl(filtros: FiltrosProyectos): URLSearchParams {
  const parametros = new URLSearchParams()
  for (const clave of CLAVES_TEXTO) {
    const valor = filtros[clave]
    if (valor) {
      parametros.set(clave, valor)
    }
  }
  if (filtros.pagina !== PAGINA_POR_DEFECTO) {
    parametros.set('pagina', String(filtros.pagina))
  }
  if (filtros.tamano !== TAMANO_POR_DEFECTO) {
    parametros.set('tamano', String(filtros.tamano))
  }
  return parametros
}

/** Filtros → query string para la API (incluye siempre pagina y tamano). */
export function aConsultaApi(filtros: FiltrosProyectos): string {
  const parametros = aParametrosUrl(filtros)
  parametros.set('pagina', String(filtros.pagina))
  parametros.set('tamano', String(filtros.tamano))
  return parametros.toString()
}

export interface CambioFiltros {
  estado?: string
  grupo?: string
  texto?: string
  desde?: string
  hasta?: string
  pagina?: number
  tamano?: number
}

/**
 * Filtros sincronizados con la URL (recargar o volver atrás conserva la búsqueda).
 * Cualquier cambio que no sea de página vuelve a la página 1.
 */
export function useFiltrosUrl() {
  const [parametros, setParametros] = useSearchParams()
  const consulta = parametros.toString()
  const filtros = useMemo(() => leerFiltros(new URLSearchParams(consulta)), [consulta])

  const actualizar = useCallback(
    (cambios: CambioFiltros, opciones?: { reemplazar?: boolean }) => {
      const nuevo: FiltrosProyectos = { ...filtros, ...cambios }
      if (cambios.pagina === undefined) {
        nuevo.pagina = PAGINA_POR_DEFECTO
      }
      setParametros(aParametrosUrl(nuevo), { replace: opciones?.reemplazar ?? false })
    },
    [filtros, setParametros],
  )

  const limpiar = useCallback(() => {
    // Conserva el tamaño de página elegido; borra filtros y página.
    setParametros(aParametrosUrl({ pagina: PAGINA_POR_DEFECTO, tamano: filtros.tamano }))
  }, [filtros.tamano, setParametros])

  const hayFiltros = CLAVES_TEXTO.some((clave) => filtros[clave] !== undefined)

  return { filtros, actualizar, limpiar, hayFiltros }
}

function leerEntero(valor: string | null, porDefecto: number): number {
  if (valor === null || valor.trim() === '') {
    return porDefecto
  }
  const numero = Number(valor)
  return Number.isInteger(numero) ? numero : porDefecto
}
