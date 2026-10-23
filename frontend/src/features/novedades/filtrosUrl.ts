// Filtros del listado de novedades en la URL (TAREA-24a; patrón de proyectos/filtrosUrl.ts). Lógica pura + hook.
// A diferencia de proyectos, un valor ilegible en la URL vuelve al valor por defecto en vez de enviarse a la API.
import { useCallback, useMemo } from 'react'
import { useSearchParams } from 'react-router'
import type { FiltrosNovedades } from './tipos'

export const PAGINA_POR_DEFECTO = 1
export const TAMANO_POR_DEFECTO = 20 // igual al de la API
export const TAMANOS_PAGINA = [10, 20, 50]
export const APLICACIONES = ['PERSONA', 'PROYECTO', 'GENERAL'] as const

const CLAVES_TEXTO = ['aplicacion', 'tipo', 'origen', 'texto', 'desde', 'hasta'] as const
const PATRON_FECHA = /^\d{4}-\d{2}-\d{2}$/

/** URL → filtros. Aplicación desconocida, fecha sin formato yyyy-MM-dd, página < 1 o tamaño fuera de 10/20/50 → por defecto. */
export function leerFiltros(parametros: URLSearchParams): FiltrosNovedades {
  const filtros: FiltrosNovedades = {
    incluirAnuladas: parametros.get('incluirAnuladas') === 'true',
    pagina: leerEntero(parametros.get('pagina'), PAGINA_POR_DEFECTO, (n) => n >= 1),
    tamano: leerEntero(parametros.get('tamano'), TAMANO_POR_DEFECTO, (n) => TAMANOS_PAGINA.includes(n)),
  }
  for (const clave of CLAVES_TEXTO) {
    const valor = parametros.get(clave)?.trim()
    if (valor && esValido(clave, valor)) {
      filtros[clave] = valor
    }
  }
  return filtros
}

/** Filtros → query string de la URL (omite vacíos y valores por defecto para una URL limpia). */
export function aParametrosUrl(filtros: FiltrosNovedades): URLSearchParams {
  const parametros = new URLSearchParams()
  for (const clave of CLAVES_TEXTO) {
    const valor = filtros[clave]
    if (valor) {
      parametros.set(clave, valor)
    }
  }
  if (filtros.incluirAnuladas) {
    parametros.set('incluirAnuladas', 'true')
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
export function aConsultaApi(filtros: FiltrosNovedades): string {
  const parametros = aParametrosUrl(filtros)
  parametros.set('pagina', String(filtros.pagina))
  parametros.set('tamano', String(filtros.tamano))
  return parametros.toString()
}

export type CambioFiltrosNovedades = Partial<FiltrosNovedades>

/** Filtros sincronizados con la URL. Cualquier cambio que no sea de página vuelve a la página 1. */
export function useFiltrosUrl() {
  const [parametros, setParametros] = useSearchParams()
  const consulta = parametros.toString()
  const filtros = useMemo(() => leerFiltros(new URLSearchParams(consulta)), [consulta])

  const actualizar = useCallback(
    (cambios: CambioFiltrosNovedades, opciones?: { reemplazar?: boolean }) => {
      const nuevo: FiltrosNovedades = { ...filtros, ...cambios }
      if (cambios.pagina === undefined) {
        nuevo.pagina = PAGINA_POR_DEFECTO
      }
      setParametros(aParametrosUrl(nuevo), { replace: opciones?.reemplazar ?? false })
    },
    [filtros, setParametros],
  )

  const limpiar = useCallback(() => {
    // Conserva el tamaño de página elegido; borra filtros y página.
    setParametros(aParametrosUrl({ incluirAnuladas: false, pagina: PAGINA_POR_DEFECTO, tamano: filtros.tamano }))
  }, [filtros.tamano, setParametros])

  const hayFiltros = filtros.incluirAnuladas || CLAVES_TEXTO.some((clave) => filtros[clave] !== undefined)

  return { filtros, actualizar, limpiar, hayFiltros }
}

function esValido(clave: (typeof CLAVES_TEXTO)[number], valor: string): boolean {
  if (clave === 'aplicacion') {
    return (APLICACIONES as readonly string[]).includes(valor)
  }
  if (clave === 'desde' || clave === 'hasta') {
    return PATRON_FECHA.test(valor)
  }
  return true
}

function leerEntero(valor: string | null, porDefecto: number, valido: (n: number) => boolean): number {
  const numero = Number(valor)
  return valor !== null && valor.trim() !== '' && Number.isInteger(numero) && valido(numero) ? numero : porDefecto
}
