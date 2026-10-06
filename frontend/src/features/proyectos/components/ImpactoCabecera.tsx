import { MessageBar, MessageBarBody, MessageBarTitle, Text, makeStyles, tokens } from '@fluentui/react-components'
import { formatearFecha } from '../../../utils/formato'
import { etiquetaCampo, textoValorCambio } from '../edicionCabecera'
import { useNombresCatalogo } from '../hooks'
import { textoPersona as persona, textoRango as rango, textoRol as rol } from '../textos'
import type { ActividadResultante, PrevisualizacionCabecera } from '../tipos'
import { ImpactoRecorte, SeccionImpacto } from './ImpactoRecorte'

const useEstilos = makeStyles({
  contenedor: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
})

/** Impacto de "Editar datos generales" (POST …/cabecera/previsualizar). No guarda nada. */
export function ImpactoCabecera({ datos }: { datos: PrevisualizacionCabecera }) {
  const estilos = useEstilos()
  const nombres = useNombresCatalogo()

  return (
    <div className={estilos.contenedor}>
      <Text weight="semibold">
        Etapa: {nombres.movimiento(datos.tipoEtapa)} · Proyecto: {rango(datos.fechaInicioNueva, datos.fechaFinNueva)}
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

      <SeccionImpacto titulo="Cambios" vacia={datos.cambios.length === 0}>
        {datos.cambios.map((c) => (
          <li key={c.campo}>
            {etiquetaCampo(c.campo)}: {textoValorCambio(c.anterior)} → {textoValorCambio(c.nuevo)}
          </li>
        ))}
      </SeccionImpacto>

      <ImpactoRecorte
        diasEliminados={datos.diasEliminados}
        personalEliminado={datos.personalEliminado}
        personalRecortado={datos.personalRecortado}
      />

      {/* H15: el descanso posterior de los backs que quedan se vuelve a insertar después del recorte. */}
      <SeccionImpacto titulo="Días que se agregan" vacia={datos.diasAgregados.length === 0}>
        {datos.diasAgregados.map((d) => (
          <li key={`${d.empleado.id}|${d.rol}`}>
            {persona(d.empleado)} · {rol(d.rol)}: {d.cantidad} {d.cantidad === 1 ? 'día' : 'días'} ({rango(d.desde, d.hasta)})
          </li>
        ))}
      </SeccionImpacto>

      <SeccionImpacto titulo="Actividades resultantes" vacia={datos.actividades.length === 0}>
        {datos.actividades.map((a) => (
          <li key={`${a.codigo}|${a.version ?? 'nueva'}`}>{textoActividad(a)}</li>
        ))}
      </SeccionImpacto>
    </div>
  )
}

function textoActividad(a: ActividadResultante): string {
  const nombre = `${a.codigo}${a.version === null ? '' : ` v${a.version}`}${a.descripcion ? ` – ${a.descripcion}` : ''}`
  switch (a.accion) {
    case 'NUEVA':
      return `${nombre}: nueva (${rango(a.fechaInicio, a.fechaFin)})`
    case 'ELIMINADA':
      return `${nombre}: se elimina (${rango(a.fechaInicio, a.fechaFin)})`
    case 'MODIFICADA':
      return `${nombre}: ${rangoAnterior(a)} → ${rango(a.fechaInicio, a.fechaFin)}`
    default:
      return `${nombre}: sin cambios (${rango(a.fechaInicio, a.fechaFin)})`
  }
}

function rangoAnterior(a: ActividadResultante): string {
  return a.fechaInicioAnterior && a.fechaFinAnterior
    ? rango(a.fechaInicioAnterior, a.fechaFinAnterior)
    : formatearFecha(a.fechaFinAnterior ?? a.fechaFin)
}
