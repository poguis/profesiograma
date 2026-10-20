import { Button, MessageBar, MessageBarActions, MessageBarBody, MessageBarTitle, Text, makeStyles, tokens } from '@fluentui/react-components'
import type { ReactNode } from 'react'
import { ErrorApi } from '../../../api/errores'
import { EstadoError } from '../../../components/EstadoError'
import { type ContextoCronograma, textoAvisoRecorte } from '../cronograma'
import type { Cronograma, PersonaCronograma } from '../tipos'
import { CuadriculaCronograma } from './CuadriculaCronograma'
import { LeyendaCronograma } from './LeyendaCronograma'

const useEstilos = makeStyles({
  resumen: { color: tokens.colorNeutralForeground3 },
})

export interface VistaCronogramaProps {
  datos: Cronograma
  /** Personas ya filtradas en el cliente (P9 / Q6). */
  personas: PersonaCronograma[]
  /** Texto del filtro de persona, para el mensaje sin coincidencias. */
  textoPersona?: string
  colores: ReadonlyMap<number, string>
  hoy: string
  /** "No hay asignaciones en el rango." en /cronograma; el de la pestaña, propio. */
  mensajeVacio: string
  /** Texto o enlace que sigue al aviso de recorte (p. ej. la sugerencia de la pestaña). */
  complementoAviso?: ReactNode
  contexto?: ContextoCronograma
  /** Pestaña del detalle (Q3): las celdas sin asignación en este proyecto se ven como "otro proyecto". */
  proyectoContexto?: number
}

/**
 * Resumen, aviso de recorte (D5 revisada), vacío, cuadrícula y leyenda (TAREA-21b, Q1: extraído de PaginaCronograma).
 * Lo usan /cronograma y la pestaña "Cronograma" del detalle del proyecto.
 */
export function VistaCronograma({
  datos, personas, textoPersona, colores, hoy, mensajeVacio, complementoAviso, contexto = 'general', proyectoContexto,
}: VistaCronogramaProps) {
  const estilos = useEstilos()
  const aviso = textoAvisoRecorte(datos)

  return (
    <>
      <Text size={200} className={estilos.resumen}>
        {personas.length} de {datos.personas.length} persona(s) · {datos.proyectos.length} proyecto(s) · máximo {datos.maxDias} días
      </Text>
      {/* D5 revisada (TAREA-21): el rango pedido se conserva; aquí se avisa del rango que se muestra. */}
      {aviso && (
        <MessageBar intent="warning">
          <MessageBarBody>
            {aviso}
            {complementoAviso && <> {complementoAviso}</>}
          </MessageBarBody>
        </MessageBar>
      )}
      {datos.personas.length === 0 ? (
        <MessageBar intent="info">
          <MessageBarBody>{mensajeVacio}</MessageBarBody>
        </MessageBar>
      ) : personas.length === 0 ? (
        <MessageBar intent="info">
          <MessageBarBody>Ninguna persona coincide con «{textoPersona}».</MessageBarBody>
        </MessageBar>
      ) : (
        <CuadriculaCronograma
          datos={datos}
          personas={personas}
          colores={colores}
          hoy={hoy}
          contexto={contexto}
          proyectoContexto={proyectoContexto}
        />
      )}
      <LeyendaCronograma proyectos={datos.proyectos} novedades={datos.leyendaNovedades} colores={colores} contexto={contexto} />
    </>
  )
}

export interface ErrorCronogramaProps {
  error: unknown
  reintentando: boolean
  onReintentar: () => void
}

/** 403/5xx/red: el error y "Reintentar" (P10: sin reintentos automáticos). Los 400 van en su filtro (no se muestran aquí). */
export function ErrorCronograma({ error, reintentando, onReintentar }: ErrorCronogramaProps) {
  const errorApi = error instanceof ErrorApi ? error : undefined
  if (errorApi?.tipo === 'validacion') {
    return null
  }

  return (
    <div>
      <EstadoError error={error} />
      {errorApi?.tipo !== 'prohibido' && (
        <MessageBar intent="info">
          <MessageBarBody>
            <MessageBarTitle>No se pudo cargar el cronograma.</MessageBarTitle>
            Revise la conexión (VPN) y vuelva a intentar.
          </MessageBarBody>
          <MessageBarActions>
            <Button onClick={onReintentar} disabled={reintentando}>
              Reintentar
            </Button>
          </MessageBarActions>
        </MessageBar>
      )}
    </div>
  )
}
