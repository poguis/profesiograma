// Token de concurrencia BASE (TAREA-19b2, pendiente 35). Lógica pura, sin React: se prueba con Vitest.
// El registro envía la versión de los datos con que se armó el formulario (los que vio el usuario), nunca la de la
// vista previa: si otro usuario registró después de abrir la pantalla, la vista previa trae otra versión y el registro
// se bloquea; si registró después de la vista previa, el servidor responde 409 porque la base ya no es la actual.
// Origen del token base: GET …/edicion (personal), GET …/cabecera (cabecera), última etapa del detalle al abrir el
// diálogo (cambio de estado) y GET …/reactivacion (reactivación, TAREA-19c).

export const MENSAJE_CAMBIO_POR_OTRO = 'El proyecto cambió desde que abriste esta pantalla. Recarga los datos para continuar.'

/** Versión de la última etapa (0 si no hay): token base del cambio de estado, que no tiene un GET propio. */
export function versionDeEtapas(etapas: readonly { version: number }[]): number {
  return etapas.reduce((maxima, e) => Math.max(maxima, e.version), 0)
}

/**
 * La vista previa trae otra versión que el token base: alguien registró después de que se abrió la pantalla.
 * (La vista previa lee la versión antes que los datos, así que nunca es menor que la base.)
 */
export function cambioPorOtro(vista: { datos: { versionProyecto: number } } | null, versionBase: number): boolean {
  return vista !== null && vista.datos.versionProyecto !== versionBase
}

/** Cuerpo del registro con el token base. */
export function conToken<T extends object>(cuerpo: T, versionBase: number): T & { versionProyecto: number } {
  return { ...cuerpo, versionProyecto: versionBase }
}
