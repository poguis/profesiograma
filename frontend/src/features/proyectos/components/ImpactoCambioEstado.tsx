import { MessageBar, MessageBarBody, MessageBarTitle, Text, makeStyles, tokens } from '@fluentui/react-components'
import type { ReactNode } from 'react'
import { formatearFecha } from '../../../utils/formato'
import type { EmpleadoCambio, PrevisualizacionCambioEstado } from '../tipos'

const useEstilos = makeStyles({
  contenedor: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
  seccion: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalXXS },
  lista: { margin: 0, paddingLeft: tokens.spacingHorizontalL },
  secundario: { color: tokens.colorNeutralForeground3 },
})

const NOMBRE_ROL: Readonly<Record<string, string>> = { PRINCIPAL: 'Principal', BACK: 'Back', DESCANSO: 'Descanso' }

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

      <Seccion titulo="Días que se eliminan" vacia={datos.diasEliminados.length === 0}>
        {datos.diasEliminados.map((d) => (
          <li key={`${d.empleado.id}|${d.rol}`}>
            {persona(d.empleado)} · {rol(d.rol)}: {d.cantidad} {d.cantidad === 1 ? 'día' : 'días'} ({rango(d.desde, d.hasta)})
          </li>
        ))}
      </Seccion>

      <Seccion titulo="Personal que se elimina" vacia={datos.personalEliminado.length === 0}>
        {datos.personalEliminado.map((p) => (
          <li key={`${p.rol}|${p.numero}`}>
            {rol(p.rol)} {p.numero} · {persona(p.empleado)} ({rango(p.fechaInicio, p.fechaFin)})
          </li>
        ))}
      </Seccion>

      <Seccion titulo="Personal recortado" vacia={datos.personalRecortado.length === 0}>
        {datos.personalRecortado.map((p) => (
          <li key={`${p.rol}|${p.numero}`}>
            {rol(p.rol)} {p.numero} · {persona(p.empleado)}: fin {formatearFecha(p.fechaFinAnterior)} →{' '}
            {formatearFecha(p.fechaFinNueva)}
          </li>
        ))}
      </Seccion>

      <Seccion titulo="Actividades afectadas" vacia={datos.actividadesAfectadas.length === 0}>
        {datos.actividadesAfectadas.map((a) => (
          <li key={`${a.actividadCodigo}|${a.version}`}>
            {a.actividadCodigo} v{a.version}:{' '}
            {a.accion === 'ELIMINADA'
              ? `se elimina (${rango(a.fechaInicio, a.fechaFinAnterior)})`
              : `fin ${formatearFecha(a.fechaFinAnterior)} → ${formatearFecha(a.fechaFinNueva)}`}
          </li>
        ))}
      </Seccion>
    </div>
  )
}

function Seccion({ titulo, vacia, children }: { titulo: string; vacia: boolean; children: ReactNode }) {
  const estilos = useEstilos()
  return (
    <div className={estilos.seccion}>
      <Text weight="semibold">{titulo}</Text>
      {vacia ? <Text className={estilos.secundario}>Sin cambios</Text> : <ul className={estilos.lista}>{children}</ul>}
    </div>
  )
}

function persona(e: EmpleadoCambio) {
  return `${e.codigoEkon} ${e.nombreCompleto}`
}

function rol(codigo: string) {
  return NOMBRE_ROL[codigo] ?? codigo
}

function rango(desde: string, hasta: string) {
  return desde === hasta ? formatearFecha(desde) : `${formatearFecha(desde)} – ${formatearFecha(hasta)}`
}
