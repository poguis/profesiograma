// Reglas de rol en el frontend (pendiente 70, TAREA-25-2; antes en features/novedades/sincronizacion.ts). Solo deciden
// qué se muestra: la API exige las políticas igual. Lógica pura, sin React.
import type { UsuarioActualDto } from '../api/tipos'

/** Rol Admin según GET /api/usuarios/me, sin distinguir mayúsculas (como UsuarioActual.TieneRol). */
export function esAdmin(usuario: Pick<UsuarioActualDto, 'roles'> | null | undefined): boolean {
  return (usuario?.roles ?? []).some((rol) => rol.toLowerCase() === 'admin')
}
