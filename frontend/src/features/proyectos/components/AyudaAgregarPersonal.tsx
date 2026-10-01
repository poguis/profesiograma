import { Text } from '@fluentui/react-components'

export interface AyudaAgregarPersonalProps {
  rangoListo: boolean
  lleno: boolean
  maximo: number
  /** "principales" | "backs" */
  plural: string
}

/** Ayuda junto al botón "Agregar": explica por qué está deshabilitado. */
export function AyudaAgregarPersonal({ rangoListo, lleno, maximo, plural }: AyudaAgregarPersonalProps) {
  if (!rangoListo) {
    return <Text size={200}>Defina la fecha de inicio y fin del proyecto para agregar personal.</Text>
  }
  if (lleno) {
    return (
      <Text size={200}>
        Se alcanzó el máximo de {maximo} {plural}.
      </Text>
    )
  }
  return null
}
