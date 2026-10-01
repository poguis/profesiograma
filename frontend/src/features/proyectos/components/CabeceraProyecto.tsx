import { Card, Text, makeStyles, tokens } from '@fluentui/react-components'
import type { ReactNode } from 'react'
import { formatearFecha, formatearHora } from '../../../utils/formato'
import type { ProyectoDetalle } from '../tipos'
import { EtiquetaEstado } from './EtiquetaEstado'

const useEstilos = makeStyles({
  contenedor: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
  titulo: { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: tokens.spacingHorizontalM },
  tarjetas: {
    display: 'grid',
    gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))',
    gap: tokens.spacingHorizontalL,
  },
  tarjeta: { padding: tokens.spacingHorizontalL },
  datos: {
    display: 'grid',
    gridTemplateColumns: 'max-content 1fr',
    columnGap: tokens.spacingHorizontalL,
    rowGap: tokens.spacingVerticalXS,
  },
  secundario: { color: tokens.colorNeutralForeground3 },
})

function rangoHoras(desde: string | null, hasta: string | null): string | null {
  return desde && hasta ? `${formatearHora(desde)} – ${formatearHora(hasta)}` : null
}

function Dato({ etiqueta, children }: { etiqueta: string; children: ReactNode }) {
  return (
    <>
      <Text weight="semibold">{etiqueta}</Text>
      <Text>{children ?? '—'}</Text>
    </>
  )
}

export interface CabeceraProyectoProps {
  proyecto: ProyectoDetalle
  /** Acciones junto al título (p. ej. "Cambiar estado"). */
  acciones?: ReactNode
}

export function CabeceraProyecto({ proyecto, acciones }: CabeceraProyectoProps) {
  const estilos = useEstilos()
  const { erp, horario, almuerzo, actividadVigente: actividad } = proyecto
  const tieneErp = Object.values(erp).some((valor) => valor !== null && valor !== '')
  const horas = rangoHoras(horario.horaEntrada, horario.horaSalida)

  return (
    <section className={estilos.contenedor} aria-label="Datos del proyecto">
      <div className={estilos.titulo}>
        <Text as="h2" size={600} weight="semibold">
          {proyecto.codigo} · {proyecto.nombre}
        </Text>
        <EtiquetaEstado codigo={proyecto.estado.codigo} nombre={proyecto.estado.nombre} />
        {acciones}
      </div>

      <div className={estilos.tarjetas}>
        <Card className={estilos.tarjeta}>
          <Text weight="semibold" size={400}>
            General
          </Text>
          <div className={estilos.datos}>
            <Dato etiqueta="Grupo">{proyecto.grupo.nombre}</Dato>
            <Dato etiqueta="Compañía">
              {proyecto.compania.nombre}
              {proyecto.compania.ruc && <span className={estilos.secundario}> · RUC {proyecto.compania.ruc}</span>}
            </Dato>
            <Dato etiqueta="Inicio">{formatearFecha(proyecto.fechaInicio)}</Dato>
            <Dato etiqueta="Fin">{formatearFecha(proyecto.fechaFin)}</Dato>
            <Dato etiqueta="Departamento">{proyecto.departamento}</Dato>
            <Dato etiqueta="Propietario">
              {proyecto.propietario.nombreMostrar}
              <span className={estilos.secundario}> · {proyecto.propietario.email}</span>
            </Dato>
          </div>
        </Card>

        <Card className={estilos.tarjeta}>
          <Text weight="semibold" size={400}>
            Horario
          </Text>
          <div className={estilos.datos}>
            <Dato etiqueta="Horario">{horario.descripcion ?? horas}</Dato>
            <Dato etiqueta="Entrada – salida">{horas}</Dato>
            <Dato etiqueta="Almuerzo">{rangoHoras(almuerzo.salida, almuerzo.regreso)}</Dato>
            {horario.tipo && <Dato etiqueta="Tipo">{horario.tipo}</Dato>}
          </div>
        </Card>

        <Card className={estilos.tarjeta}>
          <Text weight="semibold" size={400}>
            Actividad vigente
          </Text>
          {actividad ? (
            <div className={estilos.datos}>
              <Dato etiqueta="Actividad">
                {actividad.actividadCodigo}
                {actividad.actividadDescripcion && ` – ${actividad.actividadDescripcion}`}
              </Dato>
              {actividad.actividadTipo && <Dato etiqueta="Tipo">{actividad.actividadTipo}</Dato>}
              <Dato etiqueta="Vigencia">
                {formatearFecha(actividad.fechaInicio)} – {formatearFecha(actividad.fechaFin)}
              </Dato>
              <Dato etiqueta="Versión">{actividad.version}</Dato>
            </div>
          ) : (
            <Text>Sin actividad registrada.</Text>
          )}
        </Card>

        {tieneErp && (
          <Card className={estilos.tarjeta}>
            <Text weight="semibold" size={400}>
              ERP
            </Text>
            <div className={estilos.datos}>
              {erp.proyectoErpId && <Dato etiqueta="Proyecto ERP">{erp.proyectoErpId}</Dato>}
              {erp.proyectoErpNombre && <Dato etiqueta="Nombre ERP">{erp.proyectoErpNombre}</Dato>}
              {erp.proyectoErpEstado && <Dato etiqueta="Estado ERP">{erp.proyectoErpEstado}</Dato>}
              {erp.dimensionUegpId && <Dato etiqueta="Dimensión">{erp.dimensionUegpId}</Dato>}
              {erp.dimensionDescripcion && <Dato etiqueta="Descripción dimensión">{erp.dimensionDescripcion}</Dato>}
            </div>
          </Card>
        )}
      </div>
    </section>
  )
}
