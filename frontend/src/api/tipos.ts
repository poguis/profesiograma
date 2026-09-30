// DTOs de la API (JSON en camelCase).

/** RFC 9110 / ASP.NET Core ProblemDetails. */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  instance?: string
  traceId?: string
}

/** ValidationProblem de ASP.NET Core: errores por campo. */
export interface ValidationProblemDetails extends ProblemDetails {
  errors: Record<string, string[]>
}

/** GET /api/usuarios/me */
export interface UsuarioActualDto {
  id: number
  email: string
  nombreMostrar: string | null
  roles: string[]
}
