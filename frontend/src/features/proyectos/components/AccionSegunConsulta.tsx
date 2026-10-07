import { Button, Text, makeStyles, tokens } from '@fluentui/react-components'
import type { UseQueryResult } from '@tanstack/react-query'
import type { ReactElement } from 'react'

const useEstilos = makeStyles({
  accion: { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: tokens.spacingHorizontalS },
  secundario: { color: tokens.colorNeutralForeground3 },
})

/** Lo mínimo que la consulta debe traer: si se puede editar y, si no, por qué. */
export interface PermisoEdicion {
  puedeEditar: boolean
  motivo: string | null
}

export interface AccionSegunConsultaProps {
  consulta: UseQueryResult<PermisoEdicion>
  etiqueta: string
  icono: ReactElement
  onAbrir: () => void
}

/**
 * Acción del detalle habilitada según una consulta (TAREA-19a, P3; generalizada en la TAREA-19b): habilitada si
 * `puedeEditar`; si no, deshabilitada con el motivo. Si la consulta falla, deshabilitada con aviso y "Reintentar"
 * (el detalle se muestra igual).
 */
export function AccionSegunConsulta({ consulta, etiqueta, icono, onAbrir }: AccionSegunConsultaProps) {
  const estilos = useEstilos()
  const boton = (habilitado: boolean) => (
    <Button icon={icono} disabled={!habilitado} onClick={onAbrir}>
      {etiqueta}
    </Button>
  )

  if (consulta.isPending) {
    return boton(false)
  }
  if (consulta.isError) {
    return (
      <span className={estilos.accion}>
        {boton(false)}
        <Text size={200} className={estilos.secundario}>
          No se pudo verificar si el proyecto se puede editar.
        </Text>
        <Button size="small" disabled={consulta.isFetching} onClick={() => void consulta.refetch()}>
          Reintentar
        </Button>
      </span>
    )
  }
  if (!consulta.data.puedeEditar) {
    return (
      <span className={estilos.accion}>
        {boton(false)}
        <Text size={200} className={estilos.secundario}>
          {consulta.data.motivo}
        </Text>
      </span>
    )
  }
  return boton(true)
}
