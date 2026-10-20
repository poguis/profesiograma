import { makeStyles, mergeClasses, tokens } from '@fluentui/react-components'
import { memo, useCallback, useMemo, useRef, useState } from 'react'
import {
  type ContextoCronograma,
  aparienciaCelda,
  esFinDeSemana,
  estadoEnContexto,
  etiquetaCelda,
  expandirDias,
  indexarCeldas,
  inicialDia,
  mesesDelRango,
  numeroDia,
  textoCelda,
} from '../cronograma'
import { formatearFecha } from '../../../utils/formato'
import type { CeldaCronograma, Cronograma, PersonaCronograma, ProyectoCronograma } from '../tipos'
import { DetalleCelda } from './DetalleCelda'

// Medidas del original (l. 586–587): columna de la persona 240 px y días de 40 px.
const ANCHO_PERSONA = 240
const ANCHO_DIA = 40
const ALTO_MES = 24
/** OTRO_PROYECTO (D2): rayado gris, sin datos del proyecto ajeno. */
export const FONDO_RAYADO = 'repeating-linear-gradient(45deg, #BDBDBD 0 3px, #E6E6E6 3px 7px)'

const useEstilos = makeStyles({
  contenedor: {
    overflow: 'auto',
    maxHeight: '70vh',
    border: `1px solid ${tokens.colorNeutralStroke2}`,
    borderRadius: tokens.borderRadiusMedium,
    backgroundColor: tokens.colorNeutralBackground1,
  },
  tabla: { borderCollapse: 'separate', borderSpacing: 0, tableLayout: 'fixed', fontSize: tokens.fontSizeBase200 },
  titulo: { textAlign: 'left', padding: tokens.spacingHorizontalS, fontWeight: tokens.fontWeightSemibold },
  encabezadoPersona: {
    position: 'sticky',
    top: 0,
    left: 0,
    zIndex: 3,
    width: `${ANCHO_PERSONA}px`,
    textAlign: 'left',
    padding: `0 ${tokens.spacingHorizontalS}`,
    backgroundColor: tokens.colorNeutralBackground2,
    borderRight: `1px solid ${tokens.colorNeutralStroke2}`,
    borderBottom: `2px solid ${tokens.colorNeutralStroke2}`,
  },
  mes: {
    position: 'sticky',
    top: 0,
    zIndex: 2,
    height: `${ALTO_MES}px`,
    textAlign: 'left',
    padding: `0 ${tokens.spacingHorizontalS}`,
    textTransform: 'capitalize',
    backgroundColor: tokens.colorNeutralBackground2,
    borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    borderLeft: `1px solid ${tokens.colorNeutralStroke2}`,
  },
  dia: {
    position: 'sticky',
    top: `${ALTO_MES}px`,
    zIndex: 2,
    width: `${ANCHO_DIA}px`,
    padding: '2px 0',
    textAlign: 'center',
    fontWeight: tokens.fontWeightRegular,
    backgroundColor: tokens.colorNeutralBackground1,
    borderRight: `1px solid ${tokens.colorNeutralStroke3}`,
    borderBottom: `2px solid ${tokens.colorNeutralStroke2}`,
  },
  inicial: { display: 'block', color: tokens.colorNeutralForeground3, fontSize: tokens.fontSizeBase100 },
  numero: { display: 'block', fontWeight: tokens.fontWeightSemibold },
  // Fin de semana (original l. 780 y 831–836): fondo sombreado y número en rojo.
  finDeSemana: { backgroundColor: tokens.colorNeutralBackground3 },
  numeroFinDeSemana: { color: tokens.colorPaletteRedForeground1 },
  hoy: { boxShadow: `inset 0 -3px 0 ${tokens.colorBrandStroke1}` },
  columnaHoy: { boxShadow: `inset 1px 0 0 ${tokens.colorBrandStroke1}, inset -1px 0 0 ${tokens.colorBrandStroke1}` },
  persona: {
    position: 'sticky',
    left: 0,
    zIndex: 1,
    width: `${ANCHO_PERSONA}px`,
    padding: `2px ${tokens.spacingHorizontalS}`,
    textAlign: 'left',
    fontWeight: tokens.fontWeightRegular,
    backgroundColor: tokens.colorNeutralBackground1,
    borderRight: `1px solid ${tokens.colorNeutralStroke2}`,
    borderBottom: `1px solid ${tokens.colorNeutralStroke3}`,
  },
  nombre: { display: 'block', fontWeight: tokens.fontWeightSemibold, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' },
  secundario: {
    display: 'block',
    color: tokens.colorNeutralForeground3,
    fontSize: tokens.fontSizeBase100,
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
  },
  celda: {
    height: '30px',
    padding: 0,
    borderRight: `1px solid ${tokens.colorNeutralStroke3}`,
    borderBottom: `1px solid ${tokens.colorNeutralStroke3}`,
  },
  boton: {
    width: '100%',
    height: '100%',
    border: 'none',
    padding: 0,
    cursor: 'pointer',
    fontSize: tokens.fontSizeBase100,
    fontWeight: tokens.fontWeightBold,
    whiteSpace: 'nowrap',
    overflow: 'hidden',
    backgroundColor: 'transparent',
    ':focus-visible': { outline: `2px solid ${tokens.colorStrokeFocus2}`, outlineOffset: '-2px' },
  },
  // NOVEDAD (P2): ficha redondeada con borde blanco de 2 px, sigla en negrita y "+n".
  ficha: {
    display: 'inline-flex',
    alignItems: 'center',
    justifyContent: 'center',
    minWidth: '26px',
    height: '22px',
    padding: '0 2px',
    boxSizing: 'border-box',
    borderRadius: tokens.borderRadiusCircular,
    border: '2px solid #FFFFFF',
    fontWeight: tokens.fontWeightBold,
  },
  extra: { fontSize: '8px', verticalAlign: 'super', marginLeft: '1px' },
})

type Clases = ReturnType<typeof useEstilos>

export interface CuadriculaCronogramaProps {
  datos: Cronograma
  /** Personas ya filtradas en el cliente (P9). */
  personas: PersonaCronograma[]
  colores: ReadonlyMap<number, string>
  /** Hoy en Ecuador, "yyyy-MM-dd". */
  hoy: string
  /** Q7 (TAREA-21b): textos del detalle según la vista. Por defecto 'general' (/cronograma). */
  contexto?: ContextoCronograma
  /** Q3 (TAREA-21b): pestaña de un proyecto; sus celdas sin asignación en él se ven como "otro proyecto". */
  proyectoContexto?: number
}

interface CeldaAbierta {
  persona: PersonaCronograma
  celda: CeldaCronograma
  destino: HTMLElement
}

/** Cuadrícula persona × día (P1: tabla HTML con sticky, sin virtualización; P4: un solo Popover compartido). */
export function CuadriculaCronograma({ datos, personas, colores, hoy, contexto = 'general', proyectoContexto }: CuadriculaCronogramaProps) {
  const clases = useEstilos()
  const dias = useMemo(() => expandirDias(datos.desde, datos.hasta), [datos.desde, datos.hasta])
  const meses = useMemo(() => mesesDelRango(dias), [dias])
  const proyectos = useMemo(() => new Map(datos.proyectos.map((p) => [p.id, p])), [datos.proyectos])
  const [abierta, setAbierta] = useState<CeldaAbierta>()
  const ultimoDestino = useRef<HTMLElement | null>(null)

  const abrir = useCallback((persona: PersonaCronograma, celda: CeldaCronograma, destino: HTMLElement) => {
    ultimoDestino.current = destino
    setAbierta({ persona, celda, destino })
  }, [])

  const cerrar = useCallback(() => {
    setAbierta(undefined)
    ultimoDestino.current?.focus() // devuelve el foco a la celda (teclado)
  }, [])

  return (
    <div className={clases.contenedor}>
      <table className={clases.tabla} style={{ width: `${ANCHO_PERSONA + dias.length * ANCHO_DIA}px` }}>
        <caption className={clases.titulo}>
          Cronograma del {formatearFecha(datos.desde)} al {formatearFecha(datos.hasta)}
        </caption>
        <thead>
          <tr>
            <th scope="col" rowSpan={2} className={clases.encabezadoPersona}>
              Colaborador
            </th>
            {meses.map((m) => (
              <th key={m.mes} scope="colgroup" colSpan={m.dias} className={clases.mes}>
                {m.mes}
              </th>
            ))}
          </tr>
          <tr>
            {dias.map((dia) => {
              const finde = esFinDeSemana(dia)
              return (
                <th
                  key={dia}
                  scope="col"
                  aria-label={formatearFecha(dia) + (dia === hoy ? ' (hoy)' : '')}
                  className={mergeClasses(clases.dia, finde && clases.finDeSemana, dia === hoy && clases.hoy)}
                >
                  <span className={clases.inicial}>{inicialDia(dia)}</span>
                  <span className={mergeClasses(clases.numero, finde && clases.numeroFinDeSemana)}>{numeroDia(dia)}</span>
                </th>
              )
            })}
          </tr>
        </thead>
        <tbody>
          {personas.map((p) => (
            <FilaPersona
              key={p.codigoEkon}
              persona={p}
              dias={dias}
              proyectos={proyectos}
              colores={colores}
              hoy={hoy}
              clases={clases}
              contexto={contexto}
              proyectoContexto={proyectoContexto}
              onAbrir={abrir}
            />
          ))}
        </tbody>
      </table>

      {abierta && (
        <DetalleCelda
          persona={abierta.persona}
          celda={abierta.celda}
          destino={abierta.destino}
          proyectos={proyectos}
          contexto={contexto}
          onCerrar={cerrar}
        />
      )}
    </div>
  )
}

interface FilaPersonaProps {
  persona: PersonaCronograma
  dias: string[]
  proyectos: ReadonlyMap<number, ProyectoCronograma>
  colores: ReadonlyMap<number, string>
  hoy: string
  clases: Clases
  contexto: ContextoCronograma
  proyectoContexto?: number
  onAbrir: (persona: PersonaCronograma, celda: CeldaCronograma, destino: HTMLElement) => void
}

/** Una fila por persona; memorizada para no volver a pintar todas al abrir un detalle. */
const FilaPersona = memo(function FilaPersona({
  persona, dias, proyectos, colores, hoy, clases, contexto, proyectoContexto, onAbrir,
}: FilaPersonaProps) {
  const celdas = useMemo(() => indexarCeldas(persona.dias), [persona.dias])
  const codigos = persona.asignadoEn.map((a) => proyectos.get(a.proyectoId)?.codigo ?? `Proyecto ${a.proyectoId}`).join(', ')

  return (
    <tr>
      <th scope="row" className={clases.persona}>
        <span className={clases.nombre} title={persona.nombre}>
          {persona.nombre}
        </span>
        <span className={clases.secundario} title={codigos}>
          EKON {persona.codigoEkon}
          {codigos ? ` · ${codigos}` : ''}
        </span>
      </th>
      {dias.map((dia) => {
        const celda = celdas.get(dia)
        return (
          <td
            key={dia}
            className={mergeClasses(clases.celda, esFinDeSemana(dia) && clases.finDeSemana, dia === hoy && clases.columnaHoy)}
          >
            {celda && (
              <Celda
                persona={persona}
                // Q3: en la pestaña del proyecto, el estado en ese contexto (CHOQUE entre ajenos → OTRO_PROYECTO).
                celda={proyectoContexto === undefined ? celda : { ...celda, estado: estadoEnContexto(celda, proyectoContexto) }}
                proyectos={proyectos}
                colores={colores}
                clases={clases}
                contexto={contexto}
                onAbrir={onAbrir}
              />
            )}
          </td>
        )
      })}
    </tr>
  )
})

interface CeldaProps {
  persona: PersonaCronograma
  celda: CeldaCronograma
  proyectos: ReadonlyMap<number, ProyectoCronograma>
  colores: ReadonlyMap<number, string>
  clases: Clases
  contexto: ContextoCronograma
  onAbrir: FilaPersonaProps['onAbrir']
}

function Celda({ persona, celda, proyectos, colores, clases, contexto, onAbrir }: CeldaProps) {
  const apariencia = aparienciaCelda(celda, colores)
  const { texto, extra } = textoCelda(celda, proyectos)
  const estiloBoton =
    apariencia.forma === 'ficha'
      ? undefined
      : { background: apariencia.forma === 'rayado' ? FONDO_RAYADO : apariencia.fondo, color: apariencia.texto }

  return (
    <button
      type="button"
      className={clases.boton}
      style={estiloBoton}
      aria-label={`${persona.nombre}, ${etiquetaCelda(celda, proyectos, contexto)}`}
      aria-haspopup="dialog"
      onClick={(e) => onAbrir(persona, celda, e.currentTarget)}
    >
      {apariencia.forma === 'ficha' ? (
        <span className={clases.ficha} style={{ backgroundColor: apariencia.fondo, color: apariencia.texto }}>
          {texto}
          {extra && <span className={clases.extra}>{extra}</span>}
        </span>
      ) : (
        texto
      )}
    </button>
  )
}
