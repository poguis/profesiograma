// Autenticación simulada (DevAuth) — SOLO desarrollo. La API la acepta únicamente en su entorno Development.
// Se carga solo con import dinámico dentro de ramas `devAuthActivo` (modo.ts): no llega al bundle de producción.

export const USUARIOS_DEV = ['admin', 'gestor', 'anonimo'] as const
export type UsuarioDev = (typeof USUARIOS_DEV)[number]

const CLAVE_ALMACEN = 'profesiograma.devUsuario'
const USUARIO_POR_DEFECTO: UsuarioDev = 'admin' // igual al UsuarioPorDefecto de la API

export function leerUsuarioDev(): UsuarioDev {
  try {
    const valor = localStorage.getItem(CLAVE_ALMACEN)
    return esUsuarioDev(valor) ? valor : USUARIO_POR_DEFECTO
  } catch {
    return USUARIO_POR_DEFECTO // localStorage no disponible
  }
}

export function guardarUsuarioDev(usuario: UsuarioDev): void {
  try {
    localStorage.setItem(CLAVE_ALMACEN, usuario)
  } catch {
    // localStorage no disponible: el cambio dura solo hasta recargar la página
  }
}

export function esUsuarioDev(valor: unknown): valor is UsuarioDev {
  return typeof valor === 'string' && (USUARIOS_DEV as readonly string[]).includes(valor)
}
