import { useMemo } from 'react'
import { useCatalogos } from '../proyectos/hooks'
import type { SujetoNovedad } from './tipos'

/** Código → nombre de aplicaciones y orígenes según GET /api/catalogos (si faltan, se muestra el código). */
export function useNombresNovedad() {
  const { data } = useCatalogos()
  return useMemo(() => {
    const aplicaciones = new Map((data?.tiposAplicacionNovedad ?? []).map((a) => [a.codigo, a.nombre]))
    const origenes = new Map((data?.origenesNovedad ?? []).map((o) => [o.codigo, o.nombre]))
    return {
      aplicacion: (codigo: string) => aplicaciones.get(codigo) ?? codigo,
      origen: (codigo: string) => origenes.get(codigo) ?? codigo,
    }
  }, [data])
}

/** "DEV001 · Nombre" (PERSONA), "PRY-… · Nombre" (PROYECTO) o "General" (sujeto null). */
export function textoSujeto(sujeto: SujetoNovedad | null): string {
  if (sujeto?.empleado) {
    return `${sujeto.empleado.codigoEkon} · ${sujeto.empleado.nombre}`
  }
  if (sujeto?.proyecto) {
    return `${sujeto.proyecto.codigo} · ${sujeto.proyecto.nombre}`
  }
  return 'General'
}
