import { Button, Card, Spinner, Text, makeStyles, tokens } from '@fluentui/react-components'
import { ArrowLeft20Regular } from '@fluentui/react-icons'
import { useMemo, useReducer, useState } from 'react'
import { useNavigate } from 'react-router'
import { EstadoError } from '../../../components/EstadoError'
import { BuscadorEmpleados } from '../components/BuscadorEmpleados'
import { ListaBacks } from '../components/ListaBacks'
import { ListaPrincipales } from '../components/ListaPrincipales'
import { SeccionCabecera } from '../components/SeccionCabecera'
import {
  advertenciasFechas,
  crearEstadoInicial,
  etiquetasPorEmpleado,
  reducerFormulario,
  tieneRangoProyecto,
  validarCliente,
} from '../formularioProyecto'
import { useCatalogos, useCompaniasErp, useHorariosErp, useOpcionesFormulario } from '../hooks'
import type { Catalogos, CompaniaErp, HorarioErp, OpcionesFormularioProyecto } from '../tipos'

const useEstilos = makeStyles({
  pagina: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalL },
  volver: { alignSelf: 'flex-start' },
  seccion: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
})

/** Ruta /proyectos/nuevo. Carga los datos de apoyo y luego monta el formulario (estado inicial con P5). */
export function NuevoProyecto() {
  const estilos = useEstilos()
  const navigate = useNavigate()
  const catalogos = useCatalogos()
  const opciones = useOpcionesFormulario()
  const companias = useCompaniasErp()
  const horarios = useHorariosErp()

  const error = catalogos.error ?? opciones.error ?? companias.error ?? horarios.error

  return (
    <section className={estilos.pagina}>
      <Button
        className={estilos.volver}
        appearance="subtle"
        icon={<ArrowLeft20Regular />}
        onClick={() => void navigate('/proyectos')}
      >
        Volver al listado
      </Button>

      <Text as="h2" size={600} weight="semibold">
        Nuevo proyecto
      </Text>

      {error ? (
        <EstadoError error={error} />
      ) : catalogos.data && opciones.data && companias.data && horarios.data ? (
        <FormularioNuevoProyecto
          catalogos={catalogos.data}
          opciones={opciones.data}
          companias={companias.data}
          horarios={horarios.data}
        />
      ) : (
        <Spinner label="Cargando datos del formulario…" />
      )}
    </section>
  )
}

type Buscador = 'principal' | 'back' | null

interface FormularioNuevoProyectoProps {
  catalogos: Catalogos
  opciones: OpcionesFormularioProyecto
  companias: CompaniaErp[]
  horarios: HorarioErp[]
}

function FormularioNuevoProyecto({ catalogos, opciones, companias, horarios }: FormularioNuevoProyectoProps) {
  const estilos = useEstilos()
  const [estado, dispatch] = useReducer(
    reducerFormulario,
    { departamentos: opciones.departamentos, companias },
    crearEstadoInicial,
  )
  const [buscador, setBuscador] = useState<Buscador>(null)

  const erroresCliente = useMemo(() => validarCliente(estado), [estado])
  const advertencias = useMemo(() => advertenciasFechas(estado), [estado])
  const etiquetas = useMemo(() => etiquetasPorEmpleado(estado), [estado])
  const rangoListo = tieneRangoProyecto(estado.cabecera)

  const puedeAgregar =
    buscador === 'principal'
      ? estado.principales.length < opciones.maxPrincipales
      : estado.backs.length < opciones.maxBacks

  return (
    <>
      <Card className={estilos.seccion}>
        <Text as="h3" size={500} weight="semibold">
          Cabecera
        </Text>
        <SeccionCabecera
          cabecera={estado.cabecera}
          dispatch={dispatch}
          grupos={catalogos.gruposProyecto}
          companias={companias}
          horarios={horarios}
          opciones={opciones}
          errores={erroresCliente.cabecera}
        />
      </Card>

      <ListaPrincipales
        principales={estado.principales}
        dispatch={dispatch}
        jornadas={catalogos.jornadas}
        maximo={opciones.maxPrincipales}
        rangoListo={rangoListo}
        advertencias={advertencias}
        errores={erroresCliente.filas}
        onAgregar={() => setBuscador('principal')}
      />

      <ListaBacks
        backs={estado.backs}
        principales={estado.principales}
        dispatch={dispatch}
        maximo={opciones.maxBacks}
        maxDiasDescanso={opciones.backMaxDiasDescanso}
        rangoListo={rangoListo}
        advertencias={advertencias}
        errores={erroresCliente.filas}
        onAgregar={() => setBuscador('back')}
      />

      <BuscadorEmpleados
        abierto={buscador !== null}
        titulo={buscador === 'back' ? 'Agregar back' : 'Agregar principal'}
        etiquetas={etiquetas}
        puedeAgregar={puedeAgregar}
        onSeleccionar={(empleado) =>
          dispatch(
            buscador === 'back'
              ? { tipo: 'agregarBack', empleado, maximo: opciones.maxBacks }
              : { tipo: 'agregarPrincipal', empleado, maximo: opciones.maxPrincipales },
          )
        }
        onCerrar={() => setBuscador(null)}
      />
    </>
  )
}
