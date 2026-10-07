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
import { ArrowLeft20Regular, ArrowSwap20Regular, Edit20Regular, People20Regular } from '@fluentui/react-icons'
import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router'
import { ErrorApi } from '../../../api/errores'
import { EstadoError } from '../../../components/EstadoError'
import { mensajeExito, puedeCambiarEstado } from '../cambioEstado'
import { clavesProyectos } from '../api'
import { AccionSegunConsulta } from '../components/AccionSegunConsulta'
import { CabeceraProyecto } from '../components/CabeceraProyecto'
import { DialogoCambioEstado } from '../components/DialogoCambioEstado'
import { DialogoEditarCabecera } from '../components/DialogoEditarCabecera'
import { TablaHistorial } from '../components/TablaHistorial'
import { TablaPersonal } from '../components/TablaPersonal'
import { useCabecera, useEdicionPersonal, useProyecto } from '../hooks'
import type { CabeceraEdicion, ProyectoDetalle } from '../tipos'
import { versionDeEtapas } from '../tokenConcurrencia'
import type { EstadoNavegacionProyectos } from './ListadoProyectos'

type Pestana = 'personal' | 'historial'

const useEstilos = makeStyles({
  pagina: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL },
  volver: { alignSelf: 'flex-start' },
  panel: { overflowX: 'auto' },
})

type Dialogo = 'estado' | 'cabecera' | null

export function DetalleProyecto() {
  const estilos = useEstilos()
  const navigate = useNavigate()
  const location = useLocation()
  const { id } = useParams()
  const numero = Number(id)
  const idValido = Number.isInteger(numero) && numero > 0
  const { data: proyecto, error, isPending } = useProyecto(numero)
  // P3: la cabecera se consulta con el detalle para decidir "Editar datos generales"; su error no bloquea la página.
  const cabecera = useCabecera(numero)
  // TAREA-19b: igual para "Actualizar personal" (GET …/edicion).
  const edicion = useEdicionPersonal(numero)
  // Vuelve al listado con los filtros con que se abrió el detalle (si se llegó desde allí). Tras "Actualizar
  // personal" llegan el aviso y la pestaña (P6 de la TAREA-19b).
  const estadoNavegacion = location.state as EstadoNavegacionProyectos | null
  const [pestana, setPestana] = useState<Pestana>(estadoNavegacion?.pestana ?? 'personal')
  const [dialogo, setDialogo] = useState<Dialogo>(null)
  const [avisoCambio, setAvisoCambio] = useState<string | undefined>(estadoNavegacion?.aviso)
  const queryClient = useQueryClient()

  const busqueda = estadoNavegacion?.busqueda ?? ''
  const codigoCreado = estadoNavegacion?.codigoCreado
  const rutaListado = `/proyectos${busqueda}`

  const noEncontrado = !idValido || (error instanceof ErrorApi && error.tipo === 'noEncontrado')

  const recargar = () => queryClient.invalidateQueries({ queryKey: clavesProyectos.todos })
  const abrir = (cual: Dialogo) => {
    setAvisoCambio(undefined)
    setDialogo(cual)
  }
  const alRealizar = (mensaje: string) => {
    setDialogo(null)
    setAvisoCambio(mensaje)
    setPestana('historial') // D2: la etapa nueva queda a la vista
  }

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
                <>
                  <AccionSegunConsulta
                    consulta={cabecera}
                    etiqueta="Editar datos generales"
                    icono={<Edit20Regular />}
                    onAbrir={() => abrir('cabecera')}
                  />
                  <AccionSegunConsulta
                    consulta={edicion}
                    etiqueta="Actualizar personal"
                    icono={<People20Regular />}
                    onAbrir={() => {
                      const estado: EstadoNavegacionProyectos = { busqueda }
                      void navigate(`/proyectos/${proyecto.id}/personal`, { state: estado })
                    }}
                  />
                  {puedeCambiarEstado(proyecto.estado.codigo) && (
                    <Button icon={<ArrowSwap20Regular />} onClick={() => abrir('estado')}>
                      Cambiar estado
                    </Button>
                  )}
                </>
              }
            />
            {/* Si el estado deja de admitir cambios (p. ej. tras "Recargar"), el diálogo se cierra solo. */}
            {dialogo === 'estado' && puedeCambiarEstado(proyecto.estado.codigo) && (
              <DialogoCambioEstado
                proyecto={{
                  id: proyecto.id,
                  estado: proyecto.estado.codigo,
                  fechaInicio: proyecto.fechaInicio,
                  fechaFin: proyecto.fechaFin,
                  versionBase: versionDeEtapas(proyecto.etapas), // TAREA-19b2: el diálogo lo fija al abrirse
                }}
                rutaListado={rutaListado}
                onCerrar={() => setDialogo(null)}
                onRealizado={(r) => alRealizar(mensajeExito(r.estado, r.version))}
                onRecargar={async () => {
                  await recargar()
                  const detalle = queryClient.getQueryData<ProyectoDetalle>(clavesProyectos.detalle(proyecto.id))
                  return detalle ? versionDeEtapas(detalle.etapas) : undefined
                }}
                onReactivar={() => {
                  // TAREA-19c (P1): la reactivación se completa en su propia pantalla.
                  const estado: EstadoNavegacionProyectos = { busqueda }
                  void navigate(`/proyectos/${proyecto.id}/reactivar`, { state: estado })
                }}
              />
            )}
            {/* Igual: si tras "Recargar" el proyecto ya no se puede editar, el diálogo se cierra. */}
            {dialogo === 'cabecera' && cabecera.data?.puedeEditar && (
              <DialogoEditarCabecera
                proyecto={{
                  id: proyecto.id,
                  companiaId: proyecto.compania.id,
                  proyectoErpId: proyecto.erp.proyectoErpId,
                }}
                cabecera={cabecera.data}
                rutaListado={rutaListado}
                onCerrar={() => setDialogo(null)}
                onRealizado={alRealizar}
                onRecargar={async () => {
                  await recargar()
                  return queryClient.getQueryData<CabeceraEdicion>(clavesProyectos.cabecera(proyecto.id))
                }}
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

