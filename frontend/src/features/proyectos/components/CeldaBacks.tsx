import { Badge, Tooltip } from '@fluentui/react-components'

const VISIBLES = 2

/** Muestra hasta 2 backs y "+N" con el resto en un tooltip. */
export function CeldaBacks({ backs }: { backs: string[] }) {
  if (backs.length === 0) {
    return <>—</>
  }

  const visibles = backs.slice(0, VISIBLES)
  const resto = backs.slice(VISIBLES)

  return (
    <span>
      {visibles.join(', ')}
      {resto.length > 0 && (
        <>
          {' '}
          <Tooltip content={resto.join(', ')} relationship="description">
            <Badge appearance="tint" color="informative" size="small" tabIndex={0}>
              +{resto.length}
            </Badge>
          </Tooltip>
        </>
      )}
    </span>
  )
}
