// Sincronización manual de PERMISO MEDICO (TAREA-24d, FASE_5_Novedades §8): quién ve el botón, etiquetas de las
// categorías de omisión (§8.5), filas del resumen e interpretación de errores. Lógica pura, sin React.
import type { UsuarioActualDto } from '../../api/tipos'
import { ErrorApi } from '../../api/errores'
import { type ErroresNovedad, interpretarErrorDesconocido } from './erroresNovedad'
import type { ResultadoSincronizacion } from './tipos'

/** Máximo de códigos omitidos que devuelve el backend. */
export const MAX_CODIGOS_OMITIDOS = 100
export const NOTA_AUSENTES = 'No se anulan; solo se cuentan.'
export const NOTA_CODIGOS = 'Se muestran hasta 100.'
export const SUGERENCIA_PROHIBIDO = 'Cierre el diálogo y recargue la página.'

/**
 * Rol Admin según GET /api/usuarios/me, sin distinguir mayúsculas (como UsuarioActual.TieneRol). Solo decide si se
 * muestra el botón: la API exige la política Admin igual.
 */
export function esAdmin(usuario: Pick<UsuarioActualDto, 'roles'> | null | undefined): boolean {
  return (usuario?.roles ?? []).some((rol) => rol.toLowerCase() === 'admin')
}

const ETIQUETAS_OMISION: Record<string, string> = {
  filaIlegible: 'Fila ilegible',
  uidInvalido: 'Identificador inválido o repetido',
  jsonInvalido: 'Detalle ilegible',
  otroOrigen: 'Otro origen (no es PERMISOS_MEDICOS)',
  tipoNoSoportado: 'Tipo de novedad no soportado',
  aplicacionNoSoportada: 'Aplicación no soportada',
  fechasInvalidas: 'Fechas inválidas',
  sinEmpleado: 'Empleado no encontrado',
  conflictoCodigo: 'Conflicto de código',
  conflictoLegacyId: 'Conflicto de identificador legado',
  noActivaSinRegistro: 'Anulada en el origen y sin registro aquí',
}

/** Etiqueta de una categoría de omisión; una clave desconocida se muestra tal cual. */
export function etiquetaOmision(categoria: string): string {
  return Object.hasOwn(ETIQUETAS_OMISION, categoria) ? ETIQUETAS_OMISION[categoria] : categoria
}

export interface FilaResumen {
  etiqueta: string
  valor: number
  nota?: string
}

export interface ResumenSincronizacion {
  conteos: FilaResumen[]
  totalOmitidas: number
  /** Solo categorías con n > 0, en el orden de la respuesta. Vacío = «Ninguna.». */
  omitidas: FilaResumen[]
  codigos: string[]
  /** true si llegaron exactamente 100 códigos (puede haber más). */
  codigosTruncados: boolean
}

/** Filas del resumen en orden fijo, con los ceros incluidos. */
export function resumenSincronizacion(r: ResultadoSincronizacion): ResumenSincronizacion {
  const omitidas = Object.entries(r.omitidas ?? {})
    .filter(([, n]) => n > 0)
    .map(([categoria, n]) => ({ etiqueta: etiquetaOmision(categoria), valor: n }))
  const codigos = r.codigosOmitidos ?? []
  return {
    conteos: [
      { etiqueta: 'Leídas', valor: r.leidas },
      { etiqueta: 'Nuevas', valor: r.nuevas },
      { etiqueta: 'Actualizadas', valor: r.actualizadas },
      { etiqueta: 'Anuladas', valor: r.anuladas },
      { etiqueta: 'Reactivadas', valor: r.reactivadas },
      { etiqueta: 'Sin cambios', valor: r.sinCambios },
      { etiqueta: 'Ausentes en el origen', valor: r.ausentesEnOrigen, nota: NOTA_AUSENTES },
    ],
    totalOmitidas: omitidas.reduce((total, fila) => total + fila.valor, 0),
    omitidas,
    codigos,
    codigosTruncados: codigos.length === MAX_CODIGOS_OMITIDOS,
  }
}

/** Errores del diálogo: los comunes (503 y red con «Reintentar»); un 403 (el rol cambió) sugiere cerrar y recargar. */
export function interpretarErrorSincronizacion(error: unknown): ErroresNovedad<never> {
  const r = interpretarErrorDesconocido<never>(error, [])
  return error instanceof ErrorApi && error.tipo === 'prohibido' ? { ...r, generales: [...r.generales, SUGERENCIA_PROHIBIDO] } : r
}
