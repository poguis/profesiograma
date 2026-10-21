import {
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
  Spinner,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components'
import { ArrowDownload20Regular, Dismiss24Regular } from '@fluentui/react-icons'
import { useEffect, useRef, useState } from 'react'
import { Link } from 'react-router'
import { ErrorApi } from '../../../api/errores'
import { EstadoError } from '../../../components/EstadoError'
import { SelectorFecha } from '../../../components/SelectorFecha'
import { interpretarErrorDialogo } from '../erroresDialogo'
import { useDescargarReporte, useResumenReporte } from '../hooks'
import {
  AVISO_CEDULAS,
  type EstadoResumen,
  MENSAJE_SIN_FILAS,
  type ValidacionRango,
  avisosResumen,
  consultaReporte,
  nombrePorDefecto,
  ofreceReintento,
  puedeDescargar,
  rangoInicial,
  rangoValido,
  validarRango,
} from '../reporteExcel'

const useEstilos = makeStyles({
  superficie: { width: 'min(560px, 100vw)', maxWidth: '100vw', maxHeight: '100dvh' },
  contenido: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM, overflowY: 'auto' },
  nombre: { color: tokens.colorNeutralForeground3 },
  fechas: { display: 'flex', flexWrap: 'wrap', gap: tokens.spacingHorizontalM },
  avisos: { margin: 0, paddingLeft: tokens.spacingHorizontalL },
})

export interface ProyectoReporte {
  id: number
  codigo: string
  nombre: string
  fechaInicio: string
  fechaFin: string
}

export interface DialogoExportarReporteProps {
  proyecto: ProyectoReporte
  /** Ruta del listado (enlace del 404). */
  rutaListado: string
  onCerrar: () => void
  /** Descarga hecha: el padre cierra el diálogo y muestra el aviso (filas = X-Reporte-Filas, si se pudo leer). */
  onRealizado: (filas: number | undefined) => void
}

/**
 * «Exportar reporte a Excel» (TAREA-22b; backend TAREA-22-2). Rango obligatorio, sin tope, que arranca en el del
 * proyecto (Z18); resumen previo informativo (Z19, Z26); descarga como mutación (Z20). El padre lo monta solo al
 * abrirlo, así que cada apertura empieza con el rango inicial y sin errores ni resultados anteriores.
 * Cerrar (Cancelar, Esc o la X) durante la generación cancela la petición (Z28).
 */
