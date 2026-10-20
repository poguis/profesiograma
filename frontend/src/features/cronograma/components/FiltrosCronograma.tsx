import { Button, Dropdown, Field, Option, SearchBox, makeStyles, tokens } from '@fluentui/react-components'
import { CalendarToday20Regular, DismissCircle20Regular } from '@fluentui/react-icons'
import { useEffect, useState } from 'react'
import { useCatalogos } from '../../proyectos/hooks'
import { type FiltrosCronograma, opcionesProyecto } from '../cronograma'
import type { Cronograma, ProyectoCronograma } from '../tipos'
import { SelectorRango } from './SelectorRango'

const ESPERA_TEXTO_MS = 300
const TODOS = ''

const useEstilos = makeStyles({
  contenedor: { display: 'flex', flexWrap: 'wrap', alignItems: 'flex-start', gap: tokens.spacingHorizontalM },
  campo: { minWidth: '170px' },
  proyecto: { minWidth: '260px', maxWidth: '360px' },
  texto: { minWidth: '220px' },
  botones: { display: 'flex', gap: tokens.spacingHorizontalXS, marginTop: '26px' }, // alinea con los controles
})

export interface FiltrosCronogramaProps {
  filtros: FiltrosCronograma
  /** Rango efectivo que devolvió el backend (D6: mes actual sin fechas; D5 revisada: recortado). Tiene prioridad sobre la URL. */
  rango?: { desde: string; hasta: string }
  /** Última respuesta (aunque sea la anterior mientras llega una nueva): da la fecha que el usuario no tocó. */
  ultimaRespuesta?: Pick<Cronograma, 'desde' | 'hasta' | 'hastaSolicitado'>
  /** Proyectos de la respuesta (P8). */
  proyectos: ProyectoCronograma[]
  /** Errores de validación (400) de la API por campo. */
  erroresApi?: Record<string, string[]>
  onCambiar: (cambios: Partial<FiltrosCronograma>, opciones?: { reemplazar?: boolean }) => void
  onLimpiar: () => void
}

export function FiltrosCronograma({ filtros, rango, ultimaRespuesta, proyectos, erroresApi, onCambiar, onLimpiar }: FiltrosCronogramaProps) {
  const estilos = useEstilos()
  const { data: catalogos, isPending: cargandoCatalogos } = useCatalogos()

  // Persona (P9): con espera; el estado local se sincroniza si la URL cambia desde fuera (limpiar, atrás/adelante).
  const personaUrl = filtros.persona ?? ''
  const [persona, setPersona] = useState(personaUrl)
  const [ultimaPersonaUrl, setUltimaPersonaUrl] = useState(personaUrl)
  if (personaUrl !== ultimaPersonaUrl) {
    setUltimaPersonaUrl(personaUrl)
    setPersona(personaUrl)
  }

  useEffect(() => {
    if (persona.trim() === personaUrl) {
      return
    }
    const temporizador = setTimeout(() => onCambiar({ persona: persona.trim() || undefined }, { reemplazar: true }), ESPERA_TEXTO_MS)
    return () => clearTimeout(temporizador)
  }, [persona, personaUrl, onCambiar])

  // "Limpiar filtros" vuelve a montar el selector de rango, que borra sus errores de formato.
  const [reinicioRango, setReinicioRango] = useState(0)

  const opciones = opcionesProyecto(proyectos, filtros.proyectoId)
  const departamentos = catalogos?.departamentos ?? []
  const textoProyecto = opciones.find((o) => o.id === filtros.proyectoId)?.texto ?? 'Todos'
  const textoDepartamento = departamentos.find((d) => d.id === filtros.departamentoId)?.nombre ?? 'Todos'
  const hayFiltros = Object.values(filtros).some((v) => v !== undefined)
  const errorDe = (campo: string) => erroresApi?.[campo]?.join(' ')

  return (
    <div className={estilos.contenedor} role="search" aria-label="Filtros del cronograma">
      {/* Los selectores muestran el rango efectivo; la URL conserva el que pidió el usuario (D5 revisada). */}
      <SelectorRango
        key={reinicioRango}
        pedido={{ desde: filtros.desde, hasta: filtros.hasta }}
        efectivo={rango}
        ultimaRespuesta={ultimaRespuesta}
        errores={{ desde: errorDe('desde'), hasta: errorDe('hasta') }}
        onCambiar={onCambiar}
        reinicio={{ texto: 'Mes actual', icono: <CalendarToday20Regular /> }}
      />

      <Field label="Proyecto" className={estilos.proyecto} validationMessage={errorDe('proyectoId')}>
        <Dropdown
          value={textoProyecto}
          selectedOptions={[filtros.proyectoId === undefined ? TODOS : String(filtros.proyectoId)]}
          onOptionSelect={(_, d) => onCambiar({ proyectoId: d.optionValue ? Number(d.optionValue) : undefined })}
        >
          <Option value={TODOS}>Todos</Option>
          {opciones.map((o) => (
            <Option key={o.id} value={String(o.id)}>
              {o.texto}
            </Option>
          ))}
        </Dropdown>
      </Field>

      <Field label="Departamento" className={estilos.campo} validationMessage={errorDe('departamentoId')}>
        <Dropdown
          value={textoDepartamento}
          disabled={cargandoCatalogos}
          selectedOptions={[filtros.departamentoId === undefined ? TODOS : String(filtros.departamentoId)]}
          onOptionSelect={(_, d) => onCambiar({ departamentoId: d.optionValue ? Number(d.optionValue) : undefined })}
        >
          <Option value={TODOS}>Todos</Option>
          {departamentos.map((d) => (
            <Option key={d.id} value={String(d.id)}>
              {d.nombre}
            </Option>
          ))}
        </Dropdown>
      </Field>

      <Field label="Persona" hint="Nombre o código EKON" className={estilos.texto}>
        <SearchBox value={persona} placeholder="Ej.: 7255 o nombre" onChange={(_, d) => setPersona(d.value)} />
      </Field>

      <div className={estilos.botones}>
        <Button
          icon={<DismissCircle20Regular />}
          disabled={!hayFiltros}
          onClick={() => {
            setReinicioRango((n) => n + 1)
            onLimpiar()
          }}
        >
          Limpiar filtros
        </Button>
      </div>
    </div>
  )
}
