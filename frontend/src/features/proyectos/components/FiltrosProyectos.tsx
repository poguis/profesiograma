import { Button, Dropdown, Field, Option, SearchBox, makeStyles, tokens } from '@fluentui/react-components'
import { DismissCircle20Regular } from '@fluentui/react-icons'
import { useEffect, useState } from 'react'
import { SelectorFecha } from '../../../components/SelectorFecha'
import type { CambioFiltros } from '../filtrosUrl'
import { useCatalogos } from '../hooks'
import type { FiltrosProyectos } from '../tipos'

const ESPERA_TEXTO_MS = 400
const TODOS = ''

const useEstilos = makeStyles({
  contenedor: {
    display: 'flex',
    flexWrap: 'wrap',
    alignItems: 'flex-start',
    gap: tokens.spacingHorizontalM,
  },
  campo: { minWidth: '180px' },
  texto: { minWidth: '260px' },
  boton: { marginTop: '26px' }, // alinea con los controles (debajo de la etiqueta)
})

export interface FiltrosProyectosProps {
  filtros: FiltrosProyectos
  hayFiltros: boolean
  /** Errores de validación (400) de la API por campo. */
  erroresApi?: Record<string, string[]>
  onCambiar: (cambios: CambioFiltros, opciones?: { reemplazar?: boolean }) => void
  onLimpiar: () => void
}

export function FiltrosProyectos({ filtros, hayFiltros, erroresApi, onCambiar, onLimpiar }: FiltrosProyectosProps) {
  const estilos = useEstilos()
  const { data: catalogos, isPending: cargandoCatalogos } = useCatalogos()

  // Texto con espera: el estado local se sincroniza cuando la URL cambia desde fuera (limpiar, atrás/adelante).
  const textoUrl = filtros.texto ?? ''
  const [texto, setTexto] = useState(textoUrl)
  const [ultimoTextoUrl, setUltimoTextoUrl] = useState(textoUrl)
  if (textoUrl !== ultimoTextoUrl) {
    setUltimoTextoUrl(textoUrl)
    setTexto(textoUrl)
  }

  useEffect(() => {
    if (texto.trim() === textoUrl) {
      return
    }
    const temporizador = setTimeout(
      () => onCambiar({ texto: texto.trim() || undefined }, { reemplazar: true }),
      ESPERA_TEXTO_MS,
    )
    return () => clearTimeout(temporizador)
  }, [texto, textoUrl, onCambiar])

  const [errorFormatoDesde, setErrorFormatoDesde] = useState<string>()
  const [errorFormatoHasta, setErrorFormatoHasta] = useState<string>()

  const estados = catalogos?.estadosProyecto ?? []
  const grupos = catalogos?.gruposProyecto ?? []
  const nombreEstado = estados.find((e) => e.codigo === filtros.estado)?.nombre ?? filtros.estado
  const nombreGrupo = grupos.find((g) => g.codigo === filtros.grupo)?.nombre ?? filtros.grupo

  return (
    <div className={estilos.contenedor} role="search" aria-label="Filtros de proyectos">
      <Field label="Estado" className={estilos.campo}>
        <Dropdown
          value={nombreEstado ?? 'Todos'}
          selectedOptions={[filtros.estado ?? TODOS]}
          disabled={cargandoCatalogos}
          onOptionSelect={(_, datos) => onCambiar({ estado: datos.optionValue || undefined })}
        >
          <Option value={TODOS}>Todos</Option>
          {estados.map((e) => (
            <Option key={e.codigo} value={e.codigo}>
              {e.nombre}
            </Option>
          ))}
        </Dropdown>
      </Field>

      <Field label="Grupo" className={estilos.campo}>
        <Dropdown
          value={nombreGrupo ?? 'Todos'}
          selectedOptions={[filtros.grupo ?? TODOS]}
          disabled={cargandoCatalogos}
          onOptionSelect={(_, datos) => onCambiar({ grupo: datos.optionValue || undefined })}
        >
          <Option value={TODOS}>Todos</Option>
          {grupos.map((g) => (
            <Option key={g.codigo} value={g.codigo}>
              {g.nombre}
            </Option>
          ))}
        </Dropdown>
      </Field>

      <Field label="Buscar" hint="Código o nombre" className={estilos.texto}>
        <SearchBox
          value={texto}
          placeholder="Ej.: PRY-2026 o nombre"
          onChange={(_, datos) => setTexto(datos.value)}
        />
      </Field>

      <Field
        label="Desde"
        className={estilos.campo}
        validationMessage={errorFormatoDesde ?? erroresApi?.desde?.join(' ')}
      >
        <SelectorFecha
          valor={filtros.desde}
          onCambiar={(valor) => onCambiar({ desde: valor })}
          onErrorFormato={setErrorFormatoDesde}
        />
      </Field>

      <Field
        label="Hasta"
        className={estilos.campo}
        validationMessage={errorFormatoHasta ?? erroresApi?.hasta?.join(' ')}
      >
        <SelectorFecha
          valor={filtros.hasta}
          onCambiar={(valor) => onCambiar({ hasta: valor })}
          onErrorFormato={setErrorFormatoHasta}
        />
      </Field>

      <Button
        className={estilos.boton}
        icon={<DismissCircle20Regular />}
        disabled={!hayFiltros}
        onClick={() => {
          setErrorFormatoDesde(undefined)
          setErrorFormatoHasta(undefined)
          onLimpiar()
        }}
      >
        Limpiar filtros
      </Button>
    </div>
  )
}
