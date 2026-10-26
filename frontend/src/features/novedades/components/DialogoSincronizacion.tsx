import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  MessageBar,
  MessageBarActions,
  MessageBarBody,
  Spinner,
  Text,
  makeStyles,
  mergeClasses,
  tokens,
} from '@fluentui/react-components'
import { useRef } from 'react'
import { formatearFechaHora } from '../../../utils/formato'
import { useSincronizarPermisosMedicos } from '../hooks'
import { NOTA_CODIGOS, interpretarErrorSincronizacion, resumenSincronizacion } from '../sincronizacion'
import type { ResultadoSincronizacion } from '../tipos'

const MOVIL = '@media (max-width: 599px)'

const useEstilos = makeStyles({
  superficie: { width: 'min(600px, 100vw)', maxWidth: '100vw', [MOVIL]: { width: '100vw' } },
  contenido: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
  tabla: { width: '100%', borderCollapse: 'collapse', tableLayout: 'fixed' },
  celda: {
    padding: `${tokens.spacingVerticalXXS} ${tokens.spacingHorizontalS}`,
    borderBottom: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke2}`,
    textAlign: 'left',
    overflowWrap: 'anywhere',
  },
  numero: { width: '64px', textAlign: 'right' },
  secundario: { color: tokens.colorNeutralForeground3 },
  codigos: { margin: 0, paddingLeft: tokens.spacingHorizontalL, overflowWrap: 'anywhere' },
  acciones: { [MOVIL]: { flexDirection: 'column', alignItems: 'stretch' } },
})

export interface DialogoSincronizacionProps {
  abierto: boolean
  onCerrar: () => void
}

/**
 * «Sincronizar permisos médicos» (TAREA-24d, solo Admin): confirmación → en curso → resultado o error. Mientras envía no
 * se cierra (ni Esc ni clic fuera). Sin `escenario` y sin datos personales: solo conteos y códigos NOV.
 */
export function DialogoSincronizacion({ abierto, onCerrar }: DialogoSincronizacionProps) {
  const estilos = useEstilos()
  const sincronizar = useSincronizarPermisosMedicos()
  const enviandoRef = useRef(false)
  const error = sincronizar.error ? interpretarErrorSincronizacion(sincronizar.error) : null

  const enviar = () => {
    // El ref evita un doble envío aunque el segundo clic llegue antes de que se deshabilite el botón.
    if (enviandoRef.current || sincronizar.isPending) {
      return
    }
    enviandoRef.current = true
    sincronizar.mutate(undefined, {
      onSettled: () => {
        enviandoRef.current = false
      },
    })
  }

  const cerrar = () => {
    if (sincronizar.isPending) {
      return
    }
    sincronizar.reset() // la próxima vez vuelve a la confirmación
    onCerrar()
  }

  return (
    <Dialog
      open={abierto}
      modalType={sincronizar.isPending ? 'alert' : 'modal'}
      onOpenChange={(_, d) => !d.open && cerrar()}
    >
      <DialogSurface className={estilos.superficie}>
        <DialogBody>
          <DialogTitle>Sincronizar permisos médicos</DialogTitle>
          <DialogContent className={estilos.contenido}>
            {sincronizar.isPending ? (
              <Spinner label="Sincronizando… puede tardar unos minutos." labelPosition="below" />
            ) : sincronizar.data ? (
              <Resultado resultado={sincronizar.data} />
            ) : (
              <>
                {error && (
                  <MessageBar intent="error">
                    <MessageBarBody>
                      {error.generales.map((mensaje) => (
                        <div key={mensaje}>{mensaje}</div>
                      ))}
                    </MessageBarBody>
                    {error.ofrecerReintento && (
                      <MessageBarActions>
                        <Button onClick={enviar}>Reintentar</Button>
                      </MessageBarActions>
                    )}
                  </MessageBar>
                )}
                <Text>
                  Lee los permisos médicos de la lista de origen y crea, actualiza, anula o reactiva las novedades de origen
                  PERMISOS_MEDICOS. Las que ya no están en el origen <strong>no se anulan</strong>: solo se cuentan.
                </Text>
                <Text className={estilos.secundario}>La operación puede tardar.</Text>
              </>
            )}
          </DialogContent>
          <DialogActions className={estilos.acciones}>
            {sincronizar.data ? (
              <Button appearance="primary" onClick={cerrar}>
                Cerrar
              </Button>
            ) : (
              <>
                <Button disabled={sincronizar.isPending} onClick={cerrar}>
                  Cancelar
                </Button>
                {!error && (
                  <Button appearance="primary" disabled={sincronizar.isPending} onClick={enviar}>
                    Sincronizar
                  </Button>
                )}
              </>
            )}
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  )
}

function Resultado({ resultado }: { resultado: ResultadoSincronizacion }) {
  const estilos = useEstilos()
  const resumen = resumenSincronizacion(resultado)

  return (
    <>
      <Text>
        Sincronizada el {formatearFechaHora(resultado.sincronizadaUtc)} · Fuente: {resultado.fuente}
      </Text>

      <table className={estilos.tabla} aria-label="Resultado de la sincronización">
        <tbody>
          {resumen.conteos.map((fila) => (
            <tr key={fila.etiqueta}>
              <th scope="row" className={estilos.celda}>
                {fila.etiqueta}
                {fila.nota && <div className={estilos.secundario}>{fila.nota}</div>}
              </th>
              <td className={mergeClasses(estilos.celda, estilos.numero)}>{fila.valor}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <Text weight="semibold">Omitidas ({resumen.totalOmitidas})</Text>
      {resumen.omitidas.length === 0 ? (
        <Text>Ninguna.</Text>
      ) : (
        <table className={estilos.tabla} aria-label="Omitidas por categoría">
          <tbody>
            {resumen.omitidas.map((fila) => (
              <tr key={fila.etiqueta}>
                <th scope="row" className={estilos.celda}>
                  {fila.etiqueta}
                </th>
                <td className={mergeClasses(estilos.celda, estilos.numero)}>{fila.valor}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {resumen.codigos.length > 0 && (
        <>
          <Text weight="semibold">Códigos omitidos</Text>
          <ul className={estilos.codigos}>
            {resumen.codigos.map((codigo, i) => (
              <li key={i}>{codigo}</li>
            ))}
          </ul>
          {resumen.codigosTruncados && <Text className={estilos.secundario}>{NOTA_CODIGOS}</Text>}
        </>
      )}
    </>
  )
}
