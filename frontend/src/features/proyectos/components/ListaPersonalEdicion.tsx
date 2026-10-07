import {
  Badge,
  Button,
  MessageBar,
  MessageBarBody,
  Table,
  TableBody,
  TableCell,
  TableHeader,
  TableHeaderCell,
  TableRow,
  Text,
  makeStyles,
  tokens,
} from '@fluentui/react-components'
import {
  Add20Regular,
  ArrowDown20Regular,
  ArrowUndo20Regular,
  ArrowUp20Regular,
  Delete20Regular,
  PersonSwap20Regular,
} from '@fluentui/react-icons'
import type { Dispatch } from 'react'
import { formatearFecha } from '../../../utils/formato'
import {
  type AccionEdicionPersonal,
  type EstadoEdicionPersonal,
  type FilaEdicion,
  ROL_PRINCIPAL,
  claveSeraInicial,
  etiquetaFila,
  opcionesRelacion,
  relacionDeValor,
  valorRelacion,
} from '../edicionPersonal'
import { type ErroresFila, mensajesSinCampo } from '../formularioProyecto'
import type { JornadaCatalogo, LimitesEdicion, PersonaEdicion } from '../tipos'
import { CAMPOS_VISIBLES_BACK, CAMPOS_VISIBLES_PRINCIPAL } from '../camposVisibles'
import { CamposBack, CamposPrincipal } from './CamposPersona'
import { TarjetaPersona } from './TarjetaPersona'

const useEstilos = makeStyles({
  seccion: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
  encabezado: { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: tokens.spacingHorizontalM },
  tabla: { overflowX: 'auto' },
  eliminada: { opacity: 0.55 },
  marcas: { display: 'flex', flexWrap: 'wrap', gap: tokens.spacingHorizontalXS },
})

const AVISO_RELACION = 'El principal relacionado ya no se envía. Elija otro o deje "Sin relación".'

export interface ListaPersonalEdicionProps {
  estado: EstadoEdicionPersonal
  dispatch: Dispatch<AccionEdicionPersonal>
  jornadas: JornadaCatalogo[]
  limites: LimitesEdicion
  /** Ayudas del cliente y errores 400 del servidor por fila (clave → campo → mensaje). */
  errores: Record<string, ErroresFila>
  secciones: { principales?: string; backs?: string }
  onAgregar: (rol: FilaEdicion['rol']) => void
  onCambiarEmpleado: (clave: string) => void
  // ---- TAREA-19c (reactivación). Sin estas props, el comportamiento es el de "Actualizar personal".
  /** Clave de la fila con la marca "Será el principal inicial"; sin valor: claveSeraInicial (P3 de la 19y). */
  claveInicial?: string | null
  /** Fila con el inicio bloqueado (primer principal nuevo en R) y su texto. */
  claveInicioFijo?: string | null
  motivoInicioFijo?: string
  /** Mínimo del inicio y del fin de las filas nuevas. */
  fechaMinimaInicio?: string
  fechaMinimaFinNuevas?: string
  /** Máximo de los selectores; sin valor: fin del proyecto del estado; null: sin máximo. */
  fechaMaxima?: string | null
  /** Id de los principales históricos iniciales (marca en la tabla de históricos). */
  inicialesHistoricas?: ReadonlySet<number>
  /** Avisos adicionales por fila (clave → mensajes). */
  avisosFila?: Record<string, string[]>
  /** Ayudas por sección que no bloquean. */
  ayudasSeccion?: { principales?: string; backs?: string }
  /** Texto de una sección sin filas (reemplaza "Sin … vigentes."). */
  textoSinFilas?: { principales: string; backs: string }
}

/** Personal de "Actualizar personal": históricos (solo lectura), vigentes según permisos y nuevas. */
export function ListaPersonalEdicion(props: ListaPersonalEdicionProps) {
  return (
    <>
      <SeccionRol rol="PRINCIPAL" {...props} />
      <SeccionRol rol="BACK" {...props} />
    </>
  )
}

