import {
  Badge,
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  Field,
  MessageBar,
  MessageBarActions,
  MessageBarBody,
  MessageBarTitle,
  SearchBox,
  Spinner,
  Switch,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components'
import { Add20Regular } from '@fluentui/react-icons'
import { useState } from 'react'
import { Paginacion } from '../../../components/Paginacion'
import { useValorConRetardo } from '../../../hooks/useValorConRetardo'
import { aEmpleadoFila, detalleEmpleado, errorBuscador, marcasEmpleado } from '../buscadorEmpleados'
import type { EmpleadoFila } from '../formularioProyecto'
import { useBusquedaEmpleados } from '../hooks'
import type { EmpleadoBusqueda } from '../tipos'

const ESPERA_TEXTO_MS = 400
const TAMANOS = [5, 10, 20]

const useEstilos = makeStyles({
  superficie: { width: 'min(720px, 100vw)', maxWidth: '100vw' },
  contenido: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
  filtros: { display: 'flex', flexWrap: 'wrap', alignItems: 'flex-end', gap: tokens.spacingHorizontalM },
  texto: { flexGrow: 1, minWidth: '220px' },
  lista: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXS, margin: 0, padding: 0 },
  item: {
    display: 'flex',
    flexWrap: 'wrap',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: tokens.spacingHorizontalS,
    padding: `${tokens.spacingVerticalS} ${tokens.spacingHorizontalS}`,
    borderBottom: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke2}`,
    listStyleType: 'none',
  },
  datos: { display: 'flex', flexDirection: 'column', minWidth: 0 },
  marcas: { display: 'flex', flexWrap: 'wrap', gap: tokens.spacingHorizontalXS },
})

export interface BuscadorEmpleadosProps {
  abierto: boolean
  /** "Agregar principal" | "Agregar back" */
  titulo: string
  /** claveEmpleado(código) → etiquetas donde ya está ("P1", "Back 2"). Se marca, pero no se bloquea (E6). */
  etiquetas: Map<string, string[]>
  /** false al llegar al máximo (R9). */
  puedeAgregar: boolean
  onSeleccionar: (empleado: EmpleadoFila) => void
  onCerrar: () => void
}

/**
 * Búsqueda de empleados activos (GET /api/empleados) con espera al escribir y paginación. Queda abierto para agregar
 * varios. TAREA-26d-2: clave `codigoEkon`; `avisoErp` como aviso no bloqueante; 503 con "Reintentar".
 */
export function BuscadorEmpleados({ abierto, titulo, etiquetas, puedeAgregar, onSeleccionar, onCerrar }: BuscadorEmpleadosProps) {
  const estilos = useEstilos()
  const [texto, setTexto] = useState('')
  const [verOtros, setVerOtros] = useState(false)
  const [pagina, setPagina] = useState(1)
  const [tamano, setTamano] = useState(10)
  const textoBusqueda = useValorConRetardo(texto.trim(), ESPERA_TEXTO_MS)

  const { data, error, isPending, isFetching, refetch } = useBusquedaEmpleados(
    { texto: textoBusqueda || undefined, soloMisDepartamentos: !verOtros, pagina, tamano },
    abierto,
  )

  const agregar = (e: EmpleadoBusqueda) => onSeleccionar(aEmpleadoFila(e))
  const fallo = error ? errorBuscador(error) : null

  return (
    <Dialog open={abierto} onOpenChange={(_, d) => !d.open && onCerrar()}>
      <DialogSurface className={estilos.superficie}>
        <DialogBody>
          <DialogTitle>{titulo}</DialogTitle>
          <DialogContent className={estilos.contenido}>
            <div className={estilos.filtros}>
              <Field label="Buscar" hint="Código EKON o nombre" className={estilos.texto}>
                <SearchBox
                  value={texto}
                  placeholder="Ej.: DEV006 o apellido"
                  onChange={(_, d) => {
                    setTexto(d.value)
                    setPagina(1)
                  }}
                />
              </Field>
              <Switch
                label="Ver otros departamentos"
                checked={verOtros}
                onChange={(_, d) => {
                  setVerOtros(d.checked)
                  setPagina(1)
                }}
              />
              {isFetching && <Spinner size="tiny" label="Buscando…" />}
            </div>

            {!puedeAgregar && (
              <MessageBar intent="warning">
                <MessageBarBody>Se alcanzó el máximo permitido; no se pueden agregar más.</MessageBarBody>
              </MessageBar>
            )}

            {fallo && (
              <MessageBar intent="error">
                <MessageBarBody>
                  <MessageBarTitle>{fallo.titulo}</MessageBarTitle>
                  {fallo.detalle}
                </MessageBarBody>
                {fallo.reintentable && (
                  <MessageBarActions>
                    <Button disabled={isFetching} onClick={() => void refetch()}>
                      Reintentar
                    </Button>
                  </MessageBarActions>
                )}
              </MessageBar>
            )}

            {!error && data?.avisoErp && (
              <MessageBar intent="warning">
                <MessageBarBody>{data.avisoErp}</MessageBarBody>
              </MessageBar>
            )}

            {data && data.total === 0 && (
              <Text>
                No hay empleados activos con ese criterio
                {verOtros ? '.' : ' en sus departamentos. Pruebe "Ver otros departamentos".'}
              </Text>
            )}

            {data && data.items.length > 0 && (
              <ul className={estilos.lista} aria-label="Empleados encontrados">
                {data.items.map((e) => {
                  const marcas = marcasEmpleado(etiquetas, e)
                  return (
                    <li key={e.codigoEkon} className={estilos.item}>
                      <div className={estilos.datos}>
                        <Text weight="semibold">{e.nombreCompleto}</Text>
                        <Text size={200}>{detalleEmpleado(e)}</Text>
                        {marcas.length > 0 && (
                          <div className={estilos.marcas}>
                            {marcas.map((m) => (
                              <Badge key={m} appearance="tint" color="informative">
                                Ya agregado: {m}
                              </Badge>
                            ))}
                          </div>
                        )}
                      </div>
                      <Button
                        icon={<Add20Regular />}
                        disabled={!puedeAgregar}
                        aria-label={`Agregar ${e.nombreCompleto}`}
                        onClick={() => agregar(e)}
                      >
                        Agregar
                      </Button>
                    </li>
                  )
                })}
              </ul>
            )}

            {data && data.total > 0 && (
              <Paginacion
                pagina={data.pagina}
                tamano={data.tamano}
                total={data.total}
                unidad={['empleado', 'empleados']}
                tamanos={TAMANOS}
                deshabilitada={isFetching}
                onPagina={setPagina}
                onTamano={(nuevo) => {
                  setTamano(nuevo)
                  setPagina(1)
                }}
              />
            )}

            {isPending && !error && <Spinner label="Cargando empleados…" />}
          </DialogContent>
          <DialogActions>
            <Button appearance="primary" onClick={onCerrar}>
              Listo
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  )
}
