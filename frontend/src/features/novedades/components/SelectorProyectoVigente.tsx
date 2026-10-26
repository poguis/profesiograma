import { Badge, Button, Combobox, Option, Spinner, Text, makeStyles, tokens } from '@fluentui/react-components'
import { useState } from 'react'
import { ErrorApi } from '../../../api/errores'
import { useValorConRetardo } from '../../../hooks/useValorConRetardo'
import type { ProyectoElegido } from '../formularioNovedad'
import { useProyectosVigentes } from '../hooks'

const ESPERA_TEXTO_MS = 400
const SUSPENDIDO = 'SUSPENDIDO'

const useEstilos = makeStyles({
  contenedor: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXS },
  combo: { width: '100%' },
  opcion: { display: 'flex', alignItems: 'center', gap: tokens.spacingHorizontalS },
  aviso: { color: tokens.colorNeutralForeground3 },
})

const etiqueta = (p: ProyectoElegido) => `${p.codigo} · ${p.nombre}`

export interface SelectorProyectoVigenteProps {
  valor: ProyectoElegido | null
  onCambiar: (proyecto: ProyectoElegido | null) => void
  id?: string
  invalido?: boolean
}

/**
 * Proyecto vigente para una novedad PROYECTO (U6): búsqueda con espera; ACTIVO y SUSPENDIDO (con insignia), una consulta
 * por estado vigente. Escribir borra la elección hasta elegir otra opción.
 */
export function SelectorProyectoVigente({ valor, onCambiar, id, invalido }: SelectorProyectoVigenteProps) {
  const estilos = useEstilos()
  const [texto, setTexto] = useState(valor ? etiqueta(valor) : '')
  const buscado = useValorConRetardo(valor ? '' : texto.trim(), ESPERA_TEXTO_MS)
  const { proyectos, cargando, error, hayMas, reintentar } = useProyectosVigentes(buscado, true)

  return (
    <div className={estilos.contenedor}>
      <Combobox
        id={id}
        className={estilos.combo}
        aria-invalid={invalido || undefined}
        freeform
        placeholder="Código o nombre del proyecto"
        value={texto}
        selectedOptions={valor ? [String(valor.id)] : []}
        onChange={(evento) => {
          setTexto(evento.target.value)
          if (valor) {
            onCambiar(null)
          }
        }}
        onOptionSelect={(_, datos) => {
          const elegido = proyectos.find((p) => String(p.id) === datos.optionValue)
          if (elegido) {
            const proyecto = { id: elegido.id, codigo: elegido.codigo, nombre: elegido.nombre }
            setTexto(etiqueta(proyecto))
            onCambiar(proyecto)
          }
        }}
      >
        {proyectos.map((p) => (
          <Option key={p.id} value={String(p.id)} text={etiqueta(p)}>
            <span className={estilos.opcion}>
              {etiqueta(p)}
              {p.estado === SUSPENDIDO && (
                <Badge appearance="tint" color="warning" size="small">
                  Suspendido
                </Badge>
              )}
            </span>
          </Option>
        ))}
        {!cargando && proyectos.length === 0 && (
          <Option value="" disabled text="">
            No hay proyectos vigentes con ese criterio.
          </Option>
        )}
      </Combobox>
      {cargando && <Spinner size="tiny" label="Buscando proyectos…" labelPosition="after" />}
      {hayMas && !valor && (
        <Text size={200} className={estilos.aviso}>
          Hay más coincidencias: escriba para afinar la búsqueda.
        </Text>
      )}
      {error && (
        <Text size={200}>
          {error instanceof ErrorApi ? error.titulo : 'No se pudieron cargar los proyectos.'}{' '}
          <Button size="small" onClick={reintentar}>
            Reintentar
          </Button>
        </Text>
      )}
    </div>
  )
}