function SeccionRol({
  rol,
  estado,
  dispatch,
  jornadas,
  limites,
  errores,
  secciones,
  onAgregar,
  onCambiarEmpleado,
  claveInicial,
  claveInicioFijo,
  motivoInicioFijo,
  fechaMinimaInicio,
  fechaMinimaFinNuevas,
  fechaMaxima,
  inicialesHistoricas,
  avisosFila,
  ayudasSeccion,
  textoSinFilas,
}: ListaPersonalEdicionProps & { rol: FilaEdicion['rol'] }) {
  const estilos = useEstilos()
  const esPrincipal = rol === ROL_PRINCIPAL
  const filas = esPrincipal ? estado.principales : estado.backs
  const historicas = estado.historicas.filter((h) => h.rol === rol)
  const enviadas = filas.filter((f) => !f.eliminada).length
  const maximo = esPrincipal ? limites.maxPrincipales : limites.maxBacks
  const titulo = esPrincipal ? 'Principales' : 'Backs'
  const errorSeccion = esPrincipal ? secciones.principales : secciones.backs
  const inicial = claveInicial === undefined ? claveSeraInicial(estado) : claveInicial
  const ayudaSeccion = esPrincipal ? ayudasSeccion?.principales : ayudasSeccion?.backs
  const sinFilas = textoSinFilas
    ? esPrincipal
      ? textoSinFilas.principales
      : textoSinFilas.backs
    : historicas.length > 0
      ? `Sin ${titulo.toLowerCase()} vigentes.`
      : `Sin ${titulo.toLowerCase()}.`

  return (
    <section className={estilos.seccion} aria-label={titulo}>
      <div className={estilos.encabezado}>
        <Text as="h3" size={500} weight="semibold">
          {titulo} ({enviadas} de {maximo})
        </Text>
        <Button icon={<Add20Regular />} disabled={enviadas >= maximo} onClick={() => onAgregar(rol)}>
          {esPrincipal ? 'Agregar principal' : 'Agregar back'}
        </Button>
        {enviadas >= maximo && <Text size={200}>Se alcanzó el máximo de {maximo}.</Text>}
      </div>

      {errorSeccion && (
        <MessageBar intent="error">
          <MessageBarBody>{errorSeccion}</MessageBarBody>
        </MessageBar>
      )}

      {ayudaSeccion && (
        <MessageBar intent="info">
          <MessageBarBody>{ayudaSeccion}</MessageBarBody>
        </MessageBar>
      )}

      {historicas.length > 0 && <TablaHistoricas historicas={historicas} jornadas={jornadas} iniciales={inicialesHistoricas} />}

      {filas.length === 0 && <Text size={200}>{sinFilas}</Text>}

      {filas.map((f) => (
        <TarjetaEdicion
          key={f.clave}
          fila={f}
          estado={estado}
          dispatch={dispatch}
          jornadas={jornadas}
          limites={limites}
          errores={errores[f.clave] ?? {}}
          seraInicial={f.clave === inicial}
          onCambiarEmpleado={() => onCambiarEmpleado(f.clave)}
          inicioFijo={f.clave === claveInicioFijo ? (motivoInicioFijo ?? '') : undefined}
          fechaMinimaInicio={fechaMinimaInicio}
          fechaMinimaFinNuevas={fechaMinimaFinNuevas}
          fechaMaxima={fechaMaxima === undefined ? estado.fechaFinProyecto : (fechaMaxima ?? undefined)}
          avisos={avisosFila?.[f.clave]}
        />
      ))}
    </section>
  )
}

