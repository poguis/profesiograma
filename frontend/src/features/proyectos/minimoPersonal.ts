// Mínimo de personal (TAREA-19y, pendiente 33), común a "Nuevo proyecto" y "Actualizar personal" (TAREA-19b).
// Con PROYECTO_EXIGE_PRINCIPAL (`exigePrincipal`) al menos 1 principal (C10); si no, al menos 1 persona (principal o
// back). El error del servidor solo llega en un 400 (`principales` o `personal`); aquí solo la ayuda y el bloqueo.

export const MINIMO_PRINCIPALES = 1

export function cumpleMinimo(principales: number, backs: number, exigePrincipal: boolean): boolean {
  return exigePrincipal ? principales >= MINIMO_PRINCIPALES : principales + backs >= 1
}

/** Ayuda neutra (no error) mientras no se cumpla el mínimo; undefined si ya se cumple. */
export function ayudaMinimo(principales: number, backs: number, exigePrincipal: boolean): string | undefined {
  if (cumpleMinimo(principales, backs, exigePrincipal)) {
    return undefined
  }
  return exigePrincipal
    ? `Agrega al menos ${MINIMO_PRINCIPALES} principal para generar la vista previa.`
    : 'Agrega al menos 1 persona (principal o back) para generar la vista previa.'
}
