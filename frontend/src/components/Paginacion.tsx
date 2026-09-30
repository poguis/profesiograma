import { Button, Dropdown, Option, Text, makeStyles, tokens } from '@fluentui/react-components'
import { ChevronLeft20Regular, ChevronRight20Regular } from '@fluentui/react-icons'
import { useId } from 'react'

const useEstilos = makeStyles({
  contenedor: {
    display: 'flex',
    flexWrap: 'wrap',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: tokens.spacingHorizontalL,
  },
  grupo: { display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS },
  tamano: { minWidth: '90px' },
})

export interface PaginacionProps {
  pagina: number
  tamano: number
  total: number
  /** Texto del contador, p. ej. ["proyecto", "proyectos"]. */
  unidad: [singular: string, plural: string]
  tamanos?: number[]
  deshabilitada?: boolean
  onPagina: (pagina: number) => void
  onTamano: (tamano: number) => void
}

export function Paginacion({
  pagina,
  tamano,
  total,
  unidad,
  tamanos = [10, 20, 50],
  deshabilitada = false,
  onPagina,
  onTamano,
}: PaginacionProps) {
  const estilos = useEstilos()
  const idTamano = useId()
  const totalPaginas = Math.max(1, Math.ceil(total / Math.max(1, tamano)))
  const opciones = tamanos.includes(tamano) ? tamanos : [...tamanos, tamano].sort((a, b) => a - b)

  return (
    <div className={estilos.contenedor}>
      <Text>
        {total} {total === 1 ? unidad[0] : unidad[1]}
      </Text>

      <div className={estilos.grupo}>
        <Button
          icon={<ChevronLeft20Regular />}
          disabled={deshabilitada || pagina <= 1}
          onClick={() => onPagina(pagina - 1)}
        >
          Anterior
        </Button>
        <Text aria-live="polite">
          Página {pagina} de {totalPaginas}
        </Text>
        <Button
          icon={<ChevronRight20Regular />}
          iconPosition="after"
          disabled={deshabilitada || pagina >= totalPaginas}
          onClick={() => onPagina(pagina + 1)}
        >
          Siguiente
        </Button>
      </div>

      <div className={estilos.grupo}>
        <Text id={idTamano}>Por página</Text>
        <Dropdown
          aria-labelledby={idTamano}
          className={estilos.tamano}
          value={String(tamano)}
          selectedOptions={[String(tamano)]}
          disabled={deshabilitada}
          onOptionSelect={(_, datos) => {
            const nuevo = Number(datos.optionValue)
            if (Number.isInteger(nuevo) && nuevo !== tamano) {
              onTamano(nuevo)
            }
          }}
        >
          {opciones.map((t) => (
            <Option key={t} value={String(t)}>
              {String(t)}
            </Option>
          ))}
        </Dropdown>
      </div>
    </div>
  )
}
