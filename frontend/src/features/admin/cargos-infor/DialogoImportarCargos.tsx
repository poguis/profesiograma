import {
  Button,
  Dialog,
  DialogActions,
  DialogBody,
  DialogContent,
  DialogSurface,
  DialogTitle,
  MessageBar,
  MessageBarBody,
  MessageBarTitle,
  Spinner,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components'
import { CloudArrowDown20Regular } from '@fluentui/react-icons'
import { useRef, useState } from 'react'
import type { ErroresDialogo } from '../../proyectos/erroresDialogo'
import { BarraErrores } from '../BarraErrores'
import { useImportarCargos, useVistaPreviaImportacion } from './api'
import {
  AVISO_LISTA_ANTERIOR,
  NOTA_IMPORTACION,
  type ResultadoImportacion,
  TEXTO_VARIANTES,
  interpretarErrorImportacion,
  textoListaDe,
  textoMasVariantes,
} from './cargosInfor'

const MOVIL = '@media (max-width: 599px)'

const useEstilos = makeStyles({
  superficie: { [MOVIL]: { width: '100%', maxWidth: '100%' } },
  contenido: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
  secundario: { color: tokens.colorNeutralForeground3 },
  lista: { margin: 0, paddingLeft: tokens.spacingHorizontalXL },
  acciones: { [MOVIL]: { flexDirection: 'column', '& > button': { width: '100%' } } },
})

const sinRecarga = () => Promise.resolve()

/**
 * Importación de los puestos del ERP (TAREA-25-3-2): vista previa con la lista en caché, pedida solo al abrir (P2); la
 * importación lee el ERP en ese momento. Se monta solo mientras está abierto.
 */
export function DialogoImportarCargos({ onCerrar }: { onCerrar: () => void }) {
  const estilos = useEstilos()
  const importar = useImportarCargos()
  const [resultado, setResultado] = useState<ResultadoImportacion | null>(null)
  const [errores, setErrores] = useState<ErroresDialogo<never> | null>(null)
  const vista = useVistaPreviaImportacion(resultado === null)
  const enviandoRef = useRef(false)
  const errorVista = vista.error ? interpretarErrorImportacion(vista.error) : null

  const enviar = () => {
    if (enviandoRef.current || importar.isPending) {
      return
    }
    enviandoRef.current = true
    setErrores(null)
    importar.mutate(undefined, {
      onSuccess: setResultado,
      onError: (error) => setErrores(interpretarErrorImportacion(error)),
      onSettled: () => {
        enviandoRef.current = false
      },
    })
  }

  const cerrar = () => {
    if (!importar.isPending) {
      onCerrar()
    }
  }

  const v = vista.data
  return (
    <Dialog open onOpenChange={(_, d) => !d.open && cerrar()}>
      <DialogSurface className={estilos.superficie}>
        <DialogBody>
          <DialogTitle>Importar puestos desde el ERP</DialogTitle>
          <DialogContent className={estilos.contenido}>
            {resultado ? (
              <>
                <MessageBar intent="success">
                  <MessageBarBody>
                    <MessageBarTitle>Importación terminada.</MessageBarTitle>
                  </MessageBarBody>
                </MessageBar>
                <ul className={estilos.lista}>
                  <li>Puestos de los activos del ERP: {resultado.puestosApi}</li>
                  <li>Cargos nuevos insertados (sin código): {resultado.insertados}</li>
                  <li>Ya existentes: {resultado.yaExistentes}</li>
                  <li>Existentes inactivos (no se reactivan): {resultado.existentesInactivos}</li>
                </ul>
              </>
            ) : (
              <>
                <Text className={estilos.secundario}>{NOTA_IMPORTACION}</Text>
                <BarraErrores
                  errores={errorVista}
                  cambios={false}
                  pendiente={vista.isFetching}
                  onRecargar={sinRecarga}
                  onReintentar={() => void vista.refetch()}
                />
                <BarraErrores errores={errores} cambios={false} pendiente={importar.isPending} onRecargar={sinRecarga} onReintentar={enviar} />
                {vista.isFetching && !v && <Spinner label="Consultando la lista de puestos del ERP…" labelPosition="after" />}
                {v && (
                  <>
                    <Text weight="semibold">{textoListaDe(v)}</Text>
                    {v.esAnterior && (
                      <MessageBar intent="warning">
                        <MessageBarBody>{AVISO_LISTA_ANTERIOR}</MessageBarBody>
                      </MessageBar>
                    )}
                    <ul className={estilos.lista}>
                      <li>Puestos de los activos del ERP: {v.puestosApi}</li>
                      <li>Nuevos (se insertarán sin código): {v.nuevos}</li>
                      <li>Ya existentes: {v.yaExistentes}</li>
                      <li>Existentes inactivos (no se reactivan): {v.existentesInactivos}</li>
                      <li>Fusionados (difieren solo en mayúsculas o en el recorte): {v.fusionados}</li>
                    </ul>
                    {v.gruposVariantes > 0 && (
                      <MessageBar intent="warning">
                        <MessageBarBody>
                          <div>
                            {TEXTO_VARIANTES} Grupos: {v.gruposVariantes}.
                          </div>
                          <ul className={estilos.lista}>
                            {v.variantes.map((grupo) => (
                              <li key={grupo.join('|')}>{grupo.join(' · ')}</li>
                            ))}
                          </ul>
                          {textoMasVariantes(v) && <div>{textoMasVariantes(v)}</div>}
                        </MessageBarBody>
                      </MessageBar>
                    )}
                  </>
                )}
              </>
            )}
          </DialogContent>
          <DialogActions className={estilos.acciones}>
            {resultado ? (
              <Button appearance="primary" onClick={onCerrar}>
                Cerrar
              </Button>
            ) : (
              <>
                <Button disabled={importar.isPending} onClick={cerrar}>
                  Cancelar
                </Button>
                <Button
                  appearance="primary"
                  disabled={!v || importar.isPending}
                  icon={importar.isPending ? <Spinner size="tiny" /> : <CloudArrowDown20Regular />}
                  onClick={enviar}
                >
                  {importar.isPending ? 'Importando…' : 'Importar'}
                </Button>
              </>
            )}
          </DialogActions>
        </DialogBody>
      </DialogSurface>
    </Dialog>
  )
}
