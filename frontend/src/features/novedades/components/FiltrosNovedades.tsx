import { Button, Dropdown, Field, Option, SearchBox, Switch, makeStyles, tokens } from '@fluentui/react-components'
import { DismissCircle20Regular } from '@fluentui/react-icons'
import { useEffect, useState } from 'react'
import { SelectorFecha } from '../../../components/SelectorFecha'
import { useCatalogos } from '../../proyectos/hooks'
import { APLICACIONES, type CambioFiltrosNovedades } from '../filtrosUrl'
import { useNombresNovedad } from '../presentacion'
import type { FiltrosNovedades as Filtros } from '../tipos'

const ESPERA_TEXTO_MS = 400
const TODOS = ''
const MOVIL = '@media (max-width: 599px)'

const useEstilos = makeStyles({
  contenedor: {
    display: 'flex',
    flexWrap: 'wrap',
    alignItems: 'flex-start',
    gap: tokens.spacingHorizontalM,
    [MOVIL]: { flexDirection: 'column', alignItems: 'stretch' },
  },
  campo: { minWidth: '180px' },
  texto: { minWidth: '260px', [MOVIL]: { minWidth: 0 } },
  boton: { marginTop: '26px', [MOVIL]: { marginTop: 0, width: '100%' } }, // alinea con los controles (debajo de la etiqueta)
})

export interface FiltrosNovedadesProps {
  filtros: Filtros
  hayFiltros: boolean
  /** Errores de validación (400) de la API por campo. */
  erroresApi?: Record<string, string[]>
  onCambiar: (cambios: CambioFiltrosNovedades, opciones?: { reemplazar?: boolean }) => void
  onLimpiar: () => void
}

/** Filtros del listado (patrón de FiltrosProyectos): aplicación, tipo, origen, texto, desde, hasta e incluir anuladas. */
export function FiltrosNovedades({ filtros, hayFiltros, erroresApi, onCambiar, onLimpiar }: FiltrosNovedadesProps) {
  const estilos = useEstilos()
  const { data: catalogos, isPending: cargandoCatalogos } = useCatalogos()
  const nombres = useNombresNovedad()

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

  const tipos = catalogos?.tiposNovedad ?? []
  const origenes = catalogos?.origenesNovedad ?? []
  const nombreTipo = tipos.find((t) => t.codigo === filtros.tipo)?.nombre ?? filtros.tipo

  return (
    <div className={estilos.contenedor} role="search" aria-label="Filtros de novedades">
      <Field label="Aplicación" className={estilos.campo} validationMessage={erroresApi?.aplicacion?.join(' ')}>
        <Dropdown
          value={filtros.aplicacion ? nombres.aplicacion(filtros.aplicacion) : 'Todas'}
          selectedOptions={[filtros.aplicacion ?? TODOS]}
          onOptionSelect={(_, datos) => onCambiar({ aplicacion: datos.optionValue || undefined })}
        >
          <Option value={TODOS}>Todas</Option>
          {APLICACIONES.map((a) => (
            <Option key={a} value={a}>
              {nombres.aplicacion(a)}
            </Option>
          ))}
        </Dropdown>
      </Field>

      <Field label="Tipo" className={estilos.campo} validationMessage={erroresApi?.tipo?.join(' ')}>
        <Dropdown
          value={nombreTipo ?? 'Todos'}
          selectedOptions={[filtros.tipo ?? TODOS]}
          disabled={cargandoCatalogos}
          onOptionSelect={(_, datos) => onCambiar({ tipo: datos.optionValue || undefined })}
        >
          <Option value={TODOS}>Todos</Option>
          {tipos.map((t) => (
            <Option key={t.codigo} value={t.codigo}>
              {t.nombre}
            </Option>
          ))}
        </Dropdown>
      </Field>

      <Field label="Origen" className={estilos.campo} validationMessage={erroresApi?.origen?.join(' ')}>
        <Dropdown
          value={filtros.origen ? nombres.origen(filtros.origen) : 'Todos'}
          selectedOptions={[filtros.origen ?? TODOS]}
          disabled={cargandoCatalogos}
          onOptionSelect={(_, datos) => onCambiar({ origen: datos.optionValue || undefined })}
        >
          <Option value={TODOS}>Todos</Option>
          {origenes.map((o) => (
            <Option key={o.codigo} value={o.codigo}>
              {o.nombre}
            </Option>
          ))}
        </Dropdown>
      </Field>

      <Field label="Buscar" hint="Código de la novedad, empleado o proyecto" className={estilos.texto}>
        <SearchBox value={texto} placeholder="Ej.: NOV-, DEV001 o nombre" onChange={(_, datos) => setTexto(datos.value)} />
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

      <Switch
        className={estilos.boton}
        label="Incluir anuladas"
        checked={filtros.incluirAnuladas}
        onChange={(_, datos) => onCambiar({ incluirAnuladas: datos.checked })}
      />

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
