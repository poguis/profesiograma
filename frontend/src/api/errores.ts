export type TipoErrorApi =
  | 'validacion' // 400
  | 'noAutenticado' // 401
  | 'prohibido' // 403
  | 'noEncontrado' // 404
  | 'servidor' // 5xx u otro estado no esperado
  | 'red' // sin respuesta (fetch falló)

const TITULOS: Record<TipoErrorApi, string> = {
  validacion: 'Los datos enviados no son válidos.',
  noAutenticado: 'No autenticado.',
  prohibido: 'No tiene permisos para esta acción.',
  noEncontrado: 'El recurso solicitado no existe.',
  servidor: 'Error del servidor o la API no está disponible.',
  red: 'No se pudo conectar con la API.',
}

/** Error tipado de la API. `errores` solo viene en 400 (ValidationProblem). */
export class ErrorApi extends Error {
  readonly tipo: TipoErrorApi
  readonly estado: number | null
  readonly titulo: string
  readonly detalle?: string
  readonly errores?: Record<string, string[]>

  constructor(datos: {
    tipo: TipoErrorApi
    estado: number | null
    titulo?: string
    detalle?: string
    errores?: Record<string, string[]>
  }) {
    const titulo = datos.titulo ?? TITULOS[datos.tipo]
    super(titulo)
    this.name = 'ErrorApi'
    this.tipo = datos.tipo
    this.estado = datos.estado
    this.titulo = titulo
    this.detalle = datos.detalle
    this.errores = datos.errores
  }
}

export function tipoPorEstado(estado: number): TipoErrorApi {
  switch (estado) {
    case 400:
      return 'validacion'
    case 401:
      return 'noAutenticado'
    case 403:
      return 'prohibido'
    case 404:
      return 'noEncontrado'
    default:
      return 'servidor'
  }
}

/** Errores que no tiene sentido reintentar (dependen de la petición o del usuario, no de la red). */
export function esErrorDefinitivo(error: unknown): boolean {
  return (
    error instanceof ErrorApi &&
    (error.tipo === 'validacion' ||
      error.tipo === 'noAutenticado' ||
      error.tipo === 'prohibido' ||
      error.tipo === 'noEncontrado')
  )
}