export function DialogoExportarReporte({ proyecto, rutaListado, onCerrar, onRealizado }: DialogoExportarReporteProps) {
  const estilos = useEstilos()
  const [rango, setRango] = useState(() => rangoInicial(proyecto))
  const [erroresFormato, setErroresFormato] = useState<ValidacionRango>({})
  const descargar = useDescargarReporte(proyecto.id)
  const controlador = useRef<AbortController | null>(null)
  const enviandoRef = useRef(false)

  const validacion = validarRango(rango.desde, rango.hasta, erroresFormato)
  const consulta = rangoValido(validacion) ? consultaReporte(rango.desde!, rango.hasta!) : undefined
  const resumen = useResumenReporte(proyecto.id, consulta, true)
  // Con keepPreviousData el resumen anterior sigue a la vista mientras llega el nuevo: hasta entonces, "cargando".
  const estadoResumen: EstadoResumen = resumen.isFetching || resumen.isPlaceholderData ? 'cargando' : resumen.error ? 'error' : 'listo'
  const enviando = descargar.isPending

  const errorDescarga = descargar.error instanceof ErrorApi ? descargar.error : undefined
  const errores = errorDescarga ? interpretarErrorDialogo(errorDescarga, { campos: ['desde', 'hasta'] as const }) : undefined

  // Al desmontar (cierre del padre por cualquier vía) también se cancela una descarga en curso.
  useEffect(() => () => controlador.current?.abort(), [])

  const cerrar = () => {
    controlador.current?.abort()
    onCerrar()
  }

  const cambiar = (campo: 'desde' | 'hasta', valor: string | undefined) => {
    descargar.reset() // los errores de la descarga anterior ya no aplican
    setRango((r) => ({ ...r, [campo]: valor }))
  }

  const formato = (campo: 'desde' | 'hasta') => (mensaje: string | undefined) =>
    setErroresFormato((e) => ({ ...e, [campo]: mensaje }))

  const iniciarDescarga = () => {
    if (enviandoRef.current || consulta === undefined || !puedeDescargar(validacion, resumen.data, estadoResumen, enviando)) {
      return // protección de doble clic: isPending tarda un render en llegar
    }

    enviandoRef.current = true
    const c = new AbortController()
    controlador.current = c
    descargar.mutate(
      { consulta, nombrePorDefecto: nombrePorDefecto(proyecto.codigo, rango.desde!, rango.hasta!), signal: c.signal },
      {
        onSuccess: (r) => onRealizado(r.filas),
        onSettled: () => {
          enviandoRef.current = false
        },
      },
    )
  }

  const avisos = resumen.data ? avisosResumen(resumen.data) : undefined
  const sinFilas = estadoResumen === 'listo' && resumen.data?.filas === 0

  return (
    <Dialog open onOpenChange={(_, d) => !d.open && cerrar()}>
      <DialogSurface className={estilos.superficie}>
        <DialogBody>
          <DialogTitle
            action={<Button appearance="subtle" aria-label="Cerrar" icon={<Dismiss24Regular />} onClick={cerrar} />}
          >
            Exportar reporte a Excel · {proyecto.codigo}
          </DialogTitle>
          <DialogContent className={estilos.contenido}>
            <Text className={estilos.nombre}>{proyecto.nombre}</Text>

            <MessageBar intent="warning">
              <MessageBarBody>{AVISO_CEDULAS}</MessageBarBody>
            </MessageBar>

            <div className={estilos.fechas}>
              <Field label="Desde" required validationMessage={validacion.desde ?? errores?.campos.desde}>
                <SelectorFecha valor={rango.desde} onCambiar={(v) => cambiar('desde', v)} onErrorFormato={formato('desde')} />
              </Field>
              <Field label="Hasta" required validationMessage={validacion.hasta ?? errores?.campos.hasta}>
                <SelectorFecha valor={rango.hasta} onCambiar={(v) => cambiar('hasta', v)} onErrorFormato={formato('hasta')} />
              </Field>
            </div>

            {consulta !== undefined && (
              <section aria-label="Resumen del reporte" aria-live="polite">
                {estadoResumen === 'cargando' && <Spinner size="tiny" label="Calculando resumen…" labelPosition="after" />}
                {estadoResumen === 'error' && (
                  <>
                    <EstadoError error={resumen.error} />
                    <Button onClick={() => void resumen.refetch()}>Reintentar</Button>
                  </>
                )}
                {estadoResumen === 'listo' && avisos && (
                  sinFilas ? (
                    <MessageBar intent="info">
                      <MessageBarBody>{MENSAJE_SIN_FILAS}</MessageBarBody>
                    </MessageBar>
                  ) : (
                    <MessageBar intent={avisos.avisos.length > 0 ? 'warning' : 'info'}>
                      <MessageBarBody>
                        <MessageBarTitle>{avisos.totales}</MessageBarTitle>
                        {avisos.avisos.length > 0 && (
                          <ul className={estilos.avisos}>
                            {avisos.avisos.map((a) => (
                              <li key={a}>{a}</li>
                            ))}
                          </ul>
                        )}
                      </MessageBarBody>
                    </MessageBar>
                  )
                )}
              </section>
            )}

            {/* Una cancelación (DOMException) no es ErrorApi: nunca llega aquí. */}
            {errores && (errores.generales.length > 0 || errores.noEncontrado) && (
              <MessageBar intent="error">
                <MessageBarBody>
                  {errores.noEncontrado ? (
                    <>
                      <MessageBarTitle>Proyecto no encontrado o sin acceso</MessageBarTitle>
                      <Link to={rutaListado}>Ir al listado de proyectos</Link>
                    </>
                  ) : (
                    <MessageBarTitle>{errores.generales.join(' ')}</MessageBarTitle>
                  )}
                </MessageBarBody>
                {errorDescarga && ofreceReintento(errorDescarga.tipo) && (
                  <MessageBarActions>
                    <Button disabled={enviando} onClick={iniciarDescarga}>
                      Reintentar
                    </Button>
                  </MessageBarActions>
                )}
              </MessageBar>
            )}
          </DialogContent>
          <DialogActions>
            <Button onClick={cerrar}>Cancelar</Button>
            <Button
              appearance="primary"
              icon={enviando ? <Spinner size="tiny" /> : <ArrowDownload20Regular />}
              aria-busy={enviando}
              disabled={!puedeDescargar(validacion, resumen.data, estadoResumen, enviando)}
              onClick={iniciarDescarga}
            >
              {enviando ? 'Generando…' : 'Descargar'}
            </Button>
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  )
}
