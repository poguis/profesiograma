import { Button, MessageBar, MessageBarBody, Text, makeStyles, tokens } from '@fluentui/react-components'
import { Add20Regular, Delete20Regular } from '@fluentui/react-icons'
import type { Dispatch } from 'react'
import {
  type AccionFormulario,
  type CambiosBack,
  type ErroresFila,
  type FilaBack,
  type FilaPrincipal,
  mensajesSinCampo,
} from '../formularioProyecto'
import { AyudaAgregarPersonal } from './AyudaAgregarPersonal'
import { CamposBack, VALOR_SIN_RELACION } from './CamposPersona'
import { TarjetaPersona } from './TarjetaPersona'

const useEstilos = makeStyles({
  seccion: { display: 'flex', flexDirection: 'column', gap: tokens.spacingVerticalM },
  encabezado: { display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: tokens.spacingHorizontalM },
})

const AVISO_RELACION = 'El principal relacionado fue quitado del proyecto. Elija otro o deje "Sin relación".'

export interface ListaBacksProps {
  backs: FilaBack[]
  principales: FilaPrincipal[]
  dispatch: Dispatch<AccionFormulario>
  maximo: number
  /** BACK_MAX_DIAS_DESCANSO (el servidor también lo valida). */
  maxDiasDescanso: number
  rangoListo: boolean
  advertencias: Map<string, string>
  errores: Record<string, ErroresFila>
  /** Error de la lista completa (p. ej. máximo superado). */
  errorSeccion?: string
  onAgregar: () => void
}

export function ListaBacks({
  backs,
  principales,
  dispatch,
  maximo,
  maxDiasDescanso,
  rangoListo,
  advertencias,
  errores,
  errorSeccion,
  onAgregar,
}: ListaBacksProps) {
  const estilos = useEstilos()
  const lleno = backs.length >= maximo

  return (
    <section className={estilos.seccion} aria-label="Backs">
      <div className={estilos.encabezado}>
        <Text as="h3" size={500} weight="semibold">
          Backs ({backs.length} de {maximo})
        </Text>
        <Button icon={<Add20Regular />} disabled={!rangoListo || lleno} onClick={onAgregar}>
          Agregar back
        </Button>
        <AyudaAgregarPersonal rangoListo={rangoListo} lleno={lleno} maximo={maximo} plural="backs" />
      </div>

      {errorSeccion && (
        <MessageBar intent="error">
          <MessageBarBody>{errorSeccion}</MessageBarBody>
        </MessageBar>
      )}

      {backs.length === 0 && <Text size={200}>Sin backs.</Text>}

      {backs.map((b, i) => (
        <TarjetaBack
          key={b.clave}
          fila={b}
          numero={i + 1}
          principales={principales}
          dispatch={dispatch}
          maxDiasDescanso={maxDiasDescanso}
          advertencias={[b.avisoRelacion ? AVISO_RELACION : undefined, advertencias.get(b.clave)].filter(
            (m): m is string => m !== undefined,
          )}
          errores={errores[b.clave] ?? {}}
        />
      ))}
    </section>
  )
}

const CAMPOS_VISIBLES = ['tipoRegistro', 'fechaInicio', 'fechaFin', 'principalRelacionado', 'observacion']

interface TarjetaBackProps {
  fila: FilaBack
  numero: number
  principales: FilaPrincipal[]
  dispatch: Dispatch<AccionFormulario>
  maxDiasDescanso: number
  advertencias: string[]
  errores: ErroresFila
}

function TarjetaBack({ fila, numero, principales, dispatch, maxDiasDescanso, advertencias, errores }: TarjetaBackProps) {
  const actualizar = (cambios: CambiosBack) => dispatch({ tipo: 'actualizarBack', clave: fila.clave, cambios })
  const etiqueta = `Back ${numero}`
  const opciones = principales.map((p, i) => ({ valor: p.clave, texto: `P${i + 1} · ${p.empleado.nombreCompleto}` }))

  return (
    <TarjetaPersona
      etiqueta={etiqueta}
      empleado={fila.empleado}
      advertencias={advertencias}
      errores={mensajesSinCampo(
        errores,
        fila.tipoRegistro === 'JORNADA' ? [...CAMPOS_VISIBLES, 'diasDescanso'] : CAMPOS_VISIBLES,
      )}
      acciones={
        <Button
          appearance="subtle"
          icon={<Delete20Regular />}
          aria-label={`Quitar ${etiqueta}`}
          title="Quitar"
          onClick={() => dispatch({ tipo: 'eliminarBack', clave: fila.clave })}
        />
      }
    >
      <CamposBack
        valores={{ ...fila, relacion: fila.principalClave ?? VALOR_SIN_RELACION }}
        onCambiar={({ relacion, ...cambios }) =>
          actualizar(relacion === undefined ? cambios : { ...cambios, principalClave: relacion || null })
        }
        opcionesRelacion={opciones}
        maxDiasDescanso={maxDiasDescanso}
        errores={errores}
        errorRelacion={errores.principalRelacionado}
      />
    </TarjetaPersona>
  )
}
