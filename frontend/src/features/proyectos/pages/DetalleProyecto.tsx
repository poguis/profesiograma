import {
  Button,
  MessageBar,
  MessageBarBody,
  MessageBarTitle,
  Spinner,
  Tab,
  TabList,
  makeStyles,
  tokens,
} from '@fluentui/react-components'
import { ArrowLeft20Regular, ArrowSwap20Regular } from '@fluentui/react-icons'
import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router'
import { ErrorApi } from '../../../api/errores'
import { EstadoError } from '../../../components/EstadoError'
import { mensajeExito, puedeCambiarEstado } from '../cambioEstado'
import { clavesProyectos } from '../api'
import { CabeceraProyecto } from '../components/CabeceraProyecto'
import { DialogoCambioEstado } from '../components/DialogoCambioEstado'
import { TablaHistorial } from '../components/TablaHistorial'
import { TablaPersonal } from '../components/TablaPersonal'
import { useProyecto } from '../hooks'
import type { EstadoNavegacionProyectos } from './ListadoProyectos'

type Pestana = 'personal' | 'historial'

const useEstilos = makeStyles({
  pagina: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL },
  volver: { alignSelf: 'flex-start' },
  panel: { overflowX: 'auto' },
})

export function DetalleProyecto() {
  const estilos = useEstilos()
  const navigate = useNavigate()
  const location = useLocation()
  const { id } = useParams()
  const numero = Number(id)
  const idValido = Number.isInteger(numero) && numero > 0
  const { data: proyecto, error, isPending } = useProyecto(numero)
  const [pestana, setPestana] = useState<Pestana>('personal')
  const [dialogoAbierto, setDialogoAbierto] = useState(false)
  const [avisoCambio, setAvisoCambio] = useState<string>()
  const queryClient = useQueryClient()

  // Vuelve al listado con los filtros con que se abrió el detalle (si se llegó desde allí).
  const estadoNavegacion = location.state as EstadoNavegacionProyectos | null
  const busqueda = estadoNavegacion?.busqueda ?? ''
  const codigoCreado = estadoNavegacion?.codigoCreado
  const rutaListado = `/proyectos${busqueda}`

  const noEncontrado = !idValido || (error instanceof ErrorApi && error.tipo === 'noEncontrado')

  return (
    <section className={estilos.pagina}>
      <Button className={estilos.volver} icon={<ArrowLeft20Regular />} onClick={() => void navigate(rutaListado)}>
        Volver al listado
      </Button>

      {avisoCambio && (
        <MessageBar intent="success">
          <MessageBarBody>
            <MessageBarTitle>{avisoCambio}</MessageBarTitle>
          </MessageBarBody>
        </MessageBar>
      )}

      {codigoCreado && !avisoCambio && (
        <MessageBar intent="success">
          <MessageBarBody>
            <MessageBarTitle>Proyecto {codigoCreado} registrado.</MessageBarTitle>
          </MessageBarBody>
        </MessageBar>
      )}

      {noEncontrado ? (
        // La API responde 404 tanto si no existe como si no es visible: no se distingue.
        <MessageBar intent="warning">
          <MessageBarBody>
            <MessageBarTitle>Proyecto no encontrado o sin acceso</MessageBarTitle>
            <Link to={rutaListado}>Ir al listado de proyectos</Link>
          </MessageBarBody>
        </MessageBar>
      ) : error ? (
        <EstadoError error={error} />
      ) : isPending ? (
        <Spinner label="Cargando proyecto…" labelPosition="after" />
      ) : (
        proyecto && (
          <>
            <CabeceraProyecto
              proyecto={proyecto}
              acciones={
                puedeCambiarEstado(proyecto.estado.codigo) && (
                  <Button
                    icon={<ArrowSwap20Regular />}
                    onClick={() => {
                      setAvisoCambio(undefined)
                      setDialogoAbierto(true)
                    }}
                  >
                    Cambiar estado
                  </Button>
                )
              }
            />
            {/* Si el estado deja de admitir cambios (p. ej. tras "Recargar"), el diálogo se cierra solo. */}
            {dialogoAbierto && puedeCambiarEstado(proyecto.estado.codigo) && (
              <DialogoCambioEstado
                proyecto={{
                  id: proyecto.id,
                  estado: proyecto.estado.codigo,
                  fechaInicio: proyecto.fechaInicio,
                  fechaFin: proyecto.fechaFin,
                }}
                rutaListado={rutaListado}
                onCerrar={() => setDialogoAbierto(false)}
                onRealizado={(r) => {
                  setDialogoAbierto(false)
                  setAvisoCambio(mensajeExito(r.estado, r.version))
                  setPestana('historial') // D2: la etapa nueva queda a la vista
                }}
                onRecargar={() => queryClient.invalidateQueries({ queryKey: clavesProyectos.todos })}
              />
            )}
            <TabList selectedValue={pestana} onTabSelect={(_, datos) => setPestana(datos.value as Pestana)}>
              <Tab value="personal">Personal ({proyecto.personal.length})</Tab>
              <Tab value="historial">Historial ({proyecto.etapas.length})</Tab>
            </TabList>
            <div className={estilos.panel} role="tabpanel">
              {pestana === 'personal' ? (
                <TablaPersonal personal={proyecto.personal} />
              ) : (
                <TablaHistorial etapas={proyecto.etapas} />
              )}
            </div>
          </>
        )
      )}
    </section>
  )
}