function TablaHistoricas({
  historicas,
  jornadas,
  iniciales,
}: {
  historicas: PersonaEdicion[]
  jornadas: JornadaCatalogo[]
  /** TAREA-19c: marca "Inicial" en los principales históricos iniciales. */
  iniciales?: ReadonlySet<number>
}) {
  const estilos = useEstilos()
  const esPrincipal = historicas[0].rol === ROL_PRINCIPAL
  return (
    <div className={estilos.tabla}>
      <Table size="small" aria-label={esPrincipal ? 'Principales históricos (solo lectura)' : 'Backs históricos (solo lectura)'}>
        <TableHeader>
          <TableRow>
            <TableHeaderCell>Histórico (solo lectura)</TableHeaderCell>
            <TableHeaderCell>{esPrincipal ? 'Jornada' : 'Registro'}</TableHeaderCell>
            <TableHeaderCell>Inicio</TableHeaderCell>
            <TableHeaderCell>Fin</TableHeaderCell>
          </TableRow>
        </TableHeader>
        <TableBody>
          {historicas.map((h) => (
            <TableRow key={h.id}>
              <TableCell>
                {esPrincipal ? 'P' : 'Back '}
                {h.numero} · {h.empleado.codigoEkon} · {h.empleado.nombreCompleto}
                {iniciales?.has(h.id) && (
                  <>
                    {' '}
                    <Badge appearance="tint" color="success">
                      Inicial
                    </Badge>
                  </>
                )}
              </TableCell>
              <TableCell>
                {esPrincipal ? (jornadas.find((j) => j.codigo === h.jornada)?.nombre ?? h.jornada ?? '—') : h.tipoRegistro}
              </TableCell>
              <TableCell>{formatearFecha(h.fechaInicio)}</TableCell>
              <TableCell>{formatearFecha(h.fechaFin)}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  )
}

interface TarjetaEdicionProps {
  fila: FilaEdicion
  estado: EstadoEdicionPersonal
  dispatch: Dispatch<AccionEdicionPersonal>
  jornadas: JornadaCatalogo[]
  limites: LimitesEdicion
  errores: ErroresFila
  seraInicial: boolean
  onCambiarEmpleado: () => void
  /** TAREA-19c: texto del inicio bloqueado (undefined = no fijo por esta regla). */
  inicioFijo?: string
  fechaMinimaInicio?: string
  fechaMinimaFinNuevas?: string
  fechaMaxima?: string
  avisos?: string[]
}

function TarjetaEdicion({
  fila,
  estado,
  dispatch,
  jornadas,
  limites,
  errores,
  seraInicial,
  onCambiarEmpleado,
  inicioFijo,
  fechaMinimaInicio,
  fechaMinimaFinNuevas,
  fechaMaxima,
  avisos = [],
}: TarjetaEdicionProps) {
  const estilos = useEstilos()
  const etiqueta = etiquetaFila(estado, fila)
  const nueva = fila.id === null
  const yaEmpezo = fila.permisos !== null && !fila.permisos.fechaInicio
  const esPrincipal = fila.rol === ROL_PRINCIPAL
  const nuevas = estado.principales.filter((p) => p.id === null)
  const posicionNueva = nuevas.findIndex((p) => p.clave === fila.clave)

  if (fila.eliminada) {
    return (
      <div className={estilos.eliminada}>
        <TarjetaPersona
          etiqueta={etiqueta}
          empleado={fila.empleado}
          advertencias={['Se eliminará al registrar (aún no empieza).']}
          acciones={
            <Button icon={<ArrowUndo20Regular />} onClick={() => dispatch({ tipo: 'restaurar', clave: fila.clave })}>
              Restaurar
            </Button>
          }
        >
          {null}
        </TarjetaPersona>
      </div>
    )
  }

  const visiblesRol = esPrincipal
    ? CAMPOS_VISIBLES_PRINCIPAL
    : [...CAMPOS_VISIBLES_BACK, 'principalClave', 'principalId', ...(fila.tipoRegistro === 'JORNADA' ? ['diasDescanso'] : [])]
  // Inicio fijo (solo lectura): su error del servidor se muestra en la tarjeta.
  const visibles = inicioFijo === undefined ? visiblesRol : visiblesRol.filter((c) => c !== 'fechaInicio')
  const inicioBloqueado = yaEmpezo || inicioFijo !== undefined
  const minimaFin = fila.permisos?.fechaFinMinima ?? (nueva ? fechaMinimaFinNuevas : undefined)

  return (
    <TarjetaPersona
      etiqueta={etiqueta}
      empleado={fila.empleado}
      advertencias={[...(fila.avisoRelacion ? [AVISO_RELACION] : []), ...avisos]}
      errores={mensajesSinCampo(errores, visibles)}
      acciones={
        <>
          <span className={estilos.marcas}>
            {nueva && <Badge appearance="tint" color="brand">Nuevo</Badge>}
            {yaEmpezo && <Badge appearance="tint" color="informative">Ya empezó</Badge>}
            {seraInicial && <Badge appearance="tint" color="success">Será el principal inicial (responsable)</Badge>}
          </span>
          {nueva && esPrincipal && (
            <>
              <Button
                appearance="subtle"
                icon={<ArrowUp20Regular />}
                aria-label={`Subir ${etiqueta}`}
                title="Subir"
                disabled={posicionNueva <= 0}
                onClick={() => dispatch({ tipo: 'moverNuevo', clave: fila.clave, direccion: -1 })}
              />
              <Button
                appearance="subtle"
                icon={<ArrowDown20Regular />}
                aria-label={`Bajar ${etiqueta}`}
                title="Bajar"
                disabled={posicionNueva === nuevas.length - 1}
                onClick={() => dispatch({ tipo: 'moverNuevo', clave: fila.clave, direccion: 1 })}
              />
            </>
          )}
          {nueva && (
            <Button
              appearance="subtle"
              icon={<PersonSwap20Regular />}
              aria-label={`Cambiar el empleado de ${etiqueta}`}
              title="Cambiar empleado"
              onClick={onCambiarEmpleado}
            />
          )}
          {(nueva || fila.permisos?.eliminable) && (
            <Button
              appearance="subtle"
              icon={<Delete20Regular />}
              aria-label={nueva ? `Quitar ${etiqueta}` : `Eliminar ${etiqueta}`}
              title={nueva ? 'Quitar' : 'Eliminar (aún no empieza)'}
              onClick={() => dispatch({ tipo: 'quitar', clave: fila.clave })}
            />
          )}
        </>
      }
    >
      {esPrincipal ? (
        <CamposPrincipal
          valores={fila}
          onCambiar={(cambios) => dispatch({ tipo: 'actualizar', clave: fila.clave, cambios })}
          jornadas={jornadas}
          errores={errores}
          cargoEmpleado={fila.empleado.cargo}
          bloqueados={{ jornada: fila.permisos !== null && !fila.permisos.jornada, fechaInicio: inicioBloqueado }}
          fechaMinimaFin={minimaFin}
          fechaMaxima={fechaMaxima}
          fechaMinimaInicio={nueva ? fechaMinimaInicio : undefined}
          motivoInicioBloqueado={inicioFijo}
        />
      ) : (
        <CamposBack
          valores={{ ...fila, relacion: valorRelacion(fila.relacion) }}
          onCambiar={({ relacion, ...cambios }) =>
            dispatch({
              tipo: 'actualizar',
              clave: fila.clave,
              cambios: relacion === undefined ? cambios : { ...cambios, relacion: relacionDeValor(relacion) },
            })
          }
          opcionesRelacion={opcionesRelacion(estado)}
          maxDiasDescanso={limites.backMaxDiasDescanso}
          errores={errores}
          errorRelacion={errores.principalClave ?? errores.principalId}
          bloqueados={{ fechaInicio: inicioBloqueado }}
          fechaMinimaFin={minimaFin}
          fechaMaxima={fechaMaxima}
          fechaMinimaInicio={nueva ? fechaMinimaInicio : undefined}
          motivoInicioBloqueado={inicioFijo}
        />
      )}
    </TarjetaPersona>
  )
}
