import { Text, makeStyles, tokens } from '@fluentui/react-components'
import { formatearFecha } from '../../../utils/formato'
import type { PrevisualizacionReactivacion } from '../tipos'
import { VistaPreviaPersonal } from './VistaPreviaPersonal'

const useEstilos = makeStyles({
  contenedor: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalS },
})

/**
 * Vista previa de la reactivación (TAREA-19c): fecha fin actual → nueva, actividad que se creará (R8) y, debajo, la
 * vista previa del personal de la 19b (advertencias, cruces, acciones y tramos unidos). Los datos tienen los mismos
 * campos que la de personal y algunos más.
 */
export function VistaPreviaReactivacion({ datos }: { datos: PrevisualizacionReactivacion }) {
  const estilos = useEstilos()
  const actividad = datos.actividad

  return (
    <div className={estilos.contenedor}>
      <Text>
        Fecha fin del proyecto: {formatearFecha(datos.fechaFinActual)} → <strong>{formatearFecha(datos.fechaFinNueva)}</strong>
      </Text>
      <Text>
        {actividad
          ? `Actividad que se creará: ${actividad.codigo}${actividad.descripcion ? ` · ${actividad.descripcion}` : ''}, del ${formatearFecha(actividad.fechaInicio)} al ${formatearFecha(actividad.fechaFin)}.`
          : 'Sin actividad: no había una actividad vigente al suspender, así que no se creará ninguna.'}
      </Text>
      <VistaPreviaPersonal
        datos={datos}
        textoCorte={`Fecha de reactivación: ${formatearFecha(datos.corte)}. El personal guardado queda histórico; los días anteriores no cambian.`}
      />
    </div>
  )
}
