import { MessageBar, MessageBarBody, MessageBarTitle, Text, makeStyles, tokens } from '@fluentui/react-components'
import { formatearFecha } from '../../../utils/formato'
import { textoRango as rango } from '../textos'
import type { PrevisualizacionCambioEstado } from '../tipos'
import { ImpactoRecorte, SeccionImpacto } from './ImpactoRecorte'

const useEstilos = makeStyles({
  contenedor: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
})

/** R3: impacto de suspender o terminar. Listas en lugar de tablas: se leen bien en móvil sin desplazamiento horizontal. */
export function ImpactoCambioEstado({ datos }: { datos: PrevisualizacionCambioEstado }) {
  const estilos = useEstilos()

  return (
    <div className={estilos.contenedor}>
      <Text weight="semibold">
        Fecha fin: {formatearFecha(datos.fechaFinActual)} → {formatearFecha(datos.fechaFinNueva)}
      </Text>

      {datos.advertencias.length > 0 && (
        <MessageBar intent="warning">
          <MessageBarBody>
            <MessageBarTitle>Advertencias</MessageBarTitle>
            {datos.advertencias.map((a) => (
              <div key={a}>{a}</div>
            ))}
          </MessageBarBody>
        </MessageBar>
      )}

      <ImpactoRecorte
        diasEliminados={datos.diasEliminados}
        personalEliminado={datos.personalEliminado}
        personalRecortado={datos.personalRecortado}
      />

      <SeccionImpacto titulo="Actividades afectadas" vacia={datos.actividadesAfectadas.length === 0}>
        {datos.actividadesAfectadas.map((a) => (
          <li key={`${a.actividadCodigo}|${a.version}`}>
            {a.actividadCodigo} v{a.version}:{' '}
            {a.accion === 'ELIMINADA'
              ? `se elimina (${rango(a.fechaInicio, a.fechaFinAnterior)})`
              : `fin ${formatearFecha(a.fechaFinAnterior)} → ${formatearFecha(a.fechaFinNueva)}`}
          </li>
        ))}
      </SeccionImpacto>
    </div>
  )
}
