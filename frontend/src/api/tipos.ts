// DTOs de la API (JSON en camelCase).

/** RFC 9110 / ASP.NET Core ProblemDetails. */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  traceId?: string
  /** Extensiones (p. ej. `cruces` del 409). */
  [extension: string]: unknown
}

/** ValidationProblem de ASP.NET Core: errores por campo. */
export interface ValidationProblemDetails extends ProblemDetails {
  errors: Record<string, string[]>
}

/** Página de un listado de la API. `total` = registros que cumplen el filtro. */
export interface PaginaResultado<T> {
  items: T[]
  pagina: number
  tamano: number
  total: number
}

/** GET /api/usuarios/me */
export interface UsuarioActualDto {
  id: number
  email: string
  nombreMostrar: string | null
  roles: string[]
}
